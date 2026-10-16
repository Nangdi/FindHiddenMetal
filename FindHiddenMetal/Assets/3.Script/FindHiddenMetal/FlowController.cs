using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 숨은 금속 찾기 체험 흐름.
// Idle → Guide → Countdown → Play → Result → Ready(원판 회전 대기) → Idle (또는 다시 체험 시 Countdown)
//
// - 칸을 8개 모두 고르고 정답 확인을 한 번 누르면 바로 판정하고 결과 화면 + 장치 LED 점등(정답공개).
// - 제한 시간이 끝나면 그때까지 고른 칸으로 판정한다(없으면 0개). 두 경우 모두 랭킹에 기록한다.
// - 체험 중 HOME을 누르면 기록 없이 원판을 돌리고 대기 화면으로 간다.
// - 결과 화면을 닫으면 LED 소등 + 원판 90도 회전, 완료 신호를 받은 뒤 다음 체험을 받는다.
public class FlowController : MonoBehaviour
{
    public enum State { Idle, Guide, Countdown, Play, Result, Ready }

    [Header("패널")]
    [SerializeField] private IdlePanel idlePanel;
    [SerializeField] private GuidePanel guidePanel;
    [SerializeField] private CountdownPanel countdownPanel;
    [SerializeField] private PlayPanel playPanel;
    [SerializeField] private ResultPanel resultPanel;
    [SerializeField] private ReadyPanel readyPanel;

    [Header("연출")]
    [Tooltip("시간이 끝난 뒤 결과 화면까지 기다리는 시간(초). 정답 확인을 누르면 바로 넘어간다")]
    [SerializeField] private float finishHoldSeconds = 2f;

    [Header("개발자용")]
    [Tooltip("체험 진행 중 정답 위치 표시를 켜고 끄는 키. 켜 둔 상태는 다음 판에도 유지된다.")]
    [SerializeField] private KeyCode debugAnswerKey = KeyCode.F2;

    public State Current { get; private set; } = State.Idle;

    private readonly GridJudge judge = new GridJudge();
    private RankingStore ranking;
    private float lastInputTime;
    private float resultShownTime;
    private bool replayAfterReady;
    private bool finishing;
    private bool showDebugAnswers;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;
    private static MetalDeviceLink Device => MetalDeviceLink.Link;

    private void Start()
    {
        ranking = new RankingStore(Config.rankingKeepDays);

        if (idlePanel != null) idlePanel.OnStart += () => GoTo(State.Guide);
        if (guidePanel != null) guidePanel.OnConfirm += () => GoTo(State.Countdown);
        if (countdownPanel != null) countdownPanel.OnDone += () => { if (Current == State.Countdown) GoTo(State.Play); };
        if (playPanel != null) playPanel.OnSubmit += Submit;
        if (playPanel != null) playPanel.OnHome += AbortPlay;
        if (resultPanel != null) resultPanel.OnClose += CloseResult;
        if (GameTimer.Instance != null) GameTimer.Instance.OnTimeOver += OnTimeOver;
        if (Device != null) Device.OnRotationDone += OnRotationDone;
        else Debug.LogWarning("MetalDeviceLink 없음 - 원판 회전 없이 진행");

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        GoTo(State.Idle);
    }

    private void OnDestroy()
    {
        if (GameTimer.Instance != null) GameTimer.Instance.OnTimeOver -= OnTimeOver;
        if (Device != null) Device.OnRotationDone -= OnRotationDone;
    }

    private void Update()
    {
        // 종료 중 JsonManager가 먼저 파괴되는 경우
        if (JsonManager.instance == null) return;

        if (Input.anyKeyDown || Input.touchCount > 0)
            lastInputTime = Time.unscaledTime;

        if (Input.GetKeyDown(debugAnswerKey))
        {
            showDebugAnswers = !showDebugAnswers;
            Debug.Log($"[개발자] 정답 위치 표시 {(showDebugAnswers ? "켬" : "끔")}");
            ApplyDebugAnswers();
        }

        if (Current == State.Play && !finishing && GameTimer.Instance != null)
        {
            var timer = GameTimer.Instance;
            if (playPanel != null) playPanel.SetTime(timer.CurrentTime, timer.TimeLimit);
        }

        CheckIdleTimeout();
    }

    public void GoTo(State next)
    {
        Current = next;
        lastInputTime = Time.unscaledTime;

        SetPanel(idlePanel, next == State.Idle);
        SetPanel(guidePanel, next == State.Guide);
        SetPanel(countdownPanel, next == State.Countdown);
        SetPanel(playPanel, next == State.Play);
        SetPanel(resultPanel, next == State.Result);
        SetPanel(readyPanel, next == State.Ready);

        switch (next)
        {
            case State.Idle:
                Resources.UnloadUnusedAssets();
                break;
            case State.Countdown:
                if (countdownPanel != null && Config.countdownSeconds > 0) countdownPanel.Begin(Config.countdownSeconds);
                else GoTo(State.Play);
                break;
            case State.Play:
                BeginPlay();
                break;
            case State.Result:
                resultShownTime = Time.unscaledTime;
                break;
        }
    }

