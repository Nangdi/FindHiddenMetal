using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 숨은 금속 찾기 UI 에디터 도구들이 같이 쓰는 경로, 색, 생성 헬퍼.
public static class MetalUIKit
{
    #region 경로

    public const string SpriteRoot = "Assets/4.Sprite";
    public const string StaticDir = SpriteRoot + "/Static";
    public const string PartsDir = SpriteRoot + "/Parts";
    public const string GuideDir = SpriteRoot + "/_Guide";

    public const string PrefabRoot = "Assets/2.Model/Prefabs/FindHiddenMetal";
    public const string PartPrefabDir = PrefabRoot + "/Parts";
    public const string PanelPrefabDir = PrefabRoot + "/Panels";
    public const string CanvasPrefabPath = PrefabRoot + "/Canvas_FindHiddenMetal.prefab";

    public const string StaticLayerName = "StaticLayer";

    // 디자인(PSD) 글꼴 Paperlogy. 숫자 등 동적 글자만 쓰므로 Dynamic 폰트 에셋이다.
    private const string MediumFontPath = "Assets/10.Font/Paperlogy/Paperlogy-5Medium SDF.asset";
    private const string BoldFontPath = "Assets/10.Font/Paperlogy/Paperlogy-7Bold SDF.asset";
    private const string ExtraBoldFontPath = "Assets/10.Font/Paperlogy/Paperlogy-8ExtraBold SDF.asset";

    // 패널 이름 (Panel_xxx 프리팹, Static_xxx 이미지, Guide_xxx 이미지에 같이 쓰인다)
    public static readonly string[] PanelNames = { "Idle", "Guide", "Countdown", "Play", "Result", "Ready", "Edit" };

    public static string StaticSpritePath(string panel) => $"{StaticDir}/Static_{panel}.png";
    public static string GuidePath(string panel) => $"{GuideDir}/Guide_{panel}.png";
    public static string PanelPrefabPath(string panel) => $"{PanelPrefabDir}/Panel_{panel}.prefab";
    public static string PartSpritePath(string name) => $"{PartsDir}/{name}.png";
    public static string PartPrefabPath(string name) => $"{PartPrefabDir}/{name}.prefab";

    #endregion

    #region 화면 규격과 색

    // 세로형 터치모니터 (디자인 PSD 1200 x 1920)
    public const int ScreenWidth = 1200;
    public const int ScreenHeight = 1920;
    public static readonly Vector2 ReferenceResolution = new Vector2(ScreenWidth, ScreenHeight);

    // 디자인 색 (PSD 글자 색)
    public static readonly Color TextColor = new Color32(52, 55, 90, 255);       // 남색 기본 글자
    public static readonly Color AccentColor = new Color32(250, 114, 85, 255);   // 주황 강조 (1위, 오답 수)
    public static readonly Color GreenColor = new Color32(0, 190, 124, 255);     // 선택 수, 찾은 금속
    public static readonly Color LightTextColor = Color.white;                   // 진한 배경 위 글자
    public static readonly Color CountdownColor = new Color32(247, 43, 0, 255);
    public static readonly Color DisabledTint = new Color(0.6f, 0.6f, 0.6f, 0.5f); // 누를 수 없는 버튼 색 곱 (운영자 화면)
    public static readonly Color DebugColor = new Color32(255, 0, 255, 255);     // 개발자용 정답 위치
    public static readonly Color PlaceholderColor = new Color32(255, 0, 200, 255); // 디자인 PNG가 없을 때 임시 이미지

    // 격자 영역 (정적 레이어의 격자 바깥 선까지). 칸 크기는 GridBoard가 gridSize로 나눠 정한다.
    public static readonly Vector2 PlayGridCenter = new Vector2(3, 101);
    public static readonly Vector2 PlayGridSize = new Vector2(1120, 1082);
    public static readonly Vector2 ResultGridCenter = new Vector2(-4, 404);
    public static readonly Vector2 ResultGridSize = new Vector2(800, 784);
    public const int GridLine = 2; // 격자 선 두께 (GridLayoutGroup padding/spacing)

    // 부품 PNG 크기 (Docs/Design/export_psd.py가 PSD에서 뽑는다. 없으면 이 크기로 임시 이미지를 만든다)
    public static readonly (string name, int width, int height)[] PartSizes =
    {
        ("Button_Start", 1030, 230),
        ("Button_Confirm", 1080, 190),
        ("Button_Submit", 380, 170),
        ("Button_Submit_Off", 380, 170),
        ("Button_Home", 380, 170),
        ("Cell_Select", 92, 88),
        ("Mark_Match", 65, 63),
        ("Mark_Correct", 65, 63),
        ("Mark_Wrong", 65, 63),
        ("RankRow_Mine", 1050, 90),
        ("Badge_MyRank", 130, 50),
        ("Dot", 64, 64),
    };

    #endregion

    #region 리소스

    private static TMP_FontAsset mediumFont;
    private static TMP_FontAsset boldFont;
    private static TMP_FontAsset extraBoldFont;

    public static TMP_FontAsset MediumFont => mediumFont != null ? mediumFont : mediumFont = LoadFont(MediumFontPath);
    public static TMP_FontAsset BoldFont => boldFont != null ? boldFont : boldFont = LoadFont(BoldFontPath);
    public static TMP_FontAsset ExtraBoldFont => extraBoldFont != null ? extraBoldFont : extraBoldFont = LoadFont(ExtraBoldFontPath);

    private static TMP_FontAsset LoadFont(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;

        Debug.LogWarning($"폰트를 찾지 못해 TMP 기본 폰트를 사용합니다: {path}");
        return TMP_Settings.defaultFontAsset;
    }

    public static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"스프라이트를 찾지 못했습니다: {path}");
        return sprite;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion

    #region 생성 헬퍼

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 부모 중앙 기준 위치/크기 배치
    public static RectTransform Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // 앵커 비율로 늘려 배치. inset이 음수면 바깥으로 커진다.
    public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, float inset = 0f)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static RectTransform StretchFull(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one);

    public static Image AddImage(RectTransform rt, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 스프라이트 이미지. 9-slice 테두리가 있으면 Sliced로 늘린다.
    public static Image AddSprite(RectTransform rt, Sprite sprite, Color? tint = null)
    {
        Image image = AddImage(rt, tint ?? Color.white);
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    public static TextMeshProUGUI AddText(RectTransform rt, string text, float fontSize, Color color, bool bold = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = bold ? BoldFont : MediumFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static TextMeshProUGUI CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        return AddText(Place(CreateRect(name, parent), pos, size), text, fontSize, color, bold, alignment);
    }

    #endregion

    #region 인스펙터 연결

    public static void Wire(Object target, params (string field, Object value)[] pairs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in pairs)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name}에 '{field}' 필드가 없습니다.");
                continue;
            }
            property.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void WireList(Object target, string field, IList<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null || !property.isArray)
        {
            Debug.LogError($"{target.GetType().Name}에 '{field}' 리스트 필드가 없습니다.");
            return;
        }

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
