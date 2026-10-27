using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// 배경음악(BGM)·효과음(SFX) 관리. 소리 파일은 현장에서 바꿀 수 있게 StreamingAssets/Audio 에서 실행 중에 읽는다.
//  - StreamingAssets/Audio/sound.json : 이름(키) → 파일 이름 (+ 개별 볼륨). 파일은 wav / ogg / mp3
//  - 볼륨·음소거는 Setting.json(masterVolume, bgmVolume, sfxVolume, bgmMuted, sfxMuted)을 매 프레임 따라가므로
//    설정창(SoundSettingsUI)에서 바꾸면 바로 반영된다. 음소거 중에도 BGM 은 계속 재생되어 풀면 이어서 들린다.
//  - 파일이나 이름이 없으면 소리 없이 넘어가고 로그만 남긴다 (앱은 멈추지 않는다).
// 사용: SoundManager.PlayBgm("idle"), SoundManager.PlaySfx("button"), SoundManager.StopBgm()
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class SoundEntry
    {
        public string key;
        public string file;
        public float volume = 1f;
    }

    [Serializable]
    public class SoundTable
    {
        public List<SoundEntry> bgm = new List<SoundEntry>();
        public List<SoundEntry> sfx = new List<SoundEntry>();
    }

    public const string Folder = "Audio";
    public const string TableFile = "sound.json";

    public static SoundManager Instance { get; private set; }

    [Tooltip("같은 효과음을 이 시간(초) 안에 다시 부르면 무시 (짧은 시간에 같은 소리가 겹쳐 커지지 않게)")]
    [SerializeField] private float sfxRepeatGuardSeconds = 0.08f;

    public bool Loaded { get; private set; }

    private readonly Dictionary<string, (AudioClip clip, float volume)> bgm = new Dictionary<string, (AudioClip, float)>();
    private readonly Dictionary<string, (AudioClip clip, float volume)> sfx = new Dictionary<string, (AudioClip, float)>();
    private readonly Dictionary<string, float> lastSfxTime = new Dictionary<string, float>();
    private readonly HashSet<string> warned = new HashSet<string>();

    // BGM 은 두 소스를 번갈아 써서 겹쳐 넘어간다
    private AudioSource[] bgmSources;
    private AudioSource sfxSource;
    private int activeBgm;
    private string currentBgm;
    private string pendingBgm;
    private float fade = 1f;
    private float[] entryVolume = { 1f, 1f };

    private static GameDynamicData Config => JsonManager.instance != null ? JsonManager.instance.gameDynamicData : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("SoundManager 가 씬에 2개 이상 있습니다.", this);
            return;
        }
        Instance = this;

        bgmSources = new[] { CreateSource("Bgm_A", true), CreateSource("Bgm_B", true) };
        sfxSource = CreateSource("Sfx", false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator Start()
    {
        yield return Load();
    }

    private AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    #region 불러오기

    private IEnumerator Load()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, Folder);
        string tablePath = Path.Combine(folder, TableFile);
        SoundTable table = null;
        try
        {
            if (File.Exists(tablePath)) table = JsonUtility.FromJson<SoundTable>(File.ReadAllText(tablePath));
            else Debug.LogWarning($"[Sound] 소리 목록이 없습니다 - 소리 없이 진행: {tablePath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Sound] 소리 목록을 읽지 못함 - 소리 없이 진행: {tablePath} ({e.Message})");
        }

        if (table != null)
        {
            foreach (var entry in table.bgm) yield return LoadClip(folder, entry, bgm);
            foreach (var entry in table.sfx) yield return LoadClip(folder, entry, sfx);
            Debug.Log($"[Sound] 불러옴: BGM {bgm.Count}개, 효과음 {sfx.Count}개 ({folder})");
        }

        Loaded = true;
        // 불러오는 동안 요청된 BGM 을 이제 튼다
        if (!string.IsNullOrEmpty(pendingBgm)) PlayBgmInternal(pendingBgm);
    }

    private static IEnumerator LoadClip(string folder, SoundEntry entry, Dictionary<string, (AudioClip, float)> into)
    {
        if (entry == null || string.IsNullOrEmpty(entry.key) || string.IsNullOrEmpty(entry.file)) yield break;
        string path = Path.Combine(folder, entry.file);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Sound] 파일 없음 - '{entry.key}' 소리 없이 진행: {path}");
            yield break;
        }

        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, TypeOf(path)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Sound] 파일을 읽지 못함 - '{entry.key}' 소리 없이 진행: {path} ({request.error})");
                yield break;
            }
            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = entry.key;
            into[entry.key] = (clip, Mathf.Clamp01(entry.volume));
        }
    }

    private static AudioType TypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.WAV;
        }
    }

    #endregion

    #region 재생

    public static void PlayBgm(string key) => Instance?.PlayBgmInternal(key);
    public static void StopBgm() => Instance?.PlayBgmInternal(null);
    public static void PlaySfx(string key) => Instance?.PlaySfxInternal(key);

    private void PlayBgmInternal(string key)
    {
        if (!Loaded)
        {
            pendingBgm = key;
            return;
        }
        if (key == currentBgm) return;
        currentBgm = key;

        // 지금 곡은 줄어들고, 다른 소스에서 새 곡이 커진다
        activeBgm = 1 - activeBgm;
        AudioSource next = bgmSources[activeBgm];
        next.Stop();
        next.clip = null;
        if (!string.IsNullOrEmpty(key))
        {
            if (bgm.TryGetValue(key, out var entry))
            {
                next.clip = entry.clip;
                entryVolume[activeBgm] = entry.volume;
                next.Play();
            }
            else Warn($"BGM '{key}' 이 없습니다 (sound.json)");
        }
        fade = 0f;
        Debug.Log($"[Sound] BGM: {(string.IsNullOrEmpty(key) ? "끔" : key)}");
    }

    private void PlaySfxInternal(string key)
    {
        if (string.IsNullOrEmpty(key) || !Loaded) return;
        if (!sfx.TryGetValue(key, out var entry))
        {
            Warn($"효과음 '{key}' 이 없습니다 (sound.json)");
            return;
        }
        if (lastSfxTime.TryGetValue(key, out float last) && Time.unscaledTime - last < sfxRepeatGuardSeconds) return;
        lastSfxTime[key] = Time.unscaledTime;

        var cfg = Config;
        if (cfg != null && cfg.sfxMuted) return;
        float volume = cfg != null ? Mathf.Clamp01(cfg.masterVolume) * Mathf.Clamp01(cfg.sfxVolume) : 1f;
        sfxSource.PlayOneShot(entry.clip, entry.volume * volume);
        Debug.Log($"[Sound] 효과음: {key}");
    }

    private void Update()
    {
        var cfg = Config;
        if (cfg == null || bgmSources == null) return;

        float fadeSeconds = Mathf.Max(0.01f, cfg.bgmFadeSeconds);
        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / fadeSeconds);
        float volume = cfg.bgmMuted ? 0f : Mathf.Clamp01(cfg.masterVolume) * Mathf.Clamp01(cfg.bgmVolume);
        for (int i = 0; i < bgmSources.Length; i++)
        {
            float level = i == activeBgm ? fade : 1f - fade;
            bgmSources[i].volume = volume * entryVolume[i] * level;
            if (i != activeBgm && level <= 0f && bgmSources[i].isPlaying) bgmSources[i].Stop();
        }
    }

    // 같은 경고는 한 번만 남긴다
    private void Warn(string message)
    {
        if (warned.Add(message)) Debug.LogWarning("[Sound] " + message);
    }

    #endregion
}
