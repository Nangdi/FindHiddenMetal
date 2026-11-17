using System;
using System.Collections;
using System.IO;
using UnityEngine;

[Serializable]
public class DeviceStateData
{
    // 회전 원판의 현재 단계 (0~3). 재실행해도 정답 좌표가 맞도록 저장한다.
    public int rotationIndex;
    // 마지막 회전이 역방향(단계 감소)이었는지. 0→1→2→3→2→1→0 왕복 중 어느 쪽으로 가는지 이어가기 위해 저장한다.
    public bool reverse;
}

// 금속탐지 장치 컨트롤러와의 RS232 통신.
// SerialPortManager 대신 이 컴포넌트를 씬에 둔다. 규약은 Docs/ControllerProtocol.md 참고.
//
// PC → 컨트롤러 : START / LED_ON / LED_OFF / ROTATE / STATE?
// 컨트롤러 → PC : ROT_DONE[,n] / STATE,n / ERR,내용
//
// 컨트롤러 id는 구분하지 않는다: 열린 어느 포트로 들어온 메시지든 똑같이 처리하고, 명령은 열린 모든 포트로 보낸다.
// 원판은 0→1→2→3→2→1→0→1… 처럼 왕복한다. 방향은 컨트롤러가 정하고 ROT_DONE,n 으로 단계를 알려 준다.
// n이 없거나 응답이 없으면 PC가 같은 왕복 순서로 다음 단계를 계산한다.
// 열린 포트가 없으면(장치 없음, port.json 비활성) 회전을 흉내 내어 체험 흐름이 멈추지 않게 한다.
public class MetalDeviceLink : SerialPortManager
{
    public const string CmdStart = "START";
    public const string CmdLedOn = "LED_ON";
    public const string CmdLedOff = "LED_OFF";
    public const string CmdRotate = "ROTATE";
    public const string CmdState = "STATE?";
    public const string MsgRotateDone = "ROT_DONE";
    public const string MsgState = "STATE";
    public const string MsgError = "ERR";

    public static MetalDeviceLink Link => Instance as MetalDeviceLink;

    [Tooltip("포트가 닫혀 있을 때 회전 완료로 처리하기까지의 시간(초)")]
    [SerializeField] private float simulatedRotateSeconds = 1f;

    // 회전 완료 (현재 단계). 시간 초과로 넘어간 경우에도 호출된다.
    public event Action<int> OnRotationDone;
    // STATE,n 수신 (컨트롤러가 알려 준 현재 단계)
    public event Action<int> OnStateReceived;

    public int RotationIndex => state.rotationIndex;
    // 마지막으로 STATE,n 을 받은 시각 (Time.unscaledTime). 받은 적 없으면 -1.
    public float LastStateTime { get; private set; } = -1f;
    public bool IsRotating { get; private set; }
    public bool IsDeviceOpen => IsAnyOpen;

    private DeviceStateData state = new DeviceStateData();
    private string statePath;
    private Coroutine rotateRoutine;

    protected override void Awake()
    {
        base.Awake();
        statePath = Path.Combine(Application.persistentDataPath, "deviceState.json");
        try
        {
            state = JsonManager.LoadData(statePath, new DeviceStateData());
        }
        catch (Exception ex)
        {
            Debug.LogError($"장치 상태 파일 읽기 실패 - 0단계로 시작: {ex.Message}");
            state = new DeviceStateData();
        }
        state.rotationIndex = Normalize(state.rotationIndex);
    }

    protected override void Start()
    {
        base.Start();
        // 컨트롤러가 현재 단계를 알고 있으면 STATE,n 으로 맞춰 준다. 응답이 없으면 저장값을 쓴다.
        RequestState();
    }

    // 컨트롤러에 현재 단계를 묻는다. 응답(STATE,n)은 OnStateReceived. 포트가 없으면 아무 일도 없다.
    public void RequestState()
    {
        if (IsDeviceOpen) SendAll(CmdState);
    }

    // 디버그 패널·로그에 안내를 남긴다 (FlowController의 단계 확인 결과 등).
    public void Note(string message, bool warning = false) => Info(message, warning);

    // 다음 회전의 단계 변화. 끝(0, 3)에서 방향을 바꾸고, 그 사이에서는 하던 방향을 이어간다.
    public int NextStep()
    {
        if (state.rotationIndex >= TargetLayout.RotationSteps - 1) return -1;
        if (state.rotationIndex <= 0) return 1;
        return state.reverse ? -1 : 1;
    }

