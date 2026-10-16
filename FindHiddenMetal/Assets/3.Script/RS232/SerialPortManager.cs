using System;
using System.Collections.Generic;
using UnityEngine;

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance { get; private set; }

    private readonly List<SerialPortChannel> channels = new List<SerialPortChannel>();

    // RS232 송수신/연결 변화를 외부(UI 등)에 알리기 위한 이벤트
    public event Action<int, string> OnDataReceived;
    public event Action<int, string> OnDataSent;
    public event Action<int, bool> OnConnectionChanged;

    public bool IsControllerOpen(int controllerId)
    {
        var ch = FindChannel(controllerId);
        return ch != null && ch.IsOpen;
    }

    // 열린 포트가 하나라도 있는지 (어느 컨트롤러든 상관없을 때)
    public bool IsAnyOpen
    {
        get
        {
            foreach (var ch in channels) if (ch.IsOpen) return true;
            return false;
        }
    }

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void Start()
    {
        var portJson = JsonManager.instance.portJson;
        if (portJson?.ports == null || portJson.ports.Count == 0)
        {
            Debug.LogError("port.json에 포트 설정이 없습니다.");
            return;
        }

        foreach (var cfg in portJson.ports)
        {
            Debug.Log($"포트 설정 로드됨: id={cfg.controllerId}, COM={cfg.com}, Baud={cfg.baudLate}, enabled={cfg.enabled}");

            // 컨트롤러별 RS232 사용 여부 - 비활성 시 채널 생성/포트 오픈 자체를 건너뜀
            if (!cfg.enabled)
            {
                Debug.Log($"[Ctrl {cfg.controllerId}] RS232 비활성 - 포트 열지 않음 ({cfg.com})");
                continue;
            }

            // 동일 controllerId 중복 시 첫 번째만 사용
            if (FindChannel(cfg.controllerId) != null)
            {
                Debug.LogWarning($"중복된 controllerId 무시: {cfg.controllerId}");
                continue;
            }

            OpenChannel(cfg);
        }
    }

    // port.json 의 현재 값으로 해당 컨트롤러 포트를 닫았다가 다시 연다 (디버그 패널에서 포트를 바꿨을 때).
    // 비활성(enabled=false)이면 닫기만 한다. 열리면 true.
    public bool Reconnect(int controllerId)
    {
        var channel = FindChannel(controllerId);
        if (channel != null)
        {
            channel.Close();
            channels.Remove(channel);
        }

        var cfg = FindConfig(controllerId);
        if (cfg == null)
        {
            Debug.LogWarning($"[Ctrl {controllerId}] port.json에 설정이 없어 재연결하지 않음");
            return false;
        }
        if (!cfg.enabled)
        {
            Debug.Log($"[Ctrl {controllerId}] RS232 비활성 - 포트 닫음");
            OnConnectionChanged?.Invoke(controllerId, false);
            return false;
        }

        Debug.Log($"[Ctrl {controllerId}] 재연결 ({cfg.com}, {cfg.baudLate}bps)");
        return OpenChannel(cfg);
    }

    public static PortConfig FindConfig(int controllerId)
    {
        var ports = JsonManager.instance != null ? JsonManager.instance.portJson?.ports : null;
        if (ports == null) return null;
        foreach (var cfg in ports)
            if (cfg.controllerId == controllerId) return cfg;
        return null;
    }

    private bool OpenChannel(PortConfig cfg)
    {
        var channel = new SerialPortChannel(
            cfg.controllerId, cfg.com, cfg.baudLate,
            OnChannelMessage,
            OnChannelSent,
            OnChannelConnectionChanged);
        channels.Add(channel);
        return channel.Open();
    }

    private void OnChannelMessage(int controllerId, string data)
    {
        OnDataReceived?.Invoke(controllerId, data);
        ReceivedData(controllerId, data);
    }

    private void OnChannelSent(int controllerId, string data)
    {
        OnDataSent?.Invoke(controllerId, data);
    }

    private void OnChannelConnectionChanged(int controllerId, bool isOpen)
    {
        OnConnectionChanged?.Invoke(controllerId, isOpen);
    }

    // 어느 포트(컨트롤러)에서 들어왔는지를 알 수 있도록 controllerId를 함께 전달한다.
    protected virtual void ReceivedData(int controllerId, string data)
    {
        // 상속받은 클래스에서 프로젝트별 처리 구현
    }

    // 특정 컨트롤러의 포트로 송신
    public void SendData(int controllerId, string message)
    {
        var channel = FindChannel(controllerId);
        if (channel == null)
        {
            Debug.LogWarning($"[Ctrl {controllerId}] 채널 미존재 - 송신 실패");
            return;
        }
        channel.Send(message);
    }

    // 열린 모든 포트로 송신 (어느 컨트롤러로 가야 하는지 구분할 필요가 없을 때). 보낸 포트 수를 돌려준다.
    public int SendAll(string message)
    {
        int sent = 0;
        foreach (var ch in channels)
        {
            if (!ch.IsOpen) continue;
            ch.Send(message);
            sent++;
        }
        return sent;
    }

    protected SerialPortChannel FindChannel(int controllerId)
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].ControllerId == controllerId) return channels[i];
        }
        return null;
    }

    private void Cleanup()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            channels[i].Close();
        }
        channels.Clear();
    }

    void OnApplicationQuit()
    {
        Debug.Log("Task 종료");
        Cleanup();
    }

    protected virtual void OnDestroy()
    {
        Cleanup();
    }
}
