using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 10;
    [SerializeField] private int currentHp;

    [Header("Damage Control")]
    [Tooltip("ダメージ後の無敵時間（連続接触で減りすぎるのを防ぐ）")]
    [SerializeField] private float invincibleSeconds = 0.3f;
    private float invincibleTimer;

    [Header("Result (Game Over)")]
    [Tooltip("未指定ならシーンから自動取得します")]
    [SerializeField] private ResultMenuController resultMenu;

    [Tooltip("死亡時にResultを表示する")]
    [SerializeField] private bool showResultOnDeath = true;

    private bool deathHandled;

    [Header("Damage Voice (2D)")]
    [Tooltip("未指定なら Player 上の AudioSource を自動取得。なければ自動追加します。")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("ダメージ時に鳴らすボイス（例：univ1091）")]
    [SerializeField] private AudioClip damageVoiceClip;

    [Range(0f, 1f)]
    [Tooltip("ダメージボイスの音量（Inspectorで調整）")]
    [SerializeField] private float damageVoiceVolume = 1f;

    [Tooltip("接触ダメージ等で連打になりすぎる場合のクールダウン（秒）")]
    [SerializeField] private float voiceCooldown = 0.1f;

    private float nextVoiceTime;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    public event Action<int, int> OnHpChanged; // (current, max)
    public event Action OnDied;                // 必要なら外部で購読

    private void Awake()
    {
        if (maxHp < 1) maxHp = 1;

        // currentHp が 0 のまま保存されているケースもあるので、初期化
        if (currentHp <= 0) currentHp = maxHp;

        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        OnHpChanged?.Invoke(currentHp, maxHp);

        // ResultMenu
        if (resultMenu == null)
            resultMenu = FindFirstObjectByType<ResultMenuController>();

        // ===== ボイス用 AudioSource 準備（距離減衰なし=2D）=====
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();
        if (!voiceSource) voiceSource = gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f; // 0=2D（距離減衰なし）
    }

    private void Update()
    {
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 実際にダメージが通ったら true（無敵中などで通らなければ false）
    /// </summary>
    public bool ApplyDamage(int damage)
    {
        if (damage <= 0) return false;
        if (currentHp <= 0) return false;
        if (invincibleTimer > 0f) return false;

        int before = currentHp;

        currentHp -= damage;
        if (currentHp < 0) currentHp = 0;

        invincibleTimer = invincibleSeconds;
        OnHpChanged?.Invoke(currentHp, maxHp);

        // 実際に減った時だけボイス
        if (currentHp < before)
            PlayDamageVoice();

        if (currentHp <= 0)
            Die();

        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        if (currentHp <= 0) return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// 例えば「リスタート時にHPを満タンに戻す」などで使用
    /// </summary>
    public void ResetHpToMax()
    {
        deathHandled = false;
        invincibleTimer = 0f;

        currentHp = maxHp;
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    private void PlayDamageVoice()
    {
        if (!damageVoiceClip) return;
        if (!voiceSource) return;

        // 鳴りすぎ抑制（必要なければ voiceCooldown を 0 にしてOK）
        if (voiceCooldown > 0f && Time.time < nextVoiceTime) return;
        nextVoiceTime = Time.time + Mathf.Max(0f, voiceCooldown);

        voiceSource.PlayOneShot(damageVoiceClip, Mathf.Clamp01(damageVoiceVolume));
    }

    private void Die()
    {
        if (deathHandled) return;
        deathHandled = true;

        Debug.Log("Player died.");

        OnDied?.Invoke();

        // Result表示（死亡した“瞬間のTime”をResultMenuがキャプチャする）
        if (showResultOnDeath && resultMenu != null)
        {
            resultMenu.ShowResult();
        }

        // ここで Destroy(gameObject) すると ResultMenu が参照する物が壊れる場合があるので注意
        // Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        if (invincibleSeconds < 0f) invincibleSeconds = 0f;
        if (voiceCooldown < 0f) voiceCooldown = 0f;
        damageVoiceVolume = Mathf.Clamp01(damageVoiceVolume);
    }
#endif
}
