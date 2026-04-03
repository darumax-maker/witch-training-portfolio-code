using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemySensor sensor;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float chaseStopDistance = 1.2f;

    [Header("Wander")]
    [SerializeField] private float wanderSpeed = 2.0f;
    [SerializeField] private float changeDirInterval = 2.0f;   // 何秒ごとに方向を変えるか
    [SerializeField] private float wanderTurnDegPerSec = 360f;

    [Header("Rotate")]
    [SerializeField] private float chaseTurnDegPerSec = 720f;

    [Header("Obstacle Avoid (optional)")]
    [SerializeField] private float avoidCheckDistance = 1.0f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private Rigidbody rb;
    private Vector3 wanderDir;
    private float changeDirTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (sensor == null) sensor = GetComponentInChildren<EnemySensor>();

    }

    private void Start()
    {
        PickNewWanderDir();
        changeDirTimer = changeDirInterval * 0.5f;
    }

    private void FixedUpdate()
    {
        Transform target = sensor != null ? sensor.CurrentTarget : null;

        if (target != null)
            DoChase(target);
        else
            DoWander();
    }

    private void DoChase(Transform target)
    {
        Vector3 to = target.position - transform.position;
        to.y = 0f;

        float dist = to.magnitude;
        if (dist < 0.001f) return;

        Vector3 dir = to / dist;

        // 向きをターゲットへ
        RotateTowards(dir, chaseTurnDegPerSec);

        // 止まる距離内なら停止（Y速度維持）
        Vector3 v = rb.linearVelocity;
        if (dist <= chaseStopDistance)
        {
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        Vector3 planar = dir * chaseSpeed;
        rb.linearVelocity = new Vector3(planar.x, v.y, planar.z);
    }

    private void DoWander()
    {
        changeDirTimer -= Time.fixedDeltaTime;
        if (changeDirTimer <= 0f)
        {
            PickNewWanderDir();
            changeDirTimer = changeDirInterval;
        }

        // 目の前が壁なら方向変更（任意）
        if (avoidCheckDistance > 0f)
        {
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(origin, transform.forward, avoidCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                PickNewWanderDir();
                changeDirTimer = changeDirInterval;
            }
        }

        RotateTowards(wanderDir, wanderTurnDegPerSec);

        Vector3 v = rb.linearVelocity;
        Vector3 planar = wanderDir * wanderSpeed;
        rb.linearVelocity = new Vector3(planar.x, v.y, planar.z);
    }

    private void PickNewWanderDir()
    {
        Vector2 r = Random.insideUnitCircle.normalized;
        wanderDir = new Vector3(r.x, 0f, r.y);
        if (wanderDir.sqrMagnitude < 0.0001f) wanderDir = transform.forward;
    }

    private void RotateTowards(Vector3 dir, float turnDegPerSec)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion next = Quaternion.RotateTowards(rb.rotation, targetRot, turnDegPerSec * Time.fixedDeltaTime);
        rb.MoveRotation(next);
    }
}
