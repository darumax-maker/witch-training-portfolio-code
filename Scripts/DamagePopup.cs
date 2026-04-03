using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamagePopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text text;

    [Header("Motion")]
    [SerializeField] private float lifeSeconds = 0.8f;
    [SerializeField] private float riseSpeed = 1.5f;
    [SerializeField] private float randomHorizontal = 0.2f;

    [Header("Fade")]
    [SerializeField] private bool fadeOut = true;
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Time")]
    [Tooltip("true: ポーズ中も動く / false: ポーズで止まる")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Billboard")]
    [Tooltip("未指定なら Camera.main を使用")]
    [SerializeField] private Camera targetCamera;

    private float t;
    private Color baseColor;
    private Vector3 drift;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (text != null) baseColor = text.color;

        drift = new Vector3(
            Random.Range(-randomHorizontal, randomHorizontal),
            0f,
            Random.Range(-randomHorizontal, randomHorizontal)
        );
    }

    public void Setup(int damage, Camera cam = null, Color? colorOverride = null)
    {
        if (text != null)
        {
            text.text = damage.ToString();
            if (colorOverride.HasValue) text.color = colorOverride.Value;
            baseColor = text.color;
        }

        if (cam != null) targetCamera = cam;
    }

    private void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        t += dt;
        if (t >= lifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        // 位置：上昇＋軽い横ドリフト
        transform.position += (Vector3.up * riseSpeed + drift) * dt;

        // Billboard（常にカメラ正面）
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null)
        {
            Vector3 toCam = transform.position - cam.transform.position;
            if (toCam.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        // Fade
        if (fadeOut && text != null)
        {
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, lifeSeconds));
            float a = alphaCurve != null ? alphaCurve.Evaluate(u) : (1f - u);
            Color c = baseColor;
            c.a = a;
            text.color = c;
        }
    }
}
