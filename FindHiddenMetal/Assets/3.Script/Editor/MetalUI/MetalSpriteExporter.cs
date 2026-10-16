using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MetalUIKit;

// 디자이너용 가이드 이미지와, 디자인 PNG가 없을 때 쓸 임시 이미지를 만든다.
//  - Static/Static_xxx.png, Parts/*.png : 디자인 PSD에서 Docs/Design/export_psd.py로 뽑는다.
//                                         파일이 없으면 분홍색 임시 이미지를 만들어 빠진 것이 바로 보이게 한다.
//  - _Guide/Guide_xxx.png              : 정적 레이어 위에 동적 요소 자리를 표시한 작업 가이드
// 디자인 PNG로 같은 경로의 파일을 덮어쓰면 씬/프리팹 수정 없이 바로 반영된다.
public static class MetalSpriteExporter
{
    private static readonly Color GuideColor = new Color(1f, 0.1f, 0.8f, 1f);

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

            Texture2D tex = Render(ScreenWidth, ScreenHeight, root => DrawGuide(root, prefab));
            WritePng(GuidePath(panel), tex);
        }
        Debug.Log($"가이드 PNG 생성 완료: {GuideDir}");
    }

    // 없는 디자인 PNG만 임시 이미지로 만든다 (있는 파일은 건드리지 않음).
    public static void EnsurePlaceholders()
    {
        EnsureFolder(StaticDir);
        EnsureFolder(PartsDir);
        int count = 0;

        foreach (string panel in PanelNames)
            count += Placeholder(StaticSpritePath(panel), ScreenWidth, ScreenHeight);
        foreach (var part in PartSizes)
            count += Placeholder(PartSpritePath(part.name), part.width, part.height);

        if (count > 0)
            Debug.LogWarning($"디자인 PNG {count}개가 없어 분홍색 임시 이미지를 만들었습니다. Docs/Design/export_psd.py로 PSD에서 다시 뽑으세요.");
    }

    private static int Placeholder(string path, int width, int height)
    {
        if (File.Exists(path)) return 0;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = PlaceholderColor;
        tex.SetPixels32(pixels);
        tex.Apply();
        WritePng(path, tex);
        return 1;
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

        var legend = CreateText(root, "Legend", "분홍 박스 = 동적 요소 자리 (정적 PNG에 그리지 말 것)",
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
    private static Texture2D Render(int width, int height, Action<RectTransform> build)
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

            Color32[] pixels = Opaque(Capture(cam, rt, Color.black));

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

    private static void WritePng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    #endregion
}
