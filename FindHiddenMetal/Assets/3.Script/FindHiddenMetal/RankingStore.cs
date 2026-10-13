using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class RankRecord
{
    // 기록 시각 (DateTime.Ticks, 로컬 시간)
    public long ticks;
    // 소요 시간(초, 오답 감점 포함). 작을수록 높은 순위.
    public float seconds;
    public int wrongCount;
}

[Serializable]
public class RankingData
{
    public List<RankRecord> records = new List<RankRecord>();
}

// 모두 찾는 데 걸린 시간 기준 랭킹. persistentDataPath/ranking.json 에 저장한다.
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

    // 기록을 추가한다. 순위는 GetRank(record.seconds).
    public RankRecord Add(float seconds, int wrongCount)
    {
        PurgeOld();
        var record = new RankRecord { ticks = DateTime.Now.Ticks, seconds = seconds, wrongCount = wrongCount };
        data.records.Add(record);
        Save();
        return record;
    }

    // 같은 시간이면 같은 순위.
    public int GetRank(float seconds)
    {
        int rank = 1;
        foreach (var r in data.records)
            if (r.seconds < seconds) rank++;
        return rank;
    }

    public List<RankRecord> GetTop(int count)
    {
        var list = new List<RankRecord>(data.records);
        list.Sort((a, b) => a.seconds != b.seconds ? a.seconds.CompareTo(b.seconds) : a.ticks.CompareTo(b.ticks));
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
