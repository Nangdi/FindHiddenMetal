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
// 숨은 금속 찾기 운영값. 파일을 직접 열어 고친다. 정답 배치와 회전 방향은 정답 편집 모드(F3)에서도 바꾼다.
[Serializable]
public class GameDynamicData
{
    // 격자 한 변의 칸 수 (12 → 12x12). 바꾸면 Static_Play/Static_Result.png의 격자 그림도 바꿔야 한다.
    public int gridSize = 12;
    // 제한 시간(초). 0 이하이면 무제한.
    public float timeLimitSeconds = 60f;
    // 체험 방법 → 체험 시작 사이 카운트다운(초). 0 이하이면 바로 시작.
    public int countdownSeconds = 3;
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
    // 회전 0단계일 때 탐지목표물 8개의 격자 좌표. 정답 편집 모드(F3)에서 실제 원판을 보고 입력한다.
    public List<GridPos> baseTargets = new List<GridPos>
    {
        new GridPos(2, 2), new GridPos(5, 1), new GridPos(8, 2), new GridPos(10, 5),
        new GridPos(7, 6), new GridPos(2, 8), new GridPos(5, 10), new GridPos(9, 9),
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

    // 현재 포트 설정을 port.json 파일에 저장합니다 (디버그 패널에서 포트를 바꿨을 때).
    public void SavePortJson()
    {
        SaveData(portJson, portPath);
    }

    // 현재 운영값(정답 배치, 회전 방향 등)을 Setting.json 파일에 저장합니다.
    public void SaveGameDynamicData()
    {
        SaveData(gameDynamicData, gameDynamicDataPath);
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
