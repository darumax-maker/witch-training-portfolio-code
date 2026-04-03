using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RecordPanelUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private RecordRowUI rowPrefab;

    [Header("Layout (Manual)")]
    [SerializeField] private float topPadding = 8f;
    [SerializeField] private float bottomPadding = 8f;
    [SerializeField] private float rowHeight = 64f;
    [SerializeField] private float rowSpacing = 4f;

    [Header("Policy")]
    [SerializeField] private int showMax = 20;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        EnsureStoreExists();

        ClearRows();

        if (scrollRect == null || content == null || rowPrefab == null) return;

        var list = RunRecordStore.Instance.GetTopRecords(showMax);

        // Content設定（上基準）
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        float y = topPadding;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];

            var row = Instantiate(rowPrefab, content);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.pivot = new Vector2(0.5f, 1f);

            rowRT.sizeDelta = new Vector2(0f, rowHeight); // 横はストレッチ、縦だけ固定
            rowRT.anchoredPosition = new Vector2(0f, -y);

            row.SetRow(i + 1, e.survivalSeconds, e.dateYmd, e.attackCount, e.speedCount);

            spawned.Add(row.gameObject);

            y += rowHeight + rowSpacing;
        }

        float contentH = topPadding + bottomPadding;
        if (list.Count > 0)
            contentH += list.Count * rowHeight + (list.Count - 1) * rowSpacing;

        // Contentの高さを確保（縦スクロールのために重要）
        var sd = content.sizeDelta;
        sd.y = contentH;
        content.sizeDelta = sd;

        // 開いた瞬間は一番上
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearRows()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    private void EnsureStoreExists()
    {
        if (RunRecordStore.Instance != null) return;

        // TitleSceneに置き忘れても動く保険
        var existing = FindFirstObjectByType<RunRecordStore>();
        if (existing != null) return;

        var go = new GameObject("RunRecordStore");
        go.AddComponent<RunRecordStore>();
    }
}
