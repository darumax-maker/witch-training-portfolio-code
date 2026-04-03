using UnityEngine;

[DisallowMultipleComponent]
public class PotionPickup : MonoBehaviour
{
    [Header("Heal")]
    [Tooltip("回復量（例：+5）")]
    [SerializeField] private int healAmount = 5;

    [Header("Detection")]
    [Tooltip("PlayerのTag。タグ運用しないなら空でOK（PlayerHealth探索のみで拾う）")]
    [SerializeField] private string playerTag = "Player";

    [Header("SFX (2D)")]
    [Tooltip("取得時に鳴らすSE")]
    [SerializeField] private AudioClip pickupSfxClip;

    [Tooltip("取得SE音量（距離減衰なしの2D）")]
    [SerializeField, Range(0f, 1f)] private float pickupSfxVolume = 1f;

    [Tooltip("2Dで鳴らすAudioSource（推奨：Player配下の専用AudioSource）。未指定なら一時AudioSourceを生成して鳴らす")]
    [SerializeField] private AudioSource pickupSfxSource;

    private bool picked;

    private void OnTriggerEnter(Collider other)
    {
        if (picked) return;

        // Tagで絞る運用の場合
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return;

        // 取得対象：PlayerHealth を持つ Player 側
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;

        // 取得成功：回復
        if (healAmount > 0)
            health.Heal(healAmount);

        // 取得SE（1回だけ）
        PlayPickupSfx(other.transform);

        picked = true;
        Destroy(gameObject);
    }

    private void PlayPickupSfx(Transform playerTransform)
    {
        if (pickupSfxClip == null) return;

        // 1) 指定されたAudioSourceで鳴らす（推奨）
        if (pickupSfxSource != null)
        {
            pickupSfxSource.spatialBlend = 0f; // 2D
            pickupSfxSource.playOnAwake = false;

            pickupSfxSource.PlayOneShot(pickupSfxClip, Mathf.Clamp01(pickupSfxVolume));
            return;
        }

        // 2) 未指定なら、一時AudioSourceを生成して鳴らす（DestroyされるPickupに紐づけない）
        GameObject go = new GameObject("PotionPickupSFX");
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f; // 2D
        a.volume = Mathf.Clamp01(pickupSfxVolume);
        a.clip = pickupSfxClip;

        a.Play();
        Destroy(go, pickupSfxClip.length + 0.1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (healAmount < 0) healAmount = 0;
        pickupSfxVolume = Mathf.Clamp01(pickupSfxVolume);
    }
#endif
}
