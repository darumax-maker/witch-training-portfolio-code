using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPickupStats : MonoBehaviour
{
    [Header("Counts (ReadOnly at runtime)")]
    [SerializeField] private int attackPowerBoostCount;
    [SerializeField] private int projectileSpeedBoostCount;

    public int AttackPowerBoostCount => attackPowerBoostCount;
    public int ProjectileSpeedBoostCount => projectileSpeedBoostCount;

    public event Action<int> OnAttackPowerBoostCountChanged;
    public event Action<int> OnProjectileSpeedBoostCountChanged;

    public void AddAttackPowerBoost(int add = 1)
    {
        if (add <= 0) return;
        attackPowerBoostCount += add;
        OnAttackPowerBoostCountChanged?.Invoke(attackPowerBoostCount);
    }

    public void AddProjectileSpeedBoost(int add = 1)
    {
        if (add <= 0) return;
        projectileSpeedBoostCount += add;
        OnProjectileSpeedBoostCountChanged?.Invoke(projectileSpeedBoostCount);
    }

    /// <summary>
    /// リスタート時などに取得数を0へ戻す
    /// </summary>
    public void ResetCounts()
    {
        attackPowerBoostCount = 0;
        projectileSpeedBoostCount = 0;

        OnAttackPowerBoostCountChanged?.Invoke(attackPowerBoostCount);
        OnProjectileSpeedBoostCountChanged?.Invoke(projectileSpeedBoostCount);
    }

    /// <summary>
    /// セーブ/ロード等で復元したい場合用（任意）
    /// </summary>
    public void SetCounts(int attackCount, int speedCount, bool notify = true)
    {
        attackPowerBoostCount = Mathf.Max(0, attackCount);
        projectileSpeedBoostCount = Mathf.Max(0, speedCount);

        if (notify)
        {
            OnAttackPowerBoostCountChanged?.Invoke(attackPowerBoostCount);
            OnProjectileSpeedBoostCountChanged?.Invoke(projectileSpeedBoostCount);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (attackPowerBoostCount < 0) attackPowerBoostCount = 0;
        if (projectileSpeedBoostCount < 0) projectileSpeedBoostCount = 0;
    }
#endif
}
