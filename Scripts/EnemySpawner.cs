using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random; // ★これで Random のあいまいさを解消（UnityEngine.Random 固定）

[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    public enum AreaShape { Circle, Box }

    [Header("Prefabs (Normal Random)")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Prefabs (Rare)")]
    [Tooltip("Rare枠のPrefab（EnemyRare）。未指定ならRare抽選は行われません。")]
    [SerializeField] private GameObject rarePrefab;

    [Tooltip("1体スポーンするたびに、この確率でrarePrefabを優先します（例：0.05=5%）")]
    [Range(0f, 1f)]
    [SerializeField] private float rareSpawnChance = 0.05f;

    [Header("Time Source (Sync with UI)")]
    [Tooltip("ElapsedTimeUI（useUnscaledTime=false運用）と同じ時間でスポーン判定します")]
    [SerializeField] private ElapsedTimeUI elapsedTimeUI;

    [Header("Spawn Timing (UI-Synced)")]
    [Tooltip("例：5なら、UIの経過秒が 5,10,15... を跨いだときに湧く")]
    [SerializeField] private float spawnInterval = 5f;

    [Tooltip("1回のタイミングで湧かせる数（例：2で5秒おきに2体）")]
    [SerializeField] private int spawnCountPerInterval = 2;

    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("0以下で無制限。1回で2体湧かせる場合でも上限チェックします")]
    [SerializeField] private int maxAlive = 20;

    [Header("HP Scaling")]
    [Tooltip("ONにすると、経過時間に応じて生成個体のMaxHPを倍率で増やします")]
    [SerializeField] private bool enableHpScaling = true;

    [Tooltip("この分数に到達した時点から倍率適用（1 推奨：1分以降に3倍）")]
    [SerializeField] private int hpScaleStartMinutes = 1;

    [Tooltip("何分ごとにさらに倍率を掛けるか（通常 1）")]
    [SerializeField] private int hpScaleEveryMinutes = 1;

    [Tooltip("1ステップの倍率（通常 3）")]
    [SerializeField] private float hpScaleMultiplierPerStep = 3f;

    [Tooltip("暴走防止：最大ステップ数（0なら無制限）")]
    [SerializeField] private int maxHpScaleSteps = 20;

    [Tooltip("暴走防止：MaxHP上限（0以下で無制限）")]
    [SerializeField] private int maxHpCap = 1000000;

    [Tooltip("生成時にHPを満タンにする")]
    [SerializeField] private bool refillHpOnSpawn = true;

    [Header("Ground Placement")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayStartHeight = 50f;
    [SerializeField] private float rayDistance = 200f;
    [SerializeField] private float spawnYOffset = 0.02f;

    [Header("Spawn Area")]
    [SerializeField] private AreaShape shape = AreaShape.Circle;
    [SerializeField] private float radius = 10f;
    [SerializeField] private Vector3 boxSize = new Vector3(20f, 0f, 20f);

    [Header("Collision Avoid")]
    [SerializeField] private float clearanceRadius = 0.5f;
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] private int maxTriesPerSpawn = 15;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private readonly List<GameObject> alive = new List<GameObject>();
    private int lastProcessedIndex;

    private static readonly object[] InvokeArgs = new object[3]; // (float mult, bool refill, int cap)

    private void Awake()
    {
        if (elapsedTimeUI == null)
            elapsedTimeUI = FindFirstObjectByType<ElapsedTimeUI>();

        ClampParams();
    }

    private void OnEnable()
    {
        float t = GetGameElapsedSeconds();
        lastProcessedIndex = Mathf.FloorToInt(t / spawnInterval);

        if (spawnOnStart)
        {
            CleanupAliveList();
            SpawnBatch(t);
        }
    }

    private void LateUpdate()
    {
        CleanupAliveList();
        if (!HasAnyPrefab()) return;

        float t = GetGameElapsedSeconds();
        int currentIndex = Mathf.FloorToInt(t / spawnInterval);

        if (currentIndex <= lastProcessedIndex) return;

        for (int idx = lastProcessedIndex + 1; idx <= currentIndex; idx++)
        {
            float boundaryTime = idx * spawnInterval;
            bool spawnedAny = SpawnBatch(boundaryTime);
            if (!spawnedAny) break;
        }

        lastProcessedIndex = currentIndex;
    }

    private float GetGameElapsedSeconds()
    {
        if (elapsedTimeUI != null)
            return Mathf.Max(0f, elapsedTimeUI.GetElapsedSeconds());

        return Mathf.Max(0f, Time.timeSinceLevelLoad);
    }

    private void CleanupAliveList()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            var go = alive[i];
            if (go == null || !go.activeInHierarchy)
                alive.RemoveAt(i);
        }
    }

    private bool HasAnyPrefab()
    {
        if (rarePrefab != null) return true;

        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return false;
        for (int i = 0; i < enemyPrefabs.Length; i++)
            if (enemyPrefabs[i] != null) return true;

        return false;
    }

    private GameObject PickNormalRandomPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;

        for (int i = 0; i < 20; i++)
        {
            int idx = Random.Range(0, enemyPrefabs.Length);
            var p = enemyPrefabs[idx];
            if (p != null) return p;
        }

        for (int i = 0; i < enemyPrefabs.Length; i++)
            if (enemyPrefabs[i] != null) return enemyPrefabs[i];

        return null;
    }

    private GameObject PickSpawnPrefab()
    {
        if (rarePrefab != null && rareSpawnChance > 0f)
        {
            if (Random.value < rareSpawnChance)
                return rarePrefab;
        }

        return PickNormalRandomPrefab();
    }

    private bool SpawnBatch(float timeStampSeconds)
    {
        if (spawnCountPerInterval <= 0) return false;

        float hpMult = GetHpMultiplierAtTime(timeStampSeconds);

        int spawned = 0;
        for (int n = 0; n < spawnCountPerInterval; n++)
        {
            if (maxAlive > 0 && alive.Count >= maxAlive)
            {
                if (debugLog) Debug.Log($"[EnemySpawner] Skip: maxAlive reached ({alive.Count}/{maxAlive}) time={timeStampSeconds:F2}");
                break;
            }

            if (TrySpawnOneInternal(hpMult))
                spawned++;
            else
                break;
        }

        return spawned > 0;
    }

    private bool TrySpawnOneInternal(float hpMult)
    {
        var prefab = PickSpawnPrefab();
        if (prefab == null)
        {
            if (debugLog) Debug.Log("[EnemySpawner] Skip: prefab is null (normal+rare)");
            return false;
        }

        for (int i = 0; i < maxTriesPerSpawn; i++)
        {
            if (TryGetSpawnPoint(out var pos, out var rot))
            {
                var go = Instantiate(prefab, pos, rot);
                alive.Add(go);

                ApplyHpScalingIfPossible(go, hpMult);

                var tracker = go.GetComponent<EnemySpawnTracker>();
                if (tracker == null) tracker = go.AddComponent<EnemySpawnTracker>();
                tracker.Init(this);

                if (debugLog)
                {
                    bool isRare = (rarePrefab != null && go != null && go.name.StartsWith(rarePrefab.name, StringComparison.Ordinal));
                    Debug.Log($"[EnemySpawner] Spawned: {go.name} Alive={alive.Count} hpMult={hpMult:F2} rare={(isRare ? "YES" : "no")}");
                }

                return true;
            }
        }

        if (debugLog) Debug.Log("[EnemySpawner] Failed: could not find spawn point");
        return false;
    }

    private void ApplyHpScalingIfPossible(GameObject go, float hpMult)
    {
        if (!enableHpScaling) return;
        if (go == null) return;

        // 1) EnemyHealth（既存）
        var eh = go.GetComponent<EnemyHealth>();
        if (eh == null) eh = go.GetComponentInChildren<EnemyHealth>(true);
        if (eh != null)
        {
            eh.ApplySpawnHpScale(hpMult, refillHpOnSpawn, maxHpCap);
            return;
        }

        // 2) EnemyRareHealth（実装済み前提）
        var erh = go.GetComponent<EnemyRareHealth>();
        if (erh == null) erh = go.GetComponentInChildren<EnemyRareHealth>(true);
        if (erh != null)
        {
            erh.ApplySpawnHpScale(hpMult, refillHpOnSpawn, maxHpCap);
            return;
        }

        // 3) 保険：Health系で ApplySpawnHpScale(float,bool,int) を持つものへ反射（任意）
        TryInvokeApplySpawnHpScale(go, hpMult, refillHpOnSpawn, maxHpCap);
    }

    private void TryInvokeApplySpawnHpScale(GameObject go, float mult, bool refill, int cap)
    {
        var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
        if (monos == null || monos.Length == 0) return;

        InvokeArgs[0] = mult;
        InvokeArgs[1] = refill;
        InvokeArgs[2] = cap;

        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;

            string typeName = mb.GetType().Name;
            if (!typeName.Contains("Health")) continue;

            MethodInfo m = mb.GetType().GetMethod(
                "ApplySpawnHpScale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(float), typeof(bool), typeof(int) },
                null
            );

            if (m == null) continue;

            try
            {
                m.Invoke(mb, InvokeArgs);
                return;
            }
            catch (Exception e)
            {
                if (debugLog) Debug.LogWarning($"[EnemySpawner] ApplySpawnHpScale invoke failed on {typeName}: {e.Message}");
            }
        }
    }

    private float GetHpMultiplierAtTime(float timeSeconds)
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

    private bool TryGetSpawnPoint(out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = Quaternion.identity;

        Vector3 offset;
        if (shape == AreaShape.Circle)
        {
            Vector2 r = Random.insideUnitCircle * Mathf.Max(0f, radius);
            offset = new Vector3(r.x, 0f, r.y);
        }
        else
        {
            float halfX = Mathf.Max(0f, boxSize.x) * 0.5f;
            float halfZ = Mathf.Max(0f, boxSize.z) * 0.5f;
            offset = new Vector3(Random.Range(-halfX, halfX), 0f, Random.Range(-halfZ, halfZ));
        }

        Vector3 xz = transform.position + offset;

        Vector3 rayStart = xz + Vector3.up * rayStartHeight;
        if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        pos = hit.point + Vector3.up * spawnYOffset;

        if (clearanceRadius > 0f)
        {
            Vector3 checkCenter = pos + Vector3.up * 0.5f;
            if (Physics.CheckSphere(checkCenter, clearanceRadius, blockingMask, QueryTriggerInteraction.Ignore))
                return false;
        }

        rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        return true;
    }

    internal void Unregister(GameObject enemy)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == enemy)
            {
                alive.RemoveAt(i);
                if (debugLog) Debug.Log($"[EnemySpawner] Unregister: {enemy?.name} Alive={alive.Count}");
                return;
            }
        }
    }

    private void ClampParams()
    {
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        spawnCountPerInterval = Mathf.Max(1, spawnCountPerInterval);

        rareSpawnChance = Mathf.Clamp01(rareSpawnChance);

        hpScaleStartMinutes = Mathf.Max(0, hpScaleStartMinutes);
        hpScaleEveryMinutes = Mathf.Max(1, hpScaleEveryMinutes);
        hpScaleMultiplierPerStep = Mathf.Max(1f, hpScaleMultiplierPerStep);
        maxHpScaleSteps = Mathf.Max(0, maxHpScaleSteps);
    }

#if UNITY_EDITOR
    private void OnValidate() => ClampParams();
#endif
}

public sealed class EnemySpawnTracker : MonoBehaviour
{
    private EnemySpawner owner;

    public void Init(EnemySpawner spawner) => owner = spawner;

    private void OnDisable() => owner?.Unregister(gameObject);
    private void OnDestroy() => owner?.Unregister(gameObject);
}
