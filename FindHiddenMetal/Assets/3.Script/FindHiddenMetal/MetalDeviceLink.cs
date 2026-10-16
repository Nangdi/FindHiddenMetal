using System;
using System.Collections;
using System.IO;
using UnityEngine;

[Serializable]
public class DeviceStateData
{
    // 회전 원판의 현재 단계 (0~3). 재실행해도 정답 좌표가 맞도록 저장한다.
    public int rotationIndex;
}

// 금속탐지 장치 컨트롤러와의 RS232 통신.
// SerialPortManager 대신 이 컴포넌트를 씬에 둔다. 규약은 Docs/ControllerProtocol.md 참고.
//
// PC → 컨트롤러 : LED_ON / LED_OFF / ROTATE / STATE?
// 컨트롤러 → PC : ROT_DONE[,n] / STATE,n / ERR,내용
//
// 컨트롤러 id는 구분하지 않는다: 열린 어느 포트로 들어온 메시지든 똑같이 처리하고, 명령은 열린 모든 포트로 보낸다.
// 열린 포트가 없으면(장치 없음, port.json 비활성) 회전을 흉내 내어 체험 흐름이 멈추지 않게 한다.
public class MetalDeviceLink : SerialPortManager
{
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

    public int RotationIndex => state.rotationIndex;
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
        if (IsDeviceOpen) SendAll(CmdState);
    }

    public void LedOn() => SendIfOpen(CmdLedOn);
    public void LedOff() => SendIfOpen(CmdLedOff);

    // 원판을 90도 돌린다. 완료되면 OnRotationDone.
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
        CompleteRotation(state.rotationIndex + 1);
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
                // 단계 값이 없으면 PC가 저장한 단계에서 하나 올린다.
                index = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : state.rotationIndex + 1;
                CompleteRotation(index);
                break;
            case MsgState:
                if (parts.Length > 1 && int.TryParse(parts[1], out index))
                {
                    SetRotationIndex(index);
                    Debug.Log($"[Ctrl {controllerId}] 원판 단계 동기화: {state.rotationIndex}");
                }
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

        SetRotationIndex(index);
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
