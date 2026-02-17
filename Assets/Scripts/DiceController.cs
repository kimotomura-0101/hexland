using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DiceController : MonoBehaviour
{
    [Header("Settings")]
    public Dice dice1;
    public Dice dice2;
    public Transform spawnPoint; // サイコロが出現する位置
    public float throwForce = 15f;
    public float rotationForce = 50f; // 回転力を強める
    public float wallSize = 3.5f; // 壁の範囲（中心からの距離）

    [Header("UI Rendering")]
    public RawImage diceDisplay; // UIに表示するためのRawImage
    public Camera diceCamera;    // ダイスだけを映す専用カメラ
    public Color backgroundColor = new Color(0.05f, 0.2f, 0.05f, 1.0f); // 背景色

    private bool isRolling = false;
    private RenderTexture renderTexture;
    private Vector3 isolatedPos = new Vector3(0, -500, 0); // マップから遠く離れた場所
    private GameObject[] walls; // 壁の参照保持用

    void Start()
    {
        // Inspectorでプレハブが割り当てられている場合、実体を生成する
        if (dice1 != null && !dice1.gameObject.scene.IsValid()) dice1 = Instantiate(dice1);
        if (dice2 != null && !dice2.gameObject.scene.IsValid()) dice2 = Instantiate(dice2);

        // UI表示設定がある場合のみセットアップ
        if (diceDisplay != null && diceCamera != null)
        {
            SetupUIEnvironment();
        }

        // 最初は非表示にしておく（画面に残らないように）
        if (dice1 != null) dice1.gameObject.SetActive(false);
        if (dice2 != null) dice2.gameObject.SetActive(false);
        if (diceDisplay != null) diceDisplay.gameObject.SetActive(false);
    }

    void Update()
    {
        // 実行中にInspectorで壁の範囲を調整できるようにする
        if (walls != null)
        {
            UpdateWallPositions();
        }
    }

    void SetupUIEnvironment()
    {
        // 1. 物理演算用の床（見えない床）を生成
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "DiceFloor";
        // 床を極端に分厚くして貫通を物理的に防ぐ
        float floorThickness = 100.0f;
        floor.transform.position = isolatedPos + Vector3.down * (1.0f + floorThickness * 0.5f); // 表面の高さは変えずに下に伸ばす
        floor.transform.localScale = new Vector3(100, floorThickness, 100); // 十分な広さと厚さ
        // 床のレンダラーを無効化（背景色だけでいい場合）
        if (floor.GetComponent<Renderer>()) floor.GetComponent<Renderer>().enabled = false;

        // 床に摩擦を設定（滑り防止）
        Collider floorCol = floor.GetComponent<Collider>();
        if (floorCol != null)
        {
            PhysicsMaterial floorMat = new PhysicsMaterial();
            floorMat.dynamicFriction = 0.8f;
            floorMat.staticFriction = 0.8f;
            floorCol.material = floorMat;
        }
        
        // 壁を生成してダイスが画面外に出ないようにする
        walls = new GameObject[4];
        float wallThickness = 100.0f; // 壁を極端に厚くしてすり抜けを防ぐ
        float wallHeight = 100.0f; // 高さも十分にとる
        
        walls[0] = CreateInvisibleWall(Vector3.zero, new Vector3(wallThickness, wallHeight, 100));
        walls[1] = CreateInvisibleWall(Vector3.zero, new Vector3(wallThickness, wallHeight, 100));
        walls[2] = CreateInvisibleWall(Vector3.zero, new Vector3(100, wallHeight, wallThickness));
        walls[3] = CreateInvisibleWall(Vector3.zero, new Vector3(100, wallHeight, wallThickness));

        UpdateWallPositions();

        // 2. カメラをセットアップ
        diceCamera.transform.position = isolatedPos + Vector3.up * 10.0f;
        diceCamera.transform.rotation = Quaternion.Euler(90, 0, 0); // 真上から見下ろす
        diceCamera.orthographic = true; // 正投影
        diceCamera.orthographicSize = 3f; // サイズ調整（値を小さくすると大きく映る）
        diceCamera.farClipPlane = 50f;
        diceCamera.clearFlags = CameraClearFlags.SolidColor;
        diceCamera.backgroundColor = backgroundColor;
        
        // 3. Render Textureの生成と割り当て
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            renderTexture.Create();
        }

        diceCamera.targetTexture = renderTexture;
        diceDisplay.texture = renderTexture;
        diceDisplay.color = Color.white;
    }

    void UpdateWallPositions()
    {
        if (walls == null) return;

        float wallDist = wallSize;
        float wallThickness = 100.0f;

        if (walls[0]) walls[0].transform.position = isolatedPos + Vector3.right * (wallDist + wallThickness * 0.5f);
        if (walls[1]) walls[1].transform.position = isolatedPos + Vector3.left * (wallDist + wallThickness * 0.5f);
        if (walls[2]) walls[2].transform.position = isolatedPos + Vector3.forward * (wallDist + wallThickness * 0.5f);
        if (walls[3]) walls[3].transform.position = isolatedPos + Vector3.back * (wallDist + wallThickness * 0.5f);
    }

    GameObject CreateInvisibleWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "DiceWall";
        wall.transform.position = position;
        wall.transform.localScale = scale;
        if (wall.GetComponent<Renderer>()) wall.GetComponent<Renderer>().enabled = false;

        // 壁の物理設定
        Collider wallCol = wall.GetComponent<Collider>();
        if (wallCol != null)
        {
            PhysicsMaterial wallMat = new PhysicsMaterial();
            wallMat.dynamicFriction = 0.0f; // 壁は引っかからないように滑りやすく
            wallMat.bounciness = 0.5f;      // 少し跳ね返る
            wallCol.material = wallMat;
        }
        return wall;
    }

    // コールバック付きでダイスロールを実行
    public void RollDice(System.Action<int, int> onResult)
    {
        if (isRolling) return;
        StartCoroutine(RollRoutine(onResult));
    }

    IEnumerator RollRoutine(System.Action<int, int> onResult)
    {
        isRolling = true;

        // UIを表示
        if (diceDisplay != null) diceDisplay.gameObject.SetActive(true);

        // ダイスを表示してアクティブ化
        if (dice1 != null) dice1.gameObject.SetActive(true);
        if (dice2 != null) dice2.gameObject.SetActive(true);

        // スポーン位置の決定
        Vector3 startPos = Vector3.zero;
        
        if (diceDisplay != null)
        {
            // UIモードなら隔離空間
            startPos = isolatedPos + Vector3.up * 5.0f;
        }
        else if (spawnPoint != null)
        {
            startPos = spawnPoint.position;
        }
        else if (Camera.main != null)
        {
            // カメラの前方5m、上空3mあたり
            startPos = Camera.main.transform.position + Camera.main.transform.forward * 5f + Vector3.up * 3f;
        }

        // ダイスの初期化（物理リセット）
        PrepareDice(dice1, startPos - Vector3.right * 0.5f);
        PrepareDice(dice2, startPos + Vector3.right * 0.5f);

        // 1フレーム待って物理演算を確実にリセットさせる
        yield return new WaitForFixedUpdate();

        // 投げる
        if (dice1 != null) dice1.Throw(GetRandomThrowForce(), GetRandomTorque());
        if (dice2 != null) dice2.Throw(GetRandomThrowForce(), GetRandomTorque());

        // 2. 投げた直後の誤判定を防ぐため少し待つ
        yield return new WaitForSeconds(0.5f);

        // 3. 両方のサイコロが止まるまで待機
        // 無限ループ防止のため最大5秒で打ち切る
        float timeElapsed = 0f;
        while (timeElapsed < 5.0f)
        {
            if (dice1.IsSleeping() && dice2.IsSleeping()) break;
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 結果を取得して返す
        // 斜めで止まっている場合に備えて、一番近い面にスナップ（補正）させる
        if (dice1 != null) dice1.SnapToResult();
        if (dice2 != null) dice2.SnapToResult();

        int result1 = dice1 != null ? dice1.GetResult() : 1;
        int result2 = dice2 != null ? dice2.GetResult() : 1;

        Debug.Log($"Physics Dice Result: {result1}, {result2}");
        
        isRolling = false;
        onResult?.Invoke(result1, result2);

        // 結果確認用に少し待つが、非表示にはしない（表示を残す）
        yield return new WaitForSeconds(2.0f);
    }

    void PrepareDice(Dice dice, Vector3 position)
    {
        if (dice == null) return;
        
        dice.transform.position = position;
        dice.transform.rotation = Random.rotation;
        
        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true; // 一旦物理を止める
            // 次のフレームでThrowされるときにisKinematic=falseになる
        }
    }

    Vector3 GetRandomThrowForce()
    {
        // 基本は下向き、少しランダムに散らす
        Vector3 randomDir = Random.insideUnitSphere;
        randomDir.y = 0; // 水平成分のみランダム
        return (Vector3.down + randomDir * 0.2f) * throwForce;
    }

    Vector3 GetRandomTorque()
    {
        return Random.insideUnitSphere * rotationForce;
    }
}
