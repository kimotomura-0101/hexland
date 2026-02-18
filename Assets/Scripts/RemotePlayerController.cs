using UnityEngine;

/// <summary>
/// リモートプレイヤーの位置/回転/アニメーションを補間で滑らかに表示する。
/// </summary>
public class RemotePlayerController : MonoBehaviour
{
    private Vector3 targetPos;
    private float targetRotY;
    private float targetAnimSpeed;
    private Animator animator;

    public float lerpSpeed = 10f;

    void Start()
    {
        targetPos = transform.position;
        targetRotY = transform.eulerAngles.y;
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);

        float currentY = transform.eulerAngles.y;
        float newY = Mathf.LerpAngle(currentY, targetRotY, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Euler(0, newY, 0);

        if (animator != null)
            animator.SetFloat("Speed", targetAnimSpeed, 0.1f, Time.deltaTime);
    }

    public void ApplyNetworkState(Vector3 pos, float rotY, float animSpeed)
    {
        targetPos = pos;
        targetRotY = rotY;
        targetAnimSpeed = animSpeed;
    }
}
