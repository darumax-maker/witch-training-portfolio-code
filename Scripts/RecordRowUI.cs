using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RecordRowUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text attackCountText;
    [SerializeField] private TMP_Text speedCountText;

    [Header("Format")]
    [SerializeField] private string rankFormat = "{0}.";
    [SerializeField] private string countFormat = ": {0}";

    public void SetRow(int rank1Based, float survivalSeconds, string dateYmd, int atk, int spd)
    {
        if (rankText != null) rankText.text = string.Format(rankFormat, rank1Based);

        if (timeText != null) timeText.text = ElapsedTimeUI.FormatSeconds(survivalSeconds);

        if (dateText != null) dateText.text = dateYmd ?? "";

        if (attackCountText != null) attackCountText.text = string.Format(countFormat, atk);
        if (speedCountText != null) speedCountText.text = string.Format(countFormat, spd);
    }
}
