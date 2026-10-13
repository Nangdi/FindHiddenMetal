using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MetalUIKit;

// 숨은 금속 찾기 UI 프리팹을 만들고 현재 씬에 배치한다.
// 메뉴: Tools > 숨은 금속 찾기 > 1. 프리팹 생성 및 씬 적용
//
// 구조
//  Canvas_FindHiddenMetal.prefab
//   └ Panel_xxx.prefab (Idle, Guide, Play, Result, Ready)
//      ├ StaticLayer  : 정적 PNG 한 장 (4.Sprite/Static/Static_xxx.png)
//      └ 동적 요소    : 버튼, 격자(GridCell 런타임 생성), 기록, 랭킹 줄(RankRow 런타임 생성)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// 프리팹을 처음 상태로 되돌리고 싶으면 해당 .prefab 파일을 지운 뒤 다시 실행한다.
public static class MetalPrefabBuilder
{
    private const string CanvasName = "Canvas_FindHiddenMetal";
    private const string SystemName = "MetalSystem";

    private static GameObject cellPrefab;
    private static GameObject rankRowPrefab;

    [MenuItem("Tools/숨은 금속 찾기/1. 프리팹 생성 및 씬 적용", priority = 1)]
    public static void BuildAll()
    {
        MetalSpriteExporter.Export(overwrite: false);
        EnsureFolder(PartPrefabDir);
        EnsureFolder(PanelPrefabDir);

        // 부품
        cellPrefab = GetOrCreate(PartPrefabPath("GridCell"), CreateGridCell);
        rankRowPrefab = GetOrCreate(PartPrefabPath("RankRow"), CreateRankRow);

        // 패널
        var panelBuilders = new Dictionary<string, Func<GameObject>>
        {
            { "Idle", CreateIdlePanel },
            { "Guide", CreateGuidePanel },
            { "Play", CreatePlayPanel },
            { "Result", CreateResultPanel },
            { "Ready", CreateReadyPanel },
        };
        var panels = new List<GameObject>();
        foreach (string panel in PanelNames)
            panels.Add(GetOrCreate(PanelPrefabPath(panel), panelBuilders[panel]));

        GameObject canvasPrefab = GetOrCreate(CanvasPrefabPath, () => CreateCanvas(panels));

        ApplyToScene(canvasPrefab);
        MetalSpriteExporter.ExportGuides();
        AssetDatabase.SaveAssets();
        Debug.Log("숨은 금속 찾기 프리팹 적용 완료. 씬을 저장(Ctrl+S)하세요.");
    }

