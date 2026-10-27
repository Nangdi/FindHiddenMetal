using UnityEngine;

// 숨은 금속 찾기의 게임 진행 → 소리 연결. 소리 이름(키)은 StreamingAssets/Audio/sound.json 과 같아야 한다.
//
// BGM (화면 단계 기준)
//  idle   : 대기·체험 안내·정답 편집
//  play   : 카운트다운·탐색
//  result : 결과·원판 회전 대기
// 효과음
//  button / start / submit : 버튼 (ButtonSfx 컴포넌트가 버튼마다 낸다)
//  count (3·2·1) / go (탐색 시작) / select·deselect (칸 고르기·풀기) / full (8칸을 다 골라 더 못 고름)
//  tick (남은 시간 warnSeconds 이하일 때 1초마다) / timeup (시간 종료)
//  결과 순차 공개(ResultPanel): picks (내가 고른 칸 표시) / correct (정답 일치) / wrong (오답) / reveal (전체 정답 공개)
//                               success (모두 찾음) / finish (모두 찾지 못함) - 랭킹이 나타날 때
public class MetalSoundDirector : MonoBehaviour
{
    [SerializeField] private FlowController flow;
    [SerializeField] private CountdownPanel countdownPanel;
    [SerializeField] private PlayPanel playPanel;
    [SerializeField] private ResultPanel resultPanel;
    [Tooltip("남은 시간이 이 값(초) 이하가 되면 1초마다 tick")]
    [SerializeField] private int warnSeconds = 5;

    private FlowController.State lastState = (FlowController.State)(-1);
    private int lastRemain = -1;

    private void Start()
    {
        if (flow == null) flow = FindObjectOfType<FlowController>(true);
        if (countdownPanel == null) countdownPanel = FindObjectOfType<CountdownPanel>(true);
        if (playPanel == null) playPanel = FindObjectOfType<PlayPanel>(true);
        if (resultPanel == null) resultPanel = FindObjectOfType<ResultPanel>(true);

        if (countdownPanel != null) countdownPanel.OnNumber += _ => SoundManager.PlaySfx("count");
        if (playPanel != null) playPanel.OnCellTap += OnCellTap;
        if (resultPanel != null) resultPanel.OnReveal += OnReveal;
        if (GameTimer.Instance != null) GameTimer.Instance.OnTimeOver += OnTimeOver;
    }

    private void OnDestroy()
    {
        if (GameTimer.Instance != null) GameTimer.Instance.OnTimeOver -= OnTimeOver;
    }

    private void Update()
    {
        if (flow == null || JsonManager.instance == null) return; // 앱 종료 중

        if (flow.Current != lastState)
        {
            FlowController.State prev = lastState;
            lastState = flow.Current;
            SoundManager.PlayBgm(BgmFor(lastState));
            if (lastState == FlowController.State.Play && prev == FlowController.State.Countdown) SoundManager.PlaySfx("go");
            lastRemain = -1;
        }

        UpdateWarnTick();
    }

    private static string BgmFor(FlowController.State state)
    {
        switch (state)
        {
            case FlowController.State.Countdown:
            case FlowController.State.Play: return "play";
            case FlowController.State.Result:
            case FlowController.State.Ready: return "result";
            default: return "idle";
        }
    }

    // 제한 시간이 있을 때 마지막 몇 초는 1초마다 tick
    private void UpdateWarnTick()
    {
        var timer = GameTimer.Instance;
        if (lastState != FlowController.State.Play || timer == null || !timer.IsRunning || timer.TimeLimit <= 0f) return;

        int remain = Mathf.CeilToInt(timer.RemainingTime);
        if (remain == lastRemain) return;
        lastRemain = remain;
        if (remain > 0 && remain <= warnSeconds) SoundManager.PlaySfx("tick");
    }

    private void OnTimeOver()
    {
        if (lastState == FlowController.State.Play) SoundManager.PlaySfx("timeup");
    }

    private static void OnCellTap(PlayPanel.CellTap tap)
    {
        switch (tap)
        {
            case PlayPanel.CellTap.Select: SoundManager.PlaySfx("select"); break;
            case PlayPanel.CellTap.Deselect: SoundManager.PlaySfx("deselect"); break;
            default: SoundManager.PlaySfx("full"); break;
        }
    }

    private static void OnReveal(ResultPanel.RevealStep step, bool success)
    {
        switch (step)
        {
            case ResultPanel.RevealStep.Picks: SoundManager.PlaySfx("picks"); break;
            case ResultPanel.RevealStep.Correct: SoundManager.PlaySfx("correct"); break;
            case ResultPanel.RevealStep.Wrong: SoundManager.PlaySfx("wrong"); break;
            case ResultPanel.RevealStep.Missed: SoundManager.PlaySfx("reveal"); break;
            default: SoundManager.PlaySfx(success ? "success" : "finish"); break;
        }
    }
}
