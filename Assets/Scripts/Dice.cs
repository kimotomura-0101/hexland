using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class Dice : MonoBehaviour
{
    private Rigidbody rb;
    private bool hasStopped = false;

    [Header("Face Sprites")]
    public Sprite face1; // Up
    public Sprite face2; // Forward
    public Sprite face3; // Right
    public Sprite face4; // Left
    public Sprite face5; // Back
    public Sprite face6; // Down

    [Header("Rendering")]
    public Shader customShader; // シェーダーを手動で指定する場合

    // サイコロの目がローカル座標のどの方向に対応しているか定義
    // 一般的なサイコロの展開図（UV）に合わせて調整してください
    // ここでは標準的な例として: 上=1, 下=6, 前=2, 後=5, 右=3, 左=4 とします
    private readonly Vector3[] faceDirections = new Vector3[]
    {
        Vector3.up,         // 1
        Vector3.forward,    // 2
        Vector3.right,      // 3
        Vector3.left,       // 4
        Vector3.back,       // 5
        Vector3.down        // 6
    };

    private readonly int[] faceNumbers = { 1, 2, 3, 4, 5, 6 };

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // 物理挙動の安定化設定
        rb.mass = 1.0f;
        rb.linearDamping = 0.1f; // 空気抵抗（少し下げる）
        rb.angularDamping = 0.5f; // 回転抵抗を下げる（5.0だと回転せずにスライドしてしまうため）
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // すり抜け防止
        rb.interpolation = RigidbodyInterpolation.Interpolate; // 描画の補間（見た目の貫通防止）

        // コライダーの設定（摩擦を追加）
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col is MeshCollider mc) mc.convex = true;

            PhysicsMaterial mat = new PhysicsMaterial();
            mat.dynamicFriction = 0.8f; // 動摩擦（転がりやすくする）
            mat.staticFriction = 0.8f;  // 静止摩擦（止まりやすくする）
            mat.bounciness = 0.3f;      // 跳ね返り
            col.material = mat;
        }

        // スプライトが設定されていればメッシュを生成して適用
        if (face1 != null)
            SetupMesh();
    }

    public void Throw(Vector3 force, Vector3 torque)
    {
        hasStopped = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // 力を加える
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(torque, ForceMode.Impulse);
    }

    public bool IsSleeping()
    {
        // 速度がほぼゼロなら停止とみなす
        return rb.linearVelocity.sqrMagnitude < 0.01f && rb.angularVelocity.sqrMagnitude < 0.01f;
    }

    public int GetResult()
    {
        // ワールド座標の「上(Vector3.up)」に最も近いローカル面の方向を探す
        float maxDot = -1.0f;
        int result = 1;

        for (int i = 0; i < faceDirections.Length; i++)
        {
            // ローカルの面方向をワールド方向に変換
            Vector3 worldDir = transform.TransformDirection(faceDirections[i]);
            
            // 内積計算（1に近いほど上を向いている）
            float dot = Vector3.Dot(worldDir, Vector3.up);
            if (dot > maxDot)
            {
                maxDot = dot;
                result = faceNumbers[i];
            }
        }
        return result;
    }

    // 停止時に斜めになっていたら、一番近い面を真上に向けて補正する
    public void SnapToResult()
    {
        if (rb == null) return;

        float maxDot = -1.0f;
        int bestIndex = 0;

        // 一番上を向いている面を探す
        for (int i = 0; i < faceDirections.Length; i++)
        {
            Vector3 worldDir = transform.TransformDirection(faceDirections[i]);
            float dot = Vector3.Dot(worldDir, Vector3.up);
            if (dot > maxDot)
            {
                maxDot = dot;
                bestIndex = i;
            }
        }

        // その面が完全に真上(Vector3.up)を向くように回転させる
        Vector3 currentUp = transform.TransformDirection(faceDirections[bestIndex]);
        Quaternion correction = Quaternion.FromToRotation(currentUp, Vector3.up);
        transform.rotation = correction * transform.rotation;

        // 念のため速度もゼロにする
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // 6面それぞれにマテリアルを割り当てられるCubeメッシュを生成して適用する
    void SetupMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

        // 6面分のマテリアルを作成
        Material[] mats = new Material[6];
        Sprite[] sprites = { face1, face2, face3, face4, face5, face6 };
        
        // シェーダーの決定
        Shader shaderToUse = customShader;
        if (shaderToUse == null)
        {
            // URPなどのSRPを使用しているか確認
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                // URP用シェーダー
                shaderToUse = Shader.Find("Universal Render Pipeline/Lit");
                // 見つからない場合はHDRPなどを試す
                if (shaderToUse == null) shaderToUse = Shader.Find("HDRP/Lit");
            }
            else
            {
                // Built-in Render Pipeline用
                shaderToUse = Shader.Find("Standard");
            }
        }
        // それでも見つからない場合のフォールバック
        if (shaderToUse == null) shaderToUse = Shader.Find("Diffuse");

        for(int i=0; i<6; i++)
        {
            mats[i] = new Material(shaderToUse);
            if (sprites[i] != null)
            {
                mats[i].mainTexture = sprites[i].texture;
                // スプライトの色味をそのまま出すために白にする
                mats[i].color = Color.white;
                
                // 光沢を抑える
                if (mats[i].HasProperty("_Glossiness")) 
                    mats[i].SetFloat("_Glossiness", 0.0f); // Standard
                else if (mats[i].HasProperty("_Smoothness")) 
                    mats[i].SetFloat("_Smoothness", 0.0f); // URP
            }
        }
        mr.materials = mats;

        // メッシュ生成
        Mesh mesh = CreateMultiMaterialCube();
        mf.mesh = mesh;
        
        // コライダー更新
        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = mesh;
            mc.convex = true;
        }
    }

    Mesh CreateMultiMaterialCube()
    {
        Mesh mesh = new Mesh();
        mesh.name = "DiceMesh";

        float size = 0.5f; // 幅1.0
        Vector3[] v = new Vector3[24];
        Vector3[] n = new Vector3[24];
        Vector2[] uv = new Vector2[24];

        // 頂点定義ヘルパー
        void SetFace(int index, Vector3 normal, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int i = index * 4;
            v[i+0] = v0; v[i+1] = v1; v[i+2] = v2; v[i+3] = v3;
            n[i+0] = n[i+1] = n[i+2] = n[i+3] = normal;
            // UVは左上、右上、右下、左下の順（頂点定義順に合わせる）
            uv[i+0] = new Vector2(0, 1);
            uv[i+1] = new Vector2(1, 1);
            uv[i+2] = new Vector2(1, 0);
            uv[i+3] = new Vector2(0, 0);
        }

        // Face 1: Up (Y+)
        SetFace(0, Vector3.up, 
            new Vector3(-size, size, size), new Vector3(size, size, size), 
            new Vector3(size, size, -size), new Vector3(-size, size, -size));

        // Face 2: Forward (Z+)
        SetFace(1, Vector3.forward,
            new Vector3(-size, size, size), new Vector3(size, size, size),
            new Vector3(size, -size, size), new Vector3(-size, -size, size));

        // Face 3: Right (X+)
        SetFace(2, Vector3.right,
            new Vector3(size, size, size), new Vector3(size, size, -size),
            new Vector3(size, -size, -size), new Vector3(size, -size, size));

        // Face 4: Left (X-)
        SetFace(3, Vector3.left,
            new Vector3(-size, size, -size), new Vector3(-size, size, size),
            new Vector3(-size, -size, size), new Vector3(-size, -size, -size));

        // Face 5: Back (Z-)
        SetFace(4, Vector3.back,
            new Vector3(size, size, -size), new Vector3(-size, size, -size),
            new Vector3(-size, -size, -size), new Vector3(size, -size, -size));

        // Face 6: Down (Y-)
        SetFace(5, Vector3.down,
            new Vector3(-size, -size, size), new Vector3(size, -size, size),
            new Vector3(size, -size, -size), new Vector3(-size, -size, -size));

        mesh.vertices = v;
        mesh.normals = n;
        mesh.uv = uv;

        mesh.subMeshCount = 6;
        for (int i = 0; i < 6; i++)
        {
            int baseIndex = i * 4;
            int[] tris;
            
            // Face 1 (Up) は 0-1-2 で正しい法線(Y+)になりますが、
            // 他の面は座標系の関係で 0-1-2 だと内側を向いてしまうため、順序を逆にします。
            if (i == 0)
            {
                tris = new int[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 };
            }
            else
            {
                tris = new int[] { baseIndex, baseIndex + 2, baseIndex + 1, baseIndex, baseIndex + 3, baseIndex + 2 };
            }
            mesh.SetTriangles(tris, i);
        }
        
        mesh.RecalculateBounds();
        return mesh;
    }
}
