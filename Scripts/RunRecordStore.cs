using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunRecordStore : MonoBehaviour
{
    [Serializable]
    public sealed class RecordEntry
    {
        public float survivalSeconds;
        public string dateYmd;   // "yyyy-MM-dd"
        public int attackCount;
        public int speedCount;

        // 同点タイブレーク用（新しい方を上にしたい等）
        public long createdUnixSeconds;
    }

    [Serializable]
    private sealed class RecordListWrapper
    {
        public List<RecordEntry> list = new List<RecordEntry>();
    }

    public static RunRecordStore Instance { get; private set; }

    [Header("Policy")]
    [SerializeField] private int maxRecords = 20;

    private const string PrefKey = "RUN_RECORDS_V1";
    private RecordListWrapper data = new RecordListWrapper();

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ClampAndSortAndTrim(save: false);
    }

    public IReadOnlyList<RecordEntry> GetTopRecords(int limit = 20)
    {
        ClampAndSortAndTrim(save: false);

        int n = Mathf.Clamp(limit, 0, data.list.Count);
        // 参照をそのまま渡すと外部改変され得るのでコピー
        var copy = new List<RecordEntry>(n);
        for (int i = 0; i < n; i++) copy.Add(data.list[i]);
        return copy;
    }

    public void AddRecord(float survivalSeconds, int attackCount, int speedCount, DateTime? date = null)
    {
        float s = Mathf.Max(0f, survivalSeconds);
        int atk = Mathf.Max(0, attackCount);
        int spd = Mathf.Max(0, speedCount);

        DateTime dt = date ?? DateTime.Now;

        var e = new RecordEntry
        {
            survivalSeconds = s,
            dateYmd = dt.ToString("yyyy-MM-dd"),
            attackCount = atk,
            speedCount = spd,
            createdUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };

        data.list.Add(e);

        ClampAndSortAndTrim(save: true);
    }

    public void ClearAll()
    {
        data.list.Clear();
        Save();
    }

    private void ClampAndSortAndTrim(bool save)
    {
        if (maxRecords < 1) maxRecords = 1;

        // 生存時間 降順、同点なら新しい方を上
        data.list.Sort((a, b) =>
        {
            int t = b.survivalSeconds.CompareTo(a.survivalSeconds);
            if (t != 0) return t;
            return b.createdUnixSeconds.CompareTo(a.createdUnixSeconds);
        });

        if (data.list.Count > maxRecords)
        {
            data.list.RemoveRange(maxRecords, data.list.Count - maxRecords);
        }

        if (save) Save();
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(PrefKey))
        {
            data = new RecordListWrapper();
            return;
        }

        string json = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json))
        {
            data = new RecordListWrapper();
            return;
        }

        try
        {
            data = JsonUtility.FromJson<RecordListWrapper>(json);
            if (data == null || data.list == null) data = new RecordListWrapper();
        }
        catch
        {
            data = new RecordListWrapper();
        }
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PrefKey, json);
        PlayerPrefs.Save();
    }
}
