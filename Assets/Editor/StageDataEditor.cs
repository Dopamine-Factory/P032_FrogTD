#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class StageDataEditor : EditorWindow
{
    // ── 데이터 모델 ──────────────────────────────────────────────────────
    [Serializable]
    private class MonsterEntry
    {
        public int   id    = 200001;
        public int   level = 1;
        public float y     = 0f;
    }

    [Serializable]
    private class WaveRow
    {
        public float              spawnTime = 0f;
        public List<MonsterEntry> monsters  = new();
    }

    [Serializable]
    private class StageData
    {
        public string        bgPrefab = "Field_BG_1";
        public string        bgm      = "BGM1";
        public List<WaveRow> waves    = new();
    }

    // ── 상태 ─────────────────────────────────────────────────────────────
    private const string CSV_FOLDER = "Assets/04.StageData";

    private StageData _stage       = new();
    private string    _filePath;
    private string[]  _csvFiles    = Array.Empty<string>();
    private int       _selectedIdx = -1;
    private bool      _isDirty;

    private Vector2 _fileScroll;
    private Vector2 _waveScroll;
    private string  _newFileName = "NewStage";

    // ── 스타일 ───────────────────────────────────────────────────────────
    private GUIStyle _styleDirtyLabel;
    private bool     _stylesReady;

    private static readonly Color BgWaveEven  = new(0.20f, 0.20f, 0.22f);
    private static readonly Color BgWaveOdd   = new(0.17f, 0.17f, 0.19f);
    private static readonly Color BgMonster   = new(0.14f, 0.18f, 0.24f);
    private static readonly Color AccentBlue  = new(0.26f, 0.52f, 0.96f);
    private static readonly Color AccentGreen = new(0.24f, 0.73f, 0.33f);
    private static readonly Color AccentRed   = new(0.80f, 0.25f, 0.25f);

    // ════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/Stage Data Editor")]
    public static void Open()
    {
        var w = GetWindow<StageDataEditor>("Stage Data Editor");
        w.minSize = new Vector2(720, 480);
    }

    private void OnEnable() => RefreshFileList();

    // ════════════════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EnsureStyles();
        HandleShortcuts();

        EditorGUILayout.BeginHorizontal();
        DrawFilePanel();
        GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
        DrawMainPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════════════
    //  왼쪽 파일 패널
    // ════════════════════════════════════════════════════════════════════
    private void DrawFilePanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(190));
        GUILayout.Label("스테이지 파일", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _newFileName = EditorGUILayout.TextField(_newFileName);
        if (GUILayout.Button("생성", GUILayout.Width(36)))
            CreateNewFile();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);
        _fileScroll = EditorGUILayout.BeginScrollView(_fileScroll);

        for (int i = 0; i < _csvFiles.Length; i++)
        {
            string label = Path.GetFileNameWithoutExtension(_csvFiles[i]);
            bool   sel   = i == _selectedIdx;

            Color prev = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = AccentBlue;
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                GUI.backgroundColor = prev;
                if (!AskDiscardIfDirty()) continue;
                _selectedIdx = i;
                LoadFile(_csvFiles[i]);
            }
            GUI.backgroundColor = prev;
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(4);
        if (GUILayout.Button("목록 새로고침", EditorStyles.miniButton))
            RefreshFileList();

        EditorGUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════════════════
    //  오른쪽 메인 패널
    // ════════════════════════════════════════════════════════════════════
    private void DrawMainPanel()
    {
        EditorGUILayout.BeginVertical();

        if (_filePath == null)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("← 왼쪽에서 파일을 선택하세요", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawToolbar();
        GUILayout.Space(4);
        DrawInfoRow();
        GUILayout.Space(6);
        DrawWaveTableHeader();
        DrawWaveRows();
        GUILayout.Space(4);
        DrawAddWaveButton();

        EditorGUILayout.EndVertical();
    }

    // ── 툴바 ─────────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        string fname = Path.GetFileName(_filePath);
        string title = _isDirty ? $"● {fname}" : fname;
        GUILayout.Label(title, _isDirty ? _styleDirtyLabel : EditorStyles.label, GUILayout.Width(180));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("시간순 정렬", EditorStyles.toolbarButton, GUILayout.Width(72)))
            SortByTime();

        Color prev = GUI.backgroundColor;
        if (_isDirty) GUI.backgroundColor = AccentGreen;
        if (GUILayout.Button("저장  Ctrl+S", EditorStyles.toolbarButton, GUILayout.Width(84)))
            SaveFile();
        GUI.backgroundColor = prev;

        if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(56)))
            if (AskDiscardIfDirty(force: true)) LoadFile(_filePath);

        EditorGUILayout.EndHorizontal();
    }

    // ── 1행: BG / BGM ────────────────────────────────────────────────────
    private void DrawInfoRow()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("스테이지 기본 정보", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("배경 프리팹", GUILayout.Width(72));
        _stage.bgPrefab = EditorGUILayout.TextField(_stage.bgPrefab);
        GUILayout.Space(16);
        GUILayout.Label("BGM", GUILayout.Width(32));
        _stage.bgm = EditorGUILayout.TextField(_stage.bgm);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck()) _isDirty = true;
        EditorGUILayout.EndVertical();
    }

    // ── 웨이브 테이블 헤더 ───────────────────────────────────────────────
    private void DrawWaveTableHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("#",        EditorStyles.miniBoldLabel, GUILayout.Width(24));
        GUILayout.Label("등장(초)", EditorStyles.miniBoldLabel, GUILayout.Width(64));
        GUILayout.Label("몬스터  ( 번호  /  레벨  /  Y좌표 )", EditorStyles.miniBoldLabel);
        EditorGUILayout.EndHorizontal();

        Rect line = GUILayoutUtility.GetLastRect();
        line.y += EditorGUIUtility.singleLineHeight;
        line.height = 1;
        EditorGUI.DrawRect(line, new Color(0.4f, 0.4f, 0.4f));
        GUILayout.Space(2);
    }

    // ── 웨이브 행 목록 ───────────────────────────────────────────────────
    private void DrawWaveRows()
    {
        _waveScroll = EditorGUILayout.BeginScrollView(_waveScroll);

        for (int wi = 0; wi < _stage.waves.Count; wi++)
        {
            WaveRow wave = _stage.waves[wi];

            Rect rowRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rowRect, wi % 2 == 0 ? BgWaveEven : BgWaveOdd);

            EditorGUILayout.BeginHorizontal();

            // 행 번호
            GUILayout.Label((wi + 1).ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(24));

            // 등장 시간
            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.FloatField(wave.spawnTime, GUILayout.Width(64));
            if (EditorGUI.EndChangeCheck()) { wave.spawnTime = Mathf.Max(0f, newTime); _isDirty = true; }

            GUILayout.Space(4);

            // 몬스터 카드
            bool removedMonster = false;
            for (int mi = 0; mi < wave.monsters.Count; mi++)
            {
                int capturedMi = mi;
                DrawMonsterCard(wave.monsters[mi], () =>
                {
                    wave.monsters.RemoveAt(capturedMi);
                    _isDirty = true;
                    removedMonster = true;
                });
                if (removedMonster) break;
                if (mi < wave.monsters.Count - 1) GUILayout.Space(2);
            }

            // + 몬스터
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = AccentBlue;
            if (GUILayout.Button("+몬스터", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(18)))
            { wave.monsters.Add(new MonsterEntry()); _isDirty = true; }
            GUI.backgroundColor = prev;

            GUILayout.FlexibleSpace();

            // 행 삭제
            GUI.backgroundColor = AccentRed;
            bool deleteRow = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(18));
            GUI.backgroundColor = prev;

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.EndVertical();

            if (deleteRow)
            {
                if (EditorUtility.DisplayDialog("행 삭제", $"{wi + 1}번 웨이브를 삭제하시겠습니까?", "삭제", "취소"))
                { _stage.waves.RemoveAt(wi); _isDirty = true; }
                break;
            }
            if (removedMonster) break;
        }

        EditorGUILayout.EndScrollView();
    }

    // ── 몬스터 카드 ──────────────────────────────────────────────────────
    private void DrawMonsterCard(MonsterEntry m, Action onDelete)
    {
        Rect cardRect = EditorGUILayout.BeginVertical(GUILayout.Width(208));
        EditorGUI.DrawRect(cardRect, BgMonster);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        GUILayout.Label("번호", EditorStyles.miniLabel, GUILayout.Width(26));
        int   newId = EditorGUILayout.IntField(m.id,    GUILayout.Width(54));
        GUILayout.Label("Lv",   EditorStyles.miniLabel, GUILayout.Width(16));
        int   newLv = EditorGUILayout.IntField(m.level, GUILayout.Width(24));
        GUILayout.Label("Y",    EditorStyles.miniLabel, GUILayout.Width(12));
        float newY  = EditorGUILayout.FloatField(m.y,   GUILayout.Width(34));

        if (EditorGUI.EndChangeCheck())
        {
            m.id    = Mathf.Max(0, newId);
            m.level = Mathf.Max(1, newLv);
            m.y     = newY;
            _isDirty = true;
        }

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = AccentRed;
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(16), GUILayout.Height(16)))
            onDelete?.Invoke();
        GUI.backgroundColor = prev;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ── 웨이브 추가 버튼 ─────────────────────────────────────────────────
    private void DrawAddWaveButton()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = AccentGreen;
        if (GUILayout.Button("+ 웨이브 추가", GUILayout.Width(120), GUILayout.Height(26)))
        {
            float t = _stage.waves.Count > 0 ? _stage.waves[^1].spawnTime + 2f : 0f;
            _stage.waves.Add(new WaveRow { spawnTime = t });
            _isDirty = true;
        }
        GUI.backgroundColor = prev;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════════════
    //  파일 I/O
    // ════════════════════════════════════════════════════════════════════
    private void RefreshFileList()
    {
        string abs = AbsFolder();
        if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        _csvFiles = Directory.GetFiles(abs, "*.csv");
        Array.Sort(_csvFiles, (a, b) =>
        {
            bool an = int.TryParse(Path.GetFileNameWithoutExtension(a), out int ai);
            bool bn = int.TryParse(Path.GetFileNameWithoutExtension(b), out int bi);
            if (an && bn) return ai.CompareTo(bi);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
        Repaint();
    }

    private void LoadFile(string path)
    {
        _filePath = path;
        _isDirty  = false;
        _stage    = new StageData();

        try
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return;

            // 1행: BG프리팹, BGM
            var first = SplitCsv(lines[0]);
            _stage.bgPrefab = first.Count > 0 ? first[0].Trim() : "";
            _stage.bgm      = first.Count > 1 ? first[1].Trim() : "";

            // 2행~: 등장시간, 몬스터ID/레벨/Y ...
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = SplitCsv(lines[i]);
                if (cols.Count == 0) continue;

                var wave = new WaveRow();
                float.TryParse(cols[0], System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               out wave.spawnTime);

                for (int c = 1; c < cols.Count; c++)
                {
                    string[] parts = cols[c].Trim().Split('/');
                    if (parts.Length < 3) continue;
                    var me = new MonsterEntry();
                    int.TryParse(parts[0].Trim(), out me.id);
                    int.TryParse(parts[1].Trim(), out me.level);
                    float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out me.y);
                    wave.monsters.Add(me);
                }
                _stage.waves.Add(wave);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StageDataEditor] 로드 실패: {ex.Message}");
        }

        Repaint();
    }

    private void SaveFile()
    {
        if (_filePath == null) return;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{_stage.bgPrefab},{_stage.bgm}");

            foreach (var wave in _stage.waves)
            {
                var parts = new List<string>
                {
                    wave.spawnTime.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                };
                foreach (var m in wave.monsters)
                {
                    string entry = $"{m.id}/{m.level}/{m.y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
                    if (!string.IsNullOrWhiteSpace(entry))
                        parts.Add(entry);
                }
                // 빈 열 제거 후 저장
                parts.RemoveAll(string.IsNullOrWhiteSpace);
                sb.AppendLine(string.Join(",", parts));
            }

            File.WriteAllText(_filePath, sb.ToString().TrimEnd('\r', '\n'), Encoding.UTF8);
            _isDirty = false;
            AssetDatabase.Refresh();
            Debug.Log($"[StageDataEditor] 저장 완료: {_filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StageDataEditor] 저장 실패: {ex.Message}");
        }
    }

    private void CreateNewFile()
    {
        string name = _newFileName.Trim();
        if (string.IsNullOrEmpty(name)) name = "NewStage";
        string path = Path.Combine(AbsFolder(), name + ".csv");
        if (File.Exists(path))
        {
            EditorUtility.DisplayDialog("중복", $"{name}.csv 이미 존재합니다.", "확인");
            return;
        }
        File.WriteAllText(path, "Field_BG_1,BGM1\n0,200001/1/0", Encoding.UTF8);
        RefreshFileList();
        for (int i = 0; i < _csvFiles.Length; i++)
        {
            if (Path.GetFileNameWithoutExtension(_csvFiles[i]) == name)
            { _selectedIdx = i; LoadFile(_csvFiles[i]); break; }
        }
        AssetDatabase.Refresh();
    }

    // ════════════════════════════════════════════════════════════════════
    //  유틸
    // ════════════════════════════════════════════════════════════════════
    private void SortByTime()
    {
        _stage.waves.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));
        _isDirty = true;
    }

    private string AbsFolder()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", CSV_FOLDER));
    }

    private bool AskDiscardIfDirty(bool force = false)
    {
        if (!_isDirty) return true;
        return EditorUtility.DisplayDialog(
            "저장되지 않은 변경",
            "변경사항이 있습니다. 버리시겠습니까?",
            force ? "버리기" : "계속",
            "취소");
    }

    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.S)
        { SaveFile(); e.Use(); }
    }

    private static List<string> SplitCsv(string line)
    {
        var  result  = new List<string>();
        var  cur     = new StringBuilder();
        bool inQuote = false;
        foreach (char ch in line)
        {
            if      (ch == '"')              { inQuote = !inQuote; }
            else if (ch == ',' && !inQuote)  { result.Add(cur.ToString()); cur.Clear(); }
            else                             { cur.Append(ch); }
        }
        result.Add(cur.ToString());
        return result;
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        _styleDirtyLabel = new GUIStyle(EditorStyles.label)
        {
            normal    = { textColor = new Color(1f, 0.85f, 0.3f) },
            fontStyle = FontStyle.Bold,
        };
        _stylesReady = true;
    }
}
#endif
