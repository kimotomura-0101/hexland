using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class CatanMapGenerator : MonoBehaviour
{
    public enum HexType { Desert, Wood, Brick, Ore, Wheat, Sheep, Any, Beach }

    [System.Serializable]
    public struct HexSetting { public HexType type; public Material material; public Sprite icon; public GameObject resourcePrefab; public float resourceScale; public GameObject tilePrefab; }

    [Header("プレハブ")]
    public GameObject hexPrefab;
    public GameObject vertexPrefab;
    public GameObject numberTokenPrefab;
    public GameObject portPrefab; // 港のプレハブ
    public GameObject edgePrefab; // 道を置く場所（クリック判定用）
    public GameObject robberPrefab; // 盗賊のプレハブ
    public GameObject pierPrefab; // 桟橋のプレハブ
    public float robberHeightOffset = 1.0f; // 盗賊の高さオフセット
    public float robberScale = 1.0f; // 盗賊のスケール
    public float robberDropHeight = 10.0f; // 盗賊が出現する高さ
    
    [Header("盗賊ハイライト設定")]
    public GameObject robberHighlightPrefab; // ハイライト用プレハブ
    public float robberHighlightScale = 1.8f; // XZスケール（タイルの大きさに対する倍率）
    public float robberHighlightHeightScale = 0.5f; // Yスケール（扁平率）
    public float robberHighlightYOffset = 0.0f; // 高さオフセット
    public Color robberHighlightColor = new Color(1f, 0.8f, 0.2f, 0.5f); // ハイライトの色

    [Header("港トークン設定")]
    public GameObject portTokenPrefab;
    public float portTokenHeightOffset = 2.0f;
    public string resourceSpawnPointName = "ResourceSpawnPoint";
    public Vector3 resourcePrefabOffset = new Vector3(0, 0.5f, -2.0f);
    public float resourcePrefabScale = 1.0f;
    public Color silhouetteColor = new Color(0, 0, 0, 0.8f);
    public Color transparencyKeyColor = Color.gray;
    public float transparencyThreshold = 0.1f;
    public float pierWidth = 0.2f; // 桟橋の幅
    public float pierLengthRatio = 0.9f; // 港までの距離に対する桟橋の長さの割合
    public float pierHeightOffset = 0.1f; // 高さ調整

    [Header("サイズ調整（重要）")]
    [Tooltip("自動でサイズを計算するか")]
    public bool useAutoSize = false; // ★ここをFalseにすると手動調整モードになります

    [Tooltip("手動設定時のタイルの大きさ（半径）")]
    public float manualSize = 1.0f; // ★これをいじって間隔を広げてください

    [Tooltip("頂点の位置がズレる場合はこれを変更 (30, 90, 0 など)")]
    public float vertexAngleOffset = 30.0f;

    [Tooltip("頂点の高さ調整 (z=-30など)。タイルの上にくるように調整")]
    public float vertexHeightOffset = -30.0f;

    [Tooltip("数字トークンの高さ調整")]
    public float tokenHeightOffset = 2.0f;

    [Tooltip("道の高さ調整")]
    public float edgeHeightOffset = 0.1f;

    [Tooltip("建設された道の位置微調整 (x, y, z)")]
    public Vector3 roadPositionOffset;

    [Tooltip("さらに隙間を空けたい場合")]
    public float gap = 0.0f;

    [Tooltip("港の配置距離（タイル中心からの倍率）")]
    public float portRadiusOffset = 1.0f;

    [Header("砂浜設定")]
    [Tooltip("砂浜の層の数")]
    public int beachLayers = 1;
    [Tooltip("砂浜のタイルを外側にずらす距離")]
    public float beachDistanceOffset = 0.0f;
    [Tooltip("砂浜の傾斜角度")]
    public float beachSlopeAngle = 15.0f;
    [Tooltip("砂浜のY座標を下げる量（マップの高さに合わせる）")]
    public float beachDropAmount = 5.0f;

    [Header("設定")]
    public int mapRadius = 2;
    public List<HexSetting> materialSettings;
    public Transform centerReference; // 基準の回転などはここから取る
    public Transform parentObject;

    // データ管理用
    private Dictionary<string, VertexPoint> vertexMap = new Dictionary<string, VertexPoint>();
    private Dictionary<string, EdgePoint> edgeMap = new Dictionary<string, EdgePoint>();
    private List<HexTileData> allTiles = new List<HexTileData>();
    private Dictionary<HexType, Sprite> processedIcons = new Dictionary<HexType, Sprite>();

    public float CurrentHexSize { get; private set; } = 1.0f;

    Vector3 GetHexPos(int q, int r, float width, float height, Vector3 centerPos)
    {
        float x = width * (q + r / 2.0f);
        float z = height * r;
        return new Vector3(x, 0, z) + centerPos;
    }

    [ContextMenu("マップ生成 (Generate)")]
    public void GenerateMap()
    {
        ClearMap();
        vertexMap.Clear();
        edgeMap.Clear();
        allTiles.Clear();

        if (hexPrefab == null || vertexPrefab == null) return;

        Transform container = parentObject != null ? parentObject : this.transform;

        // ★サイズ決定ロジック
        float hexSize;
        if (useAutoSize && centerReference != null)
        {
            MeshFilter mf = centerReference.GetComponent<MeshFilter>();
            // メッシュの幅から半径を推測
            if (mf != null && mf.sharedMesh != null) hexSize = mf.sharedMesh.bounds.size.x * centerReference.localScale.x / 2.0f;
            else hexSize = centerReference.localScale.x;
        }
        else
        {
            // 手動設定の値を使う
            hexSize = manualSize;
        }
        CurrentHexSize = hexSize;

        // 隙間を足す
        float currentRadius = hexSize + (gap / 2.0f);

        // 六角形の配置間隔（ポイントトップ型）
        float width = Mathf.Sqrt(3) * currentRadius;
        float height = 2.0f * currentRadius * 0.75f;

        Vector3 centerPos = (centerReference != null) ? centerReference.position : Vector3.zero;


        // 資源の標準的な枚数プール (木4, 麦4, 羊4, 土3, 鉄3) 計18枚
        List<HexType> resourcePool = new List<HexType>();
        for (int i = 0; i < 4; i++) resourcePool.Add(HexType.Wood);
        for (int i = 0; i < 4; i++) resourcePool.Add(HexType.Wheat);
        for (int i = 0; i < 4; i++) resourcePool.Add(HexType.Sheep);
        for (int i = 0; i < 3; i++) resourcePool.Add(HexType.Brick);
        for (int i = 0; i < 3; i++) resourcePool.Add(HexType.Ore);

        // 資源シャッフル
        for (int i = 0; i < resourcePool.Count; i++)
        {
            HexType temp = resourcePool[i];
            int randomIndex = Random.Range(i, resourcePool.Count);
            resourcePool[i] = resourcePool[randomIndex];
            resourcePool[randomIndex] = temp;
        }
        int resourceIndex = 0;

        Dictionary<string, HexTileData> landTiles = new Dictionary<string, HexTileData>();

        // 1. 土地タイルの生成
        for (int q = -mapRadius; q <= mapRadius; q++)
        {
            int r1 = Mathf.Max(-mapRadius, -q - mapRadius);
            int r2 = Mathf.Min(mapRadius, -q + mapRadius);

            for (int r = r1; r <= r2; r++)
            {
                // 座標計算
                Vector3 worldPos = GetHexPos(q, r, width, height, centerPos);

                // 資源タイプ決定
                HexType resourceType;
                if (q == 0 && r == 0)
                {
                    resourceType = HexType.Desert;
                }
                else
                {
                    if (resourceIndex < resourcePool.Count)
                    {
                        resourceType = resourcePool[resourceIndex];
                        resourceIndex++;
                    }
                    else resourceType = (HexType)Random.Range(1, 6);
                }

                // プレハブ決定
                HexSetting setting = materialSettings.Find(s => s.type == resourceType);
                GameObject prefabToUse = (setting.tilePrefab != null) ? setting.tilePrefab : hexPrefab;

                // 生成
                GameObject newTileObj = Instantiate(prefabToUse, worldPos, Quaternion.identity, container);
                
                // 基準オブジェクトがあれば回転とスケールを合わせる
                if (centerReference != null)
                {
                    newTileObj.transform.localScale = centerReference.localScale;
                    newTileObj.transform.rotation = centerReference.rotation;
                }

                // データ設定
                HexTileData tileData = newTileObj.AddComponent<HexTileData>();
                tileData.q = q;
                tileData.r = r;
                tileData.resourceType = resourceType;
                
                tileData.diceNumber = 0; // 後で設定します

                // ★当たり判定の強化
                EnsureCollider(newTileObj);
                
                // ★盗賊用ハイライトの作成
                CreateHighlight(newTileObj, tileData);

                newTileObj.name = $"Hex_{q}_{r}_{tileData.resourceType}_{tileData.diceNumber}";

                // 固有プレハブがない場合のみマテリアル適用
                if (setting.tilePrefab == null)
                {
                    ApplyMaterial(newTileObj, tileData.resourceType);
                }

                
                // 頂点生成
                CreateAndLinkVertices(newTileObj, tileData, hexSize);
                allTiles.Add(tileData);
                landTiles.Add($"{q}_{r}", tileData);
            }
        }

        // 2. ビーチタイルの生成（外側の辺から伸ばす）
        int[] dq = { 1, 1, 0, -1, -1, 0 };
        int[] dr = { 0, -1, -1, 0, 1, 1 };

        foreach (var kvp in landTiles)
        {
            HexTileData landTile = kvp.Value;

            for (int i = 0; i < 6; i++)
            {
                int nq = landTile.q + dq[i];
                int nr = landTile.r + dr[i];
                string nKey = $"{nq}_{nr}";

                // 隣が土地でない場合、そこにビーチを生成して接続
                if (!landTiles.ContainsKey(nKey))
                {
                    Vector3 beachPos = GetHexPos(nq, nr, width, height, centerPos);

                    HexSetting setting = materialSettings.Find(s => s.type == HexType.Beach);
                    GameObject prefabToUse = (setting.tilePrefab != null) ? setting.tilePrefab : hexPrefab;

                    GameObject beachObj = Instantiate(prefabToUse, beachPos, Quaternion.identity, landTile.transform); // 親を土地にする
                    
                    if (centerReference != null)
                    {
                        beachObj.transform.localScale = centerReference.localScale;
                        beachObj.transform.rotation = centerReference.rotation;
                    }

                    HexTileData beachData = beachObj.AddComponent<HexTileData>();
                    beachData.q = nq;
                    beachData.r = nr;
                    beachData.resourceType = HexType.Beach;
                    beachData.diceNumber = 0;

                    // ★当たり判定の強化
                    EnsureCollider(beachObj);
                    
                    // ビーチには盗賊を置かないのでハイライトは不要だが、
                    // 必要ならここでCreateHighlightを呼ぶ

                    beachObj.name = $"Beach_{nq}_{nr}_from_{landTile.q}_{landTile.r}";
                    
                    if (setting.tilePrefab == null)
                    {
                        ApplyMaterial(beachObj, HexType.Beach);
                    }
                    
                    allTiles.Add(beachData);
                }
            }
        }
        
        // 数字の割り当てとトークン生成（隣接チェック付き）
        AssignNumbersAndCreateTokens();

        // ビーチの形状適用
        UpdateBeachTiles();

        // 港の生成
        GeneratePorts();

        // 盗賊の初期配置
        PlaceInitialRobber();
    }

    // タイル全体をクリックできるようにコライダーを調整する
    void EnsureCollider(GameObject obj)
    {
        // 以前の大きなBoxColliderロジックを廃止し、
        // シンプルにMeshCollider（Convex）を使用するように戻します。
        // これにより、道や家へのクリックを阻害しなくなります。
        
        // ルートにメッシュがあるか確認
        if (obj.GetComponent<MeshFilter>() != null)
        {
            MeshCollider mc = obj.GetComponent<MeshCollider>();
            if (mc == null) mc = obj.AddComponent<MeshCollider>();
            mc.convex = true;
        }
        else
        {
            // ルートにメッシュがない場合（空の親オブジェクトなど）、子オブジェクトを確認
            bool hasCollider = false;
            foreach(var childCollider in obj.GetComponentsInChildren<Collider>())
            {
                hasCollider = true;
                // 子のMeshColliderもConvexにする（Rigidbodyとの干渉を防ぐため）
                if (childCollider is MeshCollider mc) mc.convex = true;
            }

            if (!hasCollider)
            {
                // コライダーが全くない場合、子オブジェクトのメッシュにコライダーを追加
                foreach(var mf in obj.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.gameObject.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.convex = true;
                    }
                }
            }
        }
    }

    // 盗賊配置用のハイライトオブジェクトを作成
    void CreateHighlight(GameObject tileObj, HexTileData tileData)
    {
        GameObject highlight;

        if (robberHighlightPrefab != null)
        {
            // プレハブから生成
            highlight = Instantiate(robberHighlightPrefab, tileObj.transform);
            highlight.name = "RobberHighlight";
            
            // コライダーがない場合は追加する（クリック判定用）
            if (highlight.GetComponent<Collider>() == null)
            {
                MeshCollider mc = highlight.AddComponent<MeshCollider>();
                mc.convex = true;
            }
        }
        else
        {
            // プレハブがない場合はプロシージャル生成
            highlight = new GameObject("RobberHighlight");
            highlight.transform.SetParent(tileObj.transform);
            
            MeshFilter mf = highlight.AddComponent<MeshFilter>();
            MeshRenderer mr = highlight.AddComponent<MeshRenderer>();
            
            // 球体のメッシュを使用
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);

            // 半透明のマテリアルを作成
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = robberHighlightColor;
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mr.material = mat;

            // コライダーを追加
            MeshCollider mc = highlight.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = mf.mesh;
        }

        // 共通設定
        highlight.transform.localPosition = new Vector3(0, robberHighlightYOffset, 0);
        highlight.transform.localRotation = Quaternion.identity;
        
        float hSize = CurrentHexSize;
        float scale = hSize * robberHighlightScale;
        highlight.transform.localScale = new Vector3(scale, scale * robberHighlightHeightScale, scale);
        
        // 初期状態は非表示
        highlight.SetActive(false);
        
        tileData.highlightObject = highlight;
    }

    void AssignNumbersAndCreateTokens()
    {
        // 1. 資源タイル（砂漠以外、かつビーチ以外）を抽出
        var resourceTiles = allTiles.Where(t => t.resourceType != HexType.Desert && t.resourceType != HexType.Beach).ToList();

        // 2. 数字プール作成
        List<int> numberPool = new List<int> { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 };

        // 3. 配置ロジック（リトライ付き）
        int maxRetries = 1000;
        bool success = false;
        
        // 座標マップ作成（隣接チェック用）
        Dictionary<string, HexTileData> tileMap = new Dictionary<string, HexTileData>();
        foreach (var t in allTiles) tileMap[$"{t.q}_{t.r}"] = t;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // プールをシャッフル
            ShuffleList(numberPool);
            
            // 仮割り当て
            for (int i = 0; i < resourceTiles.Count; i++)
            {
                if (i < numberPool.Count)
                {
                    resourceTiles[i].diceNumber = numberPool[i];
                }
                else
                {
                    // プールが足りない場合はランダム生成
                    int n = Random.Range(2, 13);
                    while (n == 7) n = Random.Range(2, 13);
                    resourceTiles[i].diceNumber = n;
                }
            }

            // 制約チェック（同じ数字が隣り合っていないか）
            if (CheckPlacementValidity(resourceTiles, tileMap))
            {
                success = true;
                break;
            }
        }

        if (!success)
        {
            Debug.LogWarning("数字の配置に失敗しました（制約を満たせませんでした）。ランダムな配置のまま続行します。");
        }

        // 4. トークン生成
        foreach (var tile in allTiles)
        {
            if (tile.diceNumber > 0 && numberTokenPrefab != null)
            {
                GameObject tokenObj = Instantiate(numberTokenPrefab, tile.transform);
                tokenObj.transform.localPosition = new Vector3(0, tokenHeightOffset, 0);
                tokenObj.transform.localRotation = Quaternion.identity;

                // コライダーを削除してクリックが貫通するようにする
                foreach (var c in tokenObj.GetComponentsInChildren<Collider>()) Destroy(c);

                // 常にカメラの方を向くようにコンポーネントを追加
                if (tokenObj.GetComponent<Billboard>() == null) tokenObj.AddComponent<Billboard>();

                TextMeshPro tmp = tokenObj.GetComponentInChildren<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = tile.diceNumber.ToString();
                    tmp.color = (tile.diceNumber == 6 || tile.diceNumber == 8) ? Color.red : Color.black;

                    // 常に地形の手前に表示されるようにZTestを無効化（Always）にする
                    // これにより、カメラからの距離に関わらず最前面に描画されます
                    tmp.fontMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    tmp.fontMaterial.renderQueue = 4000; // Overlay
                }
                
                // 名前も更新しておく
                tile.name = $"Hex_{tile.q}_{tile.r}_{tile.resourceType}_{tile.diceNumber}";
            }
        }
    }

    bool CheckPlacementValidity(List<HexTileData> tiles, Dictionary<string, HexTileData> map)
    {
        // Axial座標の隣接オフセット
        int[] dq = { 1, 1, 0, -1, -1, 0 };
        int[] dr = { 0, -1, -1, 0, 1, 1 };

        foreach (var tile in tiles)
        {
            for (int i = 0; i < 6; i++)
            {
                int nq = tile.q + dq[i];
                int nr = tile.r + dr[i];
                string key = $"{nq}_{nr}";

                if (map.ContainsKey(key))
                {
                    var neighbor = map[key];
                    // 0は砂漠なので無視
                    if (neighbor.diceNumber != 0)
                    {
                        // 隣接タイルが同じ数字ならNG
                        if (neighbor.diceNumber == tile.diceNumber) return false;

                        // 6と8が隣り合うのもNG
                        if ((tile.diceNumber == 6 && neighbor.diceNumber == 8) ||
                            (tile.diceNumber == 8 && neighbor.diceNumber == 6))
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }

    void GeneratePorts()
    {
        if (portPrefab == null) return;

        // 港の種類プール (標準: 3:1 x4, 各資源2:1 x1)
        List<HexType> portPool = new List<HexType>();
        for (int i = 0; i < 4; i++) portPool.Add(HexType.Any);
        portPool.Add(HexType.Wood);
        portPool.Add(HexType.Brick);
        portPool.Add(HexType.Ore);
        portPool.Add(HexType.Wheat);
        portPool.Add(HexType.Sheep);
        
        ShuffleList(portPool);

        // 外周タイルを取得（中心からの距離が mapRadius のもの）
        // Axial座標距離: (|q| + |r| + |q+r|) / 2 ... ではなく max(|q|, |r|, |q+r|)
        List<HexTileData> borderTiles = allTiles.Where(t => 
            Mathf.Max(Mathf.Abs(t.q), Mathf.Abs(t.r), Mathf.Abs(t.q + t.r)) == mapRadius
        ).ToList();

        // 角度順にソート（反時計回り）
        Vector3 centerPos = (centerReference != null) ? centerReference.position : Vector3.zero;
        borderTiles.Sort((a, b) => {
            float angleA = Mathf.Atan2(a.transform.position.z - centerPos.z, a.transform.position.x - centerPos.x);
            float angleB = Mathf.Atan2(b.transform.position.z - centerPos.z, b.transform.position.x - centerPos.x);
            return angleA.CompareTo(angleB);
        });

        // 港を配置 (簡易的に2タイルに1つのペースで配置)
        int portIndex = 0;
        for (int i = 0; i < borderTiles.Count; i++)
        {
            if (portIndex >= portPool.Count) break;
            
            // 偶数番目のタイルにのみ配置（間隔を空けるため）
            if (i % 2 != 0) continue;

            HexTileData tile = borderTiles[i];
            HexType portType = portPool[portIndex];
            portIndex++;

            // ★変更: 最も外側にある「辺（2つの頂点）」を探す
            Vector3 tileDir = (tile.transform.position - centerPos).normalized;
            if (tileDir == Vector3.zero) tileDir = Vector3.forward;

            VertexPoint bestV1 = null;
            VertexPoint bestV2 = null;
            float maxDot = -1.0f;
            Vector3 bestEdgePos = Vector3.zero;

            // 隣接する頂点ペア（辺）を走査
            int vCount = tile.adjacentVertices.Count;
            for (int k = 0; k < vCount; k++)
            {
                VertexPoint v1 = tile.adjacentVertices[k];
                VertexPoint v2 = tile.adjacentVertices[(k + 1) % vCount]; // 次の頂点（ループする）

                // 辺の中点
                Vector3 edgePos = (v1.transform.position + v2.transform.position) / 2.0f;
                
                // タイル中心から辺へのベクトル
                Vector3 toEdgeDir = (edgePos - tile.transform.position).normalized;

                // マップ中心からの方向と最も一致する辺を選ぶ
                float dot = Vector3.Dot(tileDir, toEdgeDir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestV1 = v1;
                    bestV2 = v2;
                    bestEdgePos = edgePos;
                }
            }

            // 港の位置：辺の中点から少し外側にオフセットさせる
            // Y軸を無視して計算（海と平行にするため、かつY=0にするため）
            Vector3 flatEdgePos = new Vector3(bestEdgePos.x, 0, bestEdgePos.z);
            Vector3 flatTilePos = new Vector3(tile.transform.position.x, 0, tile.transform.position.z);

            Vector3 dir = (flatEdgePos - flatTilePos).normalized;
            Vector3 portPos = flatEdgePos + dir * (manualSize * (portRadiusOffset - 1.0f));

            GameObject portObj = Instantiate(portPrefab, portPos, Quaternion.identity, parentObject);
            portObj.name = $"Port_{portType}";

            // 港はタイルの中心を向く (水平に)
            portObj.transform.LookAt(flatTilePos);

            // 港トークンの生成
            if (portTokenPrefab != null)
            {
                GameObject tokenObj = Instantiate(portTokenPrefab, portObj.transform);
                tokenObj.transform.localPosition = new Vector3(0, portTokenHeightOffset, 0);
                tokenObj.transform.localRotation = Quaternion.identity;

                // コライダーを削除してクリックが貫通するようにする
                foreach (var c in tokenObj.GetComponentsInChildren<Collider>()) Destroy(c);

                if (tokenObj.GetComponent<Billboard>() == null) tokenObj.AddComponent<Billboard>();

                TextMeshPro tmp = tokenObj.GetComponentInChildren<TextMeshPro>();
                SpriteRenderer sr = tokenObj.GetComponentInChildren<SpriteRenderer>();

                if (portType == HexType.Any)
                {
                    // 3:1 (Any) の場合は真ん中に文字だけ
                    if (tmp != null)
                    {
                        tmp.text = "3:1";
                    }
                    if (sr != null)
                    {
                        sr.gameObject.SetActive(false);
                    }
                }
                else
                {
                    // 2:1 (資源) の場合は上に画像、下に文字
                    if (tmp != null)
                    {
                        tmp.text = "2:1";
                    }

                    if (sr != null)
                    {
                        sr.gameObject.SetActive(false);
                    }

                    // 設定から3Dモデルを取得して生成
                    HexSetting setting = materialSettings.Find(s => s.type == portType);
                    if (setting.resourcePrefab != null)
                    {
                        Vector3 worldPos;
                        Quaternion worldRot;

                        Transform spawnPoint = !string.IsNullOrEmpty(resourceSpawnPointName) ? portObj.transform.Find(resourceSpawnPointName) : null;
                        if (spawnPoint != null)
                        {
                            worldPos = spawnPoint.position;
                            worldRot = spawnPoint.rotation;
                        }
                        else
                        {
                            // 船の位置と向きから座標を計算 (フォールバック)
                            worldPos = portObj.transform.TransformPoint(resourcePrefabOffset);
                            worldRot = portObj.transform.rotation;
                        }

                        // 船の子にはせず、parentObjectの子として生成（座標バグ回避のため）
                        GameObject resObj = Instantiate(setting.resourcePrefab, worldPos, worldRot, parentObject);
                        resObj.name = "ResourceModel";
                        float scale = setting.resourceScale > 0 ? setting.resourceScale : resourcePrefabScale;
                        resObj.transform.localScale = Vector3.one * scale;

                        // 資源モデルのコライダーも削除
                        foreach (var c in resObj.GetComponentsInChildren<Collider>()) Destroy(c);
                    }
                }
            }

            // ★頂点に港情報を書き込む
            if (bestV1 != null) { bestV1.hasPort = true; bestV1.portType = portType; }
            if (bestV2 != null) { bestV2.hasPort = true; bestV2.portType = portType; }

            // ★桟橋の生成
            if (bestV1 != null) CreatePier(bestV1.transform.position, portObj.transform.position);
            if (bestV2 != null) CreatePier(bestV2.transform.position, portObj.transform.position);
        }
    }

    void CreatePier(Vector3 vertexPos, Vector3 portPos)
    {
        Vector3 start = vertexPos;
        Vector3 end = portPos;
        
        // 水平方向のベクトルと距離を計算
        Vector3 dir = (end - start);
        dir.y = 0;
        float distance = dir.magnitude;
        
        if (distance < 0.001f) return;
        
        dir.Normalize();

        // 桟橋の長さ
        float length = distance * pierLengthRatio;
        
        // 配置位置：頂点から港へ向かって、長さの半分だけ進んだ位置（Cube等の中心基準スケーリングを想定）
        Vector3 pos = start + dir * (length * 0.5f);
        
        // 高さは頂点の高さ + オフセット
        pos.y = start.y + pierHeightOffset;

        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-90, 0, 0);

        GameObject pier;
        if (pierPrefab != null)
            pier = Instantiate(pierPrefab, pos, rot, parentObject);
        else
        {
            pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pier.transform.position = pos;
            pier.transform.rotation = rot;
            pier.transform.SetParent(parentObject);
        }
        pier.name = "Pier";

        // 桟橋データを保存（動的更新用）
        PierData data = pier.GetComponent<PierData>();
        if (data == null) data = pier.AddComponent<PierData>();
        data.startPosition = start;
        data.endPosition = end;
    }

    void PlaceInitialRobber()
    {
        // 砂漠タイルを探す
        HexTileData desert = allTiles.Find(t => t.resourceType == HexType.Desert);
        if (desert != null)
        {
            Vector3 spawnPos = desert.transform.position + Vector3.up * robberDropHeight;
            GameObject robber;

            if (robberPrefab != null)
            {
                // 盗賊を生成
                robber = Instantiate(robberPrefab, spawnPos, Quaternion.identity, parentObject);
            }
            else
            {
                // プレハブがない場合は仮のオブジェクト（黒い円柱）を作成
                robber = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                robber.transform.position = spawnPos;
                robber.transform.SetParent(parentObject);
                
                // 黒色にする
                var renderer = robber.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.black;
                
                // コライダーは物理演算（着地）のために残す
            }

            robber.transform.localScale = Vector3.one * robberScale;
            robber.name = "Robber";

            // 物理演算コンポーネントの確認・追加
            Rigidbody rb = robber.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = robber.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // 転倒防止
            }
            // ★追加: 高速落下時のすり抜けを防ぐ設定
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // ★追加: コライダーの確認と自動設定
            Collider[] cols = robber.GetComponentsInChildren<Collider>();
            if (cols.Length == 0)
            {
                // コライダーが一つもない場合は、簡易的にCapsuleColliderを追加
                CapsuleCollider cc = robber.AddComponent<CapsuleCollider>();
                cc.center = Vector3.up * 1.0f; // 足元基準と仮定して中心を上げる
                cc.height = 2.0f;
                cc.radius = 0.5f;
            }
            else
            {
                // MeshColliderを使用している場合、Rigidbodyと共に使うにはConvexである必要がある
                foreach (var c in cols)
                {
                    if (c is MeshCollider mc && !mc.convex) mc.convex = true;
                }
            }

            
            // GameManagerに登録
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetRobber(robber, desert);
            }
        }
    }

    public void UpdateBeachTiles()
    {
        if (allTiles == null || allTiles.Count == 0) return;

        float hexSize = CurrentHexSize;
        // 隙間を足す
        float currentRadius = hexSize + (gap / 2.0f);
        float width = Mathf.Sqrt(3) * currentRadius;
        float height = 2.0f * currentRadius * 0.75f;
        Vector3 centerPos = (centerReference != null) ? centerReference.position : Vector3.zero;

        foreach (var tile in allTiles)
        {
            if (tile.resourceType == HexType.Beach)
            {
                // 親（土地タイル）を取得
                var parentTile = tile.transform.parent?.GetComponent<HexTileData>();
                if (parentTile == null) continue;

                Vector3 landPos = GetHexPos(parentTile.q, parentTile.r, width, height, centerPos);
                Vector3 beachPos = GetHexPos(tile.q, tile.r, width, height, centerPos);
                
                // 回転とスケールのリセット（基準に合わせる）
                if (centerReference != null)
                {
                    tile.transform.localScale = centerReference.localScale;
                    tile.transform.rotation = centerReference.rotation;
                }
                else
                {
                    tile.transform.localScale = Vector3.one;
                    tile.transform.rotation = Quaternion.identity;
                }
                
                // 位置を一旦リセット
                tile.transform.position = beachPos;

                // 距離オフセット適用
                if (beachDistanceOffset != 0.0f)
                {
                    Vector3 dir = (beachPos - landPos).normalized;
                    tile.transform.position += dir * beachDistanceOffset;
                }

                // ★メッシュ変形による傾斜処理
                MeshFilter mf = tile.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // 元のメッシュ（プレハブの形状）を取得
                    // 注意: ここでmf.meshを使うと、既に変形済みのメッシュを取得してしまうため、
                    // 常に「変形前」の状態である hexPrefab のメッシュ情報を参照するのが理想ですが、
                    // 簡易的に sharedMesh (アセットのメッシュ) を基準にします。
                    Mesh originalMesh = mf.sharedMesh;
                    Vector3[] originalVertices = originalMesh.vertices;
                    Vector3[] newVertices = new Vector3[originalVertices.Length];

                    // 傾斜の計算係数
                    float tanAngle = Mathf.Tan(beachSlopeAngle * Mathf.Deg2Rad);

                    for (int i = 0; i < originalVertices.Length; i++)
                    {
                        // 頂点のワールド座標を計算（変形前のフラットな状態での位置）
                        Vector3 worldV = tile.transform.TransformPoint(originalVertices[i]);

                        // 親タイル（陸地）の中心からの水平距離を計算
                        float dist = Vector3.Distance(new Vector3(worldV.x, landPos.y, worldV.z), landPos);

                        // 六角形の半径（hexSize）を超えた分だけ下げる
                        // ※ hexSize は中心から角までの距離。辺までは sqrt(3)/2 * hexSize なので、
                        //   厳密には辺から下げ始めるなら係数が必要ですが、hexSize基準でなだらかにするのが自然です。
                        float drop = 0;
                        if (dist > hexSize)
                        {
                            drop = (dist - hexSize) * tanAngle;
                        }

                        // Y座標を下げる（ワールド空間で下げる）
                        Vector3 newWorldV = worldV - Vector3.up * drop;

                        // ローカル座標に戻して格納
                        newVertices[i] = tile.transform.InverseTransformPoint(newWorldV);
                    }

                    // 新しいメッシュを作成して割り当て
                    Mesh newMesh = new Mesh();
                    newMesh.vertices = newVertices;
                    newMesh.triangles = originalMesh.triangles;
                    newMesh.uv = originalMesh.uv;
                    newMesh.normals = originalMesh.normals; // 必要ならRecalculateNormals()
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();

                    mf.mesh = newMesh;

                    // コライダーも更新
                    MeshCollider mc = tile.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        mc.sharedMesh = newMesh;
                    }
                }
            }
        }
    }

    public void UpdatePortTokenHeight()
    {
        Transform container = parentObject != null ? parentObject : this.transform;
        if (container == null) return;

        // 港オブジェクトを探してトークンの高さを更新
        foreach (Transform child in container)
        {
            if (child.name.StartsWith("Port_"))
            {
                foreach (Transform token in child)
                {
                    // Billboardがついているものがトークンとみなす
                    if (token.GetComponent<Billboard>() != null)
                    {
                        Vector3 pos = token.localPosition;
                        pos.y = portTokenHeightOffset;
                        token.localPosition = pos;
                    }
                }
            }
        }
    }

    void OnValidate()
    {
        if (allTiles != null && allTiles.Count > 0)
        {
            UpdateBeachTiles();
            UpdateRobberHighlightSettings();
        }
        UpdatePortTokenHeight();
        UpdatePierSettings();
        UpdateRobberSettings();
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    void CreateAndLinkVertices(GameObject tileObj, HexTileData tileData, float size)
    {
        for (int i = 0; i < 6; i++)
        {
            // ★変更: ここで angleOffset を足すようにしました
            float angle_deg = 60 * i + vertexAngleOffset;
            
            float angle_rad = angle_deg * Mathf.Deg2Rad;
            
            // Unityの座標系(X=横, Z=縦)に合わせて計算
            // Cos=X, Sin=Z なので、90度が画面上の「上」になります
            Vector3 offset = new Vector3(Mathf.Cos(angle_rad) * size, vertexHeightOffset, Mathf.Sin(angle_rad) * size);
            Vector3 vPos = tileObj.transform.position + offset;

            // 誤差対策で丸める
            string key = $"{Mathf.Round(vPos.x * 100)}_{Mathf.Round(vPos.z * 100)}";

            VertexPoint vPoint;
            if (vertexMap.ContainsKey(key))
            {
                vPoint = vertexMap[key];
            }
            else
            {
                GameObject vObj = Instantiate(vertexPrefab, vPos, Quaternion.identity, parentObject);
                vObj.name = "Vertex";
                vPoint = vObj.AddComponent<VertexPoint>();
                vertexMap.Add(key, vPoint);
            }
            tileData.adjacentVertices.Add(vPoint);
            vPoint.adjacentTiles.Add(tileData);
        }

        // 辺(Edge)の生成とリンク
        for (int i = 0; i < 6; i++)
        {
            VertexPoint v1 = tileData.adjacentVertices[i];
            VertexPoint v2 = tileData.adjacentVertices[(i + 1) % 6];
            CreateEdge(v1, v2);
        }
    }

    void ApplyMaterial(GameObject tile, HexType type)
    {
        foreach (var s in materialSettings)
        {
            if (s.type == type)
            {
                MeshRenderer r = tile.GetComponent<MeshRenderer>();
                // マテリアル配列の全要素を変えるか、1つ目だけ変えるか
                if (r) {
                    Material[] m = r.sharedMaterials;
                    if (m.Length > 0) { 
                        // ProBuilderなどで側面がある場合、上面(m[0]かm[1])だけ変えたいケースが多い
                        m[0] = s.material; 
                        r.sharedMaterials = m; 
                    }
                }
            }
        }
    }

    void CreateEdge(VertexPoint v1, VertexPoint v2)
    {
        if (edgePrefab == null) return;

        Vector3 p1 = v1.transform.position;
        Vector3 p2 = v2.transform.position;

        // キー生成 (座標ベースでユニークにする)
        // 頂点生成時と同じ丸め方をする
        string k1 = $"{Mathf.Round(p1.x * 100)}_{Mathf.Round(p1.z * 100)}";
        string k2 = $"{Mathf.Round(p2.x * 100)}_{Mathf.Round(p2.z * 100)}";
        
        // 順序を固定してキーを作る (例: "小さい方-大きい方")
        string edgeKey = string.Compare(k1, k2) < 0 ? $"{k1}-{k2}" : $"{k2}-{k1}";

        if (edgeMap.ContainsKey(edgeKey)) return;

        // 配置位置と回転
        Vector3 mid = (p1 + p2) / 2.0f;
        
        Vector3 dir = p2 - p1;
        if (dir.sqrMagnitude < 0.001f) return; // 重なっている場合はスキップ

        // 道が直角になるのを防ぐため、90度回転させる
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 90, 0);

        GameObject edgeObj = Instantiate(edgePrefab, mid, rot, parentObject);
        edgeObj.name = $"Edge_{edgeKey}";
        
        EdgePoint ep = edgeObj.AddComponent<EdgePoint>();
        ep.vertex1 = v1;
        ep.vertex2 = v2;
        ep.roadPositionOffset = roadPositionOffset;

        // 頂点に辺を登録（接続チェック用）
        v1.edges.Add(ep);
        v2.edges.Add(ep);

        edgeMap.Add(edgeKey, ep);
    }

    public void ClearMap()
    {
        // 念のためリストもクリア
        vertexMap.Clear();
        edgeMap.Clear();
        allTiles.Clear();
        ClearProcessedIcons();
        
        // 子オブジェクト削除
        Transform container = parentObject != null ? parentObject : this.transform;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(container.GetChild(i).gameObject);
        }
    }

    public List<HexTileData> GetAllTiles() => allTiles;

    void ClearProcessedIcons()
    {
        foreach (var sprite in processedIcons.Values)
        {
            if (sprite != null)
            {
                if (sprite.texture != null)
                {
                    if (Application.isPlaying) Destroy(sprite.texture);
                    else DestroyImmediate(sprite.texture);
                }
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
        }
        processedIcons.Clear();
    }

    Sprite CreateSilhouetteSprite(Sprite original)
    {
        if (original == null) return null;

        Texture2D tex = original.texture;
        if (tex == null) return null;

        // Read/Write Enabledがオフでも動作するようにRenderTextureを経由して読み込む
        Texture2D readableTex = null;
        if (tex.isReadable)
        {
            readableTex = tex;
        }
        else
        {
            RenderTexture tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            readableTex = new Texture2D(tex.width, tex.height);
            readableTex.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readableTex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
        }

        try
        {
            Color[] pixels = readableTex.GetPixels();
            Color[] newPixels = new Color[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                
                // 元々透明な部分は透明のままにする
                if (p.a < 0.1f)
                {
                    newPixels[i] = Color.clear;
                    continue;
                }

                // 灰色成分（キーカラー）との距離を計算
                float diff = Mathf.Abs(p.r - transparencyKeyColor.r) + 
                             Mathf.Abs(p.g - transparencyKeyColor.g) + 
                             Mathf.Abs(p.b - transparencyKeyColor.b);

                // 閾値以下なら透明、それ以外はシルエット色
                newPixels[i] = (diff < transparencyThreshold) ? Color.clear : silhouetteColor;
            }

            Texture2D newTex = new Texture2D(readableTex.width, readableTex.height, TextureFormat.RGBA32, false);
            newTex.SetPixels(newPixels);
            newTex.Apply();

            return Sprite.Create(newTex, original.rect, original.pivot, original.pixelsPerUnit);
        }
        catch (UnityException e)
        {
            Debug.LogWarning($"スプライト '{original.name}' の処理に失敗しました。テクスチャのインポート設定で 'Read/Write Enabled' をオンにしてください。\n{e.Message}");
            return original;
        }
        finally
        {
            // テンポラリで作成した場合は破棄
            if (readableTex != null && readableTex != tex)
            {
                if (Application.isPlaying) Destroy(readableTex);
                else DestroyImmediate(readableTex);
            }
        }
    }

    public void UpdateRobberHighlightSettings()
    {
        // リストが空なら再取得を試みる（コンパイル後など）
        if (allTiles == null || allTiles.Count == 0)
        {
            if (parentObject != null)
                allTiles = new List<HexTileData>(parentObject.GetComponentsInChildren<HexTileData>());
            else
                allTiles = new List<HexTileData>(GetComponentsInChildren<HexTileData>());
        }

        if (allTiles == null || allTiles.Count == 0) return;

        // サイズ再計算（GenerateMapと同じロジック）
        float hexSize = manualSize;
        if (useAutoSize && centerReference != null)
        {
            MeshFilter mf = centerReference.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) 
                hexSize = mf.sharedMesh.bounds.size.x * centerReference.localScale.x / 2.0f;
            else 
                hexSize = centerReference.localScale.x;
        }
        CurrentHexSize = hexSize;

        foreach (var tile in allTiles)
        {
            if (tile != null && tile.highlightObject != null)
            {
                // スケールと位置の更新
                float scale = hexSize * robberHighlightScale;
                tile.highlightObject.transform.localScale = new Vector3(scale, scale * robberHighlightHeightScale, scale);
                tile.highlightObject.transform.localPosition = new Vector3(0, robberHighlightYOffset, 0);

                // 色の更新（プレハブを使っていない場合のみ）
                if (robberHighlightPrefab == null)
                {
                    Renderer r = tile.highlightObject.GetComponent<Renderer>();
                    if (r != null && r.sharedMaterial != null)
                    {
                        r.sharedMaterial.color = robberHighlightColor;
                    }
                }
            }
        }
    }

    public void UpdatePierSettings()
    {
        PierData[] piers;
        if (parentObject != null)
            piers = parentObject.GetComponentsInChildren<PierData>();
        else
            piers = GetComponentsInChildren<PierData>();

        if (piers == null) return;

        foreach (var p in piers)
        {
            Vector3 start = p.startPosition;
            Vector3 end = p.endPosition;
            
            Vector3 dir = (end - start);
            dir.y = 0;
            float distance = dir.magnitude;
            
            if (distance < 0.001f) continue;
            
            dir.Normalize();

            float length = distance * pierLengthRatio;
            
            Vector3 pos = start + dir * (length * 0.5f);
            pos.y = start.y + pierHeightOffset;

            p.transform.position = pos;
            p.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-90, 0, 0);
        }
    }

    public void UpdateRobberSettings()
    {
        Transform container = parentObject != null ? parentObject : this.transform;
        Transform robberTr = container.Find("Robber");
        if (robberTr != null)
        {
            robberTr.localScale = Vector3.one * robberScale;
        }
    }
}