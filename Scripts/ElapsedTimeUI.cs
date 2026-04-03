using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElapsedTimeUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text timeText;

    [Header("Mode")]
    [Tooltip("true: Time.timeScale の影響を受けない（ポーズ中も進む） / false: ゲーム時間に連動")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("開始からのオフセット秒（再開/演出などで調整したい場合）")]
    [SerializeField] private float startOffsetSeconds = 0f;

    private float elapsed;

    private void Awake()
    {
        if (timeText == null) timeText = GetComponent<TMP_Text>();
        elapsed = 0f;
        UpdateText(0f);
    }

    private void Update()
    {
        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        UpdateText(elapsed + startOffsetSeconds);
    }

    private void UpdateText(float seconds)
    {
        if (timeText == null) return;
        timeText.text = FormatSeconds(seconds);
    }

    public void ResetTimer(float offsetSeconds = 0f)
    {
        elapsed = 0f;
        startOffsetSeconds = Mathf.Max(0f, offsetSeconds);
        UpdateText(startOffsetSeconds);
    }

    public float GetElapsedSeconds() => elapsed + startOffsetSeconds;

    /// <summary>
    /// 秒を "分.秒2桁"（例: 01.05）に変換
    /// </summary>
    public static string FormatSeconds(float seconds)
    {
        if (seconds < 0f) seconds = 0f;

        int total = Mathf.FloorToInt(seconds);
        int minutes = total / 60;
        int secs = total % 60;

        return $"{minutes:00}.{secs:00}";
    }
}
