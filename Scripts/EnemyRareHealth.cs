using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyRareHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 3;
    [SerializeField] private int currentHp;

    [Header("Rare Drop (Fixed)")]
    [Tooltip("必ず落とす AttackPowerBoost のPrefab")]
    [SerializeField] private GameObject attackPowerBoostPrefab;

    [Tooltip("落とす個数（デフォルト3。Inspectorで調整）")]
    [Min(1)]
    [SerializeField] private int dropCount = 3;

    [Tooltip("同じ位置に重ならないよう散らす半径")]
    [SerializeField] private float dropScatterRadius = 0.6f;

    [Tooltip("地面に埋まらないよう上に少し出す")]
    [SerializeField] private float dropUpOffset = 0.2f;

    [Tooltip("地面にスナップして落とす（Terrain等で有効）")]
    [SerializeField] private bool snapDropToGround = true;

    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayStartHeight = 50f;
    [SerializeField] private float rayDistance = 200f;
    [SerializeField] private float dropGroundYOffset = 0.02f;

    [Tooltip("Enemyが他所からDestroyされても、倒された扱いならDropしたい場合ON（保険）")]
    [SerializeField] private bool dropAlsoFromOnDestroy = true;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    public event Action<int, int> OnHpChanged; // (current, max)

    private bool dead;
    private bool dropped;

    // スポーン時スケール基準（Prefabの初期maxHp）
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
    /// スポーン直後に最大HPをスケールして満タンにする（EnemyHealthと同じ仕様）
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

    private void Die()
    {
        if (dead) return;
        dead = true;

        TryDropFixed();

        Destroy(gameObject);
    }

    private void TryDropFixed()
    {
        if (dropped) return;
        dropped = true;

        if (attackPowerBoostPrefab == null)
        {
            Debug.LogWarning($"[EnemyRareHealth] AttackPowerBoost prefab is not set. on {name}");
            return;
        }

        int n = Mathf.Max(1, dropCount);

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = GetDropPosition(i, n);
            Instantiate(attackPowerBoostPrefab, pos, Quaternion.identity);
        }
    }

    private Vector3 GetDropPosition(int index, int total)
    {
        // 散布（円）
        Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, dropScatterRadius);
        Vector3 basePos = transform.position + new Vector3(r.x, 0f, r.y);

        // 地面スナップ（任意）
        if (snapDropToGround)
        {
            Vector3 rayStart = basePos + Vector3.up * rayStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * Mathf.Max(0f, dropGroundYOffset);
            }
        }

        // スナップしない場合は上オフセット
        return basePos + Vector3.up * Mathf.Max(0f, dropUpOffset);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (!dropAlsoFromOnDestroy) return;

        if (!dropped && currentHp <= 0)
            TryDropFixed();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        if (dropCount < 1) dropCount = 1;
        if (dropScatterRadius < 0f) dropScatterRadius = 0f;
        if (dropUpOffset < 0f) dropUpOffset = 0f;
        if (rayStartHeight < 0f) rayStartHeight = 0f;
        if (rayDistance < 0f) rayDistance = 0f;
        if (dropGroundYOffset < 0f) dropGroundYOffset = 0f;
    }
#endif
}
