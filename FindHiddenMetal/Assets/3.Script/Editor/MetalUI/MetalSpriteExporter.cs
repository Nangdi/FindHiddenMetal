using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MetalUIKit;

// 임시 스프라이트와 디자이너용 가이드 이미지를 PNG로 만든다.
//  - Static/Static_xxx.png : 패널별 정적 레이어 (MetalStaticLayouts 내용을 렌더링)
//  - Parts/*.png           : 카드 배경, 버튼, 게이지 등 동적 요소용 부품
//  - _Guide/Guide_xxx.png  : 정적 레이어 위에 동적 요소 자리를 표시한 작업 가이드
// 디자인 PNG로 같은 경로의 파일을 덮어쓰면 씬/프리팹 수정 없이 바로 반영된다.
public static class MetalSpriteExporter
{
    // 둥근 박스 부품 (9-slice). radius가 곧 slice 테두리 크기.
    private static readonly (string name, Color color, int radius)[] ShapeParts =
    {
        ("Card_Bg", CardColor, 16),
        ("Cell_Bg", Color.white, 6),       // 색은 프리팹이 칠한다
        ("RankRow_Bg", Color.white, 8),    // 색은 프리팹이 칠한다
    };

    private const string DotName = "Dot"; // 흰 원. 발견/정답 마크 색은 프리팹이 칠한다
    private const int DotSize = 64;

    private static readonly Color GuideColor = new Color(1f, 0.1f, 0.8f, 1f);

    [MenuItem("Tools/숨은 금속 찾기/3. 임시 스프라이트 다시 내보내기 (덮어쓰기)", priority = 3)]
    private static void ExportAllMenu()
    {
        bool ok = EditorUtility.DisplayDialog("숨은 금속 찾기",
            "4.Sprite/Static, 4.Sprite/Parts의 PNG를 모두 임시 디자인으로 덮어씁니다.\n디자이너가 넣은 이미지도 사라집니다. 계속할까요?",
            "덮어쓰기", "취소");
        if (ok) Export(overwrite: true);
    }

