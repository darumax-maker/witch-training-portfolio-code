using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Serializable]
    public struct DropEntry
    {
        [Tooltip("ドロップするPrefab")]
        public GameObject prefab;

        [Tooltip("このアイテムが選ばれる重み（相対値）。例：40 / 40 / 20")]
        [Min(0f)] public float weight;
    }

    [Header("HP")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private int currentHp;

    [Header("Drop (Loot Table)")]
    [Tooltip("まずこの確率で「アイテムを落とすか」を抽選します（例：0.5 = 50%）")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.5f;

    [Tooltip("ドロップ候補（重み付き）。weightの比で抽選されます。")]
    [SerializeField]
    private DropEntry[] dropTable =
    {
        new DropEntry { prefab = null, weight = 40f }, // AttackPowerBoost
        new DropEntry { prefab = null, weight = 40f }, // ProjectileSpeedBoost
        new DropEntry { prefab = null, weight = 20f }, // Potion
    };

    [Tooltip("地面に埋まらないよう上に少し出す")]
    [SerializeField] private float dropUpOffset = 0.2f;

    [Tooltip("Enemyが他所からDestroyされても、倒された扱いならDropしたい場合ON（保険）")]
    [SerializeField] private bool dropAlsoFromOnDestroy = true;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    public event Action<int, int> OnHpChanged; // (current, max)

    private bool dead;
    private bool dropped;

    // ★追加：スポーン時スケールの基準（Prefabの初期maxHp）
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
    /// ★追加：スポーン直後に「最大HP」をスケールして満タンにする。
    /// 例：multiplier=3 のとき MaxHp = baseMaxHp*3, CurrentHp=MaxHp
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
        {
            Die();
        }
    }

    private void Die()
    {
        if (dead) return;
        dead = true;

        TryDrop();

        Destroy(gameObject);
    }

    private void TryDrop()
    {
        if (dropped) return;
        dropped = true;

        if (UnityEngine.Random.value >= dropChance) return;

        GameObject chosen = ChooseDropPrefab();
        if (chosen == null)
        {
            Debug.LogWarning($"[EnemyHealth] DropTable has no valid entries (prefab/weight). on {name}");
            return;
        }

        Vector3 pos = transform.position + Vector3.up * Mathf.Max(0f, dropUpOffset);
        Instantiate(chosen, pos, Quaternion.identity);
    }

    private GameObject ChooseDropPrefab()
    {
        if (dropTable == null || dropTable.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < dropTable.Length; i++)
        {
            var e = dropTable[i];
            if (e.prefab == null) continue;
            if (e.weight <= 0f) continue;
            total += e.weight;
        }

        if (total <= 0f) return null;

        float r = UnityEngine.Random.value * total;
        for (int i = 0; i < dropTable.Length; i++)
        {
            var e = dropTable[i];
            if (e.prefab == null) continue;
            if (e.weight <= 0f) continue;

            r -= e.weight;
            if (r <= 0f) return e.prefab;
        }

        for (int i = dropTable.Length - 1; i >= 0; i--)
        {
            var e = dropTable[i];
            if (e.prefab != null && e.weight > 0f) return e.prefab;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (!dropAlsoFromOnDestroy) return;

        if (!dropped && currentHp <= 0)
        {
            TryDrop();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        dropChance = Mathf.Clamp01(dropChance);
        if (dropUpOffset < 0f) dropUpOffset = 0f;

        if (dropTable != null)
        {
            for (int i = 0; i < dropTable.Length; i++)
            {
                if (dropTable[i].weight < 0f) dropTable[i].weight = 0f;
            }
        }
    }
#endif
}
