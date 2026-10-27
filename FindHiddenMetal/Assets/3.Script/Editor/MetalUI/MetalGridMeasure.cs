using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static MetalUIKit;

// 정적 레이어 그림(Static_<패널>.png)에서 격자 칸 위치를 재서 패널 프리팹의 GridBoard에 넣는다.
// 디자이너 그림의 격자선 간격이 일정하지 않아(예: 한 줄만 3px 작음) 똑같이 나누면 칸이 선에서 벗어나기 때문이다.
// 그림을 바꾸면 이 메뉴를 다시 실행한다 (프리팹 생성 시에도 자동 실행).
//
// 측정: 지금 격자 영역 주변(margin)에서 열/행 방향 밝기 단면을 만들고, 어두운 구간(= 칸)을 찾는다.
//       칸 안의 숫자에 흔들리지 않게 단면은 각 위치의 하위 20% 밝기를 쓴다.
//       선 두께가 2px/3px(반투명 1px)로 섞여 칸 폭이 64.5px 같은 소수가 되므로, 칸 양 끝의 반쯤 밝은 픽셀은
//       밝기 비율만큼만 칸에 넣는다 (소수점 위치까지 측정).
//       칸이 gridSize개가 아니면 프리팹을 바꾸지 않고 오류만 남긴다.
public static class MetalGridMeasure
{
    private static readonly string[] GridPanels = { "Play", "Result", "Edit" };
    private const int Margin = 24;          // 지금 격자 영역 바깥으로 더 보는 폭 (px)
    private const float DarkThreshold = 150; // 칸(어두움) / 격자선·바깥 배경(밝음) 경계 밝기
    private const float LineLum = 255;       // 격자선 밝기
    private const int MinCellPx = 10;       // 이보다 짧은 어두운 구간은 칸이 아님

    [MenuItem("Tools/숨은 금속 찾기/2. 격자 칸 위치 측정", priority = 2)]
    public static void MeasureAll()
    {
        int gridSize = ReadGridSize();
        foreach (string panel in GridPanels) Measure(panel, gridSize);
        AssetDatabase.SaveAssets();
    }

    // 기본 설정 원본(StreamingAssets/Setting.json)의 격자 칸 수
    private static int ReadGridSize()
    {
        var data = new GameDynamicData();
        string path = Path.Combine(Application.streamingAssetsPath, "Setting.json");
        if (File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path), data);
        return data.gridSize;
    }

    public static bool Measure(string panel, int n)
    {
        string prefabPath = PanelPrefabPath(panel);
        string imagePath = StaticSpritePath(panel);
        if (!File.Exists(prefabPath) || !File.Exists(imagePath))
        {
            Debug.LogWarning($"[격자 측정] {panel}: 프리팹 또는 정적 레이어 그림 없음");
            return false;
        }

        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(imagePath));
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            GridBoard board = root.GetComponentInChildren<GridBoard>(true);
            if (board == null) return false;
            var rt = (RectTransform)board.transform;
            int w = tex.width, h = tex.height;

            // 지금 격자 영역 (그림 왼쪽 위 기준 px). 패널 = 그림 크기, 격자는 가운데 기준 배치
            float left = w / 2f + rt.anchoredPosition.x - rt.sizeDelta.x / 2f;
            float top = h / 2f - rt.anchoredPosition.y - rt.sizeDelta.y / 2f;
            var area = new RectInt(Mathf.RoundToInt(left), Mathf.RoundToInt(top), Mathf.RoundToInt(rt.sizeDelta.x), Mathf.RoundToInt(rt.sizeDelta.y));

            Color32[] pixels = tex.GetPixels32();
            float Lum(int x, int yTop)
            {
                Color32 c = pixels[(h - 1 - yTop) * w + x];
                return (c.r + c.g + c.b) / 3f;
            }

            // 열 단면: x마다 격자 안쪽 세로 띠의 하위 20% 밝기
            List<Vector2> cols = DarkRuns(area.xMin - Margin, area.xMax + Margin,
                x => Low(area.yMin + Margin, area.yMax - Margin, y => Lum(x, y)));
            List<Vector2> rows = DarkRuns(area.yMin - Margin, area.yMax + Margin,
                y => Low(area.xMin + Margin, area.xMax - Margin, x => Lum(x, y)));

            if (cols.Count != n || rows.Count != n)
            {
                Debug.LogError($"[격자 측정] {panel}: 칸을 {cols.Count}x{rows.Count}개 찾음 ({n}x{n} 필요) - 프리팹을 바꾸지 않음. 격자 영역 위치나 그림을 확인하세요.");
                return false;
            }

            // 격자 영역 = 첫 칸 시작 ~ 마지막 칸 끝. 칸 위치는 이 영역 왼쪽 위 기준
            float x0 = cols[0].x, x1 = cols[n - 1].y, y0 = rows[0].x, y1 = rows[n - 1].y;
            rt.sizeDelta = new Vector2(x1 - x0, y1 - y0);
            rt.anchoredPosition = new Vector2((x0 + x1) / 2f - w / 2f, h / 2f - (y0 + y1) / 2f);
            var colSpans = new Vector2[n];
            var rowSpans = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                colSpans[i] = cols[i] - new Vector2(x0, x0);
                rowSpans[i] = rows[i] - new Vector2(y0, y0);
            }
            board.SetMeasured(colSpans, rowSpans);
            EditorUtility.SetDirty(board);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"[격자 측정] {panel}: 영역 {rt.sizeDelta} @ {rt.anchoredPosition}\n" +
                      $"열 폭 {Widths(colSpans)}\n행 높이 {Widths(rowSpans)}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            Object.DestroyImmediate(tex);
        }
    }

    private static float Low(int from, int to, System.Func<int, float> lum)
    {
        var values = new List<float>(to - from);
        for (int i = from; i < to; i++) values.Add(lum(i));
        values.Sort();
        return values.Count > 0 ? values[values.Count / 5] : 255f;
    }

    // 단면에서 어두운 구간(칸)을 [시작, 끝) px 로 찾는다 (소수점까지)
    private static List<Vector2> DarkRuns(int from, int to, System.Func<int, float> profile)
    {
        var values = new float[to - from + 2];
        for (int i = 0; i < values.Length; i++) values[i] = profile(from - 1 + i);
        float V(int i) => values[i - from + 1];

        var runs = new List<Vector2>();
        int start = -1;
        for (int i = from; i <= to; i++)
        {
            bool dark = i < to && V(i) < DarkThreshold;
            if (dark && start < 0) start = i;
            else if (!dark && start >= 0)
            {
                if (i - start >= MinCellPx)
                {
                    // 칸 색 = 구간 가운데 밝기. 양 끝 픽셀은 칸 색에서 선 색까지의 비율만큼 칸으로 본다
                    float cell = V((start + i) / 2);
                    float a = start + 1 - Darkness(V(start), cell) - Darkness(V(start - 1), cell);
                    float b = i - 1 + Darkness(V(i - 1), cell) + Darkness(V(i), cell);
                    runs.Add(new Vector2(a, b));
                }
                start = -1;
            }
        }
        return runs;
    }

    // 픽셀이 칸(어두움)에 해당하는 비율 0~1
    private static float Darkness(float lum, float cell) => Mathf.Clamp01((LineLum - lum) / (LineLum - cell));

    private static string Widths(Vector2[] spans)
    {
        var parts = new string[spans.Length];
        for (int i = 0; i < spans.Length; i++) parts[i] = (spans[i].y - spans[i].x).ToString("0.#");
        return string.Join(" ", parts);
    }
}
