using UnityEngine;
using System.Collections.Generic;

public class HexTileData : MonoBehaviour
{
    public int q;
    public int r;
    public CatanMapGenerator.HexType resourceType;
    public int diceNumber; // 2〜12の数字
    public bool hasRobber = false; // 盗賊がいるか
    public GameObject highlightObject; // 盗賊移動時に表示するハイライト

    // このタイルの周囲にある6つの頂点
    public List<VertexPoint> adjacentVertices = new List<VertexPoint>();

    // ダイスが当たった時の処理
    public void DistributeResources()
    {
        if (resourceType == CatanMapGenerator.HexType.Desert) return;

        // 盗賊がいる場合は資源が出ない
        if (hasRobber) 
        {
            Debug.Log($"盗賊が {resourceType} タイル(数字:{diceNumber}) をブロックしました！");
            return;
        }

        Debug.Log($"タイル[{diceNumber}]({resourceType}) がヒット！ 周囲の家を確認します...");

        foreach (var vertex in adjacentVertices)
        {
            if (vertex.hasBuilding)
            {
                int amount = vertex.isCity ? 2 : 1;
                Debug.Log($"-> {vertex.ownerPlayer} に {resourceType} を{amount}つ付与！");
                GameManager.Instance.AddResource(vertex.ownerPlayer, resourceType, amount);
                if (ResourceFlyEffect.Instance != null)
                {
                    ResourceFlyEffect.Instance.Fly(transform.position, vertex.ownerPlayer, resourceType, amount);
                }
            }
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance != null && GameManager.Instance.isExploreMode) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHexClicked(this);
        }
    }
}