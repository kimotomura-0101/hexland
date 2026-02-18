using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// サーバーからのゲームアクションを既存のGameManager/VertexPoint/EdgePointメソッドに変換するブリッジ。
/// NetworkManager.OnGameAction イベントを購読し、対応するローカルメソッドを呼び出す。
/// </summary>
public class NetworkBridge : MonoBehaviour
{
    public static NetworkBridge Instance { get; private set; }

    // 座標キー → オブジェクト逆引き
    private Dictionary<string, VertexPoint> vertexLookup;
    private Dictionary<string, EdgePoint> edgeLookup;
    private Dictionary<string, HexTileData> tileLookup;

    // リモートプレイヤーキャラクター
    private Dictionary<int, GameObject> remoteCharacters = new Dictionary<int, GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        var net = NetworkManager.Instance;
        if (net == null) return;

        net.OnGameStarted += OnNetworkGameStart;
        net.OnGameAction += OnNetworkGameAction;
        net.OnExploreSync += OnNetworkExploreSync;
        net.OnPlayerDisconnected += OnPlayerDisconnected;
    }

    void OnDestroy()
    {
        var net = NetworkManager.Instance;
        if (net == null) return;

        net.OnGameStarted -= OnNetworkGameStart;
        net.OnGameAction -= OnNetworkGameAction;
        net.OnExploreSync -= OnNetworkExploreSync;
        net.OnPlayerDisconnected -= OnPlayerDisconnected;
    }

    // ===== ゲーム開始 =====

    void OnNetworkGameStart(GameStartData data)
    {
        var gm = GameManager.Instance;

        // シードでRNG初期化 → 全員同一マップ生成
        UnityEngine.Random.InitState(data.seed);

        // プレイヤー名リスト作成
        var names = new List<string>();
        for (int i = 0; i < data.players.Length; i++)
            names.Add(data.players[i].name);

        // ゲーム開始
        gm.StartNewGame(data.players.Length, data.mapRadius, names);

        // isAIフラグを上書き (リモート人間はAIではない)
        for (int i = 0; i < data.players.Length; i++)
            gm.players[i].isAI = data.players[i].isAI;

        // ルックアップテーブル構築
        BuildLookupMaps();
    }

    // ===== ルックアップ構築 =====

    public void BuildLookupMaps()
    {
        vertexLookup = new Dictionary<string, VertexPoint>();
        foreach (var v in VertexPoint.AllVertices)
        {
            string key = MakeVertexKey(v.transform.position);
            vertexLookup[key] = v;
        }

        edgeLookup = new Dictionary<string, EdgePoint>();
        foreach (var v in VertexPoint.AllVertices)
        {
            foreach (var e in v.edges)
            {
                string key = MakeEdgeKey(e.vertex1.transform.position, e.vertex2.transform.position);
                if (!edgeLookup.ContainsKey(key))
                    edgeLookup[key] = e;
            }
        }

        tileLookup = new Dictionary<string, HexTileData>();
        var tiles = GameManager.Instance.mapGenerator.GetAllTiles();
        foreach (var t in tiles)
            tileLookup[$"{t.q}_{t.r}"] = t;

        Debug.Log($"[NetworkBridge] Lookup built: {vertexLookup.Count} vertices, {edgeLookup.Count} edges, {tileLookup.Count} tiles");
    }

    // ===== キー生成ヘルパー (CatanMapGeneratorと同一形式) =====

    public static string MakeVertexKey(Vector3 pos)
    {
        return $"{Mathf.Round(pos.x * 100)}_{Mathf.Round(pos.z * 100)}";
    }

    public static string MakeEdgeKey(Vector3 p1, Vector3 p2)
    {
        string k1 = $"{Mathf.Round(p1.x * 100)}_{Mathf.Round(p1.z * 100)}";
        string k2 = $"{Mathf.Round(p2.x * 100)}_{Mathf.Round(p2.z * 100)}";
        return string.Compare(k1, k2) < 0 ? $"{k1}-{k2}" : $"{k2}-{k1}";
    }

    // ===== ゲームアクション受信 =====

    void OnNetworkGameAction(GameActionData data)
    {
        var gm = GameManager.Instance;
        string playerName = gm.players[data.playerIndex].name;

        switch (data.action)
        {
            case "build_settlement":
                if (vertexLookup.TryGetValue(data.vertexKey, out var sv))
                    sv.BuildSettlement(playerName);
                break;

            case "build_road":
                if (edgeLookup.TryGetValue(data.edgeKey, out var re))
                    re.BuildRoad(playerName);
                break;

            case "upgrade_city":
                if (vertexLookup.TryGetValue(data.vertexKey, out var cv))
                {
                    // 都市化コスト消費
                    gm.TryConsumeResources(playerName, 0, 0, 3, 2, 0);
                    cv.UpgradeToCity(playerName);
                }
                break;

            case "dice_result":
                gm.OnDiceRolledNetwork(data.d1, data.d2);
                break;

            case "move_robber":
                string tileKey = $"{data.tileQ}_{data.tileR}";
                if (tileLookup.TryGetValue(tileKey, out var tile))
                    gm.MoveRobberTo(tile);
                break;

            case "execute_trade":
                var giveType = (CatanMapGenerator.HexType)data.giveType;
                var getType = (CatanMapGenerator.HexType)data.getType;
                int cost = gm.GetTradeCost(playerName, giveType);
                gm.ExecuteTrade(playerName, giveType, getType, cost);
                break;

            case "buy_dev_card":
                gm.OnClickBuyCard();
                break;

            case "use_dev_card":
                gm.UseDevCard(data.playerIndex, data.cardIndex);
                break;

            case "execute_monopoly":
                gm.ExecuteMonopoly((CatanMapGenerator.HexType)data.resourceType);
                break;

            case "end_turn":
                gm._isFromNetwork = true;
                gm.OnClickEndTurn();
                break;
        }
    }

    // ===== Explore同期受信 =====

    void OnNetworkExploreSync(ExploreSyncData data)
    {
        if (NetworkManager.Instance == null) return;
        if (data.playerIndex == NetworkManager.Instance.LocalPlayerIndex) return;

        if (!remoteCharacters.TryGetValue(data.playerIndex, out var charObj) || charObj == null)
        {
            // リモートキャラ生成 (ExploreControllerのcharacterPrefabを使用)
            var ec = FindAnyObjectByType<ExploreController>();
            if (ec == null || ec.characterPrefab == null) return;

            Vector3 pos = new Vector3(data.pos[0], data.pos[1], data.pos[2]);
            charObj = Instantiate(ec.characterPrefab, pos, Quaternion.identity);
            charObj.name = $"RemotePlayer_{data.playerIndex}";

            // ローカル入力を無効化
            var localEC = charObj.GetComponent<ExploreController>();
            if (localEC != null) Destroy(localEC);

            charObj.AddComponent<RemotePlayerController>();
            remoteCharacters[data.playerIndex] = charObj;
        }

        var controller = charObj.GetComponent<RemotePlayerController>();
        if (controller != null)
        {
            controller.ApplyNetworkState(
                new Vector3(data.pos[0], data.pos[1], data.pos[2]),
                data.rot,
                data.anim
            );
        }
    }

    void OnPlayerDisconnected(int playerIndex)
    {
        var gm = GameManager.Instance;
        if (playerIndex >= 0 && playerIndex < gm.players.Count)
        {
            gm.players[playerIndex].isAI = true;
            Debug.Log($"[NetworkBridge] Player {playerIndex} disconnected, now AI");
        }

        // リモートキャラ削除
        if (remoteCharacters.TryGetValue(playerIndex, out var charObj))
        {
            Destroy(charObj);
            remoteCharacters.Remove(playerIndex);
        }
    }

    // Exploreモード終了時にリモートキャラを全削除
    public void ClearRemoteCharacters()
    {
        foreach (var kv in remoteCharacters)
            if (kv.Value != null) Destroy(kv.Value);
        remoteCharacters.Clear();
    }
}
