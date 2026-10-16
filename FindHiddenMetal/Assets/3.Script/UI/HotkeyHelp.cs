using System.Collections.Generic;
using UnityEngine;

// 운영·개발용 단축키 목록. 각 기능 스크립트가 실제로 설정된 키와 설명을 등록하고,
// F1 도움말 패널(HelpPanel)은 등록된 목록을 그대로 보여 준다.
// 키를 인스펙터에서 바꿔도 안내가 어긋나지 않도록 패널에 키를 직접 적지 않는다.
public static class HotkeyHelp
{
    public struct Entry
    {
        public KeyCode key;
        public string description;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    public static IReadOnlyList<Entry> Entries => entries;

    // 같은 키에 여러 기능이 걸려 있으면 각각 등록한다 (예: ESC = 설정창 + 통신 디버그 패널).
    public static void Register(KeyCode key, string description)
    {
        if (key == KeyCode.None || string.IsNullOrEmpty(description)) return;
        foreach (var e in entries)
            if (e.key == key && e.description == description) return;
        entries.Add(new Entry { key = key, description = description });
    }

    public static string KeyLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Escape: return "ESC";
            case KeyCode.Return: return "Enter";
            default: return key.ToString();
        }
    }

    // 플레이 모드 재진입 시(도메인 리로드 없이) 이전 목록이 남지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Clear() => entries.Clear();
}
