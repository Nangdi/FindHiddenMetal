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
// 구조 (세로 1200x1920, 배치 좌표는 디자인 PSD 기준 / Docs/Design/psd_layout.json)
//  Canvas_FindHiddenMetal.prefab (CanvasScaler Expand: 어떤 해상도에서도 1200x1920 전체가 보임)
//   └ Frame (1200x1920 고정)
//      └ Panel_xxx.prefab (Idle, Guide, Countdown, Play, Result, Ready, Edit)
//         ├ StaticLayer  : 정적 PNG 한 장 (4.Sprite/Static/Static_xxx.png)
//         └ 동적 요소    : 버튼, 숫자, 격자 칸(GridCell/ResultCell 런타임 생성), 랭킹 줄(RankRow 런타임 생성)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// 프리팹을 처음 상태로 되돌리고 싶으면 해당 .prefab 파일을 지운 뒤 다시 실행한다.
public static class MetalPrefabBuilder
{
    private const string CanvasName = "Canvas_FindHiddenMetal";
    private const string SystemName = "MetalSystem";
    private const string FrameName = "Frame"; // 기준 해상도 크기의 화면 틀 (패널들의 부모)

    // 랭킹 줄 (결과 화면 랭킹 박스 폭, 줄 높이)
    private const float RankRowWidth = 1050f;
    private const float RankRowHeight = 80f;

    private static GameObject cellPrefab;
    private static GameObject resultCellPrefab;
    private static GameObject rankRowPrefab;

