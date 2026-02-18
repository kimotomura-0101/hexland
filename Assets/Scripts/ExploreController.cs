using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class ExploreController : MonoBehaviour
{
    [Header("Character")]
    public GameObject characterPrefab; // リモートプレイヤー生成用プレハブ

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float runMultiplier = 1.5f;
    public float jumpForce = 5.0f;
    public float turnSpeed = 10.0f;

    [Header("Camera Settings")]
    public float mouseSensitivity = 2.0f;
    public float cameraDistance = 6.0f;
    public float cameraHeight = 2.0f;
    public float minVerticalAngle = -10f;
    public float maxVerticalAngle = 60f;

    [Header("Collision Settings")]
    public float colliderCenterYOffset = 0.1f; // 浮き防止用オフセット

    [Header("Physics Settings")]
    public float extraGravity = 20.0f; // 追加重力（ジャンプの滞空時間を調整）
    public float jumpAnimSpeed = 1.0f; // ジャンプ中のアニメーション速度倍率

    [Header("Animation Parameters")]
    public string animSpeedParam = "Speed";
    public string animGroundedParam = "IsGrounded";
    public string animJumpParam = "Jump";
    public string animMotionSpeedParam = "MotionSpeed";

    private Rigidbody rb;
    private Animator animator;
    private Transform mainCamera;
    private float currentX = 0f;
    private float currentY = 20f; // 初期角度

    private float distToGround;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
            // 現在のカメラ角度を引き継ぐとスムーズかも知れないが、初期値リセットの方が安定する
            Vector3 angles = mainCamera.eulerAngles;
            currentX = angles.y;
            currentY = 20f;
        }

        // Rigidbody設定
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // コライダー調整と接地判定用の距離取得
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
        {
            // コライダーの中心を少し上げて、メッシュが地面に沈む（接地する）ように調整
            col.center += Vector3.up * colliderCenterYOffset;
            distToGround = col.bounds.extents.y;
        }
        else distToGround = 1.0f;
    }

    void OnEnable()
    {
        // カーソルをロックして非表示にする
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        // カーソルロック解除
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (mainCamera == null) return;

        HandleCursorState();
        HandleCameraInput();
        HandleMovementInput();
    }

    void LateUpdate()
    {
        UpdateCameraPosition();
    }

    void FixedUpdate()
    {
        // 追加の重力を加えて、ジャンプの挙動を調整しやすくする（デフォルトの重力だけだとふわっとしがち）
        if (rb != null)
        {
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
        }
    }

    void HandleCursorState()
    {
        if (Keyboard.current == null) return;

        bool isAltPressed = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;

        if (isAltPressed)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void HandleCameraInput()
    {
        if (Mouse.current == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // マウス入力でカメラ角度を更新
        Vector2 delta = Mouse.current.delta.ReadValue();
        float lookFactor = 0.1f; // 新しいInputSystemのdeltaはピクセル単位なので調整

        currentX += delta.x * mouseSensitivity * lookFactor;
        currentY -= delta.y * mouseSensitivity * lookFactor;
        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
    }

    void HandleMovementInput()
    {
        float h = 0;
        float v = 0;
        bool isRun = false;
        bool jump = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1;
            
            isRun = Keyboard.current.leftShiftKey.isPressed;
            jump = Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        // カメラの向きに基づいた移動方向を計算
        Vector3 forward = mainCamera.forward;
        Vector3 right = mainCamera.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;
        float currentSpeed = isRun ? moveSpeed * runMultiplier : moveSpeed;

        // 移動処理
        if (moveDir.magnitude > 0.1f)
        {
            // キャラクターの向きを変える
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            
            // 位置を動かす
            Vector3 velocity = moveDir * currentSpeed;
            velocity.y = rb.linearVelocity.y; // 重力（落下速度）は維持
            rb.linearVelocity = velocity;
        }
        else
        {
            // 入力がないときは水平速度を減衰させる（滑り防止）
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0;
            velocity.z = 0;
            rb.linearVelocity = velocity;
        }

        // ジャンプ
        if (jump && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (animator) animator.SetTrigger(animJumpParam);
        }

        // アニメーションパラメータ更新
        if (animator)
        {
            bool grounded = IsGrounded();
            animator.SetBool(animGroundedParam, grounded);

            // Speed: 0 (停止), 0.5 (歩き), 1 (走り)
            float targetSpeed = 0f;
            if (moveDir.magnitude > 0.1f) targetSpeed = isRun ? 1.0f : 0.5f;
            
            animator.SetFloat(animSpeedParam, targetSpeed, 0.1f, Time.deltaTime);
            
            // 接地しているかどうかでモーション速度を変える
            // AnimatorのJumpステートのSpeed Multiplierにこのパラメータを設定しておくと有効
            float currentMotionSpeed = grounded ? 1.0f : jumpAnimSpeed;
            animator.SetFloat(animMotionSpeedParam, currentMotionSpeed);

            // マルチプレイ: 位置をサーバーに送信 (10Hz)
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsMultiplayer)
            {
                _exploreSyncTimer += Time.deltaTime;
                if (_exploreSyncTimer >= 0.1f)
                {
                    _exploreSyncTimer = 0f;
                    NetworkManager.Instance.SendExploreSync(transform.position, transform.eulerAngles.y, targetSpeed);
                }
            }
        }
    }

    private float _exploreSyncTimer;

    void UpdateCameraPosition()
    {
        if (mainCamera == null) return;

        // ターゲット（自分）を中心に回転
        Vector3 dir = new Vector3(0, 0, -cameraDistance);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        // カメラ位置計算
        Vector3 targetPos = transform.position + Vector3.up * cameraHeight + rotation * dir;
        
        mainCamera.position = targetPos;
        mainCamera.LookAt(transform.position + Vector3.up * (cameraHeight * 0.7f));
    }

    bool IsGrounded()
    {
        // 足元にレイを飛ばして接地判定
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, distToGround + 0.2f);
    }
}
