using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // 回転の中心（マップの中心など）

    [Header("Settings")]
    public float distance = 15.0f; // 初期距離
    public float xSpeed = 0.2f;    // 横回転の感度
    public float ySpeed = 0.2f;    // 縦回転の感度
    public float yMinLimit = 10f;  // 縦回転の最小角度
    public float yMaxLimit = 80f;  // 縦回転の最大角度
    public float scrollSensitivity = 0.02f; // ズーム感度
    public float minDistance = 2.0f;
    public float maxDistance = 100.0f;

    [Header("Smoothing")]
    public bool enableSmoothing = true;
    public float smoothTime = 0.1f;

    private float x = 0.0f;
    private float y = 45.0f; // 初期角度（斜め上から）

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentDistance = 0.0f;
    
    private float xVelocity = 0.0f;
    private float yVelocity = 0.0f;
    private float zoomVelocity = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = 45.0f; // 斜め上から見下ろす形に初期化

        currentX = x;
        currentY = y;
        currentDistance = distance;

        // ターゲットが設定されていない場合、原点(0,0,0)を見るようにする
        if (target == null)
        {
            GameObject go = new GameObject("CameraTarget");
            go.transform.position = Vector3.zero;
            target = go.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // マウス操作の取得 (New Input System)
        if (Mouse.current != null)
        {
            // 右クリックドラッグで回転
            if (Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                x += delta.x * xSpeed;
                y -= delta.y * ySpeed;
            }

            // マウスホイールでズーム
            float scroll = Mouse.current.scroll.ReadValue().y;
            distance -= scroll * scrollSensitivity;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // 角度制限
        y = ClampAngle(y, yMinLimit, yMaxLimit);

        // スムージング処理
        if (enableSmoothing)
        {
            currentX = Mathf.SmoothDamp(currentX, x, ref xVelocity, smoothTime);
            currentY = Mathf.SmoothDamp(currentY, y, ref yVelocity, smoothTime);
            currentDistance = Mathf.SmoothDamp(currentDistance, distance, ref zoomVelocity, smoothTime);
        }
        else
        {
            currentX = x;
            currentY = y;
            currentDistance = distance;
        }

        // 回転と位置の計算
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -currentDistance) + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
