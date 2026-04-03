using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float speed = 18f;
    [SerializeField] private float range = 30f;

    [Header("Hit Detect")]
    [SerializeField] private float hitRadius = 0.05f;

    [Tooltip("主に Enemy / EnemyHitbox 用。SpellShooter から渡される想定。")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Environment Hit (No Collider on Projectile)")]
    [SerializeField] private LayerMask environmentMask = 0;
    [SerializeField] private bool destroyOnEnvironmentHit = true;

    [SerializeField] private bool hitTriggers = true;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    public int Damage { get => damage; set => damage = Mathf.Max(1, value); }

    [Header("Enemy")]
    [SerializeField] private LayerMask enemyMask = 0;

    [Header("Damage Popup")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private float popupUpOffset = 0.15f;
    [SerializeField] private float popupNormalOffset = 0.02f;
    [SerializeField] private Color enemyPopupColor = Color.white;
    [SerializeField] private Color bossPopupColor = Color.yellow;

    [Header("VFX (Enemy Only)")]
    [SerializeField] private GameObject enemyHitVfxPrefab;
    [SerializeField] private float enemyHitVfxLife = 2f;
    [SerializeField] private bool vfxAlignToHitNormal = false;

    [Header("SFX (Enemy Only)")]
    [SerializeField] private AudioClip enemyHitSfxClip;
    [SerializeField, Range(0f, 1f)] private float enemyHitSfxVolume = 1f;
    [SerializeField] private float enemyHitSfxMinDistance = 3f;
    [SerializeField] private float enemyHitSfxMaxDistance = 30f;
    [SerializeField] private AudioRolloffMode enemyHitSfxRolloff = AudioRolloffMode.Logarithmic;
    [SerializeField] private float enemyHitSfxPitch = 1f;
    [SerializeField] private float enemyHitSfxDoppler = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private bool launched;
    private Vector3 dir;
    private float traveled;

    private QueryTriggerInteraction EnemyQTI =>
        hitTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

    // 環境判定は Trigger を無視（環境Triggerに吸われないように）
    private const QueryTriggerInteraction EnvQTI = QueryTriggerInteraction.Ignore;

    
    public void Launch(Vector3 direction, float newRange, float newSpeed, LayerMask newHitMask, bool newHitTriggers = true)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        range = newRange;
        speed = newSpeed;

        // ★重要：渡された mask は「敵側」として保持し、環境は別枠で常時見る
        hitMask = newHitMask;
        hitTriggers = newHitTriggers;

        traveled = 0f;
        launched = true;
    }

    private void Update()
    {
        if (!launched) return;

        float step = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 end = start + dir * step;

        // 0) 開始地点が既にめり込んでいる（壁際発射など）対策：Overlapで即処理
        if (destroyOnEnvironmentHit && environmentMask.value != 0)
        {
            if (Physics.CheckSphere(start, hitRadius, environmentMask, EnvQTI))
            {
                if (debugLog) Debug.Log($"[ProjectileMover] Start inside environment. Destroy. pos={start}");
                Destroy(gameObject);
                return;
            }
        }

        // 1) Enemy(Trigger含む) と Environment(Trigger無視) を別で取り、近い方を採用
        bool hasEnemyHit = Physics.SphereCast(start, hitRadius, dir, out RaycastHit enemyHit, step, hitMask, EnemyQTI);

        bool hasEnvHit = false;
        RaycastHit envHit = default;
        if (destroyOnEnvironmentHit && environmentMask.value != 0)
        {
            hasEnvHit = Physics.SphereCast(start, hitRadius, dir, out envHit, step, environmentMask, EnvQTI);
        }

        if (hasEnemyHit || hasEnvHit)
        {
            RaycastHit hit = SelectNearestHit(hasEnemyHit, enemyHit, hasEnvHit, envHit);
            HandleHit(hit);
            return;
        }

        transform.position = end;

        traveled += step;
        if (traveled >= range)
        {
            Destroy(gameObject);
        }
    }

    private static RaycastHit SelectNearestHit(bool hasA, RaycastHit a, bool hasB, RaycastHit b)
    {
        if (hasA && hasB) return (a.distance <= b.distance) ? a : b;
        if (hasA) return a;
        return b;
    }

    private void HandleHit(RaycastHit hit)
    {
        Collider col = hit.collider;
        if (col == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 n = (hit.normal.sqrMagnitude > 0.0001f) ? hit.normal : Vector3.up;

        // ============================
        // Boss Hitbox 優先（倍率込み最終ダメージ表示）
        // ============================
        var bossHitbox = col.GetComponentInParent<EnemyBossHitbox>();
        if (bossHitbox != null)
        {
            // Boss側で倍率を掛けた“最終ダメージ”を返してもらう
            int finalDamage = bossHitbox.ApplyHitAndGetFinalDamage(Damage, gameObject);

            // 多重ヒット防止等で無効なら0が返る想定
            if (finalDamage > 0)
            {
                SpawnDamagePopup(finalDamage, hit.point, n, bossPopupColor);

                SpawnEnemyHitVfx(hit.point, n);
                PlayEnemyHitSfx(hit.point);
            }

            Destroy(gameObject);
            return;
        }

        // ============================
        // 通常Enemy
        // ============================
        bool isEnemyLayer = (enemyMask.value & (1 << col.gameObject.layer)) != 0;
        EnemyHealth eh = col.GetComponentInParent<EnemyHealth>();
        EnemyRareHealth erh = col.GetComponentInParent<EnemyRareHealth>();

        bool isEnemy = isEnemyLayer || (eh != null);

        if (isEnemy)
        {
            if (eh != null) eh.ApplyDamage(Damage);
            else if (erh != null) erh.ApplyDamage(Damage);


            SpawnDamagePopup(Damage, hit.point, n, enemyPopupColor);

            SpawnEnemyHitVfx(hit.point, n);
            PlayEnemyHitSfx(hit.point);

            Destroy(gameObject);
            return;
        }

        // ============================
        // 環境（Default/Terrainなど）
        // ============================
        if (destroyOnEnvironmentHit)
        {
            if (debugLog) Debug.Log($"[ProjectileMover] Hit environment/object: {col.name} layer={col.gameObject.layer}. Destroy.");
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void SpawnDamagePopup(int dmg, Vector3 hitPoint, Vector3 normal, Color color)
    {
        if (damagePopupPrefab == null) return;

        Vector3 pos = hitPoint
                      + normal * popupNormalOffset
                      + Vector3.up * popupUpOffset;

        var popup = Instantiate(damagePopupPrefab, pos, Quaternion.identity);

        // メインカメラを使う。別カメラ運用ならここを差し替え（またはDamagePopup側で指定）
        popup.Setup(dmg, Camera.main, color);
    }

    private void SpawnEnemyHitVfx(Vector3 pos, Vector3 normal)
    {
        if (enemyHitVfxPrefab == null) return;

        Quaternion rot = vfxAlignToHitNormal
            ? Quaternion.LookRotation(normal, Vector3.up)
            : Quaternion.identity;

        GameObject vfx = Instantiate(enemyHitVfxPrefab, pos, rot);
        if (enemyHitVfxLife > 0f) Destroy(vfx, enemyHitVfxLife);
    }

    private void PlayEnemyHitSfx(Vector3 pos)
    {
        if (enemyHitSfxClip == null) return;

        GameObject go = new GameObject("EnemyHitSFX");
        go.transform.position = pos;

        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.rolloffMode = enemyHitSfxRolloff;
        a.minDistance = Mathf.Max(0.01f, enemyHitSfxMinDistance);
        a.maxDistance = Mathf.Max(a.minDistance, enemyHitSfxMaxDistance);
        a.dopplerLevel = Mathf.Max(0f, enemyHitSfxDoppler);

        a.clip = enemyHitSfxClip;
        a.volume = Mathf.Clamp01(enemyHitSfxVolume);
        a.pitch = Mathf.Max(0.01f, enemyHitSfxPitch);

        a.Play();
        Destroy(go, enemyHitSfxClip.length / a.pitch + 0.1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (damage < 1) damage = 1;
        if (speed < 0.1f) speed = 0.1f;
        if (range < 0.1f) range = 0.1f;
        if (hitRadius < 0f) hitRadius = 0f;
        if (popupUpOffset < 0f) popupUpOffset = 0f;
        if (popupNormalOffset < 0f) popupNormalOffset = 0f;
    }
#endif
}
