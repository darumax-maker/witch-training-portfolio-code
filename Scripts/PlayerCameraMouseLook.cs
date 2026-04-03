using UnityEngine;

public class PlayerCameraMouseLook : MonoBehaviour
{
    // TitleSceneController と同じキー名を使うこと
    private const string MouseSensitivityPrefsKey = "Settings.MouseSensitivity";

    [Header("Target")]
    [SerializeField] private Transform target;                 // unitychan を指定
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.6f, -3.0f);

    [Header("Follow Smoothing")]
    [Tooltip("位置追従の滑らかさ（大きいほど追従が速い）")]
    [SerializeField] private float positionFollowSharpness = 15f;

    [Header("Mouse Look")]
    [SerializeField] private float sensitivityX = 2.0f;
    [SerializeField] private float sensitivityY = 2.0f;

    [Tooltip("タイトル画面で保存した感度を読み込む（X/Y両方に適用）")]
    [SerializeField] private bool loadSensitivityFromPrefs = true;

    [Tooltip("保存されている感度をX/Y両方に同じ値で適用する")]
    [SerializeField] private bool applyLoadedSensitivityToBothAxes = true;

    [Tooltip("WebGL時の感度補正倍率（Editorより速い場合に下げる）")]
    [SerializeField] private float webGLSensitivityMultiplier = 0.5f;

    [Tooltip("GetAxisRawではなくGetAxisを使う（WebGLで体感を安定させたい場合にON推奨）")]
    [SerializeField] private bool useSmoothedAxis = true;

    [Tooltip("1フレームの最大入力を制限（WebGLのスパイク対策）")]
    [SerializeField] private float maxMouseDeltaPerFrame = 20f;

    [SerializeField] private float pitchMin = -35f;
    [SerializeField] private float pitchMax = 70f;
    [SerializeField] private bool lockCursor = true;

    [Header("Rig")]
    [Tooltip("Yawを適用するルート（通常はこのオブジェクト）")]
    [SerializeField] private Transform yawRoot;
    [Tooltip("Pitchを適用する軸（通常は CameraPitch）")]
    [SerializeField] private Transform pitchRoot;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        if (yawRoot == null) yawRoot = transform;

        LoadSensitivityFromPrefsIfNeeded();

        // WebGLではAwake時ロックが通らないことがあるが、Editor/Standaloneでは有効
        if (lockCursor)
        {
            TryLockCursor();
        }
    }

    private void Start()
    {
        // 初期角の取り込み（ローカル基準）
        yaw = yawRoot.localEulerAngles.y;

        if (pitchRoot != null)
        {
            float px = pitchRoot.localEulerAngles.x;
            if (px > 180f) px -= 360f;
            pitch = Mathf.Clamp(px, pitchMin, pitchMax);
        }

        // 初回はターゲット位置へスナップ（ガクつき防止）
        if (target != null)
        {
            Vector3 desiredPos = GetDesiredWorldPosition();
            transform.position = desiredPos;
        }
    }

    private void Update()
    {
        // WebGLのPointer Lock対策：クリック時にもロックを試みる
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            TryLockCursor();
        }

        // ロック中だけ視点回転（UI操作時の誤回転を防ぐ）
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            // Escで解放後などは回さない
            return;
        }

        // マウス入力（旧Input）
        float mx = useSmoothedAxis ? Input.GetAxis("Mouse X") : Input.GetAxisRaw("Mouse X");
        float my = useSmoothedAxis ? Input.GetAxis("Mouse Y") : Input.GetAxisRaw("Mouse Y");

        // WebGL感度補正
        float platformMul = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
        platformMul = webGLSensitivityMultiplier;
#endif

        mx *= sensitivityX * platformMul;
        my *= sensitivityY * platformMul;

        // スパイク対策（WebGLでの急回転防止）
        mx = Mathf.Clamp(mx, -maxMouseDeltaPerFrame, maxMouseDeltaPerFrame);
        my = Mathf.Clamp(my, -maxMouseDeltaPerFrame, maxMouseDeltaPerFrame);

        yaw += mx;
        yawRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        if (pitchRoot != null)
        {
            pitch -= my;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
            pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Esc でカーソル解放
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void LateUpdate()
    {
        // カメラ追従は LateUpdate 推奨（プレイヤー移動後に追従するため）
        if (target == null) return;

        Vector3 desiredPos = GetDesiredWorldPosition();

        // 位置の滑らか追従（フレームレート非依存）
        float t = 1f - Mathf.Exp(-positionFollowSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);
    }

    private Vector3 GetDesiredWorldPosition()
    {
        // yaw/pitch後の「カメラリグの向き」を基準にオフセットをワールドへ変換
        // followOffsetは「リグ基準のローカル」扱い（例: (0,1.6,-3)）
        return target.position + yawRoot.rotation * followOffset;
    }

    private void TryLockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LoadSensitivityFromPrefsIfNeeded()
    {
        if (!loadSensitivityFromPrefs) return;

        // fallbackはInspectorの現在値
        float fallback = Mathf.Max(0.01f, sensitivityX);
        float loaded = PlayerPrefs.GetFloat(MouseSensitivityPrefsKey, fallback);

        // 念のため不正値防止
        if (float.IsNaN(loaded) || float.IsInfinity(loaded))
            loaded = fallback;

        loaded = Mathf.Max(0.01f, loaded);

        sensitivityX = loaded;

        if (applyLoadedSensitivityToBothAxes)
        {
            sensitivityY = loaded;
        }
    }

    // --- 任意：将来、ゲーム内オプションから直接変更したい時に使えるAPI ---

    public void SetSensitivity(float value, bool saveToPrefs = true)
    {
        float v = Mathf.Max(0.01f, value);
        sensitivityX = v;
        sensitivityY = v;

        if (saveToPrefs)
        {
            PlayerPrefs.SetFloat(MouseSensitivityPrefsKey, v);
            PlayerPrefs.Save();
        }
    }

    public float GetSensitivityX() => sensitivityX;
    public float GetSensitivityY() => sensitivityY;
}