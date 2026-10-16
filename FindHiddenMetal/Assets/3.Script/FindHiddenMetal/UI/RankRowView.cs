using TMPro;
using UnityEngine;

// 랭킹 목록 한 줄: 순위 / 기록 시각 / 소요 시간 / 찾은 개수.
// 시계, 과녁 아이콘과 줄 배경은 정적 레이어(또는 나의 랭킹 줄 스프라이트)에 있다.
public class RankRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text foundText;

    [Header("색 (1위 강조)")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color accentColor = Color.red;

    public void Set(int rank, RankRecord record, bool accent = false)
    {
        if (record == null)
        {
            SetTexts("", "", "", "");
            return;
        }

        SetTexts($"{rank}위", record.Time.ToString("yy.MM.dd HH:mm:ss"), PlayPanel.FormatTime(record.seconds),
            $"{record.foundCount}/{record.totalCount}");

        // 순위와 기록 시각만 강조색으로 칠한다
        Color color = accent ? accentColor : normalColor;
        if (rankText != null) rankText.color = color;
        if (dateText != null) dateText.color = color;
    }

    private void SetTexts(string rank, string date, string time, string found)
    {
        if (rankText != null) rankText.text = rank;
        if (dateText != null) dateText.text = date;
        if (timeText != null) timeText.text = time;
        if (foundText != null) foundText.text = found;
    }
}
