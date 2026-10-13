using System.Collections.Generic;

public enum JudgeResult
{
    Correct,      // 새로 찾음
    Wrong,        // 금속 없음
    AlreadyFound, // 이미 찾은 칸 (감점 없음)
}

// 한 판 동안 선택한 칸의 정답 여부와 발견 개수를 관리한다.
public class GridJudge
{
    private readonly HashSet<long> targets = new HashSet<long>();
    private readonly HashSet<long> found = new HashSet<long>();

    public int TotalCount => targets.Count;
    public int FoundCount => found.Count;
    public int WrongCount { get; private set; }
    public bool IsAllFound => TotalCount > 0 && FoundCount >= TotalCount;
    public IEnumerable<GridPos> Targets
    {
        get { foreach (var k in targets) yield return FromKey(k); }
    }

    public void Begin(IEnumerable<GridPos> targetPositions)
    {
        targets.Clear();
        found.Clear();
        WrongCount = 0;
        foreach (var p in targetPositions)
            targets.Add(Key(p.x, p.y));
    }

    public JudgeResult Judge(int x, int y)
    {
        long key = Key(x, y);
        if (found.Contains(key)) return JudgeResult.AlreadyFound;

        if (targets.Contains(key))
        {
            found.Add(key);
            return JudgeResult.Correct;
        }

        WrongCount++;
        return JudgeResult.Wrong;
    }

    public bool IsFound(int x, int y) => found.Contains(Key(x, y));

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    private static GridPos FromKey(long key) => new GridPos((int)(key >> 32), (int)(key & 0xFFFFFFFF));
}
