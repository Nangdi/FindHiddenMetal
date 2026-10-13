using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 설정창(SettingsPanelUI)에 노출되는 런타임 설정.
// public bool 필드를 추가하면 설정창에서 토글로 다룰 수 있다.
[Serializable]
public class GameSettingData
{
    // Unity 빌드 창을 항상 맨 위로 띄울지 여부.
    public bool useUnityOnTop;
    // 마우스 커서 표시 여부. false면 커서를 숨긴다(키오스크/전시용).
    public bool showMouseCursor;
    // 위치 확인 후 정답 표시를 다음 위치 확인까지 남길지 여부. false면 잠깐 보였다 사라진다.
    public bool keepFoundMark = true;
    // 위치 확인 후 오답 표시를 다음 위치 확인까지 남길지 여부. false면 잠깐 보였다 사라진다.
    public bool keepWrongMark = true;
}

// 격자 좌표. x = 열(0 = 왼쪽, A열), y = 행(0 = 위쪽, 1행).
[Serializable]
public class GridPos
{
    public int x;
    public int y;

    public GridPos() { }
    public GridPos(int x, int y) { this.x = x; this.y = y; }
}

// 프로젝트별로 실행 중에 바뀌는 데이터를 담는 곳 (Setting.json).
// 숨은 금속 찾기 운영값. 파일을 직접 열어 고친다.
[Serializable]
public class GameDynamicData
{
    // 격자 한 변의 칸 수 (16 → 16x16)
    public int gridSize = 16;
    // 제한 시간(초). 0 이하이면 무제한.
    public float timeLimitSeconds = 0f;
    // 오답 시 기록에 더하는 시간(초). 제한 시간이 있으면 남은 시간이 그만큼 줄어든다.
    public float wrongPenaltySeconds = 5f;
    // 체험 중 무입력 시 대기화면 복귀(초). 0 이하이면 사용 안 함.
    public float idleTimeoutSeconds = 90f;
    // 결과 화면 자동 종료(초)
    public float resultAutoCloseSeconds = 20f;
    // 원판 회전 완료 신호(ROT_DONE)를 기다리는 최대 시간(초). 넘으면 회전된 것으로 보고 진행.
    public float rotateTimeoutSeconds = 15f;
    // 원판 회전 방향 (위에서 봤을 때 시계 방향이면 true)
    public bool rotateClockwise = true;
    // 랭킹 보관 기간(일). 지난 기록은 삭제한다.
    public int rankingKeepDays = 3;
    // 랭킹 표시 인원 (미정, 임시값)
    public int rankingShowCount = 5;
    // 금속탐지 장치 컨트롤러 id (port.json 의 controllerId)
    public int deviceControllerId = 1;
    // 회전 0단계일 때 탐지목표물 8개의 격자 좌표. 실제 장치 배치에 맞춰 수정.
    public List<GridPos> baseTargets = new List<GridPos>
    {
        new GridPos(2, 3), new GridPos(6, 1), new GridPos(11, 2), new GridPos(13, 6),
        new GridPos(9, 8), new GridPos(3, 10), new GridPos(7, 13), new GridPos(12, 12),
    };
}

[Serializable]
public class PortConfig
{
    public int controllerId;
    public string com;
    public int baudLate;
    // RS232 사용 여부. false면 해당 컨트롤러는 포트를 열지 않는다.
    public bool enabled = true;
}

[Serializable]
public class PortJson
{
    // 컨트롤러별 전용 포트 목록. 필요한 만큼 port.json 에서 늘려 쓴다.
    public List<PortConfig> ports = new List<PortConfig>
    {
        new PortConfig { controllerId = 1, com = "COM1", baudLate = 19200, enabled = false },
    };
}

[Serializable]
public class TcpJson
{
    // 메인 서버 접속 주소와 포트
    public string host = "127.0.0.1";
    public int port = 5000;
    // 끊겼을 때 재시도 간격(초). 0 이하이면 재연결 시도하지 않음.
    public float reconnectIntervalSeconds = 3f;
    // 송신 메시지 끝에 줄바꿈을 자동으로 붙일지 여부
    public bool appendOutgoingLineEnding = true;
}

