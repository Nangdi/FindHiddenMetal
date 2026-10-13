using System;
using UnityEngine;
using UnityEngine.UI;

// 01. 대기화면 - 전시명, 체험 시작 버튼
public class IdlePanel : PanelBase
{
    [SerializeField] private Button startButton;

    public event Action OnStart;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(() => OnStart?.Invoke());
    }
}
