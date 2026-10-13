using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 03. 체험진행 - 격자(여러 칸 선택), 발견 개수, 시간, 선택 개수, 위치 확인 버튼.
// 칸을 최대 maxSelect개까지 고르고 위치 확인을 누르면 OnSubmit(고른 칸 목록).
// 판정 결과는 ShowJudge로 각 칸에 바로 표시한다.
public class PlayPanel : PanelBase
{
    [Header("격자")]
    [SerializeField] private GridCellView cellPrefab;
    [Tooltip("GridLayoutGroup이 붙은 칸 부모")]
    [SerializeField] private Transform gridRoot;

    [Header("표시")]
    [SerializeField] private TMP_Text foundText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("시간 라벨 (진행 시간 / 남은 시간). 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text timeLabelText;
    [Tooltip("고른 칸 개수 (예: 3 / 8). 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text selectCountText;
    [SerializeField] private Button submitButton;

    public event Action<List<GridPos>> OnSubmit;

    private readonly List<GridCellView> cells = new List<GridCellView>();
    private readonly List<GridCellView> selected = new List<GridCellView>();
    private int gridSize;
    private int maxSelect = 1;
    private bool interactable;

    private void Awake()
    {
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
    }

    // 판을 새로 시작할 때 호출한다. 칸 수가 바뀌면 다시 만든다.
    public void Begin(int size, int totalCount, int selectLimit)
    {
        BuildGrid(size);
        foreach (var c in cells) c.ResetEffect();
        maxSelect = Mathf.Max(1, selectLimit);
        ClearSelection();
        SetProgress(0, totalCount);
    }

    public void SetProgress(int found, int total)
    {
        if (foundText != null) foundText.text = $"{found} / {total}";
    }

    // isRemaining: 제한 시간이 있으면 남은 시간, 없으면 진행 시간
    public void SetTime(float seconds, bool isRemaining)
    {
        if (timeText != null) timeText.text = FormatTime(seconds);
        if (timeLabelText != null) timeLabelText.text = isRemaining ? "남은 시간" : "진행 시간";
    }

    // keepFound / keepWrong: 마크를 다음 위치 확인까지 남길지(설정창 값), false면 잠깐 보였다 사라진다.
    public void ShowJudge(int x, int y, JudgeResult result, bool keepFound, bool keepWrong)
    {
        var cell = GetCell(x, y);
        if (cell == null) return;

        if (result == JudgeResult.Wrong) cell.ShowWrong(keepWrong);
        else cell.ShowFound(keepFound); // Correct, AlreadyFound
    }

    // 정답 공개: 찾은 금속은 정답 마크, 못 찾은 금속은 공개 마크로 모두 표시한다.
    public void RevealTargets(IEnumerable<GridPos> targets, Func<int, int, bool> isFound)
    {
        foreach (var c in cells) c.ClearMarks();
        foreach (var p in targets)
        {
            var cell = GetCell(p.x, p.y);
            if (cell == null) continue;
            if (isFound(p.x, p.y)) cell.ShowFound(true);
            else cell.ShowReveal();
        }
        SetInteractable(false);
    }

    // 개발자용: 정답 위치를 격자에 표시하거나 숨긴다.
    public void ShowDebugTargets(IEnumerable<GridPos> targets, bool on)
    {
        foreach (var c in cells) c.SetDebugTarget(false);
        if (!on || targets == null) return;

        foreach (var p in targets)
        {
            var cell = GetCell(p.x, p.y);
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

    private void BuildGrid(int size)
    {
        if (cellPrefab == null || gridRoot == null)
        {
            Debug.LogError("PlayPanel: cellPrefab 또는 gridRoot 미연결");
            return;
        }
        if (size == gridSize && cells.Count == size * size) return;

        foreach (var c in cells) if (c != null) Destroy(c.gameObject);
        cells.Clear();
        selected.Clear();
        gridSize = size;

        var layout = gridRoot.GetComponent<GridLayoutGroup>();
        if (layout != null)
        {
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = size;
        }

        // 위쪽 행부터, 왼쪽 열부터 생성 (GridLayoutGroup 기본 순서와 같음)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = Instantiate(cellPrefab, gridRoot);
                cell.Init(x, y);
                cell.OnClick += OnCellClick;
                cells.Add(cell);
            }
        }
    }

    // 고른 칸을 다시 누르면 선택 해제. 최대 개수를 채웠으면 더 고를 수 없다.
    private void OnCellClick(GridCellView cell)
    {
        if (!interactable || cell.IsLocked) return;

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
        if (submitButton != null) submitButton.interactable = interactable && selected.Count > 0;
        if (selectCountText != null) selectCountText.text = $"고른 칸 <color=#E42C34>{selected.Count}</color> / {maxSelect}";
    }

    private void Submit()
    {
        if (!interactable || selected.Count == 0) return;

        var positions = new List<GridPos>(selected.Count);
        foreach (var c in selected) positions.Add(new GridPos(c.X, c.Y));
        ClearSelection();

        // 이전 위치 확인의 마크는 이번 확인 결과로 바뀐다.
        foreach (var c in cells) c.ClearMarks();
        OnSubmit?.Invoke(positions);
    }

    private GridCellView GetCell(int x, int y)
    {
        int i = y * gridSize + x;
        return x >= 0 && y >= 0 && x < gridSize && y < gridSize && i < cells.Count ? cells[i] : null;
    }
}