    // 대기 화면에서 체험 시작을 눌렀을 때.
    public void SendStart() => SendIfOpen(CmdStart);
    public void LedOn() => SendIfOpen(CmdLedOn);
    public void LedOff() => SendIfOpen(CmdLedOff);

    // 원판을 90도 돌린다 (방향은 컨트롤러가 왕복 순서로 정함). 완료되면 OnRotationDone.
    public void Rotate()
    {
        if (IsRotating) return;
        IsRotating = true;

        if (IsDeviceOpen)
            SendAll(CmdRotate);
        else
            Debug.LogWarning("[장치] 열린 포트 없음 - 원판 회전을 흉내 냄");

        float wait = IsDeviceOpen
            ? Mathf.Max(1f, JsonManager.instance.gameDynamicData.rotateTimeoutSeconds)
            : simulatedRotateSeconds;
        rotateRoutine = StartCoroutine(RotateTimeout(wait, IsDeviceOpen));
    }

    private IEnumerator RotateTimeout(float seconds, bool isTimeout)
    {
        yield return new WaitForSecondsRealtime(seconds);
        rotateRoutine = null;
        if (isTimeout) Debug.LogWarning($"[장치] {MsgRotateDone} 응답 없음({seconds}s) - 회전된 것으로 처리");
        CompleteRotation(state.rotationIndex + NextStep());
    }

    protected override void ReceivedData(int controllerId, string data)
    {
        // 어느 컨트롤러로 들어왔는지는 구분하지 않는다 (로그에만 남김).
        if (string.IsNullOrEmpty(data)) return;

        string[] parts = data.Trim().Split(',');
        string head = parts[0].Trim().ToUpperInvariant();
        int index;

        switch (head)
        {
            case MsgRotateDone:
                // 단계 값이 없으면 PC가 저장한 단계에서 왕복 순서대로 한 칸 옮긴다.
                index = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : state.rotationIndex + NextStep();
                CompleteRotation(index);
                break;
            case MsgState:
                if (parts.Length > 1 && int.TryParse(parts[1], out index)
                    && index >= 0 && index < TargetLayout.RotationSteps)
                {
                    int before = state.rotationIndex;
                    SetRotationIndex(index);
                    LastStateTime = Time.unscaledTime;
                    // 장치가 알려 준 단계가 PC 저장값과 다르면 장치 값을 쓴다 (어느 쪽이 어긋났는지 추적용 로그).
                    if (before != index) Info($"[단계 확인] PC {before}단계 ↔ 장치 {index}단계 - 장치 값 사용", true);
                    else Info($"[단계 확인] 장치 {index}단계 (PC 저장값과 같음)");
                    OnStateReceived?.Invoke(state.rotationIndex);
                }
                else Info($"[단계 확인] 해석 못 함 - 무시: {data}", true);
                break;
            case MsgError:
                Debug.LogError($"[Ctrl {controllerId}] 장치 오류: {data}");
                break;
            default:
                Debug.LogWarning($"[Ctrl {controllerId}] 알 수 없는 메시지: {data}");
                break;
        }
    }

    private void CompleteRotation(int index)
    {
        if (rotateRoutine != null)
        {
            StopCoroutine(rotateRoutine);
            rotateRoutine = null;
        }

        // 컨트롤러가 알려 준 단계로 왕복 방향도 맞춘다 (끝에 닿으면 NextStep이 알아서 뒤집는다).
        int normalized = Normalize(index);
        if (normalized != state.rotationIndex) state.reverse = normalized < state.rotationIndex;
        SetRotationIndex(normalized);
        if (!IsRotating) return; // 요청하지 않은 ROT_DONE은 단계만 맞춘다.

        IsRotating = false;
        OnRotationDone?.Invoke(state.rotationIndex);
    }

    // 정답 편집 모드에서 지금 원판 위치를 기준 단계로 다시 정할 때도 쓴다.
    public void SetRotationIndex(int index)
    {
        state.rotationIndex = Normalize(index);
        try { JsonManager.SaveData(state, statePath); }
        catch (Exception ex) { Debug.LogError($"장치 상태 저장 실패: {ex.Message}"); }
    }

    private void SendIfOpen(string cmd)
    {
        if (IsDeviceOpen) SendAll(cmd);
        else Debug.Log($"[장치] 열린 포트 없음 - 송신 생략: {cmd}");
    }

    private static int Normalize(int index)
    {
        return ((index % TargetLayout.RotationSteps) + TargetLayout.RotationSteps) % TargetLayout.RotationSteps;
    }
}
