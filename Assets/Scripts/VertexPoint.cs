using UnityEngine;
using System.Collections.Generic;

public class VertexPoint : MonoBehaviour
{
    public bool hasBuilding = false;
    public string ownerPlayer = ""; // "Player1", "Player2" など
    public bool isCity = false;
    public bool hasPort = false;
    public CatanMapGenerator.HexType portType;

    private GameObject buildingInstance; // 建設されたオブジェクトの参照

    // この頂点に接続している辺（道）
    public List<EdgePoint> edges = new List<EdgePoint>();

    // この頂点に隣接しているタイル
    public List<HexTileData> adjacentTiles = new List<HexTileData>();

    // 全頂点のリスト（初期配置の判定用）
    public static List<VertexPoint> AllVertices = new List<VertexPoint>();

    void Awake()
    {
        AllVertices.Add(this);
        // プレイヤーとの衝突を避けるためにトリガーにする
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnDestroy()
    {
        AllVertices.Remove(this);
    }

    void Update()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;

        if (GameManager.Instance == null) return;

        // AIターン中はハイライトを表示しない
        if (GameManager.Instance.IsCurrentPlayerAI())
        {
            renderer.enabled = false;
            return;
        }

        if (hasBuilding)
        {
            // 建設済みの場合：都市へのアップグレードが可能ならハイライトする
            // 条件: 建設モード中(初期配置以外)、自分の所有、まだ都市ではない
            bool canUpgrade = GameManager.Instance.isConstructionMode
                              && !GameManager.Instance.IsSetupPhase
                              && ownerPlayer == GameManager.Instance.CurrentPlayer
                              && !isCity;
            renderer.enabled = canUpgrade;
        }
        else
        {
            // 未建設の場合：建設可能ならハイライトを表示
            bool isModeActive = GameManager.Instance.IsSetupPhase || GameManager.Instance.isConstructionMode;
            bool canBuild = isModeActive && CanBuildSettlement(GameManager.Instance.CurrentPlayer);
            renderer.enabled = canBuild;
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.isExploreMode) return;

        // 建設モードでなければ何もしない（初期配置フェーズは除く）
        if (!GameManager.Instance.IsSetupPhase && !GameManager.Instance.isConstructionMode) return;

        // クリックしたら家を建てる（簡易版）
        string currentPlayer = GameManager.Instance.CurrentPlayer;
        if (!hasBuilding)
        {
            if (CanBuildSettlement(currentPlayer))
            {
                // 初期配置フェーズはコストなし
                if (GameManager.Instance.IsSetupPhase)
                {
                    BuildSettlement(currentPlayer);
                }
                // 通常フェーズはコスト消費 (木1, 土1, 麦1, 羊1)
                else if (GameManager.Instance.TryConsumeResources(currentPlayer, 1, 1, 0, 1, 1))
                {
                    BuildSettlement(currentPlayer);
                }
                else
                {
                    Debug.Log("資源が足りません（必要: 木1, 土1, 麦1, 羊1）");
                }
            }
            else
            {
                Debug.Log("ここには建設できません（距離ルールまたは接続ルール違反）");
            }
        }
        else if (!isCity && ownerPlayer == currentPlayer)
        {
            // 都市へのアップグレード
            if (GameManager.Instance.IsSetupPhase)
            {
                Debug.Log("初期配置フェーズでは都市化できません");
            }
            // コスト消費 (鉄3, 麦2)
            else if (GameManager.Instance.TryConsumeResources(currentPlayer, 0, 0, 3, 2, 0))
            {
                UpgradeToCity(currentPlayer);
            }
            else
            {
                Debug.Log("資源が足りません（必要: 鉄3, 麦2）");
            }
        }
    }

    public void BuildSettlement(string playerID)
    {
        hasBuilding = true;
        ownerPlayer = playerID;

        // ハイライトを消す
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        // 見た目を生成
        GameObject prefab = GameManager.Instance.GetSettlementPrefab(playerID);
        if (prefab != null)
        {
            buildingInstance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            
            // 建物モデルのコライダーがクリック判定を邪魔しないように削除
            foreach(var c in buildingInstance.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        
        Debug.Log($"家を建設しました！ 所有者: {playerID}");

        // ゲームマネージャーに通知
        GameManager.Instance.OnSettlementBuilt(this);
    }

    public void UpgradeToCity(string playerID)
    {
        isCity = true;

        // 古い建物（家）を削除
        if (buildingInstance != null) Destroy(buildingInstance);

        // 都市の見た目を生成
        GameObject prefab = GameManager.Instance.GetCityPrefab(playerID);
        if (prefab != null)
        {
            buildingInstance = Instantiate(prefab, transform.position, Quaternion.identity, transform);

            // 建物モデルのコライダーがクリック判定を邪魔しないように削除
            foreach(var c in buildingInstance.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        Debug.Log($"都市にアップグレードしました！ 所有者: {playerID}");
    }

    public bool CanBuildSettlement(string playerID)
    {
        if (hasBuilding) return false;

        // 手番チェック
        if (GameManager.Instance.CurrentPlayer != playerID) return false;

        // ステップチェック（初期配置時）
        if (GameManager.Instance.IsSetupPhase && GameManager.Instance.currentStep != GameManager.TurnStep.PlaceSettlement) return false;

        // 1. 距離ルール: 隣接する頂点に建物があってはならない
        foreach (var edge in edges)
        {
            // 辺のもう片方の頂点を取得
            var neighbor = (edge.vertex1 == this) ? edge.vertex2 : edge.vertex1;
            if (neighbor.hasBuilding) return false;
        }

        // 2. 初期配置ならここでOK（接続ルール無視）
        if (GameManager.Instance.IsSetupPhase) return true;

        // 3. 接続ルール: 自分の道がつながっていること
        foreach (var edge in edges)
        {
            if (edge.hasRoad && edge.ownerPlayer == playerID) return true;
        }

        return false;
    }
}