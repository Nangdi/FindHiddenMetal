using TMPro;
using UnityEngine;

// 랭킹 목록 한 줄. Highlight는 방금 체험한 기록 표시.
public class RankRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private GameObject highlight;

    public void Set(int rank, RankRecord record, bool isMine)
    {
        if (record == null)
        {
            if (rankText != null) rankText.text = "";
            if (timeText != null) timeText.text = "";
            if (highlight != null) highlight.SetActive(false);
            return;
        }

        if (rankText != null) rankText.text = $"{rank}위";
        if (timeText != null) timeText.text = PlayPanel.FormatTime(record.seconds);
        if (highlight != null) highlight.SetActive(isMine);
    }
}
