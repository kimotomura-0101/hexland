using UnityEngine;
using System.Collections.Generic;

public class MenuBackgroundGenerator : MonoBehaviour
{
    [Header("References")]
    public CatanMapGenerator mapGenerator;

    [Header("Settings")]
    [Tooltip("背景用の広さ（半径）")]
    public int backgroundRadius = 5;

    [Tooltip("タイル間の追加隙間")]
    public float backgroundGap = 0.5f;

    private List<GameObject> spawnedTiles = new List<GameObject>();

    Transform GetContainer()
    {
        var existing = transform.Find("MenuBackgroundTiles");
        if (existing != null) return existing;
        var go = new GameObject("MenuBackgroundTiles");
        go.transform.SetParent(transform);
        return go.transform;
    }

    [ContextMenu("背景タイル生成")]
    public void Generate()
    {
        Clear();

        if (mapGenerator == null) return;

        float hexSize = mapGenerator.manualSize;
        float currentRadius = hexSize + (backgroundGap / 2.0f);
        float width = Mathf.Sqrt(3) * currentRadius;
        float height = 2.0f * currentRadius * 0.75f;

        Vector3 centerPos = (mapGenerator.centerReference != null)
            ? mapGenerator.centerReference.position
            : Vector3.zero;

        // Desert以外の資材タイプ
        CatanMapGenerator.HexType[] types = {
            CatanMapGenerator.HexType.Wood,
            CatanMapGenerator.HexType.Brick,
            CatanMapGenerator.HexType.Ore,
            CatanMapGenerator.HexType.Wheat,
            CatanMapGenerator.HexType.Sheep
        };

        for (int q = -backgroundRadius; q <= backgroundRadius; q++)
        {
            int r1 = Mathf.Max(-backgroundRadius, -q - backgroundRadius);
            int r2 = Mathf.Min(backgroundRadius, -q + backgroundRadius);

            for (int r = r1; r <= r2; r++)
            {
                float x = width * (q + r / 2.0f);
                float z = height * r;
                Vector3 pos = new Vector3(x, 0, z) + centerPos;

                // ランダムな資材タイプ
                var resourceType = types[Random.Range(0, types.Length)];
                var setting = mapGenerator.materialSettings.Find(s => s.type == resourceType);
                GameObject prefab = (setting.tilePrefab != null) ? setting.tilePrefab : mapGenerator.hexPrefab;
                if (prefab == null) continue;

                GameObject tile = Instantiate(prefab, pos, Quaternion.identity, GetContainer());

                if (mapGenerator.centerReference != null)
                {
                    tile.transform.localScale = mapGenerator.centerReference.localScale;
                    tile.transform.rotation = mapGenerator.centerReference.rotation;
                }

                // 固有プレハブがなければマテリアル適用
                if (setting.tilePrefab == null)
                {
                    var mr = tile.GetComponentInChildren<MeshRenderer>();
                    if (mr != null && setting.material != null)
                        mr.material = setting.material;
                }

                // コライダー・不要コンポーネント削除（装飾用なので）
                foreach (var col in tile.GetComponentsInChildren<Collider>())
                    Destroy(col);

                tile.name = $"BG_{q}_{r}_{resourceType}";
                spawnedTiles.Add(tile);
            }
        }
    }

    [ContextMenu("背景タイル削除")]
    public void Clear()
    {
        // リストに残っているものを削除
        foreach (var tile in spawnedTiles)
        {
            if (tile != null) DestroyImmediate(tile);
        }
        spawnedTiles.Clear();

        // コンテナごと削除（エディタで生成した分もまとめて消える）
        var container = transform.Find("MenuBackgroundTiles");
        if (container != null) DestroyImmediate(container.gameObject);
    }
}
