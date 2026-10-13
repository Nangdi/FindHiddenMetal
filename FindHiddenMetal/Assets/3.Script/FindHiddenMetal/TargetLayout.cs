using System.Collections.Generic;

// 회전 원판 단계(0~3)에 따른 탐지목표물 좌표 계산.
// 원판이 격자 중심을 기준으로 90도씩 돌기 때문에 0단계 좌표만 알면 나머지는 회전 변환으로 구한다.
public static class TargetLayout
{
    public const int RotationSteps = 4;

    public static List<GridPos> GetTargets(IList<GridPos> baseTargets, int gridSize, int rotationIndex, bool clockwise)
    {
        var result = new List<GridPos>();
        if (baseTargets == null) return result;

        int steps = ((rotationIndex % RotationSteps) + RotationSteps) % RotationSteps;
        foreach (var p in baseTargets)
        {
            int x = p.x, y = p.y;
            for (int i = 0; i < steps; i++)
            {
                int nx, ny;
                if (clockwise) { nx = gridSize - 1 - y; ny = x; }
                else { nx = y; ny = gridSize - 1 - x; }
                x = nx; y = ny;
            }
            result.Add(new GridPos(x, y));
        }
        return result;
    }

    // 격자 표기 (예: x=0,y=0 → "A1")
    public static string ToLabel(int x, int y)
    {
        return $"{(char)('A' + x)}{y + 1}";
    }
}
