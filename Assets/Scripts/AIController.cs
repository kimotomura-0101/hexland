using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AIController : MonoBehaviour
{
    public static AIController Instance { get; private set; }

    [Header("Settings")]
    public float actionDelay = 1.0f;

    private bool isProcessing = false;

    // 出目の確率スコア
    static readonly Dictionary<int, int> diceScore = new Dictionary<int, int>
    {
        {2,1},{3,2},{4,3},{5,4},{6,5},{8,5},{9,4},{10,3},{11,2},{12,1}
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// GameManagerのターン遷移後に呼ばれる
    /// </summary>
    public void OnTurnChanged()
    {
        if (isProcessing) return;
        if (GameManager.Instance.isGameOver) return;
        if (!GameManager.Instance.IsCurrentPlayerAI()) return;
        StartCoroutine(ExecuteAITurn());
    }

    IEnumerator ExecuteAITurn()
    {
        isProcessing = true;
        yield return new WaitForSeconds(actionDelay);

        var gm = GameManager.Instance;
        if (gm.isGameOver) { isProcessing = false; yield break; }

        if (gm.IsSetupPhase)
        {
            yield return SetupPhaseAI();
        }
        else if (gm.currentPhase == GameManager.GamePhase.Playing)
        {
            yield return PlayingPhaseAI();
        }

        isProcessing = false;

        // 次のプレイヤーもAIなら続けて通知（Setup中のAI連鎖用）
        OnTurnChanged();
    }

    // =========== 初期配置 ===========

    IEnumerator SetupPhaseAI()
    {
        var gm = GameManager.Instance;
        string player = gm.CurrentPlayer;

        // 1. 開拓地を配置
        if (gm.currentStep == GameManager.TurnStep.PlaceSettlement)
        {
            VertexPoint best = FindBestVertex(player);
            if (best != null)
            {
                UpdateInfoText($"{player} が開拓地を配置...");
                yield return new WaitForSeconds(actionDelay);
                best.BuildSettlement(player);
            }
        }

        yield return new WaitForSeconds(actionDelay);

        // 2. 道を配置
        if (gm.currentStep == GameManager.TurnStep.PlaceRoad)
        {
            EdgePoint roadEdge = FindSetupRoad(player);
            if (roadEdge != null)
            {
                UpdateInfoText($"{player} が道を配置...");
                yield return new WaitForSeconds(actionDelay);
                roadEdge.BuildRoad(player);
            }
        }
        // OnRoadBuilt内で次のプレイヤーに遷移 → GameManagerがAIチェックを呼ぶ
    }

    VertexPoint FindBestVertex(string playerID)
    {
        float bestScore = -1f;
        VertexPoint bestVertex = null;

        foreach (var v in VertexPoint.AllVertices)
        {
            if (!v.CanBuildSettlement(playerID)) continue;

            float score = ScoreVertex(v);
            if (score > bestScore)
            {
                bestScore = score;
                bestVertex = v;
            }
        }
        return bestVertex;
    }

    float ScoreVertex(VertexPoint v)
    {
        float score = 0f;
        HashSet<CatanMapGenerator.HexType> resourceTypes = new HashSet<CatanMapGenerator.HexType>();

        foreach (var tile in v.adjacentTiles)
        {
            if (tile.resourceType == CatanMapGenerator.HexType.Desert ||
                tile.resourceType == CatanMapGenerator.HexType.Beach)
                continue;

            // 出目確率スコア
            if (diceScore.TryGetValue(tile.diceNumber, out int ds))
                score += ds;

            resourceTypes.Add(tile.resourceType);
        }

        // 資源多様性ボーナス
        score += resourceTypes.Count * 1.5f;

        // 港ボーナス
        if (v.hasPort) score += 2f;

        // わずかなランダム性（同スコアの場合に選択がばらけるように）
        score += Random.Range(0f, 0.5f);

        return score;
    }

    EdgePoint FindSetupRoad(string playerID)
    {
        var lastSettlement = GameManager.Instance.lastBuiltSettlement;
        if (lastSettlement == null) return null;

        // lastSettlementに接続するEdgeからランダムに選ぶ
        List<EdgePoint> candidates = new List<EdgePoint>();
        foreach (var edge in lastSettlement.edges)
        {
            if (!edge.hasRoad && edge.CanBuildRoad(playerID))
                candidates.Add(edge);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    // =========== Playing フェーズ ===========

    IEnumerator PlayingPhaseAI()
    {
        var gm = GameManager.Instance;
        string player = gm.CurrentPlayer;

        // 1. ダイスロール
        UpdateInfoText($"{player} がダイスを振ります...");
        yield return new WaitForSeconds(actionDelay * 0.5f);
        gm.OnClickRollDice();

        // ダイスアニメーション完了を待つ
        while (gm.isDiceRolling)
            yield return null;

        yield return new WaitForSeconds(actionDelay);

        // 2. 盗賊移動（7が出た場合）
        if (gm.currentStep == GameManager.TurnStep.MoveRobber)
        {
            yield return HandleRobberAI(player);
            yield return new WaitForSeconds(actionDelay);
        }

        // 3. 建設＆交易ループ（最大5回まで試行）
        for (int attempt = 0; attempt < 5; attempt++)
        {
            bool didSomething = false;

            // 都市化
            if (TryUpgradeCity(player))
            {
                yield return new WaitForSeconds(actionDelay);
                didSomething = true;
                continue;
            }

            // 開拓地建設
            if (TryBuildSettlement(player))
            {
                yield return new WaitForSeconds(actionDelay);
                didSomething = true;
                continue;
            }

            // 道建設
            if (TryBuildRoad(player))
            {
                yield return new WaitForSeconds(actionDelay);
                didSomething = true;
                continue;
            }

            // 交易して再試行
            if (TryTrade(player))
            {
                yield return new WaitForSeconds(actionDelay);
                didSomething = true;
                continue;
            }

            if (!didSomething) break;
        }

        // 4. ターン終了
        yield return new WaitForSeconds(actionDelay * 0.5f);
        gm.OnClickEndTurn();
    }

    // =========== 盗賊AI ===========

    IEnumerator HandleRobberAI(string player)
    {
        var gm = GameManager.Instance;
        UpdateInfoText($"{player} が盗賊を移動...");
        yield return new WaitForSeconds(actionDelay);

        HexTileData bestTile = FindBestRobberTile(player);
        if (bestTile != null)
        {
            gm.MoveRobberTo(bestTile);
        }
    }

    HexTileData FindBestRobberTile(string player)
    {
        var gm = GameManager.Instance;
        var tiles = gm.mapGenerator.GetAllTiles();

        HexTileData bestTile = null;
        int bestScore = -1;

        foreach (var tile in tiles)
        {
            if (tile == gm.currentRobberTile) continue;
            if (tile.resourceType == CatanMapGenerator.HexType.Beach) continue;
            if (tile.resourceType == CatanMapGenerator.HexType.Desert) continue;

            int score = 0;
            bool hasOwnBuilding = false;

            foreach (var v in tile.adjacentVertices)
            {
                if (!v.hasBuilding) continue;
                if (v.ownerPlayer == player)
                {
                    hasOwnBuilding = true;
                    break;
                }
                score += v.isCity ? 2 : 1;
            }

            // 自分の建物があるタイルは避ける
            if (hasOwnBuilding) continue;

            // 出目確率も考慮
            if (diceScore.TryGetValue(tile.diceNumber, out int ds))
                score += ds;

            if (score > bestScore)
            {
                bestScore = score;
                bestTile = tile;
            }
        }

        // 何も見つからなければ適当なタイル
        if (bestTile == null)
        {
            var candidates = tiles.FindAll(t =>
                t != gm.currentRobberTile &&
                t.resourceType != CatanMapGenerator.HexType.Beach);
            if (candidates.Count > 0)
                bestTile = candidates[Random.Range(0, candidates.Count)];
        }

        return bestTile;
    }

    // =========== 建設AI ===========

    bool TryUpgradeCity(string player)
    {
        var gm = GameManager.Instance;
        // コスト: 鉄3, 麦2
        if (gm.GetResourceCount(player, CatanMapGenerator.HexType.Ore) < 3 ||
            gm.GetResourceCount(player, CatanMapGenerator.HexType.Wheat) < 2)
            return false;

        foreach (var v in VertexPoint.AllVertices)
        {
            if (v.hasBuilding && v.ownerPlayer == player && !v.isCity)
            {
                if (gm.TryConsumeResources(player, 0, 0, 3, 2, 0))
                {
                    UpdateInfoText($"{player} が都市化！");
                    v.UpgradeToCity(player);
                    return true;
                }
            }
        }
        return false;
    }

    bool TryBuildSettlement(string player)
    {
        var gm = GameManager.Instance;
        // コスト: 木1, 土1, 麦1, 羊1
        if (gm.GetResourceCount(player, CatanMapGenerator.HexType.Wood) < 1 ||
            gm.GetResourceCount(player, CatanMapGenerator.HexType.Brick) < 1 ||
            gm.GetResourceCount(player, CatanMapGenerator.HexType.Wheat) < 1 ||
            gm.GetResourceCount(player, CatanMapGenerator.HexType.Sheep) < 1)
            return false;

        // 最高スコアの場所を見つける
        VertexPoint best = FindBestVertex(player);
        if (best == null) return false;

        if (gm.TryConsumeResources(player, 1, 1, 0, 1, 1))
        {
            UpdateInfoText($"{player} が開拓地を建設！");
            best.BuildSettlement(player);
            return true;
        }
        return false;
    }

    bool TryBuildRoad(string player)
    {
        var gm = GameManager.Instance;
        // コスト: 木1, 土1
        if (gm.GetResourceCount(player, CatanMapGenerator.HexType.Wood) < 1 ||
            gm.GetResourceCount(player, CatanMapGenerator.HexType.Brick) < 1)
            return false;

        // 自分の建物/道に接続しているEdgeを探す
        EdgePoint bestEdge = FindBestRoad(player);
        if (bestEdge == null) return false;

        if (gm.TryConsumeResources(player, 1, 1, 0, 0, 0))
        {
            UpdateInfoText($"{player} が道を建設！");
            bestEdge.BuildRoad(player);
            return true;
        }
        return false;
    }

    EdgePoint FindBestRoad(string player)
    {
        // 自分の建物/道に繋がっているEdgeで、まだ道がないもの
        // 将来の開拓地候補に近い場所を優先
        List<EdgePoint> candidates = new List<EdgePoint>();

        foreach (var v in VertexPoint.AllVertices)
        {
            bool isMyVertex = (v.hasBuilding && v.ownerPlayer == player);
            bool hasMyRoad = false;
            foreach (var e in v.edges)
            {
                if (e.hasRoad && e.ownerPlayer == player)
                {
                    hasMyRoad = true;
                    break;
                }
            }

            if (!isMyVertex && !hasMyRoad) continue;

            foreach (var edge in v.edges)
            {
                if (!edge.hasRoad && edge.CanBuildRoad(player))
                    candidates.Add(edge);
            }
        }

        if (candidates.Count == 0) return null;

        // スコアリング: 道の先に良い開拓地候補があるEdgeを優先
        EdgePoint best = null;
        float bestScore = -1f;

        foreach (var edge in candidates)
        {
            float score = 0f;
            // 道の先端の頂点で開拓地が建てられそうか
            VertexPoint farVertex = GetFarVertex(edge, player);
            if (farVertex != null && !farVertex.hasBuilding)
            {
                score = ScoreVertex(farVertex);
            }
            score += Random.Range(0f, 1f);

            if (score > bestScore)
            {
                bestScore = score;
                best = edge;
            }
        }

        return best;
    }

    VertexPoint GetFarVertex(EdgePoint edge, string player)
    {
        // Edgeの2頂点のうち、自分の建物/道から遠い方
        bool v1Mine = (edge.vertex1.hasBuilding && edge.vertex1.ownerPlayer == player);
        bool v2Mine = (edge.vertex2.hasBuilding && edge.vertex2.ownerPlayer == player);

        if (v1Mine && !v2Mine) return edge.vertex2;
        if (v2Mine && !v1Mine) return edge.vertex1;

        // 両方自分 or 両方違う → ランダム
        return Random.value > 0.5f ? edge.vertex1 : edge.vertex2;
    }

    // =========== 交易AI ===========

    bool TryTrade(string player)
    {
        var gm = GameManager.Instance;

        // 各資源の所持数と必要度を計算
        CatanMapGenerator.HexType[] types = {
            CatanMapGenerator.HexType.Wood,
            CatanMapGenerator.HexType.Brick,
            CatanMapGenerator.HexType.Ore,
            CatanMapGenerator.HexType.Wheat,
            CatanMapGenerator.HexType.Sheep
        };

        // 最も余っている資材を見つける
        CatanMapGenerator.HexType bestGive = CatanMapGenerator.HexType.Desert;
        int bestGiveCount = 0;

        foreach (var type in types)
        {
            int count = gm.GetResourceCount(player, type);
            int cost = gm.GetTradeCost(player, type);
            if (count >= cost && count > bestGiveCount)
            {
                bestGiveCount = count;
                bestGive = type;
            }
        }

        if (bestGive == CatanMapGenerator.HexType.Desert) return false;

        // 最も足りない資材を見つける（0に近いものを優先）
        CatanMapGenerator.HexType bestGet = CatanMapGenerator.HexType.Desert;
        int lowestCount = int.MaxValue;

        foreach (var type in types)
        {
            if (type == bestGive) continue;
            int count = gm.GetResourceCount(player, type);
            if (count < lowestCount)
            {
                lowestCount = count;
                bestGet = type;
            }
        }

        if (bestGet == CatanMapGenerator.HexType.Desert) return false;

        int tradeCost = gm.GetTradeCost(player, bestGive);
        if (gm.GetResourceCount(player, bestGive) >= tradeCost)
        {
            UpdateInfoText($"{player} が {bestGive} → {bestGet} を交易！");
            gm.ExecuteTrade(player, bestGive, bestGet, tradeCost);
            return true;
        }

        return false;
    }

    // =========== ユーティリティ ===========

    void UpdateInfoText(string text)
    {
        if (GameManager.Instance.gameInfoText != null)
            GameManager.Instance.gameInfoText.text = text;
        Debug.Log($"[AI] {text}");
    }
}
