using UnityEngine;

public sealed class WorldBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    [Header("Options")]
    [SerializeField] private bool yawOnly = true;   // true: Y軸回転だけ（常に直立）
    [SerializeField] private bool flipForward = false; // 文字が裏向きならON

    private void OnEnable()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            ResolveCamera();
        if (targetCamera == null) return;

        var camT = targetCamera.transform;

        if (yawOnly)
        {
            // カメラ→UI方向の水平成分だけで向ける
            Vector3 toCam = camT.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-6f) return;

            var fwd = (flipForward ? -toCam : toCam).normalized;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
        else
        {
            Vector3 toCam = camT.position - transform.position;
            if (toCam.sqrMagnitude < 1e-6f) return;

            var fwd = (flipForward ? -toCam : toCam).normalized;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    private void ResolveCamera()
    {
        targetCamera = Camera.main;
        if (targetCamera != null) return;

        // MainCamera が無い場合の保険（Unity 6）
        targetCamera = FindFirstObjectByType<Camera>();
    }

    // 生成側から注入したい場合の口も用意
    public void SetCamera(Camera cam) => targetCamera = cam;
}
