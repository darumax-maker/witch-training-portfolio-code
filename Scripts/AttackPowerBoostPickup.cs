using UnityEngine;

[DisallowMultipleComponent]
public class AttackPowerBoostPickup : MonoBehaviour
{
    [Header("Boost")]
    [Tooltip("攻撃力の上昇量（例：+1）")]
    [SerializeField] private int damageIncrease = 1;

    [Header("Detection")]
    [Tooltip("PlayerのTag。タグ運用しないなら空でOK（SpellShooter探索のみで拾う）")]
    [SerializeField] private string playerTag = "Player";

    [Header("SFX (2D)")]
    [Tooltip("取得時に鳴らすSE（例：magic_cast_light）")]
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

        // 取得対象：SpellShooter を持つ Player 側
        var shooter = other.GetComponentInParent<SpellShooter>();
        if (shooter == null) return;

        // 取得成功：攻撃力UP
        shooter.AddProjectileDamage(damageIncrease);

        //取得数カウント
        var stats = other.GetComponentInParent<PlayerPickupStats>();
        if (stats != null) stats.AddAttackPowerBoost(1);


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
            // 2D固定（距離減衰なし）
            pickupSfxSource.spatialBlend = 0f;
            pickupSfxSource.playOnAwake = false;

            pickupSfxSource.PlayOneShot(pickupSfxClip, Mathf.Clamp01(pickupSfxVolume));
            return;
        }

        // 2) 未指定なら、一時AudioSourceを生成して鳴らす（DestroyされるPickupに紐づけないのが重要）
        GameObject go = new GameObject("AttackPowerBoostPickupSFX");
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f; // 2D（距離減衰なし）
        a.volume = Mathf.Clamp01(pickupSfxVolume);
        a.clip = pickupSfxClip;

        a.Play();
        Destroy(go, pickupSfxClip.length + 0.1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pickupSfxVolume = Mathf.Clamp01(pickupSfxVolume);
    }
#endif
}
