// Editor-only builder script: run via Tools > Build Merge Grid UI
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class MergeGridBuilder
{
    // ─── 색상 ───────────────────────────────────────────────────────
    static Color PanelBg    = new Color(0.91f, 0.86f, 0.75f);
    static Color PanelDark  = new Color(0.83f, 0.77f, 0.63f);
    static Color SlotEmpty  = new Color(0.79f, 0.72f, 0.59f);
    static Color SlotLocked = new Color(0.70f, 0.63f, 0.52f);
    static Color SlotHas    = new Color(0.82f, 0.75f, 0.61f);
    static Color HpGreen    = new Color(0.30f, 0.80f, 0.39f);
    static Color HpTrack    = new Color(0.16f, 0.16f, 0.16f);
    static Color HeartRed   = new Color(0.91f, 0.25f, 0.25f);
    static Color DarkText   = new Color(0.23f, 0.18f, 0.13f);
    static Color YellowBtn  = new Color(0.94f, 0.75f, 0.19f);
    static Color GreenBtn   = new Color(0.24f, 0.73f, 0.33f);
    static Color BlueBtn    = new Color(0.35f, 0.71f, 0.94f);
    static Color[] LvColors = new Color[] {
        new Color(0.91f,0.25f,0.25f),
        new Color(0.91f,0.47f,0.13f),
        new Color(0.91f,0.78f,0.13f),
        new Color(0.25f,0.78f,0.25f),
        new Color(0.25f,0.50f,0.91f),
    };

    [MenuItem("Tools/Build Merge Grid UI")]
    public static void Build()
    {
        var bottomGO = GameObject.Find("UI Root/UI Game/UI Game Ingame/Bottom");
        if (bottomGO == null) { Debug.LogError("Bottom not found!"); return; }

        // 기존 자식 제거
        for (int ci = bottomGO.transform.childCount - 1; ci >= 0; ci--)
            Object.DestroyImmediate(bottomGO.transform.GetChild(ci).gameObject);

        var bottomRT = bottomGO.GetComponent<RectTransform>();
        var bottomImg = bottomGO.GetComponent<Image>() ?? bottomGO.AddComponent<Image>();
        bottomImg.color = PanelBg;
        SetRT(bottomRT, V2(0,0), V2(1,0), V2(0.5f,0f), V2(0,0), V2(0,360));

        BuildHPRow(bottomRT);
        BuildGrid(bottomRT);
        BuildActionRow(bottomRT);

        EditorUtility.SetDirty(bottomGO);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[MergeGridBuilder] 머지 그리드 UI 생성 완료!");
    }

    // ── HP 바 행 ─────────────────────────────────────────────────────
    static void BuildHPRow(RectTransform parent)
    {
        var row = NewUI("HPRow", parent.transform);
        SetRT(row, V2(0,1), V2(1,1), V2(0.5f,1f), V2(0,-10), V2(-24,36));

        // 하트
        var heart = NewUI("Heart", row.transform);
        SetRT(heart, V2(0,0), V2(0,1), V2(0,0.5f), V2(0,0), V2(36,0));
        heart.AddComponent<Image>().color = HeartRed;

        // HP Track
        var track = NewUI("HPTrack", row.transform);
        SetRT(track, V2(0,0), V2(1,1), V2(0,0.5f), V2(44,0), V2(-184,0));
        track.AddComponent<Image>().color = HpTrack;

        // HP Fill
        var fill = NewUI("HPFill", track.transform);
        SetRT(fill, V2(0,0), V2(0.9f,1), V2(0,0.5f), V2(0,0), V2(0,0));
        fill.AddComponent<Image>().color = HpGreen;

        // HP 숫자
        MakeText(track.transform, "999", 20, Color.white, TextAnchor.MiddleCenter);

        // 데미지 pill
        var dmg = NewUI("DamagePill", row.transform);
        SetRT(dmg, V2(1,0), V2(1,1), V2(1,0.5f), V2(-4,0), V2(130,0));
        dmg.AddComponent<Image>().color = new Color(0,0,0,0.15f);
        MakeText(dmg.transform, "99.99b", 19, DarkText, TextAnchor.MiddleCenter);
    }

    // ── 머지 그리드 ──────────────────────────────────────────────────
    static void BuildGrid(RectTransform parent)
    {
        int ROWS = 4, COLS = 7;
        float gap = 5f;
        float gridW = 696f; // 720 - pad*2
        float slotSz = (gridW - gap * (COLS - 1)) / COLS;
        float gridH = slotSz * ROWS + gap * (ROWS - 1);
        float panelH = gridH + 24f;

        var panel = NewUI("MergeGridPanel", parent.transform);
        SetRT(panel, V2(0,1), V2(1,1), V2(0.5f,1f), V2(0,-54), V2(-24, panelH));
        panel.AddComponent<Image>().color = PanelDark;

        var cont = NewUI("GridContainer", panel.transform);
        SetRT(cont, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(gridW, gridH));
        var glg = cont.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(slotSz, slotSz);
        glg.spacing  = new Vector2(gap, gap);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = COLS;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.padding = new RectOffset(0,0,0,0);

        HashSet<int> locked = new HashSet<int>
            { 2,3,4,5,6, 7,8,9,10,11,12,13, 17,18,19,20, 21,22,23,24,25,26,27 };
        Dictionary<int,int> init = new Dictionary<int,int> { {0,1}, {14,2}, {15,3} };

        for (int i = 0; i < ROWS * COLS; i++)
        {
            int row = i / COLS, col = i % COLS;
            bool isLock = locked.Contains(i);

            var slot = NewUI("Slot_" + row + "_" + col, cont.transform);
            var slotImg = slot.AddComponent<Image>();
            slotImg.color = isLock ? SlotLocked : SlotEmpty;

            if (isLock)
            {
                MakeText(slot.transform, "■", 16,
                    new Color(0.55f,0.48f,0.38f,0.8f), TextAnchor.MiddleCenter);
            }
            else
            {
                slot.AddComponent<Button>().targetGraphic = slotImg;
                if (init.ContainsKey(i))
                {
                    int lv = init[i];
                    slotImg.color = SlotHas;

                    var icon = NewUI("Icon", slot.transform);
                    SetRT(icon, V2(0.1f,0.2f), V2(0.9f,0.85f), V2(0.5f,0.5f), V2(0,0), V2(0,0));
                    var iconImg = icon.AddComponent<Image>();
                    iconImg.color = LvColors[Mathf.Clamp(lv - 1, 0, LvColors.Length - 1)];
                    iconImg.raycastTarget = false;

                    var lvGO = NewUI("Level", slot.transform);
                    SetRT(lvGO, V2(0.5f,0f), V2(1f,0.35f), V2(0.5f,0f), V2(0,0), V2(0,0));
                    MakeText(lvGO.transform, lv.ToString(), 15, DarkText, TextAnchor.MiddleCenter);
                }
            }
        }
    }

    // ── 액션 버튼 행 ─────────────────────────────────────────────────
    static void BuildActionRow(RectTransform parent)
    {
        // GridPanel Y 계산
        int ROWS = 4, COLS = 7;
        float gap = 5f, gridW = 696f;
        float slotSz = (gridW - gap * (COLS-1)) / COLS;
        float gridH = slotSz * ROWS + gap * (ROWS-1);
        float panelH = gridH + 24f;
        float actionY = -54f - panelH - 8f;

        var row = NewUI("ActionRow", parent.transform);
        SetRT(row, V2(0,1), V2(1,1), V2(0.5f,1f), V2(0, actionY), V2(-24, 76));

        // AUTO MERGE
        var autoBtn = NewUI("AutoMergeBtn", row.transform);
        SetRT(autoBtn, V2(0,0), V2(0,1), V2(0,0.5f), V2(0,0), V2(116,0));
        var aImg = autoBtn.AddComponent<Image>(); aImg.color = YellowBtn;
        autoBtn.AddComponent<Button>().targetGraphic = aImg;
        MakeText(autoBtn.transform, "AUTO\nMERGE", 17, DarkText, TextAnchor.MiddleCenter);

        // SPAWN
        var spawnBtn = NewUI("SpawnBtn", row.transform);
        SetRT(spawnBtn, V2(0,0), V2(1,1), V2(0.5f,0.5f), V2(62,0), V2(-240,0));
        var sImg = spawnBtn.AddComponent<Image>(); sImg.color = GreenBtn;
        spawnBtn.AddComponent<Button>().targetGraphic = sImg;
        MakeText(spawnBtn.transform, "2 / 10", 24, Color.white, TextAnchor.MiddleCenter);

        // BULK
        var bulkBtn = NewUI("BulkBtn", row.transform);
        SetRT(bulkBtn, V2(1,0), V2(1,1), V2(1f,0.5f), V2(0,0), V2(116,0));
        var bImg = bulkBtn.AddComponent<Image>(); bImg.color = BlueBtn;
        bulkBtn.AddComponent<Button>().targetGraphic = bImg;
        MakeText(bulkBtn.transform, "x11", 22, Color.white, TextAnchor.MiddleCenter);
    }

    // ─── 유틸 ────────────────────────────────────────────────────────
    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform SetRT(RectTransform rt,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform SetRT(GameObject go,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        return SetRT(go.GetComponent<RectTransform>(), ancMin, ancMax, pivot, pos, size);
    }

    static Text MakeText(Transform parent, string txt, int sz, Color col, TextAnchor align)
    {
        var go = NewUI("Text", parent);
        SetRT(go, V2(0,0), V2(1,1), V2(0.5f,0.5f), V2(0,0), V2(0,0));
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = sz; t.color = col; t.alignment = align;
        t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
        return t;
    }

    static Vector2 V2(float x, float y) { return new Vector2(x, y); }
}
#endif
