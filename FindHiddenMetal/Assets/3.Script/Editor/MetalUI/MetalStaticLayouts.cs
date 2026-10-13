using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static MetalUIKit;

// 정적 레이어(화면당 PNG 한 장)와 글자가 들어간 부품 스프라이트의 "임시 디자인".
// 여기 있는 내용은 PNG로 구워질 뿐 씬/프리팹에는 오브젝트로 남지 않는다.
// 문구나 배치를 바꾸려면 여기를 고친 뒤 "임시 스프라이트 다시 내보내기"를 실행하거나, 디자인 PNG로 덮어쓴다.
public static class MetalStaticLayouts
{
    // 화면 전체(1920x1200)에 그려지는 패널별 정적 내용
    public static readonly Dictionary<string, Action<RectTransform>> Panels = new Dictionary<string, Action<RectTransform>>
    {
        { "Idle", Idle },
        { "Guide", Guide },
        { "Play", Play },
        { "Result", Result },
        { "Ready", Ready },
    };

    // 결과 화면 기록 칸 위치 (PrefabBuilder와 같이 쓴다)
    public static readonly Vector2 RecordBoxCenter = new Vector2(-460, -20);
    public static readonly Vector2 RecordBoxSize = new Vector2(820, 680);
    public static readonly float[] RecordRowY = { 210, 70, -70, -210 };
    public static readonly Vector2 RankBoxCenter = new Vector2(460, -20);
    public static readonly Vector2 RankBoxSize = new Vector2(820, 680);

    // 진행 화면 오른쪽 정보 칸
    public const float InfoX = 560f;
    public static readonly Vector2 FoundBoxCenter = new Vector2(InfoX, 260);
    public static readonly Vector2 TimeBoxCenter = new Vector2(InfoX, -10);
    public static readonly Vector2 InfoBoxSize = new Vector2(600, 230);

    // 글자가 들어간 부품 스프라이트 (이름, 크기, 그리기). 배경은 투명.
    public static readonly (string name, Vector2Int size, Action<RectTransform> draw)[] TextParts =
    {
        ("Button_Start", new Vector2Int(520, 130), root => ButtonSprite(root, "체험 시작", AccentColor, TextColor, 52)),
        ("Button_Confirm", new Vector2Int(520, 130), root => ButtonSprite(root, "시작하기", AccentColor, TextColor, 52)),
        ("Button_Submit", new Vector2Int(520, 130), root => ButtonSprite(root, "위치 확인", AccentColor, TextColor, 52)),
        ("Button_Again", new Vector2Int(360, 110), root => ButtonSprite(root, "다시 체험", AccentColor, TextColor, 42)),
        ("Button_Home", new Vector2Int(360, 110), root => ButtonSprite(root, "처음으로", CardColor, TextColor, 42)),
        ("Result_Title_Success", new Vector2Int(900, 140), root => FillText(root, "Text", "탐색 완료!", 92, AccentColor, true)),
        ("Result_Title_Fail", new Vector2Int(900, 140), root => FillText(root, "Text", "시간 종료", 92, SubTextColor, true)),
        ("Mark_Wrong", new Vector2Int(64, 64), WrongMark),
    };

    private static void ButtonSprite(RectTransform root, string text, Color box, Color textColor, float fontSize)
    {
        AddRoundBox(root, box);
        FillText(root, "Text", text, fontSize, textColor, true);
    }

    // X 표시 (막대 두 개를 엇갈려 그린다. 특수문자 글꼴 누락 방지)
    private static void WrongMark(RectTransform root)
    {
        foreach (float angle in new[] { 45f, -45f })
        {
            RectTransform bar = Stretch(CreateRect("Bar", root), new Vector2(-0.05f, 0.41f), new Vector2(1.05f, 0.59f));
            bar.localRotation = Quaternion.Euler(0, 0, angle);
            AddImage(bar, WrongColor);
        }
    }

    private static void Background(RectTransform root)
    {
        AddImage(StretchFull(CreateRect("Background", root)), BgColor);
        // 위쪽 강조 띠
        AddImage(Stretch(CreateRect("TopBar", root), new Vector2(0, 1), Vector2.one), AccentColor).rectTransform.offsetMin = new Vector2(0, -8);
    }

    private static void Idle(RectTransform root)
    {
        Background(root);
        CreateText(root, "Exhibit", "전기12  |  전자기 유도", new Vector2(0, 330), new Vector2(1200, 60), 34, SubTextColor, true);
        CreateText(root, "Title", "숨은 금속 찾기", new Vector2(0, 210), new Vector2(1600, 170), 128, TextColor, true);
        CreateText(root, "Description", "테이블 위의 금속탐지기를 움직여\n격자판 아래 숨어 있는 <color=#E42C34>금속 8개</color>를 찾아보세요!",
            new Vector2(0, 20), new Vector2(1500, 150), 44, TextColor);
        CreateText(root, "Hint_Start", "아래 버튼을 눌러 체험을 시작하세요", new Vector2(0, -470), new Vector2(1200, 50), 28, SubTextColor);
    }

