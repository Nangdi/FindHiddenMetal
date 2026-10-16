using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 정답 편집 모드 (운영자용, 대기 화면에서 F3).
// 1) 지금 원판 위치에서 금속이 있는 칸을 목표 개수만큼 터치한다. 이 배치가 회전 0단계 정답이 된다.
// 2) 회전 방향(시계/반시계)을 고른다.
// 3) 원판 회전 테스트: 원판을 돌리고, 고른 방향으로 계산한 예상 위치를 격자에 표시한다.
//    실제 금속 위치와 다르면 방향을 바꿔 본다. 예상 위치를 보는 동안에는 칸을 고칠 수 없다(4번 돌리면 처음 위치).
// 4) 저장: Setting.json(baseTargets, rotateClockwise)에 저장하고, 지금 원판 단계를 회전한 횟수로 맞춘다.
public class EditPanel : PanelBase
{
    [Header("격자 (선택 = Highlight, 예상 위치 = FoundMark)")]
    [SerializeField] private GridBoard board;

    [Header("표시")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text rotateInfoText;
    [Tooltip("안내 문구 강조색 (Kit AccentColor)")]
    [SerializeField] private Color accentColor = Color.red;

    [Header("회전 방향 (고른 쪽의 Highlight를 켠다)")]
    [SerializeField] private Button clockwiseButton;
    [SerializeField] private GameObject clockwiseHighlight;
    [SerializeField] private Button counterClockwiseButton;
    [SerializeField] private GameObject counterClockwiseHighlight;

    [Header("버튼")]
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    public event Action OnRotateRequest;
    // (0단계 정답, 시계 방향 여부, 편집 중 회전한 횟수)
    public event Action<List<GridPos>, bool, int> OnSave;
    public event Action OnCancel;

    public bool IsRotating { get; private set; }

    private readonly List<GridCellView> selected = new List<GridCellView>();
    private int gridSize;
    private int targetCount = 8;
    private bool clockwise = true;
    private int rotations;

    private bool IsPreview => rotations % TargetLayout.RotationSteps != 0;
    private string AccentHex => ColorUtility.ToHtmlStringRGB(accentColor);

    private void Awake()
    {
        if (board != null) board.OnCellClick += OnCellClick;
        if (clockwiseButton != null) clockwiseButton.onClick.AddListener(() => SetClockwise(true));
        if (counterClockwiseButton != null) counterClockwiseButton.onClick.AddListener(() => SetClockwise(false));
        if (rotateButton != null) rotateButton.onClick.AddListener(RequestRotate);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (cancelButton != null) cancelButton.onClick.AddListener(() => { if (!IsRotating) OnCancel?.Invoke(); });
    }

    // current: 지금 저장된 0단계 정답 (처음 선택 상태로 보여 준다)
    public void Begin(int size, IEnumerable<GridPos> current, int count, bool isClockwise)
    {
        gridSize = size;
        targetCount = Mathf.Max(1, count);
        clockwise = isClockwise;
        rotations = 0;
        IsRotating = false;
        selected.Clear();

        if (board != null)
        {
            board.Build(size);
            board.ResetCells();
            if (current != null)
            {
                foreach (var p in current)
                {
                    var cell = board.GetCell(p.x, p.y);
                    if (cell != null && !selected.Contains(cell) && selected.Count < targetCount) selected.Add(cell);
                }
            }
        }
        Refresh();
    }

    // 원판 회전이 끝났을 때 (시간 초과로 넘어간 경우 포함)
    public void RotationDone()
    {
        if (!IsRotating) return;
        IsRotating = false;
        rotations++;
        Refresh();
    }

    private void OnCellClick(GridCellView cell)
    {
        if (IsRotating || IsPreview) return;

        if (!selected.Remove(cell))
        {
            if (selected.Count >= targetCount) return;
            selected.Add(cell);
        }
        Refresh();
    }

    private void SetClockwise(bool value)
    {
        if (IsRotating) return;
        clockwise = value;
        Refresh();
    }

    private void RequestRotate()
    {
        if (IsRotating || selected.Count == 0) return;
        IsRotating = true;
        Refresh();
        OnRotateRequest?.Invoke();
    }

    private void Save()
    {
        if (IsRotating || selected.Count != targetCount) return;
        OnSave?.Invoke(Selection(), clockwise, rotations);
    }

    private List<GridPos> Selection()
    {
        var positions = new List<GridPos>(selected.Count);
        foreach (var c in selected) positions.Add(new GridPos(c.X, c.Y));
        return positions;
    }

    private void Refresh()
    {
        RefreshBoard();

        int step = rotations % TargetLayout.RotationSteps;
        if (statusText != null)
        {
            if (IsRotating)
                statusText.text = "원판이 회전하는 중입니다...";
            else if (IsPreview)
                statusText.text = $"<color=#{AccentHex}>회전 {rotations}번</color> 후 예상 위치입니다.\n실제 금속 위치와 맞는지 확인하세요. 다르면 회전 방향을 바꿔 보세요.";
            else
                statusText.text = $"지금 원판에서 금속이 있는 칸 <color=#{AccentHex}>{targetCount}곳</color>을 터치하세요.\n이 배치가 회전 0단계 정답이 됩니다.";
        }
        if (countText != null) countText.text = $"선택 {selected.Count} / {targetCount}";
        if (rotateInfoText != null)
        {
            rotateInfoText.text = rotations == 0
                ? "원판을 90° 돌려 예상 위치를 확인합니다."
                : step == 0
                    ? $"회전 {rotations}번 - 처음 위치로 돌아왔습니다. 칸을 고칠 수 있습니다."
                    : $"회전 {rotations}번 ({TargetLayout.RotationSteps - step}번 더 돌리면 처음 위치)";
        }

        if (clockwiseHighlight != null) clockwiseHighlight.SetActive(clockwise);
        if (counterClockwiseHighlight != null) counterClockwiseHighlight.SetActive(!clockwise);

        if (clockwiseButton != null) clockwiseButton.interactable = !IsRotating;
        if (counterClockwiseButton != null) counterClockwiseButton.interactable = !IsRotating;
        if (rotateButton != null) rotateButton.interactable = !IsRotating && selected.Count > 0;
        if (saveButton != null) saveButton.interactable = !IsRotating && selected.Count == targetCount;
        if (cancelButton != null) cancelButton.interactable = !IsRotating;
    }

    // 0단계: 고른 칸에 선택 표시 / 회전 후: 고른 방향으로 계산한 예상 위치에 정답 마크
    private void RefreshBoard()
    {
        if (board == null) return;
        foreach (var c in board.Cells)
        {
            c.ClearMarks();
            c.SetSelected(false);
        }

        if (!IsPreview)
        {
            foreach (var c in selected) c.SetSelected(true);
            return;
        }

        foreach (var p in TargetLayout.GetTargets(Selection(), gridSize, rotations, clockwise))
            board.GetCell(p.x, p.y)?.ShowFound(true);
    }
}
