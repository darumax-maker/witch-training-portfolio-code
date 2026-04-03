using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBossAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform player;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stopDistance = 2.0f;
    [SerializeField] private float rotateSpeedDegPerSec = 540f;

    [Header("Fire (Always)")]
    [SerializeField] private float fireCooldown = 2.5f;

    [Tooltip("ON: Fireトリガー直後だけ停止 / OFF: Fireしながら歩く")]
    [SerializeField] private bool stopWhileCastingFire = false;

    [Tooltip("stopWhileCastingFire=ON のとき、停止する秒数")]
    [SerializeField] private float stopSecondsAfterFire = 0.3f;

    [Header("Fireball")]
    [SerializeField] private Transform fireMuzzle;      // 口先Transform（推奨）
    [SerializeField] private GameObject fireballPrefab; // FireOrbitSphereなど

    [Header("Animator")]
    [SerializeField] private Animator animator;         // 未指定なら自動取得
    [SerializeField] private string fireTriggerName = "Fire";

    [Header("Rigidbody (Dynamic)")]
    [SerializeField] private bool freezeY = true;
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    private Rigidbody rb;
    private float nextFireTime;
    private float stopUntilTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // Dynamicで壁に当たって止まる
        rb.isKinematic = false;
        rb.useGravity = false;

        rb.collisionDetectionMode = collisionDetection;
        rb.interpolation = interpolation;

        var constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        if (freezeY) constraints |= RigidbodyConstraints.FreezePositionY;
        rb.constraints = constraints;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        if (player == null && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
        }

        ClampParams();
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            StopMove();
            return;
        }

        Vector3 to = player.position - rb.position;
        to.y = 0f;

        float dist = to.magnitude;
        Vector3 dir = (dist > 0.001f) ? (to / dist) : transform.forward;

        // 向きは常にPlayerへ
        RotateTo(dir);

        // 常にFire（クールダウンごと）
        TryFire();

        // Fire直後だけ止める設定（任意）
        if (stopWhileCastingFire && Time.time < stopUntilTime)
        {
            StopMove();
            return;
        }

        // 移動
        if (dist > stopDistance)
            MovePlanar(dir, moveSpeed);
        else
            StopMove();
    }

    private void RotateTo(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion nextRot = Quaternion.RotateTowards(
            rb.rotation, targetRot, rotateSpeedDegPerSec * Time.fixedDeltaTime
        );
        rb.MoveRotation(nextRot);
    }

    private void MovePlanar(Vector3 dir, float speed)
    {
        if (speed <= 0f) { StopMove(); return; }

        Vector3 v = dir * speed;
        v.y = 0f;
        rb.linearVelocity = v; // Unity 6
    }

    private void StopMove()
    {
        var v = rb.linearVelocity;
        v.x = 0f; v.z = 0f;
        rb.linearVelocity = v;
    }

    private void TryFire()
    {
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireCooldown;

        if (animator != null && !string.IsNullOrEmpty(fireTriggerName))
            animator.SetTrigger(fireTriggerName);

        if (stopWhileCastingFire && stopSecondsAfterFire > 0f)
            stopUntilTime = Time.time + stopSecondsAfterFire;

        // 発射はアニメイベント Anim_SpawnFireball() で行う想定
    }

    // ===== 火球発射（アニメイベントから呼ぶ）=====
    [Header("Fireball Motion")]
    [SerializeField] private float fireballSpeed = 12f;
    [SerializeField] private float fireballLifeSeconds = 5f;
    [SerializeField] private float aimHeightFallback = 1.0f;

    private void SpawnFireballNow()
    {
        if (fireballPrefab == null || player == null) return;

        Transform muzzle = (fireMuzzle != null) ? fireMuzzle : transform;

        Vector3 targetPos = player.position + Vector3.up * aimHeightFallback;
        if (player.TryGetComponent<Collider>(out var col))
            targetPos = col.bounds.center;

        Vector3 d = (targetPos - muzzle.position);
        if (d.sqrMagnitude < 0.0001f) d = transform.forward;
        d.Normalize();

        GameObject go = Instantiate(
            fireballPrefab,
            muzzle.position,
            Quaternion.LookRotation(d, Vector3.up)
        );

        if (go.TryGetComponent<Rigidbody>(out var frb))
        {
            frb.useGravity = false;
            frb.isKinematic = false;
            frb.linearVelocity = d * fireballSpeed;
            frb.angularVelocity = Vector3.zero;
        }

        if (fireballLifeSeconds > 0f)
            Destroy(go, fireballLifeSeconds);
    }

    // FlameAttack クリップの「吐く瞬間」に置く
    public void Anim_SpawnFireball() => SpawnFireballNow();

    private void ClampParams()
    {
        if (stopDistance < 0f) stopDistance = 0f;
        if (moveSpeed < 0f) moveSpeed = 0f;
        if (fireCooldown < 0f) fireCooldown = 0f;
        if (stopSecondsAfterFire < 0f) stopSecondsAfterFire = 0f;

        if (fireballSpeed < 0f) fireballSpeed = 0f;
        if (fireballLifeSeconds < 0f) fireballLifeSeconds = 0f;
        if (aimHeightFallback < 0f) aimHeightFallback = 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampParams();
    }
#endif
}
