using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBossHitbox : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyBossHealth bossHealth;

    public EnemyBossHealth BossHealth => bossHealth;

    [Header("Damage")]
    [Tooltip("弱点倍率。Head=2、他=1 など")]
    [SerializeField] private float damageMultiplier = 1f;

    [Header("Projectile Filter")]
    [SerializeField] private string projectileTag = "";

    [Tooltip("同じProjectileが複数Hitboxに重なって多重ヒットしないようにする")]
    [SerializeField] private bool preventMultiHitBySameProjectile = true;

    [Tooltip("命中したProjectileを破壊する（プール運用ならOFF推奨）")]
    [SerializeField] private bool destroyProjectileOnHit = true;

    private void Reset()
    {
        bossHealth = GetComponentInParent<EnemyBossHealth>();
    }

    private void Awake()
    {
        if (bossHealth == null)
            bossHealth = GetComponentInParent<EnemyBossHealth>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (bossHealth == null) return;

        if (!string.IsNullOrEmpty(projectileTag) && !other.CompareTag(projectileTag))
            return;

        if (!TryGetProjectileDamage(other, out int baseDamage, out GameObject projectileGo))
            return;

        int finalDamage = ApplyHitAndGetFinalDamage(baseDamage, projectileGo);
        if (finalDamage <= 0) return;

        if (destroyProjectileOnHit && projectileGo != null)
            Destroy(projectileGo);
    }

    /// <summary>
    /// Projectile側（SphereCast等）から呼ぶ用。
    /// 返り値：実際に適用した“最終ダメージ”（倍率込み）。無効なら0。
    /// projectileGo=null なら「同一Projectile多重ヒット防止」を使わない（Beamなど向け）。
    /// </summary>
    public int ApplyHitAndGetFinalDamage(int baseDamage, GameObject projectileGo)
    {
        if (bossHealth == null) return 0;
        if (baseDamage <= 0) return 0;

        if (preventMultiHitBySameProjectile && projectileGo != null)
        {
            var once = projectileGo.GetComponent<ProjectileHitOnce>();
            if (once == null) once = projectileGo.AddComponent<ProjectileHitOnce>();
            if (once.consumed) return 0;
            once.consumed = true;
        }

        float m = Mathf.Max(0f, damageMultiplier);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * m));

        bossHealth.ApplyDamage(baseDamage, damageMultiplier);

        if (destroyProjectileOnHit && projectileGo != null)
            Destroy(projectileGo);

        return finalDamage;
    }

    private bool TryGetProjectileDamage(Collider other, out int damage, out GameObject projectileGo)
    {
        damage = 0;
        projectileGo = other.gameObject;

        if (other.TryGetComponent(out ProjectileMover mover))
        {
            damage = mover.Damage;
            return damage > 0;
        }

        if (other.TryGetComponent(out ProjectileDamage pd))
        {
            damage = pd.damage;
            return damage > 0;
        }

        var parentPd = other.GetComponentInParent<ProjectileDamage>();
        if (parentPd != null)
        {
            projectileGo = parentPd.gameObject;
            damage = parentPd.damage;
            return damage > 0;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (damageMultiplier < 0f) damageMultiplier = 0f;
    }
#endif
}

public sealed class ProjectileHitOnce : MonoBehaviour
{
    public bool consumed;
}

public sealed class ProjectileDamage : MonoBehaviour
{
    public int damage = 1;
}
