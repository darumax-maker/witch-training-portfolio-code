using UnityEngine;

[DisallowMultipleComponent]
public class SpellShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraTransform;     // 未指定なら Camera.main
    [SerializeField] private Transform muzzle;              // Player前方の発射点
    [SerializeField] private GameObject projectilePrefab;   // Projectile
    [SerializeField] private GameObject beamPrefab;         // Fで出すビーム/斬撃VFX（BeamDamageArea付き推奨）

    [Header("Weapon")]
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private int minProjectileDamage = 1;

    [Header("Shoot (Projectile)")]
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float maxProjectileSpeed = 999f;
    [SerializeField] private float range = 30f;

    [Tooltip("狙えるレイヤー（Raycast & Projectileのヒット対象にも流用する想定）")]
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private bool aimTriggers = false;

    [Header("Spawn Offset (optional)")]
    [SerializeField] private float muzzleForwardOffset = 0.05f;

    [Header("Beam (Key)")]
    [SerializeField] private KeyCode beamKey = KeyCode.F;

    [Tooltip("Beamを出す位置（muzzleから前へ）")]
    [SerializeField] private float beamForwardOffset = 0.5f;

    [Tooltip("BeamPrefabの向きがズレている場合に補正（例：Y=90）")]
    [SerializeField] private Vector3 beamRotationOffsetEuler = Vector3.zero;

    [Tooltip("Beamを地面にスナップしたい場合ON（地面系VFX向け）")]
    [SerializeField] private bool beamSnapToGround = true;

    [SerializeField] private LayerMask beamGroundMask = ~0;
    [SerializeField] private float beamRayStartHeight = 50f;
    [SerializeField] private float beamRayDistance = 200f;
    [SerializeField] private float beamSpawnYOffset = 0.02f;

    [Tooltip("連打抑制したいなら設定（0なら無制限）")]
    [SerializeField] private float beamCooldownSeconds = 0f;

    [Header("Aim Override (Rotate is handled by PlayerController)")]
    [Tooltip("射撃時にPlayerControllerへ『向いてほしい方向』を渡す時間（秒）")]
    [SerializeField] private float aimHoldSeconds = 0.15f;

    // =========================
    // RainArcane (Right Click)
    // =========================
    [Header("RainArcane (Right Click)")]
    [SerializeField] private GameObject rainArcanePrefab;   // RainArcane（DoTスクリプト付き推奨）
    [Tooltip("足元基準にしたいTransform（未指定なら player(transform)）")]
    [SerializeField] private Transform rainSpawnAnchor;
    [SerializeField] private bool rainSnapToGround = true;
    [SerializeField] private LayerMask rainGroundMask = ~0;
    [SerializeField] private float rainRayStartHeight = 50f;
    [SerializeField] private float rainRayDistance = 200f;
    [SerializeField] private float rainSpawnYOffset = 0.02f;

    [Tooltip("RainArcaneの向き補正が必要なら（例：Y=90）")]
    [SerializeField] private Vector3 rainRotationOffsetEuler = Vector3.zero;

    [Tooltip("連打抑制（0なら無制限）")]
    [SerializeField] private float rainCooldownSeconds = 0f;

    private float nextBeamTime;
    private float nextRainTime;

    public int ProjectileDamage => projectileDamage;
    public float ProjectileSpeed => projectileSpeed;

    private const int FireMouseButton = 0;     // 左クリック
    private const int RainMouseButton = 1;     // 右クリック

    private void Awake()
    {
        ClampParams();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(FireMouseButton))
            ShootProjectile();

        if (Input.GetKeyDown(beamKey))
            CastBeam();

        if (Input.GetMouseButtonDown(RainMouseButton))
            CastRainArcane();
    }

    // --- Pickup から呼ぶAPI ---
    public void AddProjectileDamage(int add)
    {
        if (add == 0) return;
        projectileDamage = Mathf.Max(minProjectileDamage, projectileDamage + add);
    }

    public void AddProjectileSpeed(float add)
    {
        if (Mathf.Approximately(add, 0f)) return;
        projectileSpeed = Mathf.Clamp(projectileSpeed + add, 0.1f, maxProjectileSpeed);
    }

    private Transform GetCameraTransform()
    {
        if (cameraTransform != null) return cameraTransform;
        return (Camera.main != null) ? Camera.main.transform : null;
    }

    private QueryTriggerInteraction GetAimQTI()
        => aimTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        Transform cam = GetCameraTransform();
        if (cam == null) return false;

        Ray ray = new Ray(cam.position, cam.forward);
        float maxAim = range;

        aimPoint = ray.origin + ray.direction * maxAim;
        if (Physics.Raycast(ray, out RaycastHit hit, maxAim, aimMask, GetAimQTI()))
            aimPoint = hit.point;

        return true;
    }

    /// <summary>
    /// 撃った最終方向(sq)で PlayerController に向き要求（回転はPC側）
    /// </summary>
    private void RequestAimLookByShootDir(Vector3 shootDirWorld)
    {
        var pc = GetComponent<PlayerController>();
        if (pc == null) return;

        Vector3 yawDir = shootDirWorld;
        yawDir.y = 0f;

        if (yawDir.sqrMagnitude <= 0.0001f)
        {
            Transform cam = GetCameraTransform();
            if (cam != null)
            {
                yawDir = cam.forward;
                yawDir.y = 0f;
            }
        }

        if (yawDir.sqrMagnitude <= 0.0001f) return;
        pc.RequestAimLook(yawDir.normalized, aimHoldSeconds);
    }

    private void ShootProjectile()
    {
        if (muzzle == null || projectilePrefab == null) return;
        if (!TryGetAimPoint(out var aimPoint)) return;

        Vector3 shootDir = (aimPoint - muzzle.position);
        if (shootDir.sqrMagnitude < 0.0001f)
        {
            Transform cam = GetCameraTransform();
            shootDir = (cam != null) ? cam.forward : transform.forward;
        }
        shootDir.Normalize();

        RequestAimLookByShootDir(shootDir);

        Vector3 spawnPos = muzzle.position + shootDir * muzzleForwardOffset;
        GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDir, Vector3.up));

        if (go.TryGetComponent<ProjectileMover>(out var mover))
        {
            mover.Damage = projectileDamage;
            mover.Launch(shootDir, range, projectileSpeed, aimMask, aimTriggers);
            mover.Damage = projectileDamage;
        }
    }

    private void CastBeam()
    {
        if (beamPrefab == null || muzzle == null) return;

        if (beamCooldownSeconds > 0f && Time.time < nextBeamTime)
            return;

        if (!TryGetAimPoint(out var aimPoint)) return;

        Vector3 dir = (aimPoint - muzzle.position);
        dir.y = 0f;

        Vector3 shootDir = (dir.sqrMagnitude > 0.0001f)
            ? dir.normalized
            : GetFallbackForwardXZ();

        RequestAimLookByShootDir(shootDir);

        Vector3 spawnPos = muzzle.position + shootDir * beamForwardOffset;

        if (beamSnapToGround)
        {
            Vector3 rayStart = spawnPos + Vector3.up * beamRayStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, beamRayDistance, beamGroundMask, QueryTriggerInteraction.Ignore))
                spawnPos = hit.point + Vector3.up * Mathf.Max(0f, beamSpawnYOffset);
        }

        Quaternion rot = Quaternion.LookRotation(shootDir, Vector3.up) * Quaternion.Euler(beamRotationOffsetEuler);
        GameObject go = Instantiate(beamPrefab, spawnPos, rot);

        if (go.TryGetComponent<BeamDamageArea>(out var beam))
        {
            Camera useCam = (Camera.main != null) ? Camera.main : null;
            beam.Init(projectileDamage, useCam);
        }

        if (beamCooldownSeconds > 0f)
            nextBeamTime = Time.time + beamCooldownSeconds;
    }

    private void CastRainArcane()
    {
        if (rainArcanePrefab == null) return;

        if (rainCooldownSeconds > 0f && Time.time < nextRainTime)
            return;

        Transform anchor = (rainSpawnAnchor != null) ? rainSpawnAnchor : transform;

        Vector3 spawnPos = anchor.position;
        Quaternion spawnRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Quaternion.Euler(rainRotationOffsetEuler);

        if (rainSnapToGround)
        {
            Vector3 rayStart = spawnPos + Vector3.up * rainRayStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rainRayDistance, rainGroundMask, QueryTriggerInteraction.Ignore))
                spawnPos = hit.point + Vector3.up * Mathf.Max(0f, rainSpawnYOffset);
        }

        GameObject go = Instantiate(rainArcanePrefab, spawnPos, spawnRot);

        // DoTダメージを渡す（AttackPowerBoost反映済みの projectileDamage）
        if (go.TryGetComponent<RainArcaneDamageArea>(out var area))
        {
            Camera useCam = (Camera.main != null) ? Camera.main : null;
            area.Init(projectileDamage, useCam);
        }

        if (rainCooldownSeconds > 0f)
            nextRainTime = Time.time + rainCooldownSeconds;
    }

    private Vector3 GetFallbackForwardXZ()
    {
        Transform cam = GetCameraTransform();
        Vector3 f = (cam != null) ? cam.forward : transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return Vector3.forward;
        return f.normalized;
    }

    private void ClampParams()
    {
        if (minProjectileDamage < 1) minProjectileDamage = 1;
        if (projectileDamage < minProjectileDamage) projectileDamage = minProjectileDamage;

        if (range < 0.1f) range = 0.1f;

        if (maxProjectileSpeed < 0.1f) maxProjectileSpeed = 0.1f;
        if (projectileSpeed < 0.1f) projectileSpeed = 0.1f;
        if (projectileSpeed > maxProjectileSpeed) projectileSpeed = maxProjectileSpeed;

        if (muzzleForwardOffset < 0f) muzzleForwardOffset = 0f;

        if (beamForwardOffset < 0f) beamForwardOffset = 0f;
        if (beamCooldownSeconds < 0f) beamCooldownSeconds = 0f;

        if (aimHoldSeconds < 0.01f) aimHoldSeconds = 0.01f;

        if (rainCooldownSeconds < 0f) rainCooldownSeconds = 0f;
        if (rainSpawnYOffset < 0f) rainSpawnYOffset = 0f;
        if (rainRayStartHeight < 0f) rainRayStartHeight = 0f;
        if (rainRayDistance < 0.01f) rainRayDistance = 0.01f;
    }

#if UNITY_EDITOR
    private void OnValidate() => ClampParams();
#endif
}