    [MenuItem("Tools/숨은 금속 찾기/1. 프리팹 생성 및 씬 적용", priority = 1)]
    public static void BuildAll()
    {
        MetalSpriteExporter.EnsurePlaceholders();
        EnsureFolder(PartPrefabDir);
        EnsureFolder(PanelPrefabDir);

        // 부품
        cellPrefab = GetOrCreate(PartPrefabPath("GridCell"), CreateGridCell);
        resultCellPrefab = GetOrCreate(PartPrefabPath("ResultCell"), CreateResultCell);
        rankRowPrefab = GetOrCreate(PartPrefabPath("RankRow"), CreateRankRow);

        // 패널
        var panelBuilders = new Dictionary<string, Func<GameObject>>
        {
            { "Idle", CreateIdlePanel },
            { "Guide", CreateGuidePanel },
            { "Countdown", CreateCountdownPanel },
            { "Play", CreatePlayPanel },
            { "Result", CreateResultPanel },
            { "Ready", CreateReadyPanel },
            { "Edit", CreateEditPanel },
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

    // 글자가 구워진 스프라이트 버튼 (스프라이트 크기 = 배치 크기). disabledSprite가 있으면 비활성 때 바꿔 보인다.
    private static Button CreateSpriteButton(RectTransform parent, string name, string sprite, Vector2 pos, Vector2 size,
        string disabledSprite = null)
    {
        RectTransform rt = Place(CreateRect(name, parent), pos, size);
        Image image = AddSprite(rt, Part(sprite));
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (disabledSprite != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.disabledSprite = Part(disabledSprite);
            button.spriteState = state;
        }
        return button;
    }

    // 디자인 글꼴 숫자 텍스트 (한 줄)
    private static TextMeshProUGUI CreateNumber(RectTransform parent, string name, string sample, Vector2 pos, Vector2 size,
        float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI text = CreateText(parent, name, sample, pos, size, fontSize, color, true, alignment);
        text.font = font;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // 격자 영역. 칸 크기는 GridBoard가 런타임에 gridSize로 나눠 정한다.
    private static GridBoard CreateBoard(RectTransform parent, string name, Vector2 pos, Vector2 size, GameObject cell)
    {
        RectTransform root = Place(CreateRect(name, parent), pos, size);
        var grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(GridLine, GridLine, GridLine, GridLine);
        grid.spacing = new Vector2(GridLine, GridLine);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        var board = root.gameObject.AddComponent<GridBoard>();
        Wire(board, ("cellPrefab", cell.GetComponent<GridCellView>()));
        return board;
    }

    #region 부품 프리팹

    // 마크 이미지 (칸을 꽉 채움). CanvasGroup은 남기지 않는 마크를 흐리게 사라지게 할 때 쓴다.
    private static CanvasGroup CreateMark(RectTransform root, string name, string sprite)
    {
        RectTransform mark = StretchFull(CreateRect(name, root));
        AddSprite(mark, Part(sprite));
        var group = mark.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        mark.gameObject.SetActive(false);
        return group;
    }

    // 진행 화면 격자 한 칸: Background(투명 터치 영역) / Highlight(선택) / FoundMark(정답) / WrongMark(오답) / DebugMark
    // 칸 번호와 격자 선은 Static_Play.png에 있다.
    private static GameObject CreateGridCell()
    {
        RectTransform root = CreateRect("GridCell", null);
        root.sizeDelta = new Vector2(92, 88);

        Image background = AddImage(StretchFull(CreateRect("Background", root)), Color.clear);
        background.raycastTarget = true;

        RectTransform highlight = StretchFull(CreateRect("Highlight", root));
        AddSprite(highlight, Part("Cell_Select"));
        highlight.gameObject.SetActive(false);

        CanvasGroup found = CreateMark(root, "FoundMark", "Mark_Correct");
        CanvasGroup wrong = CreateMark(root, "WrongMark", "Mark_Wrong");

        // 개발자용 정답 위치 표시 (F2). 다른 마크 위에 작게 그린다.
        RectTransform debug = Stretch(CreateRect("DebugMark", root), Vector2.zero, Vector2.one, 30);
        AddSprite(debug, Part("Dot"), DebugColor).preserveAspect = true;
        debug.gameObject.SetActive(false);

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        var view = root.gameObject.AddComponent<GridCellView>();
        Wire(view,
            ("button", button),
            ("highlight", highlight.gameObject),
            ("foundMark", found),
            ("wrongMark", wrong),
            ("debugMark", debug.gameObject));
        return root.gameObject;
    }

    // 결과 화면 격자 한 칸 (누를 수 없음): FoundMark(정답 일치) / RevealMark(못 찾은 정답) / WrongMark(오답)
    private static GameObject CreateResultCell()
    {
        RectTransform root = CreateRect("ResultCell", null);
        root.sizeDelta = new Vector2(65, 63);

        CanvasGroup found = CreateMark(root, "FoundMark", "Mark_Match");
        CanvasGroup reveal = CreateMark(root, "RevealMark", "Mark_Correct");
        CanvasGroup wrong = CreateMark(root, "WrongMark", "Mark_Wrong");

        var view = root.gameObject.AddComponent<GridCellView>();
        Wire(view,
            ("foundMark", found),
            ("revealMark", reveal.gameObject),
            ("wrongMark", wrong));
        return root.gameObject;
    }

    // 랭킹 한 줄의 글자 (순위 / 기록 시각 / 소요 시간 / 찾은 개수). 아이콘과 줄 배경은 정적 레이어에 있다.
    // x 좌표는 줄 왼쪽 끝 기준 PSD 값. 기록 시각은 Paperlogy가 PSD 글꼴보다 넓어 30pt로 줄였다.
    private static RankRowView AddRankTexts(RectTransform root, Color normal, Color accent)
    {
        float left = -RankRowWidth * 0.5f;
        TMP_Text rank = CreateNumber(root, "Rank", "1위", new Vector2(left + 64, 0), new Vector2(90, 60), 28, normal, BoldFont);
        TMP_Text date = CreateNumber(root, "Date", "26.10.06 10:21:43", new Vector2(left + 131 + 170, 0), new Vector2(340, 60), 30,
            normal, BoldFont, TextAlignmentOptions.MidlineLeft);
        TMP_Text time = CreateNumber(root, "Time", "00:51", new Vector2(left + 787 + 60, 0), new Vector2(120, 60), 34,
            normal, MediumFont, TextAlignmentOptions.MidlineLeft);
        TMP_Text found = CreateNumber(root, "Found", "8/8", new Vector2(left + 1003 - 50, 0), new Vector2(100, 60), 34,
            normal, MediumFont, TextAlignmentOptions.MidlineRight);

        var view = root.gameObject.AddComponent<RankRowView>();
        Wire(view, ("rankText", rank), ("dateText", date), ("timeText", time), ("foundText", found));

        var so = new SerializedObject(view);
        so.FindProperty("normalColor").colorValue = normal;
        so.FindProperty("accentColor").colorValue = accent;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static GameObject CreateRankRow()
    {
        RectTransform root = CreateRect("RankRow", null);
        root.sizeDelta = new Vector2(RankRowWidth, RankRowHeight);
        AddRankTexts(root, TextColor, AccentColor);
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
        Button start = CreateSpriteButton(panel, "StartButton", "Button_Start", new Vector2(3, -762), new Vector2(1030, 230));

        var view = panel.gameObject.AddComponent<IdlePanel>();
        Wire(view, ("startButton", start));
        return panel.gameObject;
    }

    private static GameObject CreateGuidePanel()
    {
        RectTransform panel = CreatePanelRoot("Guide");
        Button confirm = CreateSpriteButton(panel, "ConfirmButton", "Button_Confirm", new Vector2(3, -828), new Vector2(1080, 190));

        var view = panel.gameObject.AddComponent<GuidePanel>();
        Wire(view, ("confirmButton", confirm));
        return panel.gameObject;
    }

    private static GameObject CreateCountdownPanel()
    {
        RectTransform panel = CreatePanelRoot("Countdown");
        TMP_Text number = CreateNumber(panel, "NumberText", "3", new Vector2(0, -62), new Vector2(400, 360), 300,
            CountdownColor, ExtraBoldFont);

        var view = panel.gameObject.AddComponent<CountdownPanel>();
        Wire(view, ("numberText", number));
        return panel.gameObject;
    }

    private static GameObject CreatePlayPanel()
    {
        RectTransform panel = CreatePanelRoot("Play");

        // 격자 칸은 GridBoard가 gridSize만큼 생성한다. 격자 선과 칸 번호는 정적 레이어에 있다.
        GridBoard board = CreateBoard(panel, "GridRoot", PlayGridCenter, PlayGridSize, cellPrefab);

        // 선택 수 ("/ 8"은 정적 레이어)
        TMP_Text selectCount = CreateNumber(panel, "SelectCountText", "0", new Vector2(-88, -566), new Vector2(120, 80), 66,
            GreenColor, BoldFont, TextAlignmentOptions.MidlineRight);

        // 시간 (흰 원 박스와 주황 원은 정적 레이어)
        TMP_Text time = CreateNumber(panel, "TimeText", "60", new Vector2(0, -775), new Vector2(190, 110), 82,
            LightTextColor, BoldFont);

        Button home = CreateSpriteButton(panel, "HomeButton", "Button_Home", new Vector2(-346, -783), new Vector2(380, 170));
        Button submit = CreateSpriteButton(panel, "SubmitButton", "Button_Submit", new Vector2(354, -783), new Vector2(380, 170),
            "Button_Submit_Off");
        submit.interactable = false;

        var view = panel.gameObject.AddComponent<PlayPanel>();
        Wire(view,
            ("board", board),
            ("timeText", time),
            ("selectCountText", selectCount),
            ("submitButton", submit),
            ("homeButton", home));
        return panel.gameObject;
    }

    private static GameObject CreateResultPanel()
    {
        RectTransform panel = CreatePanelRoot("Result");

        // 결과 격자 (격자 그림은 정적 레이어, 칸은 마크만)
        GridBoard board = CreateBoard(panel, "ResultGrid", ResultGridCenter, ResultGridSize, resultCellPrefab);

        // 기록 값 (라벨과 남색 박스는 정적 레이어)
        TMP_Text time = CreateNumber(panel, "TimeText", "01:23", new Vector2(-224, -158), new Vector2(200, 70), 48,
            LightTextColor, BoldFont, TextAlignmentOptions.MidlineLeft);
        TMP_Text found = CreateNumber(panel, "FoundText", "5개", new Vector2(157, -158), new Vector2(200, 70), 50,
            GreenColor, ExtraBoldFont, TextAlignmentOptions.MidlineLeft);
        TMP_Text wrong = CreateNumber(panel, "WrongText", "3개", new Vector2(454, -158), new Vector2(130, 70), 50,
            AccentColor, ExtraBoldFont, TextAlignmentOptions.MidlineLeft);

        // 랭킹 줄 (표시 인원만큼 런타임에 생성). 박스와 아이콘은 정적 레이어에 있다.
        RectTransform list = Place(CreateRect("RankList", panel), new Vector2(0, -436), new Vector2(RankRowWidth, RankRowHeight * 5));
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 나의 랭킹 줄 (주황 줄 + 배지)
        RectTransform mine = Place(CreateRect("MineRow", panel), new Vector2(0, -690), new Vector2(RankRowWidth, 90));
        AddSprite(mine, Part("RankRow_Mine"));
        // 배지는 기록 시각 바로 뒤 (PSD보다 오른쪽: 글꼴 폭 차이)
        AddSprite(Place(CreateRect("Badge", mine), new Vector2(-40, 0), new Vector2(130, 50)), Part("Badge_MyRank"));
        RankRowView mineView = AddRankTexts(mine, LightTextColor, LightTextColor);

        Button home = CreateSpriteButton(panel, "HomeButton", "Button_Home", new Vector2(4, -850), new Vector2(380, 170));

        var view = panel.gameObject.AddComponent<ResultPanel>();
        Wire(view,
            ("board", board),
            ("timeText", time),
            ("foundText", found),
            ("wrongText", wrong),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", list),
            ("mineRow", mineView),
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

    // 운영자용 정답 편집 화면. PSD 시안이 없어 정적 레이어는 진행 화면의 배경+격자만 쓰고(Static_Edit),
    // 글자와 버튼은 Kit 색과 Unity 기본 둥근 스프라이트로 만든다.
    private static GameObject CreateEditPanel()
    {
        RectTransform panel = CreatePanelRoot("Edit");

        CreateText(panel, "TitleText", "정답 편집 모드", new Vector2(0, 860), new Vector2(1100, 80), 56, TextColor, true);
        TMP_Text status = CreateText(panel, "StatusText", "지금 원판에서 금속이 있는 칸 8곳을 터치하세요.\n이 배치가 회전 0단계 정답이 됩니다.",
            new Vector2(0, 735), new Vector2(1120, 120), 34, TextColor);

        GridBoard board = CreateBoard(panel, "GridRoot", PlayGridCenter, PlayGridSize, cellPrefab);

        // 선택 수 + 회전 방향
        TMP_Text count = CreateText(panel, "CountText", "선택 8 / 8", new Vector2(-330, -515), new Vector2(420, 90), 46, TextColor, true,
            TextAlignmentOptions.MidlineLeft);
        CreateText(panel, "DirectionLabel", "회전 방향", new Vector2(-40, -515), new Vector2(180, 90), 32, TextColor, false,
            TextAlignmentOptions.MidlineRight);
        Button cw = CreateBoxButton(panel, "ClockwiseButton", "시계", new Vector2(160, -515), new Vector2(190, 90), 36, TextColor,
            out GameObject cwHighlight);
        Button ccw = CreateBoxButton(panel, "CounterClockwiseButton", "반시계", new Vector2(370, -515), new Vector2(190, 90), 36,
            TextColor, out GameObject ccwHighlight);

        // 회전 테스트
        Button rotate = CreateBoxButton(panel, "RotateButton", "원판 회전 테스트", new Vector2(-300, -650), new Vector2(480, 110), 40,
            TextColor, out _);
        TMP_Text rotateInfo = CreateText(panel, "RotateInfoText", "원판을 90° 돌려 예상 위치를 확인합니다.", new Vector2(260, -650),
            new Vector2(600, 110), 30, TextColor, false, TextAlignmentOptions.MidlineLeft);

        Button cancel = CreateBoxButton(panel, "CancelButton", "취소", new Vector2(-280, -815), new Vector2(460, 140), 48, TextColor, out _);
        Button save = CreateBoxButton(panel, "SaveButton", "저장", new Vector2(280, -815), new Vector2(460, 140), 48, AccentColor, out _);

        var view = panel.gameObject.AddComponent<EditPanel>();
        Wire(view,
            ("board", board),
            ("statusText", status),
            ("countText", count),
            ("rotateInfoText", rotateInfo),
            ("clockwiseButton", cw),
            ("clockwiseHighlight", cwHighlight),
            ("counterClockwiseButton", ccw),
            ("counterClockwiseHighlight", ccwHighlight),
            ("rotateButton", rotate),
            ("saveButton", save),
            ("cancelButton", cancel));
        var so = new SerializedObject(view);
        so.FindProperty("accentColor").colorValue = AccentColor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel.gameObject;
    }

    // 글자 버튼 (운영자 화면용). Highlight는 강조색으로 덮어 고른 상태를 보여 준다 (방향 버튼).
    private static Button CreateBoxButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize,
        Color color, out GameObject highlight)
    {
        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        RectTransform rt = Place(CreateRect(name, parent), pos, size);
        Image image = AddSprite(rt, round, color);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        RectTransform hl = StretchFull(CreateRect("Highlight", rt));
        AddSprite(hl, round, AccentColor).type = Image.Type.Sliced;
        hl.gameObject.SetActive(false);
        highlight = hl.gameObject;

        TextMeshProUGUI text = AddText(StretchFull(CreateRect("Label", rt)), label, fontSize, LightTextColor, true);
        text.enableWordWrapping = false;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = DisabledTint;
        button.colors = colors;
        return button;
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

        // 화면 비율이 기준(1200x1920 세로)과 달라도 화면 전체가 보이도록 Expand로 맞추고,
        // 패널은 기준 크기의 Frame 안에 둔다. 남는 부분은 카메라 배경색(검정)으로 채워진다.
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        go.AddComponent<GraphicRaycaster>();

        RectTransform frame = Place(CreateRect(FrameName, go.transform), Vector2.zero, ReferenceResolution);

        // 편집하기 쉽도록 대기 화면만 켜 둔다. 실행 시에는 FlowController가 단계에 맞게 켜고 끈다.
        for (int i = 0; i < panels.Count; i++)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(panels[i], frame);
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
            ("countdownPanel", canvas.GetComponentInChildren<CountdownPanel>(true)),
            ("playPanel", canvas.GetComponentInChildren<PlayPanel>(true)),
            ("resultPanel", canvas.GetComponentInChildren<ResultPanel>(true)),
            ("readyPanel", canvas.GetComponentInChildren<ReadyPanel>(true)),
            ("editPanel", canvas.GetComponentInChildren<EditPanel>(true)));

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