    [MenuItem("Tools/숨은 금속 찾기/2. 가이드 PNG 다시 만들기", priority = 2)]
    public static void ExportGuides()
    {
        EnsureFolder(GuideDir);
        foreach (string panel in PanelNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath(panel));
            if (prefab == null)
            {
                Debug.LogWarning($"패널 프리팹이 없어 가이드를 건너뜁니다: {panel}");
                continue;
            }

            Texture2D tex = Render(ScreenWidth, ScreenHeight, false, root => DrawGuide(root, prefab));
            WritePng(GuidePath(panel), tex, 0);
        }
        Debug.Log($"가이드 PNG 생성 완료: {GuideDir}");
    }

    // overwrite가 false면 없는 파일만 만든다 (디자이너가 넣은 파일 보호).
    public static void Export(bool overwrite)
    {
        EnsureFolder(StaticDir);
        EnsureFolder(PartsDir);
        int count = 0;

        foreach (var pair in MetalStaticLayouts.Panels)
        {
            string path = StaticSpritePath(pair.Key);
            if (!overwrite && File.Exists(path)) continue;
            WritePng(path, Render(ScreenWidth, ScreenHeight, false, pair.Value), 0);
            count++;
        }

        foreach (var part in MetalStaticLayouts.TextParts)
        {
            string path = PartSpritePath(part.name);
            if (!overwrite && File.Exists(path)) continue;
            WritePng(path, Render(part.size.x, part.size.y, true, part.draw), 0);
            count++;
        }

        foreach (var shape in ShapeParts)
        {
            string path = PartSpritePath(shape.name);
            if (!overwrite && File.Exists(path)) continue;
            int size = shape.radius * 2 + 2;
            WritePng(path, RoundRect(size, size, shape.radius, shape.color), shape.radius);
            count++;
        }

        string dotPath = PartSpritePath(DotName);
        if (overwrite || !File.Exists(dotPath))
        {
            WritePng(dotPath, RoundRect(DotSize, DotSize, DotSize / 2, Color.white), 0);
            count++;
        }

        if (count > 0)
            Debug.Log($"임시 스프라이트 {count}개 생성 ({(overwrite ? "덮어쓰기" : "없는 파일만")})");
    }

    #region 가이드

    private static void DrawGuide(RectTransform root, GameObject panelPrefab)
    {
        var panel = (RectTransform)UnityEngine.Object.Instantiate(panelPrefab, root).transform;
        panel.name = panelPrefab.name;
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        foreach (Transform child in panel)
        {
            if (child.name == StaticLayerName) continue;
            MarkDynamicArea((RectTransform)child);
        }

        var legend = CreateText(root, "Legend", "■ 분홍 박스 = 동적 요소 자리 (정적 PNG에 그리지 말 것)",
            new Vector2(0, ScreenHeight * 0.5f - 20), new Vector2(ScreenWidth, 40), 24, GuideColor, true);
        legend.alignment = TextAlignmentOptions.Center;
    }

    private static void MarkDynamicArea(RectTransform target)
    {
        RectTransform box = StretchFull(CreateRect("GuideBox", target));
        box.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        AddImage(box, new Color(GuideColor.r, GuideColor.g, GuideColor.b, 0.18f));

        const float t = 3f;
        AddImage(Edge(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), Vector2.zero), GuideColor);
        AddImage(Edge(box, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, t)), GuideColor);
        AddImage(Edge(box, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(t, 0)), GuideColor);
        AddImage(Edge(box, new Vector2(1, 0), Vector2.one, new Vector2(-t, 0), Vector2.zero), GuideColor);

        var label = AddText(Stretch(CreateRect("GuideLabel", box), Vector2.zero, Vector2.one, 6),
            target.name, 20, GuideColor, true, TextAlignmentOptions.TopLeft);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private static RectTransform Edge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = CreateRect("Edge", parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    #endregion

    #region 렌더링

    // 임시 프리뷰 씬에 UI를 만들어 카메라로 찍는다. 현재 씬은 건드리지 않는다.
    private static Texture2D Render(int width, int height, bool transparent, Action<RectTransform> build)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            var camGo = new GameObject("ExportCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = height * 0.5f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(0, 0, -100);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.targetTexture = rt;

            var canvasGo = new GameObject("ExportCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var root = (RectTransform)canvasGo.transform;
            root.sizeDelta = new Vector2(width, height);
            root.position = Vector3.zero;

            build(root);

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();

            Color32[] onBlack = Capture(cam, rt, Color.black);
            Color32[] pixels = transparent ? Unblend(onBlack, Capture(cam, rt, Color.white)) : Opaque(onBlack);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Color32[] Capture(Camera cam, RenderTexture rt, Color background)
    {
        cam.backgroundColor = background;
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        Color32[] pixels = tex.GetPixels32();
        UnityEngine.Object.DestroyImmediate(tex);
        return pixels;
    }

    private static Color32[] Opaque(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].a = 255;
        return pixels;
    }

    // 검은/흰 배경에 각각 찍은 결과로 원래 색과 투명도를 되살린다.
    // (배경색 = c*a + bg*(1-a) 이므로 두 결과의 차이가 1-a)
    private static Color32[] Unblend(Color32[] onBlack, Color32[] onWhite)
    {
        var result = new Color32[onBlack.Length];
        for (int i = 0; i < result.Length; i++)
        {
            Color32 b = onBlack[i];
            Color32 w = onWhite[i];
            float diff = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / (3f * 255f);
            float a = Mathf.Clamp01(1f - diff);
            if (a <= 0.002f)
            {
                result[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            result[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(b.r / a), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b.g / a), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b.b / a), 0, 255),
                (byte)Mathf.RoundToInt(a * 255f));
        }
        return result;
    }

    // 안티에일리어싱된 둥근 사각형
    private static Texture2D RoundRect(int width, int height, int radius, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        Vector2 half = new Vector2(width, height) * 0.5f;
        Vector2 inner = half - Vector2.one * radius;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
            Vector2 q = new Vector2(Mathf.Abs(p.x) - inner.x, Mathf.Abs(p.y) - inner.y);
            float dist = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - radius;
            float coverage = Mathf.Clamp01(0.5f - dist);
            pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * coverage);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void WritePng(string path, Texture2D tex, int sliceBorder)
    {
        bool isNew = !File.Exists(path);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // 9-slice 테두리는 처음 만들 때만 정한다. (이후 디자이너가 Sprite Editor에서 바꾼 값은 유지)
        if (isNew && sliceBorder > 0 && AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.spriteBorder = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);
            importer.SaveAndReimport();
        }
    }

    #endregion
}
