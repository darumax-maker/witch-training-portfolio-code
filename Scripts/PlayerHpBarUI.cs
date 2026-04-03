using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image fillImage;

    private void Awake()
    {
        if (playerHealth == null)
        {
            // ÉVÅ[ÉìÇ…1êlÇæÇØÇ»ÇÁÇ±ÇÍÇ≈Ç‡OK
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged += HandleHpChanged;

        // èâä˙îΩâf
        if (playerHealth != null)
            HandleHpChanged(playerHealth.CurrentHp, playerHealth.MaxHp);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged -= HandleHpChanged;
    }

    private void HandleHpChanged(int current, int max)
    {
        if (fillImage == null) return;
        float t = (max <= 0) ? 0f : (float)current / max;
        fillImage.fillAmount = Mathf.Clamp01(t);
    }
}
