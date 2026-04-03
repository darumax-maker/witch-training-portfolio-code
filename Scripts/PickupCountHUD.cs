using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PickupCountHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerPickupStats stats;

    [SerializeField] private TMP_Text attackCountText;
    [SerializeField] private TMP_Text speedCountText;

    [Header("Format")]
    [Tooltip("ó·: \"{0}\" ÇæÇØÅAÇ‹ÇΩÇÕ \"x{0}\" Ç»Ç«")]
    [SerializeField] private string countFormat = "x{0}";

    private void Awake()
    {
        if (stats == null) stats = FindFirstObjectByType<PlayerPickupStats>();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (stats == null) return;
        stats.OnAttackPowerBoostCountChanged += OnAttackChanged;
        stats.OnProjectileSpeedBoostCountChanged += OnSpeedChanged;
        RefreshAll();
    }

    private void OnDisable()
    {
        if (stats == null) return;
        stats.OnAttackPowerBoostCountChanged -= OnAttackChanged;
        stats.OnProjectileSpeedBoostCountChanged -= OnSpeedChanged;
    }

    private void OnAttackChanged(int value)
    {
        if (attackCountText != null) attackCountText.text = string.Format(countFormat, value);
    }

    private void OnSpeedChanged(int value)
    {
        if (speedCountText != null) speedCountText.text = string.Format(countFormat, value);
    }

    private void RefreshAll()
    {
        if (stats == null) return;
        OnAttackChanged(stats.AttackPowerBoostCount);
        OnSpeedChanged(stats.ProjectileSpeedBoostCount);
    }
}
