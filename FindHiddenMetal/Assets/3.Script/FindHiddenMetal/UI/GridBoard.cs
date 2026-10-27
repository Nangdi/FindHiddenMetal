using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 격자 칸을 gridSize x gridSize만큼 만들어 이 영역(RectTransform)에 꽉 채운다.
// 격자 선과 칸 번호는 정적 레이어 그림에 있고, 칸 오브젝트는 그 위에 겹쳐 터치/마크만 담당한다.
// 칸 위치:
//  - columns/rows 가 gridSize개 있으면 그 값 그대로 (정적 레이어 그림에서 잰 칸 위치. 그림의 선 간격이 일정하지 않아도 맞음)
//    에디터 메뉴 "Tools/숨은 금속 찾기/2. 격자 칸 위치 측정"이 그림을 읽어 채운다.
//  - 없으면 똑같이 나눈다: 칸 크기 = (영역 - padding*2 - spacing*(n-1)) / n (GridLayoutGroup 값 = 격자 선 두께)
[RequireComponent(typeof(GridLayoutGroup))]
public class GridBoard : MonoBehaviour
{
    [SerializeField] private GridCellView cellPrefab;
    [Tooltip("열마다 칸의 왼쪽(x)·오른쪽(y) 끝. 이 영역 왼쪽 위 기준 px. 개수가 gridSize와 다르면 똑같이 나눈다")]
    [SerializeField] private Vector2[] columns;
    [Tooltip("행마다 칸의 위(x)·아래(y) 끝. 이 영역 왼쪽 위 기준 px(아래로 +)")]
    [SerializeField] private Vector2[] rows;

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
        bool measured = HasMeasured(size);
        layout.enabled = !measured;
        if (!measured)
        {
            Vector2 area = ((RectTransform)transform).rect.size;
            float w = (area.x - layout.padding.horizontal - layout.spacing.x * (size - 1)) / size;
            float h = (area.y - layout.padding.vertical - layout.spacing.y * (size - 1)) / size;
            layout.cellSize = new Vector2(w, h);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = size;
        }

        // 위쪽 행부터, 왼쪽 열부터 생성 (GridLayoutGroup 기본 순서와 같음)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var cell = Instantiate(cellPrefab, transform);
                cell.Init(x, y);
                if (measured) Place((RectTransform)cell.transform, columns[x], rows[y]);
                cell.OnClick += c => OnCellClick?.Invoke(c);
                cells.Add(cell);
            }
        }
    }

    public bool HasMeasured(int gridSize) =>
        columns != null && rows != null && columns.Length == gridSize && rows.Length == gridSize;

    // 측정한 칸 위치를 넣는다 (에디터 측정 도구용)
    public void SetMeasured(Vector2[] columnSpans, Vector2[] rowSpans)
    {
        columns = columnSpans;
        rows = rowSpans;
    }

    // 이 영역 왼쪽 위 기준 px 칸 범위에 칸을 놓는다
    private static void Place(RectTransform rt, Vector2 column, Vector2 row)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(column.y - column.x, row.y - row.x);
        rt.anchoredPosition = new Vector2(column.x + rt.sizeDelta.x * rt.pivot.x, -(row.x + rt.sizeDelta.y * (1f - rt.pivot.y)));
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
