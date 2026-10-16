using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 04. 탐색/위치 입력 - 격자(여러 칸 선택), 시간, 선택 개수, 정답 확인 버튼, HOME 버튼.
// 칸을 maxSelect개(목표 개수) 모두 골라야 정답 확인을 누를 수 있고, 누르면 OnSubmit(고른 칸 목록).
// 정답 확인은 한 번뿐이며 판정 결과는 결과 화면에서 보여 준다.
public class PlayPanel : PanelBase
{
    [Header("격자")]
    [SerializeField] private GridBoard board;

    [Header("표시")]
    [Tooltip("초 단위 시간 (제한 시간이 있으면 남은 시간, 없으면 진행 시간). 주황 원은 정적 레이어")]
    [SerializeField] private TMP_Text timeText;
    [Tooltip("고른 칸 개수. '/ 8'은 정적 레이어에 있다")]
    [SerializeField] private TMP_Text selectCountText;

    [Header("버튼")]
    [SerializeField] private Button submitButton;
    [SerializeField] private Button homeButton;

    public event Action<List<GridPos>> OnSubmit;
    public event Action OnHome;

    private readonly List<GridCellView> selected = new List<GridCellView>();
    private int maxSelect = 1;
    private bool interactable;

    private void Awake()
    {
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (homeButton != null) homeButton.onClick.AddListener(() => OnHome?.Invoke());
        if (board != null) board.OnCellClick += OnCellClick;
    }

    // 판을 새로 시작할 때 호출한다. 칸 수가 바뀌면 다시 만든다.
    public void Begin(int size, int selectLimit)
    {
        if (board != null)
        {
            board.Build(size);
            board.ResetCells();
        }
        maxSelect = Mathf.Max(1, selectLimit);
        ClearSelection();
    }

    // limit > 0이면 남은 시간(올림), 아니면 진행 시간(내림)을 초 단위로 표시한다.
    public void SetTime(float seconds, float limit)
    {
        if (timeText == null) return;
        int shown = limit > 0f ? Mathf.CeilToInt(Mathf.Max(0f, limit - seconds)) : Mathf.FloorToInt(seconds);
        timeText.text = shown.ToString();
    }

    // 지금 고른 칸 목록 (시간이 끝났을 때 자동 제출용)
    public List<GridPos> GetSelection()
    {
        var positions = new List<GridPos>(selected.Count);
        foreach (var c in selected) positions.Add(new GridPos(c.X, c.Y));
        return positions;
    }

    // 개발자용: 정답 위치를 격자에 표시하거나 숨긴다.
    public void ShowDebugTargets(IEnumerable<GridPos> targets, bool on)
    {
        if (board == null) return;
        foreach (var c in board.Cells) c.SetDebugTarget(false);
        if (!on || targets == null) return;

        foreach (var p in targets)
        {
            var cell = board.GetCell(p.x, p.y);
            if (cell != null) cell.SetDebugTarget(true);
        }
    }

    public void SetInteractable(bool on)
    {
        interactable = on;
        if (!on) ClearSelection();
        RefreshSelectionUI();
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    // 고른 칸을 다시 누르면 선택 해제. 최대 개수를 채웠으면 더 고를 수 없다.
    private void OnCellClick(GridCellView cell)
    {
        if (!interactable) return;

        if (selected.Remove(cell)) cell.SetSelected(false);
        else if (selected.Count < maxSelect)
        {
            selected.Add(cell);
            cell.SetSelected(true);
        }
        RefreshSelectionUI();
    }

    private void ClearSelection()
    {
        foreach (var c in selected) if (c != null) c.SetSelected(false);
        selected.Clear();
        RefreshSelectionUI();
    }

    private void RefreshSelectionUI()
    {
        if (submitButton != null) submitButton.interactable = interactable && selected.Count >= maxSelect;
        if (selectCountText != null) selectCountText.text = selected.Count.ToString();
    }

    private void Submit()
    {
        if (!interactable || selected.Count < maxSelect) return;
        OnSubmit?.Invoke(GetSelection());
    }
}