    private static GameObject GetOrCreate(string path, Func<GameObject> build)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject temp = build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        UnityEngine.Object.DestroyImmediate(temp);
        Debug.Log($"프리팹 생성: {path}");
        return prefab;
    }

    private static Sprite Part(string name) => LoadSprite(PartSpritePath(name));

    // 글자가 구워진 스프라이트 버튼 (스프라이트 크기 = 배치 크기)
    private static Button CreateSpriteButton(RectTransform parent, string name, string sprite, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Place(CreateRect(name, parent), pos, size);
        Image image = AddSprite(rt, Part(sprite));
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    #region 부품 프리팹

    // 격자 한 칸: Background(터치 영역) / Highlight(선택) / FoundMark / RevealMark / WrongMark / DebugMark
    private static GameObject CreateGridCell()
    {
        RectTransform root = CreateRect("GridCell", null);
        root.sizeDelta = new Vector2(CellSize, CellSize);

        Image background = AddSprite(StretchFull(CreateRect("Background", root)), Part("Cell_Bg"), CellColor);
        background.raycastTarget = true;

        RectTransform highlight = StretchFull(CreateRect("Highlight", root));
        AddSprite(highlight, Part("Cell_Bg"), AccentColor);
        highlight.gameObject.SetActive(false);

        RectTransform found = Stretch(CreateRect("FoundMark", root), Vector2.zero, Vector2.one, 10);
        AddSprite(found, Part("Dot"), FoundColor).preserveAspect = true;
        var foundGroup = found.gameObject.AddComponent<CanvasGroup>();
        foundGroup.blocksRaycasts = false;
        found.gameObject.SetActive(false);

        RectTransform reveal = Stretch(CreateRect("RevealMark", root), Vector2.zero, Vector2.one, 10);
        AddSprite(reveal, Part("Dot"), RevealColor).preserveAspect = true;
        reveal.gameObject.SetActive(false);

        RectTransform wrong = Stretch(CreateRect("WrongMark", root), Vector2.zero, Vector2.one, 6);
        AddSprite(wrong, Part("Mark_Wrong")).preserveAspect = true;
        var wrongGroup = wrong.gameObject.AddComponent<CanvasGroup>();
        wrongGroup.blocksRaycasts = false;
        wrong.gameObject.SetActive(false);

        // 개발자용 정답 위치 표시 (F2). 다른 마크 위에 작게 그린다.
        RectTransform debug = Stretch(CreateRect("DebugMark", root), Vector2.zero, Vector2.one, 22);
        AddSprite(debug, Part("Dot"), DebugColor).preserveAspect = true;
        debug.gameObject.SetActive(false);

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var view = root.gameObject.AddComponent<GridCellView>();
        Wire(view,
            ("button", button),
            ("highlight", highlight.gameObject),
            ("foundMark", foundGroup),
            ("revealMark", reveal.gameObject),
            ("wrongMark", wrongGroup),
            ("debugMark", debug.gameObject));
        return root.gameObject;
    }

    // 랭킹 한 줄: 순위 / 시간. Highlight = 방금 체험한 기록
    private static GameObject CreateRankRow()
    {
        RectTransform root = CreateRect("RankRow", null);
        root.sizeDelta = new Vector2(740, 72);
        AddSprite(root, Part("RankRow_Bg"), RankRowColor);

        RectTransform highlight = StretchFull(CreateRect("Highlight", root));
        AddSprite(highlight, Part("RankRow_Bg"), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f));
        highlight.gameObject.SetActive(false);

        TMP_Text rankText = AddText(Stretch(CreateRect("Rank", root), new Vector2(0.04f, 0f), new Vector2(0.4f, 1f)),
            "1위", 34, TextColor, true, TextAlignmentOptions.MidlineLeft);
        TMP_Text timeText = AddText(Stretch(CreateRect("Time", root), new Vector2(0.5f, 0f), new Vector2(0.96f, 1f)),
            "01:23", 34, TextColor, true, TextAlignmentOptions.MidlineRight);

        var view = root.gameObject.AddComponent<RankRowView>();
        Wire(view, ("rankText", rankText), ("timeText", timeText), ("highlight", highlight.gameObject));
        return root.gameObject;
    }

    #endregion

    #region 패널 프리팹

    private static RectTransform CreatePanelRoot(string panel)
    {
        RectTransform root = StretchFull(CreateRect($"Panel_{panel}", null));
        Image staticLayer = AddSprite(StretchFull(CreateRect(StaticLayerName, root)), LoadSprite(StaticSpritePath(panel)));
        staticLayer.type = Image.Type.Simple;
        return root;
    }

    private static GameObject CreateIdlePanel()
    {
        RectTransform panel = CreatePanelRoot("Idle");
        Button start = CreateSpriteButton(panel, "StartButton", "Button_Start", new Vector2(0, -300), new Vector2(520, 130));

        var view = panel.gameObject.AddComponent<IdlePanel>();
        Wire(view, ("startButton", start));
        return panel.gameObject;
    }

    private static GameObject CreateGuidePanel()
    {
        RectTransform panel = CreatePanelRoot("Guide");
        Button confirm = CreateSpriteButton(panel, "ConfirmButton", "Button_Confirm", new Vector2(0, -470), new Vector2(520, 130));

        var view = panel.gameObject.AddComponent<GuidePanel>();
        Wire(view, ("confirmButton", confirm));
        return panel.gameObject;
    }

    private static GameObject CreatePlayPanel()
    {
        RectTransform panel = CreatePanelRoot("Play");

        // 격자 칸은 PlayPanel이 gridSize만큼 생성한다. 틀과 열/행 표기는 정적 레이어에 있다.
        RectTransform gridRoot = Place(CreateRect("GridRoot", panel), GridCenter, new Vector2(GridPixelSize, GridPixelSize));
        var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CellSize, CellSize);
        grid.spacing = new Vector2(CellSpacing, CellSpacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridCount;

        TMP_Text found = CreateText(panel, "FoundText", "0 / 8", MetalStaticLayouts.FoundBoxCenter + new Vector2(0, -30),
            new Vector2(560, 110), 84, TextColor, true);
        TMP_Text timeLabel = CreateText(panel, "TimeLabel", "진행 시간", MetalStaticLayouts.TimeBoxCenter + new Vector2(0, 70),
            new Vector2(560, 50), 32, SubTextColor, true);
        TMP_Text time = CreateText(panel, "TimeText", "00:00", MetalStaticLayouts.TimeBoxCenter + new Vector2(0, -30),
            new Vector2(560, 110), 84, TextColor, true);

        TMP_Text selectCount = CreateText(panel, "SelectCountText", "고른 칸 <color=#E42C34>0</color> / 8",
            new Vector2(MetalStaticLayouts.InfoX, -265), new Vector2(560, 50), 34, TextColor, true);
        Button submit = CreateSpriteButton(panel, "SubmitButton", "Button_Submit",
            new Vector2(MetalStaticLayouts.InfoX, -370), new Vector2(520, 130));
        submit.interactable = false;

        var view = panel.gameObject.AddComponent<PlayPanel>();
        Wire(view,
            ("cellPrefab", cellPrefab.GetComponent<GridCellView>()),
            ("gridRoot", gridRoot),
            ("foundText", found),
            ("timeText", time),
            ("timeLabelText", timeLabel),
            ("selectCountText", selectCount),
            ("submitButton", submit));
        return panel.gameObject;
    }

    private static GameObject CreateResultPanel()
    {
        RectTransform panel = CreatePanelRoot("Result");

        RectTransform success = Place(CreateRect("Group_Success", panel), new Vector2(0, 430), new Vector2(900, 140));
        AddSprite(success, Part("Result_Title_Success")).preserveAspect = true;
        RectTransform fail = Place(CreateRect("Group_Fail", panel), new Vector2(0, 430), new Vector2(900, 140));
        AddSprite(fail, Part("Result_Title_Fail")).preserveAspect = true;
        fail.gameObject.SetActive(false);

        // 기록 값 (라벨과 박스는 정적 레이어)
        string[] names = { "TimeText", "FoundText", "WrongText", "RankText" };
        string[] samples = { "01:23", "8 / 8", "2", "3위 <size=50%>/ 126명</size>" };
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            Vector2 pos = MetalStaticLayouts.RecordBoxCenter + new Vector2(180, MetalStaticLayouts.RecordRowY[i]);
            values[i] = CreateText(panel, names[i], samples[i], pos, new Vector2(380, 100), 64, i == 3 ? AccentColor : TextColor, true,
                TextAlignmentOptions.MidlineRight);
        }

        // 랭킹 줄 (표시 인원만큼 런타임에 생성). 박스와 제목은 정적 레이어에 있다.
        RectTransform list = Place(CreateRect("RankList", panel), MetalStaticLayouts.RankBoxCenter + new Vector2(0, -40),
            new Vector2(760, 540));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button again = CreateSpriteButton(panel, "AgainButton", "Button_Again", new Vector2(-200, -500), new Vector2(360, 110));
        Button home = CreateSpriteButton(panel, "HomeButton", "Button_Home", new Vector2(200, -500), new Vector2(360, 110));

        var view = panel.gameObject.AddComponent<ResultPanel>();
        Wire(view,
            ("successGroup", success.gameObject),
            ("failGroup", fail.gameObject),
            ("timeText", values[0]),
            ("foundText", values[1]),
            ("wrongText", values[2]),
            ("rankText", values[3]),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", list),
            ("againButton", again),
            ("homeButton", home));
        return panel.gameObject;
    }

    // 회전 대기 화면은 상호작용이 없어 정적 레이어 한 장뿐이다.
    private static GameObject CreateReadyPanel()
    {
        RectTransform panel = CreatePanelRoot("Ready");
        panel.gameObject.AddComponent<ReadyPanel>();
        return panel.gameObject;
    }

    #endregion

    #region 캔버스와 씬

    private static GameObject CreateCanvas(List<GameObject> panels)
    {
        var go = new GameObject(CanvasName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0; // 설정창(SettingsCanvas)보다 아래

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // 편집하기 쉽도록 대기 화면만 켜 둔다. 실행 시에는 FlowController가 단계에 맞게 켜고 끈다.
        for (int i = 0; i < panels.Count; i++)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(panels[i], go.transform);
            panel.SetActive(i == 0);
        }
        return go;
    }

    private static void ApplyToScene(GameObject canvasPrefab)
    {
        Undo.SetCurrentGroupName("Apply FindHiddenMetal UI");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject canvas = FindRoot(CanvasName);

        // 프리팹이 아닌 캔버스는 프리팹으로 교체한다
        if (canvas != null && PrefabUtility.GetCorrespondingObjectFromSource(canvas) != canvasPrefab)
        {
            Undo.DestroyObjectImmediate(canvas);
            canvas = null;
        }

        if (canvas == null)
        {
            canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(canvas, "Create FindHiddenMetal Canvas");
        }

        FlowController controller = SetupSystem();
        Wire(controller,
            ("idlePanel", canvas.GetComponentInChildren<IdlePanel>(true)),
            ("guidePanel", canvas.GetComponentInChildren<GuidePanel>(true)),
            ("playPanel", canvas.GetComponentInChildren<PlayPanel>(true)),
            ("resultPanel", canvas.GetComponentInChildren<ResultPanel>(true)),
            ("readyPanel", canvas.GetComponentInChildren<ReadyPanel>(true)));

        SetupDeviceLink();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        Selection.activeGameObject = canvas;
    }

    private static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    // 흐름 컨트롤러와 공용 GameManager/GameTimer (씬에 없을 때만 추가)
    private static FlowController SetupSystem()
    {
        GameObject go = FindRoot(SystemName);
        if (go == null)
        {
            go = new GameObject(SystemName);
            Undo.RegisterCreatedObjectUndo(go, "Create Metal System");
        }

        if (UnityEngine.Object.FindObjectOfType<GameManager>(true) == null) Undo.AddComponent<GameManager>(go);
        if (UnityEngine.Object.FindObjectOfType<GameTimer>(true) == null) Undo.AddComponent<GameTimer>(go);
        return GetOrAdd<FlowController>(go);
    }

    // 씬의 기본 SerialPortManager를 장치 통신을 처리하는 MetalDeviceLink로 교체한다.
    private static void SetupDeviceLink()
    {
        if (UnityEngine.Object.FindObjectOfType<MetalDeviceLink>(true) != null) return;

        foreach (var manager in UnityEngine.Object.FindObjectsOfType<SerialPortManager>(true))
        {
            if (manager.GetType() != typeof(SerialPortManager)) continue;

            var go = manager.gameObject;
            Undo.DestroyObjectImmediate(manager);
            Undo.AddComponent<MetalDeviceLink>(go);
            Debug.Log($"{go.name}: SerialPortManager → MetalDeviceLink 교체");
            return;
        }

        var newGo = new GameObject("SerialPortManager");
        Undo.RegisterCreatedObjectUndo(newGo, "Create MetalDeviceLink");
        newGo.AddComponent<MetalDeviceLink>();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    #endregion
}
