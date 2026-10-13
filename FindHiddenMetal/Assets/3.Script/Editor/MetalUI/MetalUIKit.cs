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

    private const string BoldFontPath = "Assets/10.Font/defaultFont/NotoSansKR-Bold SDF.asset";
    private const string MediumFontPath = "Assets/10.Font/defaultFont/NotoSansKR-Medium SDF.asset";

    // 패널 이름 (Panel_xxx 프리팹, Static_xxx 이미지, Guide_xxx 이미지에 같이 쓰인다)
    public static readonly string[] PanelNames = { "Idle", "Guide", "Play", "Result", "Ready" };

    public static string StaticSpritePath(string panel) => $"{StaticDir}/Static_{panel}.png";
    public static string GuidePath(string panel) => $"{GuideDir}/Guide_{panel}.png";
    public static string PanelPrefabPath(string panel) => $"{PanelPrefabDir}/Panel_{panel}.prefab";
    public static string PartSpritePath(string name) => $"{PartsDir}/{name}.png";
    public static string PartPrefabPath(string name) => $"{PartPrefabDir}/{name}.prefab";

    #endregion

    #region 화면 규격과 색

    // 14형 터치모니터
    public const int ScreenWidth = 1920;
    public const int ScreenHeight = 1200;
    public static readonly Vector2 ReferenceResolution = new Vector2(ScreenWidth, ScreenHeight);

    public static readonly Color BgColor = new Color32(14, 20, 34, 255);
    public static readonly Color CardColor = new Color32(30, 40, 62, 255);
    public static readonly Color SoftBoxColor = new Color32(255, 255, 255, 18);
    public static readonly Color AccentColor = new Color32(228, 44, 52, 255);
    public static readonly Color TextColor = Color.white;
    public static readonly Color SubTextColor = new Color32(170, 182, 205, 255);
    public static readonly Color DarkTextColor = new Color32(20, 24, 40, 255);

    // 격자 칸과 마크 (흰 스프라이트에 코드/프리팹이 칠하는 색)
    public static readonly Color CellColor = new Color32(52, 64, 90, 255);
    public static readonly Color FoundColor = new Color32(64, 200, 112, 255);
    public static readonly Color RevealColor = new Color32(255, 196, 40, 255);
    public static readonly Color WrongColor = AccentColor;
    public static readonly Color DebugColor = new Color32(255, 0, 255, 255); // 개발자용 정답 위치
    public static readonly Color RankRowColor = new Color(1f, 1f, 1f, 0.06f);

    // 격자 규격 (16x16). gridSize를 바꾸면 Static_Play.png의 열/행 표기도 다시 만들어야 한다.
    public const int GridCount = 16;
    public const float CellSize = 62f;
    public const float CellSpacing = 2f;
    public static float GridPixelSize => GridCount * CellSize + (GridCount - 1) * CellSpacing;
    public static readonly Vector2 GridCenter = new Vector2(-350, -40);

    #endregion

    #region 리소스

    private static TMP_FontAsset boldFont;
    private static TMP_FontAsset mediumFont;
    private static Sprite builtinRound;

    public static TMP_FontAsset BoldFont => boldFont != null ? boldFont : boldFont = LoadFont(BoldFontPath);
    public static TMP_FontAsset MediumFont => mediumFont != null ? mediumFont : mediumFont = LoadFont(MediumFontPath);

    // 정적 레이어 렌더링용 둥근 박스 (유니티 기본 UISprite)
    public static Sprite BuiltinRound => builtinRound != null
        ? builtinRound
        : builtinRound = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

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

    // 정적 레이어 렌더링용 둥근 박스 (기본 UISprite의 둥글기를 화면 규격에 맞게 키움)
    public static Image AddRoundBox(RectTransform rt, Color color)
    {
        Image image = AddImage(rt, color);
        image.sprite = BuiltinRound;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.4f;
        return image;
    }

    public static RectTransform CreateRoundBox(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform box = Place(CreateRect(name, parent), pos, size);
        AddRoundBox(box, color);
        return box;
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

    // 부모 영역을 가득 채우는 텍스트
    public static TextMeshProUGUI FillText(RectTransform parent, string name, string text, float fontSize, Color color, bool bold = false)
    {
        return AddText(Stretch(CreateRect(name, parent), Vector2.zero, Vector2.one, 10), text, fontSize, color, bold);
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
