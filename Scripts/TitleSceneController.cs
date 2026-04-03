using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Globalization;

[DisallowMultipleComponent]
public sealed class TitleSceneController : MonoBehaviour
{
    // MainScene 側でも同じキーを使って読み込む
    public const string MouseSensitivityPrefsKey = "Settings.MouseSensitivity";

    [Header("Scene")]
    [SerializeField] private string mainSceneName = "MainScene";

    [Header("UI")]
    [Tooltip("通常のタイトルUI一式（タイトル文字/Start/Controls/Record等）をまとめたRoot。")]
    [SerializeField] private GameObject titleUiRoot;

    [Tooltip("操作説明パネルRoot（最初はOFF推奨）")]
    [SerializeField] private GameObject controlsPanelRoot;

    [Tooltip("RecordパネルRoot（最初はOFF推奨）")]
    [SerializeField] private GameObject recordPanelRoot;

    [Tooltip("Recordパネルの制御スクリプト（未指定なら recordPanelRoot から取得）")]
    [SerializeField] private RecordPanelUI recordPanelUI;

    [Header("Mouse Sensitivity (Controls Panel)")]
    [Tooltip("ControlPanel内のマウス感度スライダー")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Tooltip("感度値入力欄（TMP_InputField）")]
    [SerializeField] private TMP_InputField mouseSensitivityValueInput;

    [Tooltip("（互換用）旧TMP_Text表示欄。InputField未使用時のみ使う")]
    [SerializeField] private TMP_Text mouseSensitivityValueTextFallback;

    [Tooltip("マウス感度の最小値")]
    [SerializeField] private float mouseSensitivityMin = 0.10f;

    [Tooltip("マウス感度の最大値")]
    [SerializeField] private float mouseSensitivityMax = 5.00f;

    [Tooltip("保存値がない時に使う初期値（PlayerCameraMouseLookの初期値と合わせる）")]
    [SerializeField] private float defaultMouseSensitivity = 2.00f;

    [Tooltip("表示桁数（例: 2 なら 0.30 表示）")]
    [Range(0, 4)]
    [SerializeField] private int sensitivityDisplayDigits = 2;

    [Header("Cursor (Title)")]
    [SerializeField] private bool showCursorOnTitle = true;
    [SerializeField] private bool unlockCursorOnTitle = true;

    [Header("Panel Close Options")]
    [SerializeField] private bool closeControlsWithEscape = true;
    [SerializeField] private bool closeRecordWithEscape = true;

    private bool controlsShown;
    private bool recordShown;

    private bool mouseSliderListenerRegistered;
    private bool mouseInputListenerRegistered;
    private bool suppressUiCallbacks; // Slider/InputField相互更新の再入防止

    private void Awake()
    {
        // タイトルに来た時点で timeScale が 0 の事故を潰す
        Time.timeScale = 1f;
        AudioListener.pause = false;

        ApplyTitleCursor();

        if (recordPanelUI == null && recordPanelRoot != null)
            recordPanelUI = recordPanelRoot.GetComponentInChildren<RecordPanelUI>(true);

        SetupMouseSensitivityUi();

        // 初期状態
        ShowControls(false);
        ShowRecord(false);
        ShowTitle(true);
    }

    private void OnEnable()
    {
        ApplyTitleCursor();
        RefreshMouseSensitivityUiFromSavedValue();
    }

    private void OnDestroy()
    {
        UnregisterMouseSliderListener();
        UnregisterMouseInputListener();
    }

    private void Update()
    {
        if (closeControlsWithEscape && controlsShown && Input.GetKeyDown(KeyCode.Escape))
        {
            ShowControls(false);
            ShowTitle(true);
        }

        if (closeRecordWithEscape && recordShown && Input.GetKeyDown(KeyCode.Escape))
        {
            ShowRecord(false);
            ShowTitle(true);
        }
    }

    private void ApplyTitleCursor()
    {
        if (unlockCursorOnTitle) Cursor.lockState = CursorLockMode.None;
        if (showCursorOnTitle) Cursor.visible = true;
    }

