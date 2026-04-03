using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBossSpawner : MonoBehaviour
{
    public enum SpawnMode
    {
        OnlyIfNoneAlive,
        Always
    }

    [System.Serializable]
    public sealed class BossSpawnProfile
    {
        [Header("Boss Prefab")]
        public GameObject bossPrefab;

        [Header("HP Scaling")]
        public bool enableHpScaling = true;

        [Tooltip("この分数に到達した時点から倍率適用（ボスは 2 推奨：2分で3倍 など）")]
        public int hpScaleStartMinutes = 2;

        [Tooltip("何分ごとにさらに倍率を掛けるか（通常 1）")]
        public int hpScaleEveryMinutes = 1;

        [Tooltip("1ステップの倍率（通常 3）")]
        public float hpScaleMultiplierPerStep = 3f;

        [Tooltip("暴走防止：最大ステップ数（0なら無制限）")]
        public int maxHpScaleSteps = 20;

        [Tooltip("暴走防止：MaxHP上限（0以下で無制限）")]
        public int maxHpCap = 1000000;

        [Tooltip("生成時にHPを満タンにする")]
        public bool refillHpOnSpawn = true;

        public void Clamp()
        {
            hpScaleStartMinutes = Mathf.Max(0, hpScaleStartMinutes);
            hpScaleEveryMinutes = Mathf.Max(1, hpScaleEveryMinutes);
            hpScaleMultiplierPerStep = Mathf.Max(1f, hpScaleMultiplierPerStep);
            maxHpScaleSteps = Mathf.Max(0, maxHpScaleSteps);
            // maxHpCap は 0以下で無制限扱いなので clamp しない
        }

        public float GetHpMultiplierAtTime(float timeSeconds)
        {
            if (!enableHpScaling) return 1f;

            float startAt = Mathf.Max(0, hpScaleStartMinutes) * 60f;
            float every = Mathf.Max(1, hpScaleEveryMinutes) * 60f;

            if (timeSeconds < startAt) return 1f;

            int step = Mathf.FloorToInt((timeSeconds - startAt) / every) + 1;
            if (maxHpScaleSteps > 0) step = Mathf.Min(step, maxHpScaleSteps);

            float m = Mathf.Max(1f, hpScaleMultiplierPerStep);
            return Mathf.Pow(m, step);
        }
    }

    [Header("Boss Profiles")]
    [Tooltip("20分未満で使うボス設定")]
    [SerializeField] private BossSpawnProfile boss1 = new BossSpawnProfile();

    [Tooltip("指定分数以降で使うボス設定（EnemyBoss2など）")]
    [SerializeField] private BossSpawnProfile boss2 = new BossSpawnProfile();

    [Header("Switch To Boss2")]
    [Tooltip("この分数に到達したら boss2 をスポーンする（デフォルト20分）")]
    [SerializeField] private float switchToBoss2AfterMinutes = 20f;

    [Header("Spawn Point (Fixed)")]
    [SerializeField] private Transform spawnPoint;

    [Header("Timing (Sync with ElapsedTimeUI)")]
    [SerializeField] private float spawnIntervalSeconds = 60f;
    [SerializeField] private bool spawnOnStart = false;

    [Header("Time Source")]
    [SerializeField] private ElapsedTimeUI elapsedTimeUI;

    [Header("Policy")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.OnlyIfNoneAlive;

    [Header("Optional: Ground Snap")]
    [SerializeField] private bool snapToGround = false;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayStartHeight = 50f;
    [SerializeField] private float rayDistance = 200f;
    [SerializeField] private float spawnYOffset = 0.02f;

    [Header("Optional: Block Check")]
    [SerializeField] private bool checkBlocked = false;
    [SerializeField] private float clearanceRadius = 1.0f;
    [SerializeField] private LayerMask blockingMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private GameObject currentBoss;
    private int lastProcessedIndex;

    private void Awake()
    {
        if (elapsedTimeUI == null)
            elapsedTimeUI = FindFirstObjectByType<ElapsedTimeUI>();

        ClampParams();
    }

    private void OnEnable()
    {
        float t = GetGameElapsedSeconds();
        lastProcessedIndex = Mathf.FloorToInt(t / spawnIntervalSeconds);

        if (spawnOnStart)
            TrySpawn(t);
    }

    private void LateUpdate()
    {
        // 現在時刻で使うプロファイルが空でも、次の境界で切り替わる可能性があるので
        // ここでは return しない（TrySpawn 内で判断）
        float t = GetGameElapsedSeconds();
        int currentIndex = Mathf.FloorToInt(t / spawnIntervalSeconds);

        if (currentIndex <= lastProcessedIndex) return;

        for (int idx = lastProcessedIndex + 1; idx <= currentIndex; idx++)
        {
            float boundaryTime = idx * spawnIntervalSeconds;

            bool spawned = TrySpawn(boundaryTime);

            // OnlyIfNoneAlive で「湧けない(ボス存命)」なら、このフレームの残り境界も無駄なので打ち切り
            if (!spawned && spawnMode == SpawnMode.OnlyIfNoneAlive && IsBossAlive())
                break;
        }

        lastProcessedIndex = currentIndex;
    }

    private float GetGameElapsedSeconds()
    {
        if (elapsedTimeUI != null)
            return Mathf.Max(0f, elapsedTimeUI.GetElapsedSeconds());

        return Mathf.Max(0f, Time.timeSinceLevelLoad);
    }

    private bool IsBossAlive()
    {
        return currentBoss != null && currentBoss.activeInHierarchy;
    }

    private BossSpawnProfile GetProfileForTime(float timeSeconds)
    {
        float switchSeconds = Mathf.Max(0f, switchToBoss2AfterMinutes) * 60f;

        // boss2 が未設定なら常に boss1
        if (boss2 == null || boss2.bossPrefab == null) return boss1;

        return (timeSeconds >= switchSeconds) ? boss2 : boss1;
    }

    private bool TrySpawn(float timeStampSeconds)
    {
        if (spawnMode == SpawnMode.OnlyIfNoneAlive && IsBossAlive())
        {
            if (debugLog) Debug.Log("[EnemyBossSpawner] Skip: boss still alive");
            return false;
        }

        BossSpawnProfile profile = GetProfileForTime(timeStampSeconds);
        if (profile == null || profile.bossPrefab == null)
        {
            if (debugLog) Debug.Log("[EnemyBossSpawner] Skip: boss prefab missing for current profile");
            return false;
        }

        Transform sp = (spawnPoint != null) ? spawnPoint : transform;
        Vector3 pos = sp.position;
        Quaternion rot = sp.rotation;

        if (snapToGround)
        {
            Vector3 rayStart = pos + Vector3.up * rayStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + Vector3.up * Mathf.Max(0f, spawnYOffset);
            }
            else
            {
                if (debugLog) Debug.Log("[EnemyBossSpawner] Skip: ground raycast failed");
                return false;
            }
        }

        if (checkBlocked && clearanceRadius > 0f)
        {
            Vector3 checkCenter = pos + Vector3.up * 0.5f;
            if (Physics.CheckSphere(checkCenter, clearanceRadius, blockingMask, QueryTriggerInteraction.Ignore))
            {
                if (debugLog) Debug.Log("[EnemyBossSpawner] Skip: spawn point blocked");
                return false;
            }
        }

        currentBoss = Instantiate(profile.bossPrefab, pos, rot);

        // HP倍率適用（EnemyBossHealth が付いている個体のみ）
        float hpMult = profile.GetHpMultiplierAtTime(timeStampSeconds);
        ApplyHpScalingIfPossible(currentBoss, profile, hpMult);

        if (debugLog)
        {
            string which = (profile == boss2) ? "Boss2" : "Boss1";
            Debug.Log($"[EnemyBossSpawner] Spawned({which}): {currentBoss.name} hpMult={hpMult:F2} time={timeStampSeconds:F2}s");
        }

        return true;
    }

    private void ApplyHpScalingIfPossible(GameObject go, BossSpawnProfile profile, float hpMult)
    {
        if (go == null) return;
        if (profile == null) return;
        if (!profile.enableHpScaling) return;

        var bh = go.GetComponent<EnemyBossHealth>();
        if (bh == null) bh = go.GetComponentInChildren<EnemyBossHealth>();

        if (bh != null)
        {
            // 既存APIを利用（あなたの EnemyBossHealth 実装に合わせる）
            // maxHpCap <= 0 なら無制限扱い、という運用は EnemyBossHealth 側の実装に依存
            bh.ApplySpawnHpScale(hpMult, profile.refillHpOnSpawn, profile.maxHpCap);
        }
    }

    private void ClampParams()
    {
        spawnIntervalSeconds = Mathf.Max(0.01f, spawnIntervalSeconds);
        switchToBoss2AfterMinutes = Mathf.Max(0f, switchToBoss2AfterMinutes);

        if (boss1 != null) boss1.Clamp();
        if (boss2 != null) boss2.Clamp();

        clearanceRadius = Mathf.Max(0f, clearanceRadius);
        rayStartHeight = Mathf.Max(0f, rayStartHeight);
        rayDistance = Mathf.Max(0.01f, rayDistance);
        spawnYOffset = Mathf.Max(0f, spawnYOffset);
    }

#if UNITY_EDITOR
    private void OnValidate() => ClampParams();
#endif
}