    private void BeginPlay()
    {
        finishing = false;
        int rotation = Device != null ? Device.RotationIndex : 0;
        var targets = TargetLayout.GetTargets(Config.baseTargets, Config.gridSize, rotation, Config.rotateClockwise);
        judge.Begin(targets);
        Debug.Log($"체험 시작 - 원판 {rotation}단계, 목표 {judge.TotalCount}개");

        // 목표 개수(8개)를 모두 골라야 정답 확인을 누를 수 있다.
        if (playPanel != null) playPanel.Begin(Config.gridSize, judge.TotalCount);
        if (playPanel != null) playPanel.SetInteractable(true);
        if (playPanel != null) playPanel.SetTime(0f, Config.timeLimitSeconds);
        ApplyDebugAnswers();

        if (GameTimer.Instance != null) GameTimer.Instance.SetTimeLimit(Config.timeLimitSeconds);
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }

    // 정답 확인은 한 번뿐: 고른 칸을 한꺼번에 판정하고 바로 결과 화면으로 간다.
    private void Submit(List<GridPos> positions)
    {
        if (Current != State.Play || finishing || positions == null || positions.Count == 0) return;
        StartCoroutine(Finish(positions, false));
    }

    // 시간이 끝나면 그때까지 고른 칸으로 판정한다.
    private void OnTimeOver()
    {
        if (Current != State.Play || finishing) return;
        var positions = playPanel != null ? playPanel.GetSelection() : new List<GridPos>();
        StartCoroutine(Finish(positions, true));
    }

    private void JudgeAll(List<GridPos> positions)
    {
        var log = new StringBuilder();
        foreach (var p in positions)
            log.Append($"{TargetLayout.ToLabel(p.x, p.y)}:{judge.Judge(p.x, p.y)} ");
        Debug.Log($"정답 확인 {positions.Count}칸 - 정답 {judge.FoundCount}/{judge.TotalCount}, 오답 {judge.WrongCount} → {log}");
    }

    private IEnumerator Finish(List<GridPos> positions, bool timeOver)
    {
        finishing = true;
        float seconds = GameTimer.Instance != null ? GameTimer.Instance.CurrentTime : 0f;
        if (timeOver && GameTimer.Instance != null) seconds = GameTimer.Instance.TimeLimit;
        JudgeAll(positions);
        if (GameManager.Instance != null) GameManager.Instance.EndGame();

        // 정답공개: 장치 LED (화면은 결과 격자에서 보여 준다)
        if (playPanel != null) playPanel.SetInteractable(false);
        if (Device != null) Device.LedOn();

        var data = new ResultData
        {
            success = judge.IsAllFound,
            seconds = seconds,
            foundCount = judge.FoundCount,
            totalCount = judge.TotalCount,
            wrongCount = judge.WrongCount,
            gridSize = Config.gridSize,
        };
        foreach (var p in judge.Targets)
            (judge.IsFound(p.x, p.y) ? data.found : data.missed).Add(p);
        data.wrong.AddRange(judge.WrongPositions);

        data.mine = ranking.Add(seconds, judge.FoundCount, judge.TotalCount, judge.WrongCount);
        data.rank = ranking.GetRank(data.mine);
        data.top = ranking.GetTop(Config.rankingShowCount);

        if (timeOver) yield return new WaitForSecondsRealtime(finishHoldSeconds);

        if (resultPanel != null) resultPanel.Set(data);
        GoTo(State.Result);
    }

    private void CloseResult(bool replay)
    {
        if (Current != State.Result) return;
        ResetDevice(replay);
    }

    // LED 소등 + 원판 회전. 회전이 끝나면 OnRotationDone에서 다음 화면으로 간다.
    private void ResetDevice(bool replay)
    {
        replayAfterReady = replay;
        GoTo(State.Ready);
        if (Device != null) Device.LedOff();

        if (Device != null) Device.Rotate();
        else OnRotationDone(0);
    }

    private void OnRotationDone(int index)
    {
        if (Current != State.Ready) return;
        Debug.Log($"원판 회전 완료 - {index}단계");
        GoTo(replayAfterReady ? State.Countdown : State.Idle);
    }

    private void CheckIdleTimeout()
    {
        float now = Time.unscaledTime;

        if (Current == State.Result)
        {
            if (Config.resultAutoCloseSeconds > 0f && now - resultShownTime >= Config.resultAutoCloseSeconds
                && now - lastInputTime >= Config.resultAutoCloseSeconds)
                ResetDevice(false);
            return;
        }

        if (Config.idleTimeoutSeconds <= 0f || now - lastInputTime < Config.idleTimeoutSeconds) return;

        if (Current == State.Guide)
        {
            GoTo(State.Idle);
        }
        else if (Current == State.Play && !finishing)
        {
            Debug.Log("무입력 - 체험 중단");
            AbortPlay();
        }
    }

    // 체험을 중간에 떠난 경우(HOME, 무입력): 기록 없이 원판만 돌려 다음 체험을 준비한다.
    private void AbortPlay()
    {
        if (Current != State.Play || finishing) return;
        Debug.Log("체험 중단 - 기록 없이 대기 화면으로");
        if (GameManager.Instance != null) GameManager.Instance.EndGame();
        ResetDevice(false);
    }

    // 개발자용 정답 위치 표시 (진행 화면에서만)
    private void ApplyDebugAnswers()
    {
        if (playPanel == null || Current != State.Play) return;
        playPanel.ShowDebugTargets(judge.Targets, showDebugAnswers);

        if (!showDebugAnswers) return;
        var labels = new StringBuilder();
        foreach (var p in judge.Targets) labels.Append(TargetLayout.ToLabel(p.x, p.y)).Append(' ');
        Debug.Log($"[개발자] 정답 위치: {labels}");
    }

    private static void SetPanel(PanelBase panel, bool on)
    {
        if (panel == null) return;
        if (on) panel.Show();
        else panel.Hide();
    }
}
