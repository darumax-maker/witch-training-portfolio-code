using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RainArcaneDamageArea : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("この秒数だけダメージ判定を行う（0以下で無制限）")]
    [SerializeField] private float activeSeconds = 2.0f;

    [Tooltip("エフェクト自体を消すまでの秒数（0以下で破壊しない）")]
    [SerializeField] private float destroyAfterSeconds = 2.5f;

    [Header("Damage (DoT)")]
    [SerializeField] private int damage = 1;

    [Tooltip("ダメージ間隔（秒）。例：1で1秒に1回")]
    [SerializeField] private float tickIntervalSeconds = 1.0f;

    [Tooltip("生成直後にも1回ダメージを入れる")]
    [SerializeField] private bool tickOnSpawn = true;

    [Tooltip("当てたいレイヤー（Enemy / EnemyHitbox / BossHitbox 等）")]
    [SerializeField] private LayerMask damageMask = 0;

    [Tooltip("Triggerに当てるなら true（Boss Hitbox が Trigger の場合はON推奨）")]
    [SerializeField] private bool includeTriggers = true;

    [Header("Volume (Local Capsule = Cylinder-like) - 手動調整")]
    [Tooltip("ローカル中心")]
    [SerializeField] private Vector3 localCapsuleCenter = new Vector3(0f, 0.8f, 0f);

    [Tooltip("半径")]
    [SerializeField] private float capsuleRadius = 1.5f;

    [Tooltip("全高（m）。※カプセルなので上下が丸くなります。円柱っぽくしたいなら radius より大きめに。")]
    [SerializeField] private float capsuleHeight = 3.0f;

    [Header("Damage Popup")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private float popupUpOffset = 0.15f;
    [SerializeField] private Color enemyPopupColor = Color.white;
    [SerializeField] private Color bossPopupColor = Color.yellow;

    [Header("Hit SFX (per tick per enemy)")]
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
    private float tickTimer;

    private Camera cam;

    // NonAllocバッファ
    private readonly Collider[] overlapBuf = new Collider[128];

    // 1ティック内で同じ個体に複数Colliderがあっても1回だけにする
    private readonly HashSet<int> tickHitEnemyIds = new HashSet<int>();
    private readonly HashSet<int> tickHitBossIds = new HashSet<int>();

    private QueryTriggerInteraction QTI =>
        includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

    /// <summary>
    /// SpellShooterからダメージ値を渡す
    /// </summary>
    public void Init(int newDamage, Camera camera)
    {
        damage = Mathf.Max(1, newDamage);
        cam = camera;
    }

    private void Awake()
    {
        ClampParams();

        remaining = activeSeconds;
        tickTimer = tickOnSpawn ? 0f : tickIntervalSeconds;

        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 寿命管理（0以下なら無制限）
        if (activeSeconds > 0f)
        {
            remaining -= dt;
            if (remaining <= 0f)
            {
                enabled = false; // 判定だけ止める（見た目はdestroyAfterSecondsまで残る）
                return;
            }
        }

        tickTimer -= dt;
        if (tickTimer > 0f) return;

        // 次回ティック予約（積み残し対策）
        tickTimer += tickIntervalSeconds;

        DoDamageTick();
    }

    private void DoDamageTick()
    {
        if (damageMask.value == 0) return;

        tickHitEnemyIds.Clear();
        tickHitBossIds.Clear();

        if (!TryGetOverlapCapsule(out var p0, out var p1, out var r, out var samplePoint))
            return;

        int count = Physics.OverlapCapsuleNonAlloc(p0, p1, r, overlapBuf, damageMask, QTI);

        for (int i = 0; i < count; i++)
        {
            var col = overlapBuf[i];
            overlapBuf[i] = null;
            if (col == null) continue;

            ProcessCollider(col, samplePoint);
        }
    }

    /// <summary>
    /// カプセル（円柱風）をワールド座標で生成
    /// </summary>
    private bool TryGetOverlapCapsule(out Vector3 p0, out Vector3 p1, out float radius, out Vector3 samplePoint)
    {
        Vector3 center = transform.TransformPoint(localCapsuleCenter);
        Vector3 up = transform.up;

        radius = Mathf.Max(0f, capsuleRadius);
        float height = Mathf.Max(0f, capsuleHeight);

        // サンプル点（ポップアップ位置計算の基準）
        samplePoint = center;

        if (radius <= 0f || height <= 0f) { p0 = p1 = center; return false; }

        // OverlapCapsule は「線分 + 半径」。全高 = 線分長 + 2*半径
        float half = height * 0.5f;
        float segHalf = Mathf.Max(0f, half - radius);

        p0 = center + up * segHalf;
        p1 = center - up * segHalf;

        return true;
    }

    private void ProcessCollider(Collider col, Vector3 samplePoint)
    {
        // ===== Boss優先（倍率込みの最終ダメージを表示したい） =====
        var bossHitbox = col.GetComponentInParent<EnemyBossHitbox>();
        if (bossHitbox != null)
        {
            var bossHealth = bossHitbox.BossHealth;
            if (bossHealth != null)
            {
                int bossId = bossHealth.GetInstanceID();
                if (tickHitBossIds.Add(bossId))
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

        // ===== 通常Enemy / Rare =====
        var eh = col.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            int id = eh.GetInstanceID();
            if (tickHitEnemyIds.Add(id))
            {
                eh.ApplyDamage(damage);

                Vector3 p = GetPopupPoint(col, samplePoint);
                SpawnDamagePopup(damage, p, enemyPopupColor);
                PlayHitSfx(p);
            }
            return;
        }

        var erh = col.GetComponentInParent<EnemyRareHealth>();
        if (erh != null)
        {
            int id = erh.GetInstanceID();
            if (tickHitEnemyIds.Add(id))
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

        GameObject go = new GameObject("RainArcaneHitSFX");
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

    private void ClampParams()
    {
        tickIntervalSeconds = Mathf.Max(0.01f, tickIntervalSeconds);
        damage = Mathf.Max(1, damage);
        capsuleRadius = Mathf.Max(0f, capsuleRadius);
        capsuleHeight = Mathf.Max(0f, capsuleHeight);
        popupUpOffset = Mathf.Max(0f, popupUpOffset);
        hitSfxMinDistance = Mathf.Max(0.01f, hitSfxMinDistance);
        hitSfxMaxDistance = Mathf.Max(hitSfxMinDistance, hitSfxMaxDistance);
        hitSfxPitch = Mathf.Max(0.01f, hitSfxPitch);
        hitSfxDoppler = Mathf.Max(0f, hitSfxDoppler);
    }

#if UNITY_EDITOR
    private void OnValidate() => ClampParams();

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        if (!TryGetOverlapCapsule(out var p0, out var p1, out var r, out _)) return;

        // 簡易ワイヤーカプセル：両端の球＋側面ライン4本
        Gizmos.DrawWireSphere(p0, r);
        Gizmos.DrawWireSphere(p1, r);

        Vector3 axis = (p0 - p1);
        float len = axis.magnitude;
        if (len < 0.0001f) return;

        Vector3 up = axis / len;

        // up と平行でない基準軸を選んで直交基底を作る
        Vector3 refAxis = (Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.99f) ? Vector3.up : Vector3.forward;
        Vector3 right = Vector3.Cross(up, refAxis).normalized;
        Vector3 forward = Vector3.Cross(right, up).normalized;

        Gizmos.DrawLine(p0 + right * r, p1 + right * r);
        Gizmos.DrawLine(p0 - right * r, p1 - right * r);
        Gizmos.DrawLine(p0 + forward * r, p1 + forward * r);
        Gizmos.DrawLine(p0 - forward * r, p1 - forward * r);
    }
#endif
}
