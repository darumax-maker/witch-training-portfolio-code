using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBossHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 50;
    [SerializeField] private int currentHp;

    [Header("Drops (Guaranteed)")]
    [SerializeField] private GameObject potionPrefab;
    [SerializeField] private GameObject attackPowerBoostPrefab;
    [SerializeField] private GameObject projectileSpeedBoostPrefab;

    [Tooltip("地面に埋まらないよう上に少し出す")]
    [SerializeField] private float dropUpOffset = 0.25f;

    [Tooltip("3つを同位置に重ねたくない場合に少し散らす半径")]
    [SerializeField] private float dropScatterRadius = 0.5f;

    [Tooltip("他所からDestroyされても、HP0ならDropしたい場合ON（保険）")]
    [SerializeField] private bool dropAlsoFromOnDestroy = true;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    public event Action<int, int> OnHpChanged; // (current, max)

    private bool dead;
    private bool dropped;

    // ★追加：スポーン時スケールの基準
    private int baseMaxHp;

    private void Awake()
    {
        if (maxHp < 1) maxHp = 1;
        if (currentHp <= 0) currentHp = maxHp;

        baseMaxHp = Mathf.Max(1, maxHp);

        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// ★追加：スポーン直後に最大HPをスケールして満タンにする。
    /// </summary>
    public void ApplySpawnHpScale(float multiplier, bool refill = true, int maxHpCap = 1000000)
    {
        if (baseMaxHp < 1) baseMaxHp = Mathf.Max(1, maxHp);

        float m = Mathf.Max(1f, multiplier);
        int cap = (maxHpCap <= 0) ? int.MaxValue : maxHpCap;

        int newMax = Mathf.Clamp(Mathf.RoundToInt(baseMaxHp * m), 1, cap);
        maxHp = newMax;

        if (refill) currentHp = maxHp;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public void ApplyDamage(int damage)
    {
        if (dead) return;
        if (damage <= 0) return;

        currentHp -= damage;
        if (currentHp < 0) currentHp = 0;

        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
            Die();
    }

    public void ApplyDamage(int baseDamage, float multiplier)
    {
        if (baseDamage <= 0) return;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, multiplier)));
        ApplyDamage(finalDamage);
    }

    private void Die()
    {
        if (dead) return;
        dead = true;

        DropAllGuaranteed();

        Destroy(gameObject);
    }

    private void DropAllGuaranteed()
    {
        if (dropped) return;
        dropped = true;

        Vector3 basePos = transform.position + Vector3.up * Mathf.Max(0f, dropUpOffset);

        SpawnDrop(potionPrefab, basePos, 0);
        SpawnDrop(attackPowerBoostPrefab, basePos, 1);
        SpawnDrop(projectileSpeedBoostPrefab, basePos, 2);
    }

    private void SpawnDrop(GameObject prefab, Vector3 basePos, int index)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[EnemyBossHealth] Drop prefab is None (index={index}) on {name}");
            return;
        }

        Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, dropScatterRadius);
        Vector3 pos = basePos + new Vector3(r.x, 0f, r.y);
        Instantiate(prefab, pos, Quaternion.identity);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (!dropAlsoFromOnDestroy) return;

        if (!dropped && currentHp <= 0)
            DropAllGuaranteed();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        if (dropUpOffset < 0f) dropUpOffset = 0f;
        if (dropScatterRadius < 0f) dropScatterRadius = 0f;
    }
#endif
}
