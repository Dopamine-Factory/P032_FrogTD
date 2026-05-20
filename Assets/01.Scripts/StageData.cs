using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public struct StageData
{
    public string FieldBG;
    public string BGM;
    public StageSpawnData[] StageSpawnDatas;
    public int StageSpawnLength;

    /// <summary>
    /// CSV 텍스트를 파싱해서 StageData를 채웁니다.
    ///
    /// CSV 포맷 (StageDataEditor 기준):
    ///   1행 : FieldBG,BGM
    ///   2행~ : spawnTime,몬스터ID/레벨/Y [,몬스터ID/레벨/Y ...]
    ///
    /// 예시)
    ///   Field_BG_1,BGM1
    ///   0,200001/1/0
    ///   2.5,200002/2/0.5,200003/1/-0.5
    /// </summary>
    public void Converter(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning("[StageData] Converter: data is empty.");
            StageSpawnDatas = Array.Empty<StageSpawnData>();
            return;
        }

        // 줄 단위 분리 (\r\n, \r, \n 모두 대응)
        string[] lines = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        if (lines.Length == 0)
        {
            StageSpawnDatas = Array.Empty<StageSpawnData>();
            return;
        }

        // ── 1행: FieldBG, BGM ─────────────────────────────────────────
        var firstCols = SplitCsvLine(lines[0]);
        FieldBG = firstCols.Count > 0 ? firstCols[0].Trim() : string.Empty;
        BGM     = firstCols.Count > 1 ? firstCols[1].Trim() : string.Empty;

        // ── 2행~: 스폰 데이터 ──────────────────────────────────────────
        var spawnList = new List<StageSpawnData>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Count == 0) continue;

            var spawnData = new StageSpawnData();

            // 첫 열: 등장 시간
            double.TryParse(cols[0].Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out spawnData.Time);

            // 나머지 열: 몬스터 엔트리 (ID/Level/Y)
            var monsterList = new List<StageMonsterData>();
            for (int c = 1; c < cols.Count; c++)
            {
                string entry = cols[c].Trim();
                if (string.IsNullOrEmpty(entry)) continue;

                string[] parts = entry.Split('/');
                if (parts.Length < 3) continue;

                var monster = new StageMonsterData();
                uint.TryParse (parts[0].Trim(), out monster.Id);
                int.TryParse  (parts[1].Trim(), out monster.Level);
                float.TryParse(parts[2].Trim(), NumberStyles.Float,
                               CultureInfo.InvariantCulture, out monster.PosY);

                monsterList.Add(monster);
            }

            spawnData.stageMonsterDatas = monsterList.ToArray();
            spawnList.Add(spawnData);
        }

        StageSpawnDatas = spawnList.ToArray();

        StageSpawnLength = StageSpawnDatas.Length;
    }

    // ── CSV 한 줄 파싱 (큰따옴표 안의 쉼표 무시) ──────────────────────
    private static List<string> SplitCsvLine(string line)
    {
        var result   = new List<string>();
        var current  = new System.Text.StringBuilder();
        bool inQuote = false;

        foreach (char ch in line)
        {
            if      (ch == '"')             { inQuote = !inQuote; }
            else if (ch == ',' && !inQuote) { result.Add(current.ToString()); current.Clear(); }
            else                            { current.Append(ch); }
        }
        result.Add(current.ToString());
        return result;
    }
}

public struct StageSpawnData
{
    public double Time;
    public StageMonsterData[] stageMonsterDatas;
}

public struct StageMonsterData
{
    public uint  Id;
    public int   Level;
    public float PosY;
}
