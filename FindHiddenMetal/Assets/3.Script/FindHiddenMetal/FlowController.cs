using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 숨은 금속 찾기 체험 흐름.
// Idle → Guide → Play → Result → Ready(원판 회전 대기) → Idle (또는 다시 체험 시 Play)
//
// - 칸을 최대 8개 고르고 위치 확인 → 고른 칸마다 정답/오답 표시. 오답 칸마다 기록에 감점 시간을 더한다.
// - 8개를 모두 찾거나 제한 시간(설정 시)이 끝나면 결과 화면 + 장치 LED 점등(정답공개).
// - 결과 화면을 닫으면 LED 소등 + 원판 90도 회전, 완료 신호를 받은 뒤 다음 체험을 받는다.
public class FlowController : MonoBehaviour
{
    public enum State { Idle, Guide, Play, Result, Ready }

    [Header("패널")]
    [SerializeField] private IdlePanel idlePanel;
    [SerializeField] private GuidePanel guidePanel;
    [SerializeField] private PlayPanel playPanel;
    [SerializeField] private ResultPanel resultPanel;
    [SerializeField] private ReadyPanel readyPanel;

    [Header("연출")]
    [Tooltip("마지막 금속을 찾거나 시간이 끝난 뒤 결과 화면까지 기다리는 시간(초)")]
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
        if (guidePanel != null) guidePanel.OnConfirm += () => GoTo(State.Play);
        if (playPanel != null) playPanel.OnSubmit += Submit;
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
            bool hasLimit = timer.TimeLimit > 0f;
            if (playPanel != null) playPanel.SetTime(hasLimit ? timer.RemainingTime : timer.CurrentTime, hasLimit);
        }

        CheckIdleTimeout();
    }

    public void GoTo(State next)
    {
        Current = next;
        lastInputTime = Time.unscaledTime;

        SetPanel(idlePanel, next == State.Idle);
        SetPanel(guidePanel, next == State.Guide);
        SetPanel(playPanel, next == State.Play);
        SetPanel(resultPanel, next == State.Result);
        SetPanel(readyPanel, next == State.Ready);

        switch (next)
        {
            case State.Idle:
                Resources.UnloadUnusedAssets();
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

        // 한 번에 목표 개수(8개)까지 고를 수 있다.
        if (playPanel != null) playPanel.Begin(Config.gridSize, judge.TotalCount, judge.TotalCount);
        if (playPanel != null) playPanel.SetInteractable(true);
        ApplyDebugAnswers();

        if (GameTimer.Instance != null) GameTimer.Instance.SetTimeLimit(Config.timeLimitSeconds);
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }

    // 고른 칸을 한꺼번에 판정한다. 오답 칸마다 감점 시간을 더한다.
    // 마크를 남길지는 ESC 설정창 값(keepFoundMark / keepWrongMark)을 따른다.
    private void Submit(List<GridPos> positions)
    {
        if (Current != State.Play || finishing || positions == null || positions.Count == 0) return;

        var settings = JsonManager.instance.gameSettingData;
        int wrong = 0;
        var log = new StringBuilder();
        foreach (var p in positions)
        {
            JudgeResult result = judge.Judge(p.x, p.y);
            if (result == JudgeResult.Wrong) wrong++;
            log.Append($"{TargetLayout.ToLabel(p.x, p.y)}:{result} ");
            if (playPanel != null) playPanel.ShowJudge(p.x, p.y, result, settings.keepFoundMark, settings.keepWrongMark);
        }
        Debug.Log($"위치 확인 {positions.Count}칸 (오답 {wrong}) → {log}");

        if (wrong > 0 && Config.wrongPenaltySeconds > 0f)
            if (GameTimer.Instance != null) GameTimer.Instance.AddTime(Config.wrongPenaltySeconds * wrong);

        if (playPanel != null) playPanel.SetProgress(judge.FoundCount, judge.TotalCount);

        if (judge.IsAllFound) StartCoroutine(Finish(true));
    }

    private void OnTimeOver()
    {
        if (Current == State.Play && !finishing) StartCoroutine(Finish(false));
    }

    private IEnumerator Finish(bool success)
    {
        finishing = true;
        float seconds = GameTimer.Instance != null ? GameTimer.Instance.CurrentTime : 0f;
        if (!success && GameTimer.Instance != null) seconds = GameTimer.Instance.TimeLimit;
        if (GameManager.Instance != null) GameManager.Instance.EndGame();

        // 정답공개: 화면 격자 + 장치 LED
        if (playPanel != null) playPanel.RevealTargets(judge.Targets, judge.IsFound);
        if (Device != null) Device.LedOn();

        var data = new ResultData
        {
            success = success,
            seconds = seconds,
            foundCount = judge.FoundCount,
            totalCount = judge.TotalCount,
            wrongCount = judge.WrongCount,
        };
        if (success)
        {
            data.mine = ranking.Add(seconds, judge.WrongCount);
            data.rank = ranking.GetRank(seconds);
        }
        data.playerCount = ranking.Count;
        data.top = ranking.GetTop(Config.rankingShowCount);

        yield return new WaitForSecondsRealtime(finishHoldSeconds);

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
        GoTo(replayAfterReady ? State.Play : State.Idle);
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
            // 체험을 중간에 떠난 경우: 기록 없이 원판만 돌려 다음 체험을 준비한다.
            Debug.Log("무입력 - 체험 중단");
            if (GameManager.Instance != null) GameManager.Instance.EndGame();
            ResetDevice(false);
        }
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
