using System;
using System.Collections;
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
    public List<ResultPick> picks = new List<ResultPick>(); // 고른 칸 (고른 순서대로, 순차 공개용)
    public List<GridPos> found = new List<GridPos>();   // 고른 칸 중 정답 (정답 일치)
    public List<GridPos> missed = new List<GridPos>();  // 고르지 않은 정답 (정답)
    public List<GridPos> wrong = new List<GridPos>();   // 고른 칸 중 오답 (X)
}

public class ResultPick
{
    public GridPos pos;
    public bool correct;
}

// 05. 결과 화면 - 결과 격자, 소요 시간/찾은 금속/오답 수, 랭킹 목록, 나의 랭킹.
// 정답 LED는 이 화면이 열릴 때 함께 켜진다.
// 순차 공개:
//  1) 내가 고른 칸을 모두 선택 표시로 보여 준다
//  2) 고른 순서대로 한 칸씩 정답 일치(O)/오답(X)으로 바꾸며 찾은 금속·오답 수를 센다 (간격 revealStepSeconds)
//  3) 못 찾은 정답까지 전체 정답을 공개하고, 마지막에 랭킹을 보여 준다
// Setting.json revealStepSeconds 가 0이면 연출 없이 한꺼번에 보여 준다.
public class ResultPanel : PanelBase
{
    public enum RevealStep { Picks, Correct, Wrong, Missed, Done }

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

    [Header("순차 공개")]
    [Tooltip("화면이 열린 뒤 고른 칸을 보여 주기까지 기다리는 시간(초)")]
    [SerializeField] private float revealStartDelay = 0.4f;
    [Tooltip("고른 칸을 모두 보여 준 뒤 한 칸씩 판정을 시작하기까지 기다리는 시간(초)")]
    [SerializeField] private float picksHoldSeconds = 1.2f;
    [Tooltip("못 찾은 정답을 보여 준 뒤 랭킹을 보여 주기까지 기다리는 시간(초)")]
    [SerializeField] private float rankingDelay = 0.8f;

    // true = 다시 체험, false = 처음으로
    public event Action<bool> OnClose;
    // 순차 공개 단계마다 알린다 (소리 연결용). Done 의 bool = 모두 찾음
    public event Action<RevealStep, bool> OnReveal;

    private readonly List<RankRowView> rows = new List<RankRowView>();
    private ResultData current;
    private Coroutine revealRoutine;

    private static float StepSeconds => JsonManager.instance != null ? Mathf.Max(0f, JsonManager.instance.gameDynamicData.revealStepSeconds) : 0f;

    private void Awake()
    {
        if (homeButton != null) homeButton.onClick.AddListener(() => OnClose?.Invoke(false));
    }

    // 결과를 채운다. 순차 공개는 화면이 열릴 때(Show) 시작한다.
    public void Set(ResultData data)
    {
        if (data == null) return;
        current = data;

        if (timeText != null) timeText.text = PlayPanel.FormatTime(data.seconds);
        SetCounts(0, 0);

        if (board != null)
        {
            board.Build(data.gridSize);
            board.ResetCells();
        }
        SetRows(data.top);
        if (mineRow != null && data.mine != null) mineRow.Set(data.rank, data.mine);
        SetRankingVisible(false);
    }

    public override void Show()
    {
        base.Show();
        if (current == null) return;
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(Reveal(current));
    }

    public override void Hide()
    {
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = null;
        current = null;
        base.Hide();
    }

    private IEnumerator Reveal(ResultData data)
    {
        float step = StepSeconds;
        bool animate = step > 0f;
        int found = 0, wrong = 0;

        // 1) 내가 고른 칸 전체
        if (animate && data.picks.Count > 0)
        {
            yield return new WaitForSecondsRealtime(revealStartDelay);
            foreach (var pick in data.picks)
            {
                var cell = board != null ? board.GetCell(pick.pos.x, pick.pos.y) : null;
                cell?.SetSelected(true);
                cell?.Pop();
            }
            OnReveal?.Invoke(RevealStep.Picks, data.success);
            yield return new WaitForSecondsRealtime(picksHoldSeconds);
        }

        // 2) 고른 순서대로 한 칸씩 맞음/틀림
        foreach (var pick in data.picks)
        {
            var cell = board != null ? board.GetCell(pick.pos.x, pick.pos.y) : null;
            cell?.SetSelected(false);
            if (pick.correct)
            {
                found++;
                cell?.ShowFound(true);
            }
            else
            {
                wrong++;
                cell?.ShowWrong(true);
            }
            if (!animate) continue;

            cell?.Pop();
            SetCounts(found, wrong);
            OnReveal?.Invoke(pick.correct ? RevealStep.Correct : RevealStep.Wrong, data.success);
            yield return new WaitForSecondsRealtime(step);
        }
        SetCounts(data.foundCount, data.wrongCount);

        // 3) 전체 정답 공개 (못 찾은 정답을 한꺼번에)
        if (data.missed.Count > 0)
        {
            foreach (var p in data.missed)
            {
                var cell = board != null ? board.GetCell(p.x, p.y) : null;
                cell?.ShowReveal();
                if (animate) cell?.Pop();
            }
            if (animate)
            {
                OnReveal?.Invoke(RevealStep.Missed, data.success);
                yield return new WaitForSecondsRealtime(rankingDelay);
            }
        }

        SetRankingVisible(true);
        OnReveal?.Invoke(RevealStep.Done, data.success);
        revealRoutine = null;
    }

    private void SetCounts(int found, int wrong)
    {
        if (foundText != null) foundText.text = $"{found}개";
        if (wrongText != null) wrongText.text = $"{wrong}개";
    }

    // 랭킹 목록과 나의 랭킹 줄은 순차 공개가 끝난 뒤에 보여 준다.
    private void SetRankingVisible(bool on)
    {
        if (rowRoot != null) rowRoot.gameObject.SetActive(on);
        if (mineRow != null) mineRow.gameObject.SetActive(on && current != null && current.mine != null);
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
