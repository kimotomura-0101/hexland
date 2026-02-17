using UnityEngine;
using System.Collections;

public class ResourceFlyEffect : MonoBehaviour
{
    public static ResourceFlyEffect Instance { get; private set; }

    [Header("Resource Prefabs")]
    public GameObject woodPrefab;
    public GameObject brickPrefab;
    public GameObject orePrefab;
    public GameObject wheatPrefab;
    public GameObject sheepPrefab;

    [Header("Per-Resource Scale")]
    public float woodScale = 0.3f;
    public float brickScale = 0.3f;
    public float oreScale = 0.3f;
    public float wheatScale = 0.3f;
    public float sheepScale = 0.3f;

    [Header("Animation Settings")]
    public float flyDuration = 0.8f;
    public float arcHeight = 3.0f;
    public float staggerDelay = 0.15f;
    public float spinSpeed = 360f;

    void Awake()
    {
        Instance = this;
    }

    public void Fly(Vector3 fromWorld, string playerID, CatanMapGenerator.HexType resourceType, int amount)
    {
        GameObject prefab = GetPrefab(resourceType);
        if (prefab == null) return;

        RectTransform targetRect = GetTargetRect(playerID, resourceType);
        if (targetRect == null) return;

        float scale = GetScale(resourceType);
        for (int i = 0; i < amount; i++)
        {
            StartCoroutine(FlyCoroutine(fromWorld, targetRect, prefab, i * staggerDelay, scale));
        }
    }

    GameObject GetPrefab(CatanMapGenerator.HexType type)
    {
        switch (type)
        {
            case CatanMapGenerator.HexType.Wood: return woodPrefab;
            case CatanMapGenerator.HexType.Brick: return brickPrefab;
            case CatanMapGenerator.HexType.Ore: return orePrefab;
            case CatanMapGenerator.HexType.Wheat: return wheatPrefab;
            case CatanMapGenerator.HexType.Sheep: return sheepPrefab;
            default: return null;
        }
    }

    float GetScale(CatanMapGenerator.HexType type)
    {
        switch (type)
        {
            case CatanMapGenerator.HexType.Wood: return woodScale;
            case CatanMapGenerator.HexType.Brick: return brickScale;
            case CatanMapGenerator.HexType.Ore: return oreScale;
            case CatanMapGenerator.HexType.Wheat: return wheatScale;
            case CatanMapGenerator.HexType.Sheep: return sheepScale;
            default: return 0.3f;
        }
    }

    RectTransform GetTargetRect(string playerID, CatanMapGenerator.HexType resourceType)
    {
        // Player1 → ResourceHUD の該当資材アイコンへ
        if (GameManager.Instance.players.Count > 0 && playerID == GameManager.Instance.players[0].name)
        {
            var hud = GameManager.Instance.resourceHUD;
            if (hud != null) return hud.GetTargetRect(resourceType);
        }
        else
        {
            // 他プレイヤー → PlayerListHUD の該当行へ
            var listHUD = GameManager.Instance.playerListHUD;
            if (listHUD != null) return listHUD.GetTargetRect(playerID);
        }
        return null;
    }

    IEnumerator FlyCoroutine(Vector3 from, RectTransform targetRect, GameObject prefab, float delay, float scale)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // ランダムに少しオフセットして重なりを防ぐ
        Vector3 startPos = from + new Vector3(
            Random.Range(-0.3f, 0.3f), 0.5f, Random.Range(-0.3f, 0.3f));

        GameObject obj = Instantiate(prefab, startPos, Random.rotation);
        obj.transform.localScale = Vector3.one * scale;

        // コライダーを無効化
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        Camera cam = Camera.main;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);

            // 目的地をフレームごとに再計算（カメラ移動対応）
            Vector3 endPos = GetWorldPositionFromRect(targetRect, cam);

            // ベジェ曲線（放物線）
            Vector3 mid = (startPos + endPos) * 0.5f + Vector3.up * arcHeight;
            Vector3 pos = QuadraticBezier(startPos, mid, endPos, t);
            obj.transform.position = pos;

            // 自転
            obj.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            // 到着間際に縮小
            if (t > 0.7f)
            {
                float shrink = 1f - ((t - 0.7f) / 0.3f);
                obj.transform.localScale = Vector3.one * scale * shrink;
            }

            yield return null;
        }

        Destroy(obj);
    }

    Vector3 GetWorldPositionFromRect(RectTransform rect, Camera cam)
    {
        // UI要素のスクリーン座標を取得
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        // カメラ手前の近い位置にワールド座標として変換
        screenPos.z = 2f;
        return cam.ScreenToWorldPoint(screenPos);
    }

    static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
