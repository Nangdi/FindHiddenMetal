using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 격자 칸을 gridSize x gridSize만큼 만들어 이 영역(RectTransform)에 꽉 채운다.
// 격자 선과 칸 번호는 정적 레이어 그림에 있고, 칸 오브젝트는 그 위에 겹쳐 터치/마크만 담당한다.
// 칸 크기 = (영역 - padding*2 - spacing*(n-1)) / n. padding/spacing은 GridLayoutGroup 값(= 격자 선 두께)을 쓴다.
[RequireComponent(typeof(GridLayoutGroup))]
public class GridBoard : MonoBehaviour
{
    [SerializeField] private GridCellView cellPrefab;

    private readonly List<GridCellView> cells = new List<GridCellView>();
    private int size;

    public IReadOnlyList<GridCellView> Cells => cells;
    public event Action<GridCellView> OnCellClick;

    // 칸 수가 바뀌었을 때만 다시 만든다.
    public void Build(int gridSize)
    {
        if (cellPrefab == null)
        {
            Debug.LogError($"{name}: cellPrefab 미연결");
            return;
        }
        if (gridSize == size && cells.Count == gridSize * gridSize) return;

        foreach (var c in cells) if (c != null) Destroy(c.gameObject);
        cells.Clear();
        size = gridSize;

        var layout = GetComponent<GridLayoutGroup>();
        Vector2 area = ((RectTransform)transform).rect.size;
        float w = (area.x - layout.padding.horizontal - layout.spacing.x * (size - 1)) / size;
        float h = (area.y - layout.padding.vertical - layout.spacing.y * (size - 1)) / size;
        layout.cellSize = new Vector2(w, h);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = size;

        // 위쪽 행부터, 왼쪽 열부터 생성 (GridLayoutGroup 기본 순서와 같음)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = Instantiate(cellPrefab, transform);
                cell.Init(x, y);
                cell.OnClick += c => OnCellClick?.Invoke(c);
                cells.Add(cell);
            }
        }
    }

    public void ResetCells()
    {
        foreach (var c in cells) c.ResetEffect();
    }

    public GridCellView GetCell(int x, int y)
    {
        int i = y * size + x;
        return x >= 0 && y >= 0 && x < size && y < size && i < cells.Count ? cells[i] : null;
    }
}
