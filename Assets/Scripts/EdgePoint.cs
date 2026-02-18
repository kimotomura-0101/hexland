using UnityEngine;

public class EdgePoint : MonoBehaviour
{
    public bool hasRoad = false;
    public string ownerPlayer = "";
    
    public Vector3 roadPositionOffset;

    // この辺の両端にある頂点
    public VertexPoint vertex1;
    public VertexPoint vertex2;

    void Start()
    {
        // プレイヤーとの衝突を避けるためにトリガーにする
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        // 建設済みならハイライトを消す
        if (hasRoad)
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            return;
        }

        if (GameManager.Instance == null) return;

        // AIターン中はハイライトを表示しない
        if (GameManager.Instance.IsCurrentPlayerAI())
        {
            MeshRenderer mr2 = GetComponent<MeshRenderer>();
            if (mr2 != null) mr2.enabled = false;
            return;
        }

        // 建設可能ならハイライトを表示
        // 初期配置フェーズ または 建設モードON の時のみ判定
        bool isModeActive = GameManager.Instance.IsSetupPhase || GameManager.Instance.isConstructionMode;
        bool canBuild = isModeActive && CanBuildRoad(GameManager.Instance.CurrentPlayer);
        
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = canBuild;
    }

    void OnMouseDown()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.isExploreMode) return;
        if (!GameManager.Instance.IsLocalPlayerTurn()) return;

        // 建設モードでなければ何もしない（初期配置フェーズは除く）
        if (!GameManager.Instance.IsSetupPhase && !GameManager.Instance.isConstructionMode) return;

        if (!hasRoad)
        {
            string currentPlayer = GameManager.Instance.CurrentPlayer;
            if (!CanBuildRoad(currentPlayer)) { Debug.Log("道を建設できません。"); return; }

            // マルチプレイ: サーバーに送信
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
            {
                string key = NetworkBridge.MakeEdgeKey(vertex1.transform.position, vertex2.transform.position);
                NetworkManager.Instance.SendGameAction("build_road",
                    new System.Collections.Generic.Dictionary<string, object> { { "edgeKey", key } });
                return;
            }

            // シングルプレイ: 従来通り
            if (GameManager.Instance.IsSetupPhase)
            {
                BuildRoad(currentPlayer);
            }
            else if (GameManager.Instance.currentStep == GameManager.TurnStep.RoadBuildingCard)
            {
                BuildRoad(currentPlayer);
            }
            else if (GameManager.Instance.TryConsumeResources(currentPlayer, 1, 1, 0, 0, 0))
            {
                BuildRoad(currentPlayer);
            }
            else
            {
                Debug.Log("資源が足りません（必要: 木1, 土1）");
            }
        }
    }

    public bool CanBuildRoad(string playerID)
    {
        // 手番チェック
        if (GameManager.Instance.CurrentPlayer != playerID) return false;

        // ステップチェック（初期配置時）
        if (GameManager.Instance.IsSetupPhase)
        {
            if (GameManager.Instance.currentStep != GameManager.TurnStep.PlaceRoad) return false;
            
            // 直前に建てた家に接続しているか
            var lastSettlement = GameManager.Instance.lastBuiltSettlement;
            return lastSettlement != null && (vertex1 == lastSettlement || vertex2 == lastSettlement);
        }

        // 1. 両端のどちらかに自分の家がある
        if (vertex1.ownerPlayer == playerID || vertex2.ownerPlayer == playerID) return true;

        // 2. 両端のどちらかに自分の道がつながっている
        if (HasConnectedRoad(vertex1, playerID)) return true;
        if (HasConnectedRoad(vertex2, playerID)) return true;

        return false;
    }

    bool HasConnectedRoad(VertexPoint vertex, string playerID)
    {
        foreach (var edge in vertex.edges)
        {
            // 自分自身は除外
            if (edge == this) continue;
            
            if (edge.hasRoad && edge.ownerPlayer == playerID) return true;
        }
        return false;
    }

    public void BuildRoad(string playerID)
    {
        hasRoad = true;
        ownerPlayer = playerID;

        // ハイライト（クリック判定用の表示）を消す
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        // 見た目を生成
        GameObject prefab = GameManager.Instance.GetRoadPrefab(playerID);
        if (prefab != null)
        {
            // 辺の位置と回転に合わせて道を生成
            Vector3 pos = transform.position + transform.rotation * roadPositionOffset;
            GameObject road = Instantiate(prefab, pos, transform.rotation, transform);
            // 必要に応じてスケール調整など
        }
        
        Debug.Log($"道を建設しました！ 所有者: {playerID}");

        // ゲームマネージャーに通知
        GameManager.Instance.OnRoadBuilt(this);
    }
}