    private static void Guide(RectTransform root)
    {
        Background(root);
        CreateText(root, "Title", "체험 방법", new Vector2(0, 470), new Vector2(1200, 100), 68, TextColor, true);

        string[] heads = { "탐지기 움직이기", "신호 확인하기", "위치 입력하기" };
        string[] bodies =
        {
            "손잡이를 잡고 금속탐지기를\n격자판 위로 천천히 움직여요.",
            "숨은 금속에 가까워지면\n레벨미터와 검류계가 크게 움직여요.",
            "금속이 있는 칸을 최대 8개까지 고르고\n<color=#E42C34>[위치 확인]</color>을 눌러요.",
        };
        for (int i = 0; i < heads.Length; i++)
        {
            float x = -600 + i * 600;
            RectTransform box = CreateRoundBox(root, $"Box_Step{i + 1}", new Vector2(x, 40), new Vector2(540, 560), CardColor);
            RectTransform badge = CreateRoundBox(box, "Badge", new Vector2(0, 200), new Vector2(110, 110), AccentColor);
            FillText(badge, "Text", $"{i + 1}", 64, TextColor, true);
            CreateText(box, "Head", heads[i], new Vector2(0, 80), new Vector2(500, 70), 42, TextColor, true);
            CreateText(box, "Body", bodies[i], new Vector2(0, -80), new Vector2(480, 200), 30, SubTextColor);
        }

        RectTransform notice = CreateRoundBox(root, "NoticeBox", new Vector2(0, -330), new Vector2(1400, 80), SoftBoxColor);
        FillText(notice, "Text", "숨은 금속 8개를 모두 찾는 데 걸린 시간으로 순위가 정해져요.  ※ 틀리면 기록에 5초가 더해져요.", 28, TextColor);
    }

    private static void Play(RectTransform root)
    {
        Background(root);

        // 격자 틀과 열(A~P)/행(1~16) 표기. 칸은 런타임에 생성된다.
        float grid = GridPixelSize;
        CreateRoundBox(root, "GridFrame", GridCenter, new Vector2(grid + 24, grid + 24), CardColor);
        float pitch = CellSize + CellSpacing;
        float start = -grid * 0.5f + CellSize * 0.5f;
        for (int i = 0; i < GridCount; i++)
        {
            float offset = start + i * pitch;
            CreateText(root, $"Col_{i + 1}", $"{(char)('A' + i)}", GridCenter + new Vector2(offset, grid * 0.5f + 34),
                new Vector2(CellSize, 36), 26, SubTextColor, true);
            CreateText(root, $"Row_{i + 1}", $"{i + 1}", GridCenter + new Vector2(-grid * 0.5f - 40, -offset),
                new Vector2(50, CellSize), 26, SubTextColor, true);
        }

        // 오른쪽 정보 칸
        RectTransform foundBox = CreateRoundBox(root, "Box_Found", FoundBoxCenter, InfoBoxSize, CardColor);
        CreateText(foundBox, "Label", "찾은 금속", new Vector2(0, 70), new Vector2(560, 50), 32, SubTextColor, true);
        RectTransform timeBox = CreateRoundBox(root, "Box_Time", TimeBoxCenter, InfoBoxSize, CardColor);

        CreateText(root, "Hint_Submit", "금속이 있다고 생각하는 칸을 <color=#E42C34>최대 8개</color>까지 고른 뒤\n<color=#E42C34>[위치 확인]</color>을 누르세요.",
            new Vector2(InfoX, -180), new Vector2(600, 90), 28, TextColor);

        // 범례
        RectTransform legend = Place(CreateRect("Legend", root), new Vector2(InfoX, -510), new Vector2(600, 40));
        LegendItem(legend, new Vector2(-210, 0), "찾은 금속", icon => AddRoundBox(icon, FoundColor).pixelsPerUnitMultiplier = 0.8f);
        LegendItem(legend, new Vector2(0, 0), "틀린 위치", WrongMark);
        LegendItem(legend, new Vector2(210, 0), "못 찾은 금속", icon => AddRoundBox(icon, RevealColor).pixelsPerUnitMultiplier = 0.8f);
    }

    private static void LegendItem(RectTransform parent, Vector2 pos, string label, Action<RectTransform> drawIcon)
    {
        RectTransform icon = Place(CreateRect("Icon", parent), pos + new Vector2(-80, 0), new Vector2(26, 26));
        drawIcon(icon);
        CreateText(parent, "Label", label, pos + new Vector2(30, 0), new Vector2(180, 40), 24, SubTextColor, false, TextAlignmentOptions.MidlineLeft);
    }

    private static void Result(RectTransform root)
    {
        Background(root);

        RectTransform record = CreateRoundBox(root, "Box_Record", RecordBoxCenter, RecordBoxSize, CardColor);
        string[] labels = { "소요 시간", "찾은 금속", "틀린 횟수", "나의 순위" };
        for (int i = 0; i < labels.Length; i++)
        {
            CreateText(record, $"Label_{i + 1}", labels[i], new Vector2(-200, RecordRowY[i]), new Vector2(340, 60), 36, SubTextColor, true,
                TextAlignmentOptions.MidlineLeft);
            if (i < labels.Length - 1)
                AddImage(Place(CreateRect($"Line_{i + 1}", record), new Vector2(0, RecordRowY[i] - 70), new Vector2(740, 2)), SoftBoxColor);
        }

        RectTransform rank = CreateRoundBox(root, "Box_Rank", RankBoxCenter, RankBoxSize, CardColor);
        CreateText(rank, "Header", "랭킹", new Vector2(0, 285), new Vector2(760, 70), 40, TextColor, true);

        CreateText(root, "Notice", "※ 탐지 장치에 불이 켜진 곳이 숨은 금속의 위치입니다.",
            new Vector2(0, -400), new Vector2(1600, 50), 28, SubTextColor);
    }

    private static void Ready(RectTransform root)
    {
        Background(root);
        CreateText(root, "Title", "다음 체험을 준비하고 있어요", new Vector2(0, 50), new Vector2(1600, 100), 64, TextColor, true);
        CreateText(root, "Body", "숨은 금속의 위치가 바뀌는 중입니다. 잠시만 기다려 주세요.",
            new Vector2(0, -50), new Vector2(1600, 60), 34, SubTextColor);
    }
}
