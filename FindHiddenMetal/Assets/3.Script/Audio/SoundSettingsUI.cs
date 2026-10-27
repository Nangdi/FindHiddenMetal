using UnityEngine;
using UnityEngine.UI;

// 설정창의 소리 칸: 전체·배경음악·효과음 볼륨 조절바와 배경음악·효과음 음소거.
// 움직이는 동안 바로 들리게 반영하고(SoundManager 가 매 프레임 설정값을 따라감) persistent Setting.json 에 바로 저장한다.
// 음소거해도 볼륨 값은 그대로 두어, 풀면 원래 크기로 돌아온다.
public class SoundSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [Tooltip("조절바 옆 현재 값(%). 비워두면 표시하지 않음")]
    [SerializeField] private Text masterValue;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private Text bgmValue;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private Text sfxValue;
    [SerializeField] private Toggle bgmMuteToggle;
    [SerializeField] private Toggle sfxMuteToggle;

    private bool syncing;

    private static GameDynamicData Config => JsonManager.instance != null ? JsonManager.instance.gameDynamicData : null;

    private void Start()
    {
        Sync();
        Listen(masterSlider, v => Config.masterVolume = v);
        Listen(bgmSlider, v => Config.bgmVolume = v);
        Listen(sfxSlider, v => Config.sfxVolume = v);
        if (bgmMuteToggle != null) bgmMuteToggle.onValueChanged.AddListener(on => Set(() => Config.bgmMuted = on, $"배경음악 음소거 {on}"));
        if (sfxMuteToggle != null) sfxMuteToggle.onValueChanged.AddListener(on => Set(() => Config.sfxMuted = on, $"효과음 음소거 {on}"));
    }

    // 설정창을 열 때마다 저장된 값을 다시 보여 준다
    private void OnEnable()
    {
        if (Config != null) Sync();
    }

    private void Listen(Slider slider, System.Action<float> apply)
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(v => Set(() => apply(v), null));
    }

    private void Set(System.Action apply, string log)
    {
        if (syncing || Config == null) return;
        apply();
        JsonManager.instance.SaveGameDynamicData();
        if (log != null) Debug.Log($"[Settings] {log}");
        ShowValues();
    }

    private void Sync()
    {
        var cfg = Config;
        if (cfg == null) return;
        syncing = true;
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(Mathf.Clamp01(cfg.masterVolume));
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(cfg.bgmVolume));
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(cfg.sfxVolume));
        if (bgmMuteToggle != null) bgmMuteToggle.SetIsOnWithoutNotify(cfg.bgmMuted);
        if (sfxMuteToggle != null) sfxMuteToggle.SetIsOnWithoutNotify(cfg.sfxMuted);
        syncing = false;
        ShowValues();
    }

    private void ShowValues()
    {
        var cfg = Config;
        if (cfg == null) return;
        SetText(masterValue, cfg.masterVolume, false);
        SetText(bgmValue, cfg.bgmVolume, cfg.bgmMuted);
        SetText(sfxValue, cfg.sfxVolume, cfg.sfxMuted);
    }

    private static void SetText(Text t, float volume, bool muted)
    {
        if (t != null) t.text = muted ? "음소거" : $"{Mathf.RoundToInt(Mathf.Clamp01(volume) * 100)}%";
    }
}
