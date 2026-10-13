using System;
using UnityEngine;
using UnityEngine.UI;

// 02. 체험안내 - 탐지기 사용법, 위치 입력 방법 (내용은 정적 레이어), 시작하기 버튼
public class GuidePanel : PanelBase
{
    [SerializeField] private Button confirmButton;

    public event Action OnConfirm;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(() => OnConfirm?.Invoke());
    }
}
