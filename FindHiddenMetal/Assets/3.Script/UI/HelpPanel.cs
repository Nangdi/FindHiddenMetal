using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// F1 단축키 도움말. HotkeyHelp에 등록된 키와 기능을 열 때마다 다시 읽어 보여 준다.
// 이 스크립트는 항상 켜져 있는 오브젝트(캔버스)에 두고, panelRoot를 켜고 끈다.
public class HelpPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [Tooltip("키 이름 열의 너비 (TMP <indent> 값). 설명이 길어 줄이 바뀌어도 이 위치에 맞춘다")]
    [SerializeField] private string keyColumn = "22%";

    private void Awake()
    {
        HotkeyHelp.Register(toggleKey, "단축키 도움말 (이 창) 열기/닫기");
    }

    private void Start()
    {
        SetVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && panelRoot != null)
            SetVisible(!panelRoot.activeSelf);
    }

    public void SetVisible(bool show)
    {
        if (show) Refresh();
        if (panelRoot != null) panelRoot.SetActive(show);
    }

    // 같은 키의 기능은 한 줄에 모은다. 등록 순서대로 표시한다.
    private void Refresh()
    {
        if (listText == null) return;

        var order = new List<KeyCode>();
        var descriptions = new Dictionary<KeyCode, List<string>>();
        foreach (var e in HotkeyHelp.Entries)
        {
            if (!descriptions.TryGetValue(e.key, out var list))
            {
                descriptions[e.key] = list = new List<string>();
                order.Add(e.key);
            }
            list.Add(e.description);
        }

        var sb = new StringBuilder();
        foreach (var key in order)
        {
            foreach (string description in descriptions[key])
            {
                sb.Append("<b>").Append(HotkeyHelp.KeyLabel(key)).Append("</b>");
                sb.Append("<indent=").Append(keyColumn).Append('>').Append(description).Append("</indent>\n");
            }
        }
        listText.text = sb.Length > 0 ? sb.ToString().TrimEnd('\n') : "등록된 단축키가 없습니다.";
    }
}
