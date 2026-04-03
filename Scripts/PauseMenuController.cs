using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Globalization;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    // TitleSceneController と同じキー（統一）
    private const string MouseSensitivityPrefsKey = "Settings.MouseSensitivity";

    [Header("UI Root")]
    [Tooltip("ポーズUIのルート（Panel等）。ここを丸ごとON/OFFします")]
    [SerializeField] private GameObject pauseRoot;

    [Header("Pause Panels")]
    [Tooltip("通常のポーズメニュー（Paused文字/Back/Title等）をまとめたRoot")]
    [SerializeField] private GameObject pauseMainPanelRoot;

    [Tooltip("操作説明+感度調整パネルRoot（タイトルのControlsと同等の内容）")]
    [SerializeField] private GameObject controlsPanelRoot;

    [Header("Mouse Sensitivity (Controls Panel)")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_InputField mouseSensitivityValueInput;

    [Tooltip("入力欄が用意できない時のフォールバック（任意）")]
    [SerializeField] private TMP_Text mouseSensitivityValueTextFallback;

    [SerializeField] private float mouseSensitivityMin = 0.10f;
    [SerializeField] private float mouseSensitivityMax = 5.00f;
    [SerializeField] private float defaultMouseSensitivity = 2.00f;
    [Range(0, 4)]
    [SerializeField] private int sensitivityDisplayDigits = 2;

    [Header("Apply to Camera Look (Optional)")]
    [Tooltip("感度変更をその場でPlayerCameraMouseLookに反映する（任意）")]
    [SerializeField] private bool applyToCameraLookImmediately = true;

    [Tooltip("未指定ならシーン内から自動取得（Tag/Find）します")]
    [SerializeField] private PlayerCameraMouseLook cameraLook;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Scene")]
    [Tooltip("QuitGameで戻るシーン名（Build Settings に入っている必要あり）")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Pause Options")]
    [Tooltip("ポーズ中に止めたいスクリプト（SpellShooter / PlayerController 等）を入れてください")]
    [SerializeField] private Behaviour[] disableWhilePaused;

    [Tooltip("ポーズ中にAudioも止めたい場合はON（任意）")]
    [SerializeField] private bool pauseAudioListener = false;

    [Header("Controls Close Options")]
    [Tooltip("Controlsパネル表示中にEscで閉じる")]
    [SerializeField] private bool closeControlsWithEscape = true;

    private bool isPaused;
    private bool controlsShown;

    // カーソル状態を復元したい場合の退避
    private CursorLockMode prevLockMode;
    private bool prevCursorVisible;

    // UI相互更新の再入防止
    private bool suppressUiCallbacks;
    private bool sliderListenerRegistered;
    private bool inputListenerRegistered;

    private void Awake()
    {
        if (pauseRoot != null) pauseRoot.SetActive(false);

        // 初期はメインだけ表示、Controlsは閉じる
        ShowPauseMain(true);
        ShowControls(false);

        SetupMouseSensitivityUi();
    }

    private void OnDestroy()
    {
        UnregisterUiListeners();
    }

    private void Update()
    {
        // Controls表示中のEscは「ポーズ解除」ではなく「Controlsを閉じる」優先にする
        if (isPaused && closeControlsWithEscape && controlsShown && Input.GetKeyDown(toggleKey))
        {
            OnClickControlsBack();
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (isPaused) return;
        isPaused = true;

        // 退避
        prevLockMode = Cursor.lockState;
        prevCursorVisible = Cursor.visible;

        // UI表示
        if (pauseRoot != null) pauseRoot.SetActive(true);

        // 初期はメイン表示
        ShowPauseMain(true);
        ShowControls(false);

        // 感度UIを最新化
        RefreshMouseSensitivityUiFromSavedValue();

        // ゲームを止める
        Time.timeScale = 0f;

        // 任意：音も止める
        if (pauseAudioListener) AudioListener.pause = true;

        // 任意：操作系スクリプトを止める
        SetBehavioursEnabled(false);

        // UI操作できるようにカーソルを出す
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 反映先のカメラLookを確保（任意）
        if (applyToCameraLookImmediately && cameraLook == null)
            cameraLook = FindFirstObjectByType<PlayerCameraMouseLook>();
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;

        // UI非表示
        if (pauseRoot != null) pauseRoot.SetActive(false);

        // 時間再開
        Time.timeScale = 1f;

        // 任意：音を戻す
        if (pauseAudioListener) AudioListener.pause = false;

        // 任意：操作系スクリプトを戻す
        SetBehavioursEnabled(true);

        // カーソル状態復元
        Cursor.lockState = prevLockMode;
        Cursor.visible = prevCursorVisible;
    }

    public void QuitToTitle()
    {
        // シーン遷移前に必ず戻す
        if (pauseRoot != null) pauseRoot.SetActive(false);

        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;
        SetBehavioursEnabled(true);

        SceneManager.LoadScene(titleSceneName);
    }

    // --------- Pause UI Buttons ---------

    public void OnClickBack()
    {
        // ポーズ画面のBackボタン＝Resume想定
        Resume();
    }

    public void OnClickControls()
    {
        ShowPauseMain(false);
        ShowControls(true);

        RefreshMouseSensitivityUiFromSavedValue();

        if (mouseSensitivityValueInput != null)
            mouseSensitivityValueInput.DeactivateInputField();
    }

    public void OnClickControlsBack()
    {
        // 入力中なら確定してから戻る
        SaveMouseSensitivityFromUiIfPossible();

        ShowControls(false);
        ShowPauseMain(true);
    }

    // --------- Mouse Sensitivity UI Callbacks ---------

    public void OnMouseSensitivitySliderChanged(float value)
    {
        if (suppressUiCallbacks) return;

        float clamped = Mathf.Clamp(value, mouseSensitivityMin, mouseSensitivityMax);
        SaveMouseSensitivity(clamped);

        UpdateMouseSensitivityValueDisplay(clamped, updateSlider: false, updateInput: true);
        ApplySensitivityToCameraLookIfNeeded(clamped);
    }

    public void OnMouseSensitivityInputEndEdit(string rawText)
    {
        if (suppressUiCallbacks) return;

        float current = GetCurrentSensitivityValueForFallback();

        if (!TryParseSensitivity(rawText, out float parsed))
        {
            UpdateMouseSensitivityValueDisplay(current, updateSlider: true, updateInput: true);
            return;
        }

        float clamped = Mathf.Clamp(parsed, mouseSensitivityMin, mouseSensitivityMax);
        SaveMouseSensitivity(clamped);

        UpdateMouseSensitivityValueDisplay(clamped, updateSlider: true, updateInput: true);
        ApplySensitivityToCameraLookIfNeeded(clamped);

        if (mouseSensitivityValueInput != null)
            mouseSensitivityValueInput.DeactivateInputField();
    }

    public void OnClickResetMouseSensitivity()
    {
        float def = Mathf.Clamp(defaultMouseSensitivity, mouseSensitivityMin, mouseSensitivityMax);
        SaveMouseSensitivity(def);
        UpdateMouseSensitivityValueDisplay(def, updateSlider: true, updateInput: true);
        ApplySensitivityToCameraLookIfNeeded(def);
    }

    // --------- Internal ---------

    private void SetBehavioursEnabled(bool enabled)
    {
        if (disableWhilePaused == null) return;

        for (int i = 0; i < disableWhilePaused.Length; i++)
        {
            var b = disableWhilePaused[i];
            if (b != null) b.enabled = enabled;
        }
    }

    private void ShowPauseMain(bool show)
    {
        if (pauseMainPanelRoot != null) pauseMainPanelRoot.SetActive(show);
    }

    private void ShowControls(bool show)
    {
        controlsShown = show;
        if (controlsPanelRoot != null) controlsPanelRoot.SetActive(show);
    }

    private void SetupMouseSensitivityUi()
    {
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.wholeNumbers = false;
            mouseSensitivitySlider.minValue = mouseSensitivityMin;
            mouseSensitivitySlider.maxValue = mouseSensitivityMax;
        }

        RegisterUiListeners();
        RefreshMouseSensitivityUiFromSavedValue();
    }

    private void RegisterUiListeners()
    {
        if (mouseSensitivitySlider != null && !sliderListenerRegistered)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivitySliderChanged);
            sliderListenerRegistered = true;
        }

        if (mouseSensitivityValueInput != null && !inputListenerRegistered)
        {
            mouseSensitivityValueInput.onEndEdit.AddListener(OnMouseSensitivityInputEndEdit);
            inputListenerRegistered = true;
        }
    }

    private void UnregisterUiListeners()
    {
        if (mouseSensitivitySlider != null && sliderListenerRegistered)
        {
            mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivitySliderChanged);
            sliderListenerRegistered = false;
        }

        if (mouseSensitivityValueInput != null && inputListenerRegistered)
        {
            mouseSensitivityValueInput.onEndEdit.RemoveListener(OnMouseSensitivityInputEndEdit);
            inputListenerRegistered = false;
        }
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
            ApplySensitivityToCameraLookIfNeeded(v);
        }
    }

    private void SaveMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(MouseSensitivityPrefsKey, value);
        PlayerPrefs.Save();
    }

    private float LoadMouseSensitivity(float fallback)
    {
        return PlayerPrefs.GetFloat(MouseSensitivityPrefsKey, fallback);
    }

    private void ApplySensitivityToCameraLookIfNeeded(float value)
    {
        if (!applyToCameraLookImmediately) return;

        if (cameraLook == null)
            cameraLook = FindFirstObjectByType<PlayerCameraMouseLook>();

        if (cameraLook != null)
        {
            // 前に送ったPlayerCameraMouseLook差し替え版には SetSensitivity がある想定
            cameraLook.SetSensitivity(value, saveToPrefs: false);
        }
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

        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;

        return false;
    }

    private string FormatSensitivity(float value)
    {
        string fmt = "F" + sensitivityDisplayDigits;
        return value.ToString(fmt, CultureInfo.InvariantCulture);
    }
}