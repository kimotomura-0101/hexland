using UnityEngine;

public class Billboard : MonoBehaviour
{
    // プレハブの向きに合わせて調整してください (Cylinderなら 90, 0, 0 など)
    public Vector3 offsetRotation = new Vector3(180, 270, 90);

    void LateUpdate()
    {
        // メインカメラの方を向く
        if (Camera.main != null)
        {
            // カメラの回転と同じにする
            transform.rotation = Camera.main.transform.rotation;
            // 追加の回転（オフセット）を適用
            transform.Rotate(offsetRotation);
        }
    }
}
