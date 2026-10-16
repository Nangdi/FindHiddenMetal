using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임씬에 미리 만들어진 Canvas/UI 위에서 동작하는 로그 패널.
// Inspector에서 텍스트/스크롤뷰/버튼을 직접 연결한다.
// 포트 설정: 컨트롤러를 고르고 지금 PC에 있는 COM 포트를 고르면 port.json(persistent)에 저장하고 바로 다시 연다.
// 재연결 버튼은 COM 목록을 새로 읽고 선택한 컨트롤러 포트를 다시 연다.
public class SerialPortLogPanel : MonoBehaviour
{
    [Header("연결 상태 텍스트 (인덱스 0~4 = Ctrl 1~5)")]
    [SerializeField] private TMP_Text[] connectionStatusTexts;

    [Header("로그 표시")]
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect logScrollRect;

    [Header("버튼")]
    [SerializeField] private Button clearButton;

    [Header("포트 설정 (비워두면 표시하지 않음)")]
    [Tooltip("port.json 의 컨트롤러 목록. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Dropdown controllerDropdown;
    [Tooltip("'사용 안 함' + 지금 PC에 있는 COM 포트. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Dropdown comDropdown;
    [Tooltip("COM 목록 새로고침 + 선택한 컨트롤러 재연결. 비워두면 표시하지 않음")]
    [SerializeField] private Button reconnectButton;
    [Tooltip("패널을 열었을 때 처음 고를 컨트롤러 id")]
    [SerializeField] private int defaultControllerId = 1;

    [Header("수동 송신 (선택한 컨트롤러로 보냄, 비워두면 표시하지 않음)")]
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private TMP_InputField sendInput;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private Button sendButton;

    [Header("토글 (ESC 키로 표시/숨김)")]
    [SerializeField] private GameObject togglePanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [Tooltip("시작할 때 로그 창을 숨긴 상태로 둘지 여부.")]
    [SerializeField] private bool hideOnStart = true;

    [Header("동작 설정")]
    [SerializeField] private int maxLogLines = 200;

    [Header("상태 색상")]
    [SerializeField] private Color openColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color closeColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color disabledColor = new Color(0.7f, 0.7f, 0.7f);

    private readonly Queue<string> logLines = new Queue<string>();
    private readonly StringBuilder builder = new StringBuilder();

    private SerialPortManager manager;
    private bool subscribed;

    private const string DisabledOption = "사용 안 함";
    private const string MissingSuffix = " (연결 안 됨)";
    private readonly List<int> controllerIds = new List<int>();
    private readonly List<string> comOptions = new List<string>();

    private void Start()
    {
        HotkeyHelp.Register(toggleKey, "통신(RS232) 디버그 패널 열기/닫기");

        if (clearButton != null) clearButton.onClick.AddListener(ClearLog);
        if (controllerDropdown != null) controllerDropdown.onValueChanged.AddListener(_ => RefreshComOptions());
        if (comDropdown != null) comDropdown.onValueChanged.AddListener(ApplyCom);
        if (reconnectButton != null) reconnectButton.onClick.AddListener(ReconnectSelected);
        if (sendButton != null) sendButton.onClick.AddListener(SendManual);
        if (sendInput != null) sendInput.onSubmit.AddListener(_ => SendManual());
        BuildControllerOptions();
        TrySubscribe();
        RefreshAllStatus();

        if (hideOnStart && togglePanel != null)
            togglePanel.SetActive(false);
    }

    private void Update()
    {
        // SerialPortManager.Instance가 늦게 만들어지는 경우 대비
        if (!subscribed) TrySubscribe();

        if (Input.GetKeyDown(toggleKey) && togglePanel != null)
        {
            togglePanel.SetActive(!togglePanel.activeSelf);
            // 열 때마다 COM 목록을 새로 읽는다 (USB-Serial을 꽂고 뺀 경우)
            if (togglePanel.activeSelf) RefreshComOptions();
        }
    }

    private void OnDestroy()
    {
        if (clearButton != null) clearButton.onClick.RemoveListener(ClearLog);
        if (!subscribed || manager == null) return;
        manager.OnDataReceived -= HandleReceived;
        manager.OnDataSent -= HandleSent;
        manager.OnConnectionChanged -= HandleConnectionChanged;
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        manager = SerialPortManager.Instance;
        if (manager == null) return;

        manager.OnDataReceived += HandleReceived;
        manager.OnDataSent += HandleSent;
        manager.OnConnectionChanged += HandleConnectionChanged;
        subscribed = true;
        RefreshAllStatus();
    }

    private void HandleReceived(int id, string data) => AppendLog($"[Receive][Ctrl {id}] {data}");
    private void HandleSent(int id, string data) => AppendLog($"[Send][Ctrl {id}] {data}");

    private void HandleConnectionChanged(int id, bool isOpen)
    {
        UpdateStatus(id, isOpen, IsEnabled(id));
        AppendLog($"[Connect][Ctrl {id}] {(isOpen ? "OPEN" : "CLOSE")}");
    }

    private bool IsEnabled(int controllerId)
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson == null || portJson.ports == null) return true;
        foreach (var cfg in portJson.ports)
        {
            if (cfg.controllerId == controllerId) return cfg.enabled;
        }
        return true;
    }

    private void RefreshAllStatus()
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson == null || portJson.ports == null) return;

        foreach (var cfg in portJson.ports)
        {
            bool open = manager != null && manager.IsControllerOpen(cfg.controllerId);
            UpdateStatus(cfg.controllerId, open, cfg.enabled);
        }
    }

