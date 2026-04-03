using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator playerAnimator;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private bool moveRelativeToCamera = true;

    [Header("Rotate (Facing)")]
    [SerializeField] private bool faceMoveDirection = true;
    [SerializeField] private float turnSpeedDegPerSec = 720f;

    [Header("Anti Spin")]
    [Tooltip("入力が無い/Overrideが無いときに角速度(Y)を止める")]
    [SerializeField] private bool stopYawSpinWhenIdle = true;

    [Header("Input Deadzone")]
    [SerializeField] private float inputDeadzone = 0.2f;

    [Header("Animation")]
    [SerializeField] private float runThreshold = 0.01f;

    [Header("Aim Override (SpellShooterなどから)")]
    [SerializeField] private bool allowAimOverride = true;
    [SerializeField] private float aimTurnSpeedDegPerSec = 1080f;
    [SerializeField] private float aimOverrideDefaultHoldSeconds = 0.15f;

    private float aimOverrideTimer;
    private Vector3 aimOverrideDir;

    // ===== Jump (Physics only / NO animation) =====
    [Header("Jump (Physics only)")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravityScale = 1.0f;
    [SerializeField] private float fallMultiplier = 1.2f;
    [SerializeField] private float lowJumpMultiplier = 2.0f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.22f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundedStickYVelocity = -2f;

    [Header("Rigidbody Safety")]
    [SerializeField] private bool freezeRotationXZ = true;

    private Rigidbody rb;
    private Vector3 moveDirWorld;
    private Vector2 moveDirLocal;

    private bool isRun;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool isGrounded;

    private static readonly int RunHash = Animator.StringToHash("run");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (playerAnimator == null) playerAnimator = GetComponent<Animator>();

        if (freezeRotationXZ)
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        Transform cam = cameraTransform != null
            ? cameraTransform
            : (Camera.main != null ? Camera.main.transform : null);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, z);
        if (input.sqrMagnitude < inputDeadzone * inputDeadzone)
            input = Vector2.zero;

        if (moveRelativeToCamera && cam != null)
        {
            Vector3 camF = cam.forward; camF.y = 0f;
            Vector3 camR = cam.right; camR.y = 0f;

            if (camF.sqrMagnitude < 0.0001f) camF = Vector3.forward;
            if (camR.sqrMagnitude < 0.0001f) camR = Vector3.right;

            camF.Normalize();
            camR.Normalize();

            moveDirWorld = camR * input.x + camF * input.y;
        }
        else
        {
            moveDirWorld = new Vector3(input.x, 0f, input.y);
        }

        if (moveDirWorld.sqrMagnitude > 1f) moveDirWorld.Normalize();

        Vector3 local = transform.InverseTransformDirection(moveDirWorld);
        moveDirLocal = new Vector2(local.x, local.z);

        isRun = moveDirWorld.sqrMagnitude > runThreshold;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool(RunHash, isRun);
            playerAnimator.SetFloat(MoveXHash, moveDirLocal.x);
            playerAnimator.SetFloat(MoveZHash, moveDirLocal.y);
            playerAnimator.SetBool(GroundedHash, isGrounded);
        }

        if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
        jumpHeld = Input.GetKey(KeyCode.Space);
    }

    public void RequestAimLook(Vector3 worldDir, float holdSeconds = -1f)
    {
        if (!allowAimOverride) return;

        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f) return;

        aimOverrideDir = worldDir.normalized;

        float hs = (holdSeconds < 0f) ? aimOverrideDefaultHoldSeconds : holdSeconds;
        aimOverrideTimer = Mathf.Max(0.01f, hs);
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        // 移動
        Vector3 v = rb.linearVelocity;
        Vector3 planar = moveDirWorld * moveSpeed;
        rb.linearVelocity = new Vector3(planar.x, v.y, planar.z);

        bool rotatedThisFrame = false;

        // 回転
        if (allowAimOverride && aimOverrideTimer > 0f)
        {
            aimOverrideTimer -= Time.fixedDeltaTime;
            RotateTo(aimOverrideDir, aimTurnSpeedDegPerSec);
            rotatedThisFrame = true;
        }
        else if (faceMoveDirection && moveDirWorld.sqrMagnitude > 0.0001f)
        {
            RotateTo(moveDirWorld, turnSpeedDegPerSec);
            rotatedThisFrame = true;
        }

        // ★ぐるぐる対策：回転していないフレームは角速度を止める
        if (stopYawSpinWhenIdle && !rotatedThisFrame)
        {
            // Yだけ止める（他はFreezeしてる想定だが保険）
            Vector3 av = rb.angularVelocity;
            av.y = 0f;
            rb.angularVelocity = av;
        }

        // ジャンプ（物理のみ）
        if (jumpPressed && isGrounded)
            DoJump();
        jumpPressed = false;

        ApplyScaledGravity();

        if (isGrounded)
        {
            Vector3 vv = rb.linearVelocity;
            if (vv.y < 0f)
                rb.linearVelocity = new Vector3(vv.x, groundedStickYVelocity, vv.z);
        }
    }

    private void RotateTo(Vector3 forward, float degPerSec)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Quaternion next = Quaternion.RotateTowards(rb.rotation, target, degPerSec * Time.fixedDeltaTime);
        rb.MoveRotation(next);

        // 回転をスクリプト支配に寄せる（衝突トルク等で回り続けるのを予防）
        Vector3 av = rb.angularVelocity;
        av.y = 0f;
        rb.angularVelocity = av;
    }

    private void DoJump()
    {
        float g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0f, gravityScale);
        if (g < 0.0001f) g = Mathf.Abs(Physics.gravity.y);

        float jumpVel = Mathf.Sqrt(2f * g * Mathf.Max(0f, jumpHeight));

        Vector3 v = rb.linearVelocity;
        v.y = jumpVel;
        rb.linearVelocity = v;

        isGrounded = false;
    }

    private void ApplyScaledGravity()
    {
        float scale = Mathf.Max(0f, gravityScale);

        float y = rb.linearVelocity.y;
        if (y < 0f) scale *= Mathf.Max(1f, fallMultiplier);
        else if (y > 0f && !jumpHeld) scale *= Mathf.Max(1f, lowJumpMultiplier);

        Vector3 extra = Physics.gravity * (scale - 1f);
        rb.AddForce(extra, ForceMode.Acceleration);
    }

    private bool CheckGrounded()
    {
        Vector3 p = groundCheck != null ? groundCheck.position : (transform.position + Vector3.up * 0.1f);
        return Physics.CheckSphere(p, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }
}
