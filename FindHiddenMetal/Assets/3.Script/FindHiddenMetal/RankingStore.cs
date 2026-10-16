using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class RankRecord
{
    // 기록 시각 (DateTime.Ticks, 로컬 시간)
    public long ticks;
    // 정답 확인까지 걸린 시간(초, 시간 초과면 제한 시간). 찾은 개수가 같으면 작을수록 높은 순위.
    public float seconds;
    public int foundCount;
    public int totalCount;
    public int wrongCount;

    public DateTime Time => new DateTime(ticks);
}

[Serializable]
public class RankingData
{
    public List<RankRecord> records = new List<RankRecord>();
}

// 랭킹: 찾은 개수가 많을수록, 같으면 소요 시간이 짧을수록 높다. persistentDataPath/ranking.json 에 저장한다.
// 모두 찾지 못하고 시간이 끝난 체험도 기록한다.
// 보관 기간(keepDays)이 지난 기록은 불러올 때와 추가할 때 삭제한다.
public class RankingStore
{
    private readonly string path;
    private readonly int keepDays;
    private RankingData data = new RankingData();

    public int Count => data.records.Count;

    public RankingStore(int keepDays)
    {
        this.keepDays = Mathf.Max(1, keepDays);
        path = Path.Combine(Application.persistentDataPath, "ranking.json");
        Load();
    }

    public void Load()
    {
        try
        {
            data = File.Exists(path) ? JsonUtility.FromJson<RankingData>(File.ReadAllText(path)) : new RankingData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"랭킹 파일 읽기 실패 - 빈 랭킹으로 시작: {ex.Message}");
            data = new RankingData();
        }
        data ??= new RankingData();
        data.records ??= new List<RankRecord>();

        if (PurgeOld() > 0) Save();
    }

    // 기록을 추가한다. 순위는 GetRank(record).
    public RankRecord Add(float seconds, int foundCount, int totalCount, int wrongCount)
    {
        PurgeOld();
        var record = new RankRecord
        {
            ticks = DateTime.Now.Ticks,
            seconds = seconds,
            foundCount = foundCount,
            totalCount = totalCount,
            wrongCount = wrongCount,
        };
        data.records.Add(record);
        Save();
        return record;
    }

    // 찾은 개수와 시간이 같으면 같은 순위.
    public int GetRank(RankRecord record)
    {
        int rank = 1;
        foreach (var r in data.records)
            if (Score(r, record) < 0) rank++;
        return rank;
    }

    // a가 b보다 앞 순위면 음수, 같으면 0
    public static int Score(RankRecord a, RankRecord b)
    {
        if (a.foundCount != b.foundCount) return b.foundCount.CompareTo(a.foundCount);
        return a.seconds.CompareTo(b.seconds);
    }

    public List<RankRecord> GetTop(int count)
    {
        var list = new List<RankRecord>(data.records);
        list.Sort((a, b) => Score(a, b) != 0 ? Score(a, b) : a.ticks.CompareTo(b.ticks));
        if (count >= 0 && list.Count > count) list.RemoveRange(count, list.Count - count);
        return list;
    }

    private int PurgeOld()
    {
        long limit = DateTime.Now.AddDays(-keepDays).Ticks;
        int removed = data.records.RemoveAll(r => r.ticks < limit);
        if (removed > 0) Debug.Log($"랭킹 {keepDays}일 지난 기록 {removed}건 삭제");
        return removed;
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"랭킹 저장 실패: {ex.Message}");
        }
    }
}