    // ----- UI Button -----

    public void StartGame()
    {
        // 念のため現在UI値を保存してから遷移
        SaveMouseSensitivityFromUiIfPossible();
        SceneManager.LoadScene(mainSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnClickControls()
    {
        ShowTitle(false);
        ShowRecord(false);
        ShowControls(true);

        // 開くたびに最新値を表示
        RefreshMouseSensitivityUiFromSavedValue();

        // 数値欄を選択状態にしない（Enter誤爆防止）
        if (mouseSensitivityValueInput != null)
            mouseSensitivityValueInput.DeactivateInputField();
    }

    public void OnClickControlsBack()
    {
        // 入力中なら確定してから戻る
        SaveMouseSensitivityFromUiIfPossible();

        ShowControls(false);
        ShowTitle(true);
    }

    public void OnClickRecord()
    {
        ShowTitle(false);
        ShowControls(false);
        ShowRecord(true);

        // 開くたびに最新化
        if (recordPanelUI != null) recordPanelUI.Refresh();
    }

    public void OnClickRecordBack()
    {
        ShowRecord(false);
        ShowTitle(true);
    }

    // Slider の OnValueChanged(float) から呼べる（コードでも自動登録）
    public void OnMouseSensitivitySliderChanged(float value)
    {
        if (suppressUiCallbacks) return;

        float clamped = Mathf.Clamp(value, mouseSensitivityMin, mouseSensitivityMax);

        SaveMouseSensitivity(clamped);
        UpdateMouseSensitivityValueDisplay(clamped, updateSlider: false, updateInput: true);
    }

    // TMP_InputField の OnEndEdit(string) から呼べる（コードでも自動登録）
    public void OnMouseSensitivityInputEndEdit(string rawText)
    {
        if (suppressUiCallbacks) return;

        // ESCでキャンセルや未入力時に崩れないよう、保存値/スライダー値へ戻す
        float current = GetCurrentSensitivityValueForFallback();

        if (!TryParseSensitivity(rawText, out float parsed))
        {
            UpdateMouseSensitivityValueDisplay(current, updateSlider: true, updateInput: true);
            return;
        }

        float clamped = Mathf.Clamp(parsed, mouseSensitivityMin, mouseSensitivityMax);

        SaveMouseSensitivity(clamped);
        UpdateMouseSensitivityValueDisplay(clamped, updateSlider: true, updateInput: true);

        // 見た目上も確定文字列に統一
        if (mouseSensitivityValueInput != null)
            mouseSensitivityValueInput.DeactivateInputField();
    }

    // 任意：リセットボタン用
    public void OnClickResetMouseSensitivity()
    {
        float def = Mathf.Clamp(defaultMouseSensitivity, mouseSensitivityMin, mouseSensitivityMax);
        SaveMouseSensitivity(def);
        UpdateMouseSensitivityValueDisplay(def, updateSlider: true, updateInput: true);
    }

    // ----- Internal -----

    private void ShowTitle(bool show)
    {
        if (titleUiRoot != null) titleUiRoot.SetActive(show);
        ApplyTitleCursor();
    }

    private void ShowControls(bool show)
    {
        controlsShown = show;
        if (controlsPanelRoot != null) controlsPanelRoot.SetActive(show);
        ApplyTitleCursor();
    }

    private void ShowRecord(bool show)
    {
        recordShown = show;
        if (recordPanelRoot != null) recordPanelRoot.SetActive(show);
        ApplyTitleCursor();
    }

    private void SetupMouseSensitivityUi()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.wholeNumbers = false;
            mouseSensitivitySlider.minValue = mouseSensitivityMin;
            mouseSensitivitySlider.maxValue = mouseSensitivityMax;
            RegisterMouseSliderListener();
        }

        RegisterMouseInputListener();

        RefreshMouseSensitivityUiFromSavedValue();
    }

