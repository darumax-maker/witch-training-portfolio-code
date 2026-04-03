using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossHitbox : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damageMultiplier = 1f;

    [Header("Refs")]
    [SerializeField] private EnemyHealth enemyHealth; // BossのHP管理（既存のEnemyHealthでOK）

    private void Awake()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // PlayerProjectile 側のコンポーネントに合わせる
        if (!other.TryGetComponent<ProjectileMover>(out var proj)) return;

        int baseDamage = proj.Damage;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));

        enemyHealth?.ApplyDamage(finalDamage);

        // 1発で消す運用（多段ヒット防止）
        Destroy(other.gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (damageMultiplier < 0f) damageMultiplier = 0f;
    }
#endif
}
