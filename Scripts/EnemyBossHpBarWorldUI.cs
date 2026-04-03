using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyBossHpBarWorldUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyBossHealth bossHealth; // 未設定なら自動取得
    [SerializeField] private Image fillImage;            // HPBar_Fill の Image
    [SerializeField] private Transform followTarget;     // 未設定なら bossHealth.transform

    [Header("Follow")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f); // 頭上オフセット
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private Camera targetCamera;        // 未設定なら Camera.main

    [Header("Smoothing")]
    [Tooltip("0なら即時反映。大きいほど滑らか。")]
    [SerializeField] private float smoothSpeed = 12f;

    private float targetFill01 = 1f;
    private float currentFill01 = 1f;

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponentInParent<EnemyBossHealth>();
        if (followTarget == null && bossHealth != null) followTarget = bossHealth.transform;
        if (targetCamera == null) targetCamera = Camera.main;

        // 初期表示（OnHpChanged が Awake で発火済みでも、ここで確実に同期）
        SyncImmediate();
    }

    private void OnEnable()
    {
        if (bossHealth != null)
            bossHealth.OnHpChanged += HandleHpChanged;

        // 有効化タイミングでも同期
        SyncImmediate();
    }

    private void OnDisable()
    {
        if (bossHealth != null)
            bossHealth.OnHpChanged -= HandleHpChanged;
    }

    private void LateUpdate()
    {
        // 追従
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        // カメラ正対（ビルボード）
        if (billboardToCamera && targetCamera != null)
        {
            // UIの表面がカメラを向くように「-camera.forward」に合わせる
            transform.forward = -targetCamera.transform.forward;
        }

        // Fill のスムーズ更新
        if (fillImage != null)
        {
            if (smoothSpeed <= 0f)
            {
                currentFill01 = targetFill01;
            }
            else
            {
                currentFill01 = Mathf.Lerp(currentFill01, targetFill01, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
            }

            fillImage.fillAmount = currentFill01;
        }
    }

    private void HandleHpChanged(int current, int max)
    {
        if (max <= 0) max = 1;
        targetFill01 = Mathf.Clamp01(current / (float)max);
    }

    private void SyncImmediate()
    {
        if (bossHealth == null) return;
        if (fillImage == null) return;

        int max = bossHealth.MaxHp;
        if (max <= 0) max = 1;

        targetFill01 = Mathf.Clamp01(bossHealth.CurrentHp / (float)max);
        currentFill01 = targetFill01;
        fillImage.fillAmount = currentFill01;
    }
}
