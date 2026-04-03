using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BeamDamageArea : MonoBehaviour
{
    public enum VolumeMode
    {
        LocalBox,            // 手動で箱を調整（回転込み）
        RenderBoundsAABB,     // 見た目（Renderer.bounds）のAABBで判定（回転なし）
        RenderBoundsOBB       // ★追加：Renderer群をこのtransformローカルに投影→回転付きOBBで判定（方向一致）
    }

    [Header("Lifetime")]
    [SerializeField] private float activeSeconds = 0.6f;
    [SerializeField] private float destroyAfterSeconds = 0.8f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask damageMask = 0;
    [SerializeField] private bool includeTriggers = true;

    [Header("Volume")]
    [SerializeField] private VolumeMode volumeMode = VolumeMode.RenderBoundsAABB;

    [SerializeField] private Vector3 localBoxCenter = new Vector3(0f, 0.8f, 2.5f);
    [SerializeField] private Vector3 localBoxHalfExtents = new Vector3(1.2f, 1.0f, 2.5f);

    [SerializeField] private Vector3 renderBoundsPadding = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("Damage Popup")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private float popupUpOffset = 0.15f;
    [SerializeField] private Color enemyPopupColor = Color.white;
    [SerializeField] private Color bossPopupColor = Color.yellow;

    [Header("Hit SFX (per hit)")]
    [SerializeField] private AudioClip hitSfxClip;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 1f;
    [SerializeField] private float hitSfxMinDistance = 3f;
    [SerializeField] private float hitSfxMaxDistance = 30f;
    [SerializeField] private AudioRolloffMode hitSfxRolloff = AudioRolloffMode.Logarithmic;
    [SerializeField] private float hitSfxPitch = 1f;
    [SerializeField] private float hitSfxDoppler = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugGizmos = true;

    private float remaining;
    private Camera cam;
    private Renderer[] cachedRenderers;

    private readonly Collider[] overlapBuf = new Collider[64];

    private readonly HashSet<int> hitEnemyIds = new HashSet<int>();
    private readonly HashSet<int> hitBossIds = new HashSet<int>();

    private QueryTriggerInteraction QTI => includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

    public void Init(int newDamage, Camera camera)
    {
        damage = Mathf.Max(1, newDamage);
        cam = camera;
    }

    private void Awake()
    {
        remaining = Mathf.Max(0.01f, activeSeconds);

        if (volumeMode == VolumeMode.RenderBoundsAABB || volumeMode == VolumeMode.RenderBoundsOBB)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        remaining -= dt;
        if (remaining < 0f)
        {
            enabled = false;
            return;
        }

        DoDamageCheck();
    }

    private void DoDamageCheck()
    {
        if (damageMask.value == 0) return;

        if (!TryGetOverlapBox(out var center, out var halfExtents, out var rot))
            return;

        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuf, rot, damageMask, QTI);

        for (int i = 0; i < count; i++)
        {
            var col = overlapBuf[i];
            overlapBuf[i] = null;
            if (col == null) continue;

            ProcessCollider(col, center);
        }
    }

    private bool TryGetOverlapBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rot)
    {
        center = default;
        halfExtents = default;
        rot = Quaternion.identity;

        if (volumeMode == VolumeMode.LocalBox)
        {
            center = transform.TransformPoint(localBoxCenter);
            halfExtents = new Vector3(
                Mathf.Max(0f, localBoxHalfExtents.x),
                Mathf.Max(0f, localBoxHalfExtents.y),
                Mathf.Max(0f, localBoxHalfExtents.z)
            );
            rot = transform.rotation;
            return halfExtents.sqrMagnitude > 0f;
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0) return false;

        if (volumeMode == VolumeMode.RenderBoundsAABB)
        {
            // ワールドAABB統合（回転なし）
            bool inited = false;
            Bounds b = default;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r == null) continue;

                if (!inited) { b = r.bounds; inited = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!inited) return false;

            center = b.center;
            Vector3 ext = b.extents + renderBoundsPadding;
            halfExtents = new Vector3(Mathf.Max(0f, ext.x), Mathf.Max(0f, ext.y), Mathf.Max(0f, ext.z));
            rot = Quaternion.identity;
            return halfExtents.sqrMagnitude > 0f;
        }

        // ★RenderBoundsOBB：Renderer.bounds(ワールドAABB)の8頂点を「このtransformローカル」へ投影して統合 → 回転付きOverlapBox
        {
            bool inited = false;
            Bounds localB = default;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r == null) continue;

                Bounds wb = r.bounds; // ワールドAABB
                Vector3 c = wb.center;
                Vector3 e = wb.extents;

                // ワールドAABBの8頂点
                for (int xi = -1; xi <= 1; xi += 2)
                    for (int yi = -1; yi <= 1; yi += 2)
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 cornerW = c + Vector3.Scale(e, new Vector3(xi, yi, zi));
                            Vector3 cornerL = transform.InverseTransformPoint(cornerW);

                            if (!inited)
                            {
                                localB = new Bounds(cornerL, Vector3.zero);
                                inited = true;
                            }
                            else
                            {
                                localB.Encapsulate(cornerL);
                            }
                        }
            }

            if (!inited) return false;

            Vector3 localCenter = localB.center;
            Vector3 localExt = localB.extents + renderBoundsPadding;

            center = transform.TransformPoint(localCenter);
            halfExtents = new Vector3(Mathf.Max(0f, localExt.x), Mathf.Max(0f, localExt.y), Mathf.Max(0f, localExt.z));
            rot = transform.rotation;

            return halfExtents.sqrMagnitude > 0f;
        }
    }

    private void ProcessCollider(Collider col, Vector3 samplePoint)
    {
        // Boss優先
        var bossHitbox = col.GetComponentInParent<EnemyBossHitbox>();
        if (bossHitbox != null)
        {
            var bossHealth = bossHitbox.BossHealth;
            if (bossHealth != null)
            {
                int bossId = bossHealth.GetInstanceID();
                if (hitBossIds.Add(bossId))
                {
                    int finalDamage = bossHitbox.ApplyHitAndGetFinalDamage(damage, null);
                    if (finalDamage > 0)
                    {
                        Vector3 p = GetPopupPoint(col, samplePoint);
                        SpawnDamagePopup(finalDamage, p, bossPopupColor);
                        PlayHitSfx(p);
                    }
                }
            }
            return;
        }

        // EnemyHealth
        var eh = col.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            int id = eh.GetInstanceID();
            if (hitEnemyIds.Add(id))
            {
                eh.ApplyDamage(damage);
                Vector3 p = GetPopupPoint(col, samplePoint);
                SpawnDamagePopup(damage, p, enemyPopupColor);
                PlayHitSfx(p);
            }
            return;
        }

        // ★EnemyRareHealth
        var erh = col.GetComponentInParent<EnemyRareHealth>();
        if (erh != null)
        {
            int id = erh.GetInstanceID();
            if (hitEnemyIds.Add(id))
            {
                erh.ApplyDamage(damage);
                Vector3 p = GetPopupPoint(col, samplePoint);
                SpawnDamagePopup(damage, p, enemyPopupColor);
                PlayHitSfx(p);
            }
            return;
        }
    }

    private Vector3 GetPopupPoint(Collider col, Vector3 samplePoint)
    {
        Vector3 p = col.ClosestPoint(samplePoint);
        if ((p - samplePoint).sqrMagnitude < 0.0001f)
            p = col.bounds.center;

        return p + Vector3.up * Mathf.Max(0f, popupUpOffset);
    }

    private void SpawnDamagePopup(int dmg, Vector3 pos, Color color)
    {
        if (damagePopupPrefab == null) return;

        var popup = Instantiate(damagePopupPrefab, pos, Quaternion.identity);
        Camera useCam = cam != null ? cam : Camera.main;
        popup.Setup(dmg, useCam, color);
    }

    private void PlayHitSfx(Vector3 pos)
    {
        if (hitSfxClip == null) return;

        GameObject go = new GameObject("BeamHitSFX");
        go.transform.position = pos;

        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.rolloffMode = hitSfxRolloff;
        a.minDistance = Mathf.Max(0.01f, hitSfxMinDistance);
        a.maxDistance = Mathf.Max(a.minDistance, hitSfxMaxDistance);
        a.dopplerLevel = Mathf.Max(0f, hitSfxDoppler);

        a.clip = hitSfxClip;
        a.volume = Mathf.Clamp01(hitSfxVolume);
        a.pitch = Mathf.Max(0.01f, hitSfxPitch);

        a.Play();
        Destroy(go, hitSfxClip.length / a.pitch + 0.1f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        if (!TryGetOverlapBox(out var center, out var half, out var rot)) return;

        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, half * 2f);
    }
#endif
}
