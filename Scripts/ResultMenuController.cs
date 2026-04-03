using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ResultMenuController : MonoBehaviour
{
    [Header("UI Root")]
    [SerializeField] private GameObject resultRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text boostText;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string mainSceneName = "MainScene";

    [Header("Refs")]
    [SerializeField] private ElapsedTimeUI elapsedTimeUI;
    [SerializeField] private PlayerPickupStats pickupStats;

    [Header("Result Options")]
    [SerializeField] private Behaviour[] disableWhileResult;
    [SerializeField] private bool pauseAudioListener = false;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorWhenPlaying = true;

    [Header("Record Save")]
    [Tooltip("死亡時に記録を保存する")]
    [SerializeField] private bool saveRecordOnShow = true;

    private bool shown;
    private float capturedSeconds;

    private CursorLockMode prevLockMode;
    private bool prevCursorVisible;

    private void Awake()
    {
        if (elapsedTimeUI == null) elapsedTimeUI = FindFirstObjectByType<ElapsedTimeUI>();
        if (pickupStats == null) pickupStats = FindFirstObjectByType<PlayerPickupStats>();

        if (resultRoot != null) resultRoot.SetActive(false);
        ApplyCursor(forUI: false);
    }

    public void ShowResult()
    {
        if (shown) return;
        shown = true;

        prevLockMode = Cursor.lockState;
        prevCursorVisible = Cursor.visible;

        capturedSeconds = (elapsedTimeUI != null)
            ? Mathf.Max(0f, elapsedTimeUI.GetElapsedSeconds())
            : Mathf.Max(0f, Time.timeSinceLevelLoad);

        string timeStr = ElapsedTimeUI.FormatSeconds(capturedSeconds);
        if (resultText != null) resultText.text = $"Result : {timeStr}";

        int atk = (pickupStats != null) ? pickupStats.AttackPowerBoostCount : 0;
        int spd = (pickupStats != null) ? pickupStats.ProjectileSpeedBoostCount : 0;

        if (boostText != null)
        {
            boostText.text =
                $": {atk}\n" +
                $": {spd}";
        }

        // ★ここで保存（時間と取得数が確定した直後）
        if (saveRecordOnShow)
        {
            EnsureStoreExists();
            if (RunRecordStore.Instance != null)
                RunRecordStore.Instance.AddRecord(capturedSeconds, atk, spd);
        }

        if (resultRoot != null) resultRoot.SetActive(true);

        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;

        SetBehavioursEnabled(false);
        ApplyCursor(forUI: true);

        var pause = FindFirstObjectByType<PauseMenuController>();
        if (pause != null) pause.enabled = false;
    }

    public void OnClickRestart()
    {
        PrepareLeaveResult();
        SceneManager.LoadScene(mainSceneName);
    }

    public void OnClickTitle()
    {
        PrepareLeaveResult();
        SceneManager.LoadScene(titleSceneName);
    }

    private void PrepareLeaveResult()
    {
        if (resultRoot != null) resultRoot.SetActive(false);

        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;

        SetBehavioursEnabled(true);

        Cursor.lockState = prevLockMode;
        Cursor.visible = prevCursorVisible;
    }

    private void SetBehavioursEnabled(bool enabled)
    {
        if (disableWhileResult == null) return;

        for (int i = 0; i < disableWhileResult.Length; i++)
        {
            var b = disableWhileResult[i];
            if (b != null) b.enabled = enabled;
        }
    }

    private void ApplyCursor(bool forUI)
    {
        if (forUI)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (lockCursorWhenPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void EnsureStoreExists()
    {
        if (RunRecordStore.Instance != null) return;

        var existing = FindFirstObjectByType<RunRecordStore>();
        if (existing != null) return;

        var go = new GameObject("RunRecordStore");
        go.AddComponent<RunRecordStore>();
    }
}