    private void UpdateStatus(int controllerId, bool isOpen, bool enabled)
    {
        if (connectionStatusTexts == null) return;
        int idx = controllerId - 1;
        if (idx < 0 || idx >= connectionStatusTexts.Length) return;
        var t = connectionStatusTexts[idx];
        if (t == null) return;

        string com = "";
        int baud = 0;
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson != null && portJson.ports != null)
        {
            foreach (var cfg in portJson.ports)
            {
                if (cfg.controllerId == controllerId)
                {
                    com = cfg.com;
                    baud = cfg.baudLate;
                    break;
                }
            }
        }

        string state;
        Color color;
        if (!enabled) { state = "DISABLED"; color = disabledColor; }
        else if (isOpen) { state = "OPEN"; color = openColor; }
        else { state = "CLOSE"; color = closeColor; }

        t.text = $"Ctrl {controllerId}  {com}  {baud}bps  [{state}]";
        t.color = color;
    }

    private void AppendLog(string line)
    {
        string formatted = $"{DateTime.Now:HH:mm:ss} {line}";
        logLines.Enqueue(formatted);
        while (logLines.Count > maxLogLines) logLines.Dequeue();

        builder.Clear();
        foreach (var l in logLines) builder.AppendLine(l);
        if (logText != null) logText.text = builder.ToString();

        // ContentSizeFitter 재계산 후, Content가 Viewport를 넘었을 때만 끝으로 스크롤.
        // 항상 0으로 강제하면 로그가 적어 Content가 Viewport보다 짧을 때 위로 튕겨 올라가는 현상이 생긴다.
        if (logScrollRect != null && logScrollRect.content != null && logScrollRect.viewport != null)
        {
            Canvas.ForceUpdateCanvases();
            if (logScrollRect.content.rect.height > logScrollRect.viewport.rect.height)
            {
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    #region 포트 설정

    private int SelectedControllerId =>
        controllerDropdown != null && controllerDropdown.value >= 0 && controllerDropdown.value < controllerIds.Count
            ? controllerIds[controllerDropdown.value]
            : defaultControllerId;

    private void BuildControllerOptions()
    {
        controllerIds.Clear();
        var ports = JsonManager.instance != null ? JsonManager.instance.portJson?.ports : null;
        if (ports != null)
            foreach (var cfg in ports)
                if (!controllerIds.Contains(cfg.controllerId)) controllerIds.Add(cfg.controllerId);

        if (controllerDropdown != null)
        {
            controllerDropdown.ClearOptions();
            controllerDropdown.AddOptions(controllerIds.Select(id => $"Ctrl {id}").ToList());
            int index = controllerIds.IndexOf(defaultControllerId);
            controllerDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
        }
        RefreshComOptions();
    }

    // "사용 안 함" + 지금 PC의 COM 포트. 저장된 포트가 목록에 없으면 "(연결 안 됨)"으로 붙여 현재 값을 보여 준다.
    private void RefreshComOptions()
    {
        if (comDropdown == null) return;

        comOptions.Clear();
        comOptions.Add(DisabledOption);
        try
        {
            comOptions.AddRange(SerialPort.GetPortNames().Distinct().OrderBy(n => n.Length).ThenBy(n => n));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"COM 포트 목록을 읽지 못함: {ex.Message}");
        }

        var cfg = SerialPortManager.FindConfig(SelectedControllerId);
        int selected = 0;
        if (cfg != null && cfg.enabled)
        {
            selected = comOptions.IndexOf(cfg.com);
            if (selected < 0)
            {
                comOptions.Add(cfg.com + MissingSuffix);
                selected = comOptions.Count - 1;
            }
        }

        comDropdown.ClearOptions();
        comDropdown.AddOptions(comOptions);
        comDropdown.SetValueWithoutNotify(selected);
    }

    // COM 포트를 고르면 port.json 에 저장하고 바로 다시 연다.
    private void ApplyCom(int index)
    {
        int id = SelectedControllerId;
        var cfg = SerialPortManager.FindConfig(id);
        if (cfg == null || index < 0 || index >= comOptions.Count) return;

        string option = comOptions[index];
        if (option == DisabledOption)
        {
            cfg.enabled = false;
        }
        else
        {
            cfg.com = option.EndsWith(MissingSuffix) ? option.Substring(0, option.Length - MissingSuffix.Length) : option;
            cfg.enabled = true;
        }

        if (JsonManager.instance != null) JsonManager.instance.SavePortJson();
        AppendLog($"[Setting][Ctrl {id}] {(cfg.enabled ? cfg.com : DisabledOption)} 저장 - 재연결");
        Reconnect(id);
    }

    private void ReconnectSelected()
    {
        RefreshComOptions();
        int id = SelectedControllerId;
        AppendLog($"[Setting][Ctrl {id}] 재연결");
        Reconnect(id);
    }

    private void Reconnect(int controllerId)
    {
        if (manager == null) TrySubscribe();
        if (manager == null)
        {
            AppendLog($"[Setting][Ctrl {controllerId}] SerialPortManager 없음 - 재연결 못 함");
            return;
        }
        manager.Reconnect(controllerId);
        RefreshAllStatus();
    }

    private void SendManual()
    {
        if (sendInput == null || string.IsNullOrWhiteSpace(sendInput.text)) return;
        int id = SelectedControllerId;
        string message = sendInput.text.Trim();

        if (manager == null || !manager.IsControllerOpen(id))
            AppendLog($"[Send 실패][Ctrl {id}] 포트가 열려 있지 않음: {message}");
        else
            manager.SendData(id, message); // 송신 로그는 OnDataSent 로 남는다

        sendInput.text = string.Empty;
        sendInput.ActivateInputField();
    }

    #endregion

    public void ClearLog()
    {
        logLines.Clear();
        if (logText != null) logText.text = string.Empty;
    }
}