public class JsonManager : MonoBehaviour
{
    public static JsonManager instance;
    public GameSettingData gameSettingData = new GameSettingData();
    public PortJson portJson = new PortJson();
    public TcpJson tcpJson = new TcpJson();
    public GameDynamicData gameDynamicData = new GameDynamicData();

    private string gameDataPath;
    private string gameDynamicDataPath;
    private string portPath;
    private string tcpPath;

    public string GameDataPath => gameDataPath;
    // 설정 파일이 모여 있는 폴더 (설정창에 표시용)
    public static string ConfigFolder => Application.persistentDataPath;

    // 싱글톤을 초기화하고 JSON 파일에서 런타임 설정 데이터를 불러옵니다.
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 설정 파일은 모두 persistentDataPath 에 둔다 (빌드를 덮어써도 현장 값 유지).
        // StreamingAssets 의 같은 이름 파일은 첫 실행 때 복사해 오는 기본값 원본이다.
        portPath = Path.Combine(ConfigFolder, "port.json");
        tcpPath = Path.Combine(ConfigFolder, "tcp.json");
        gameDynamicDataPath = Path.Combine(ConfigFolder, "Setting.json");
        gameDataPath = Path.Combine(ConfigFolder, "gameSettingData.json");

        gameSettingData ??= new GameSettingData();
        gameDynamicData ??= new GameDynamicData();
        portJson ??= new PortJson();
        tcpJson ??= new TcpJson();

        gameSettingData = LoadConfig(gameDataPath, gameSettingData);
        gameDynamicData = LoadConfig(gameDynamicDataPath, gameDynamicData);
        portJson = LoadConfig(portPath, portJson);
        tcpJson = LoadConfig(tcpPath, tcpJson);
        Debug.Log($"설정 폴더: {ConfigFolder}");
    }

    // persistentDataPath 설정 파일을 읽는다.
    // 없으면 StreamingAssets 의 같은 이름 파일을 복사해 오고, 그것도 없으면 코드 기본값으로 만든다.
    // 읽은 뒤 다시 저장해서 새로 추가된 필드도 파일에 기본값으로 채운다.
    // 읽기에 실패하면 기본값으로 실행한다 (파일은 덮어쓰지 않음).
    public static T LoadConfig<T>(string path, T data) where T : new()
    {
        if (data == null) data = new T();

        try
        {
            if (!File.Exists(path))
            {
                string source = Path.Combine(Application.streamingAssetsPath, Path.GetFileName(path));
                if (File.Exists(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(source, path);
                    Debug.Log($"기본 설정 복사: {source} -> {path}");
                }
            }

            data = LoadData(path, data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"설정 파일 읽기 실패 - 기본값으로 실행: {path}\n{ex.Message}");
            return data;
        }

        try { SaveData(data, path); }
        catch (Exception ex) { Debug.LogError($"설정 파일 저장 실패: {path}\n{ex.Message}"); }
        return data;
    }

    // 현재 게임 설정 데이터를 gameSettingData.json 파일에 저장합니다.
    public void SaveGameSettingData()
    {
        SaveData(gameSettingData, gameDataPath);
    }

    // 지정한 경로에 JSON 파일을 생성하거나 덮어씁니다.
    public static void SaveData<T>(T jsonObject, string path) where T : new()
    {
        if (jsonObject == null)
            jsonObject = new T();

        string directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        string json = JsonUtility.ToJson(jsonObject, true);
        File.WriteAllText(path, json);
        Debug.Log($"Saved JSON: {path}");
    }

    // JSON 파일을 읽고, 파일이 없으면 기본값으로 새 파일을 만듭니다.
    // FromJsonOverwrite를 사용해 json에 없는 필드는 인스턴스 기본값을 유지한다.
    // (예: 기존 port.json에 enabled 필드가 없어도 PortConfig.enabled = true 가 보존됨)
    public static T LoadData<T>(string path, T data) where T : new()
    {
        if (data == null) data = new T();

        if (!File.Exists(path))
        {
            Debug.LogWarning($"JSON file does not exist. Creating a new file: {path}");
            SaveData(data, path);
            return data;
        }

        Debug.Log($"Loaded JSON: {path}");
        string json = File.ReadAllText(path);
        JsonUtility.FromJsonOverwrite(json, data);
        return data;
    }
}