    private void RegisterMouseSliderListener()
    {
        if (mouseSensitivitySlider == null || mouseSliderListenerRegistered) return;

        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderChanged);
        mouseSliderListenerRegistered = true;
    }

    private void UnregisterMouseSliderListener()
    {
        if (mouseSensitivitySlider == null || !mouseSliderListenerRegistered) return;

        mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);
        mouseSliderListenerRegistered = false;
    }

    private void RegisterMouseInputListener()
    {
        if (mouseSensitivityValueInput == null || mouseInputListenerRegistered) return;

        mouseSensitivityValueInput.onEndEdit.AddListener(OnMouseSensitivityInputEndEdit);
        mouseInputListenerRegistered = true;
    }

    private void UnregisterMouseInputListener()
    {
        if (mouseSensitivityValueInput == null || !mouseInputListenerRegistered) return;

        mouseSensitivityValueInput.onEndEdit.RemoveListener(OnMouseSensitivityInputEndEdit);
        mouseInputListenerRegistered = false;
    }

    private void RefreshMouseSensitivityUiFromSavedValue()
    {
        float saved = LoadMouseSensitivity(defaultMouseSensitivity);
        saved = Mathf.Clamp(saved, mouseSensitivityMin, mouseSensitivityMax);

        UpdateMouseSensitivityValueDisplay(saved, updateSlider: true, updateInput: true);
    }

    private void SaveMouseSensitivityFromUiIfPossible()
    {
        // 入力欄があるなら入力内容優先で確定
        if (mouseSensitivityValueInput != null && !string.IsNullOrWhiteSpace(mouseSensitivityValueInput.text))
        {
            OnMouseSensitivityInputEndEdit(mouseSensitivityValueInput.text);
            return;
        }

        // それ以外はスライダー値を保存
        if (mouseSensitivitySlider != null)
        {
            float v = Mathf.Clamp(mouseSensitivitySlider.value, mouseSensitivityMin, mouseSensitivityMax);
            SaveMouseSensitivity(v);
            UpdateMouseSensitivityValueDisplay(v, updateSlider: false, updateInput: true);
        }
    }

    private void SaveMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(MouseSensitivityPrefsKey, value);
        PlayerPrefs.Save();
    }

    private void UpdateMouseSensitivityValueDisplay(float value, bool updateSlider, bool updateInput)
    {
        suppressUiCallbacks = true;

        float clamped = Mathf.Clamp(value, mouseSensitivityMin, mouseSensitivityMax);

        if (updateSlider && mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = mouseSensitivityMin;
            mouseSensitivitySlider.maxValue = mouseSensitivityMax;
            mouseSensitivitySlider.wholeNumbers = false;
            mouseSensitivitySlider.SetValueWithoutNotify(clamped);
        }

        string formatted = FormatSensitivity(clamped);

        if (updateInput && mouseSensitivityValueInput != null)
        {
            mouseSensitivityValueInput.SetTextWithoutNotify(formatted);
        }

        if (mouseSensitivityValueTextFallback != null)
        {
            mouseSensitivityValueTextFallback.text = formatted;
        }

        suppressUiCallbacks = false;
    }

    private float GetCurrentSensitivityValueForFallback()
    {
        if (mouseSensitivitySlider != null)
            return Mathf.Clamp(mouseSensitivitySlider.value, mouseSensitivityMin, mouseSensitivityMax);

        return Mathf.Clamp(LoadMouseSensitivity(defaultMouseSensitivity), mouseSensitivityMin, mouseSensitivityMax);
    }

    private bool TryParseSensitivity(string raw, out float value)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string s = raw.Trim();

        // 日本語環境でカンマ入力されても通す
        s = s.Replace(',', '.');

        // InvariantCulture でまず試す（0.3 / 1.25 など）
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        // 念のためCurrentCultureでも試す
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;

        return false;
    }

    private string FormatSensitivity(float value)
    {
        string fmt = "F" + sensitivityDisplayDigits;
        // UI表示は小数点固定にしたいので InvariantCulture を使う
        return value.ToString(fmt, CultureInfo.InvariantCulture);
    }

    public static float LoadMouseSensitivity(float fallback)
    {
        return PlayerPrefs.GetFloat(MouseSensitivityPrefsKey, fallback);
    }
}