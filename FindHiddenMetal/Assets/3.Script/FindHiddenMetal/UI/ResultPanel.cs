using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultData
{
    public bool success;      // 모두 찾음
    public float seconds;     // 정답 확인까지 걸린 시간 (시간 초과면 제한 시간)
    public int foundCount;
    public int totalCount;
    public int wrongCount;
    public int rank;          // 나의 순위 (mine이 있을 때)
    public List<RankRecord> top;
    public RankRecord mine;   // 나의 랭킹 줄에 표시할 기록 (없으면 숨김)

    // 결과 격자
    public int gridSize;
    public List<GridPos> found = new List<GridPos>();   // 고른 칸 중 정답 (정답 일치)
    public List<GridPos> missed = new List<GridPos>();  // 고르지 않은 정답 (정답)
    public List<GridPos> wrong = new List<GridPos>();   // 고른 칸 중 오답 (X)
}

// 05. 결과 화면 - 결과 격자, 소요 시간/찾은 금속/오답 수, 랭킹 목록, 나의 랭킹.
// 정답 LED는 이 화면이 열릴 때 함께 켜진다.
public class ResultPanel : PanelBase
{
    [Header("결과 격자 (정답 일치 / 정답 / 오답 마크)")]
    [SerializeField] private GridBoard board;

    [Header("기록")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text foundText;
    [SerializeField] private TMP_Text wrongText;

    [Header("랭킹 목록 (표시 인원은 Setting.json rankingShowCount)")]
    [SerializeField] private RankRowView rowPrefab;
    [SerializeField] private Transform rowRoot;
    [Tooltip("나의 랭킹 줄")]
    [SerializeField] private RankRowView mineRow;

    [Header("버튼")]
    [SerializeField] private Button homeButton;

    // true = 다시 체험, false = 처음으로
    public event Action<bool> OnClose;

    private readonly List<RankRowView> rows = new List<RankRowView>();

    private void Awake()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => OnClose?.Invoke(false));
    }

    public void Set(ResultData data)
    {
        if (data == null) return;

        if (timeText != null) timeText.text = PlayPanel.FormatTime(data.seconds);
        if (foundText != null) foundText.text = $"{data.foundCount}개";
        if (wrongText != null) wrongText.text = $"{data.wrongCount}개";

        SetBoard(data);
        SetRows(data.top);

        if (mineRow != null)
        {
            mineRow.gameObject.SetActive(data.mine != null);
            if (data.mine != null) mineRow.Set(data.rank, data.mine);
        }
    }

    private void SetBoard(ResultData data)
    {
        if (board == null) return;
        board.Build(data.gridSize);
        board.ResetCells();

        foreach (var p in data.wrong) board.GetCell(p.x, p.y)?.ShowWrong(true);
        foreach (var p in data.missed) board.GetCell(p.x, p.y)?.ShowReveal();
        foreach (var p in data.found) board.GetCell(p.x, p.y)?.ShowFound(true);
    }

    private void SetRows(List<RankRecord> top)
    {
        if (rowPrefab == null || rowRoot == null) return;

        int count = top != null ? top.Count : 0;
        while (rows.Count < count)
            rows.Add(Instantiate(rowPrefab, rowRoot));

        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < count;
            rows[i].gameObject.SetActive(on);
            if (!on) continue;

            int rank = RankOf(top, i);
            rows[i].Set(rank, top[i], rank == 1);
        }
    }

    // 찾은 개수와 시간이 같으면 같은 순위
    private static int RankOf(List<RankRecord> list, int index)
    {
        while (index > 0 && RankingStore.Score(list[index - 1], list[index]) == 0) index--;
        return index + 1;
    }
}
