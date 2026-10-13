using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultData
{
    public bool success;      // 모두 찾음 (false = 시간 초과)
    public float seconds;     // 소요 시간 (감점 포함)
    public int foundCount;
    public int totalCount;
    public int wrongCount;
    public int rank;          // 성공일 때만 의미 있음
    public int playerCount;   // 랭킹 보관 기간 안의 기록 수
    public List<RankRecord> top;
    public RankRecord mine;   // 목록에서 강조할 내 기록 (없으면 null)
}

// 04. 완료화면 - 소요 시간, 발견 개수, 순위, 랭킹 목록. 정답 LED는 이 화면이 열릴 때 함께 켜진다.
public class ResultPanel : PanelBase
{
    [Header("성공/시간초과 묶음 (제목 등 고정 글자는 스프라이트)")]
    [SerializeField] private GameObject successGroup;
    [SerializeField] private GameObject failGroup;

    [Header("기록")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text foundText;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text wrongText;
    [SerializeField] private TMP_Text rankText;

    [Header("랭킹 목록 (표시 인원은 Setting.json rankingShowCount)")]
    [SerializeField] private RankRowView rowPrefab;
    [SerializeField] private Transform rowRoot;

    [Header("버튼")]
    [SerializeField] private Button againButton;
    [SerializeField] private Button homeButton;

    // true = 다시 체험, false = 처음으로
    public event Action<bool> OnClose;

    private readonly List<RankRowView> rows = new List<RankRowView>();

    private void Awake()
    {
        if (againButton != null) againButton.onClick.AddListener(() => OnClose?.Invoke(true));
        if (homeButton != null) homeButton.onClick.AddListener(() => OnClose?.Invoke(false));
    }

    public void Set(ResultData data)
    {
        if (data == null) return;

        if (successGroup != null) successGroup.SetActive(data.success);
        if (failGroup != null) failGroup.SetActive(!data.success);

        if (timeText != null) timeText.text = PlayPanel.FormatTime(data.seconds);
        if (foundText != null) foundText.text = $"{data.foundCount} / {data.totalCount}";
        if (wrongText != null) wrongText.text = $"{data.wrongCount}";
        if (rankText != null) rankText.text = data.success ? $"{data.rank}위 <size=50%>/ {data.playerCount}명</size>" : "<size=60%>기록 없음</size>";

        SetRows(data.top, data.mine);
    }

    private void SetRows(List<RankRecord> top, RankRecord mine)
    {
        if (rowPrefab == null || rowRoot == null) return;

        int count = top != null ? top.Count : 0;
        while (rows.Count < count)
            rows.Add(Instantiate(rowPrefab, rowRoot));

        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < count;
            rows[i].gameObject.SetActive(on);
            if (on) rows[i].Set(RankOf(top, i), top[i], top[i] == mine);
        }
    }

    // 같은 시간은 같은 순위
    private static int RankOf(List<RankRecord> list, int index)
    {
        while (index > 0 && list[index - 1].seconds == list[index].seconds) index--;
        return index + 1;
    }
}
