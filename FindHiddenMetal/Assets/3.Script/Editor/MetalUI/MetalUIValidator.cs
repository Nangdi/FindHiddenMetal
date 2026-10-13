using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MetalUIKit;

// 정적 PNG 누락, 비어 있는 인스펙터 연결을 찾아 콘솔에 알려준다.
// 메뉴: Tools > 숨은 금속 찾기 > 4. 연결 검사
public static class MetalUIValidator
{
    [MenuItem("Tools/숨은 금속 찾기/4. 연결 검사", priority = 4)]
    public static int Validate()
    {
        int problems = 0;

        foreach (string panel in PanelNames)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StaticSpritePath(panel)) == null)
                problems += Report($"정적 레이어 이미지가 없습니다: {StaticSpritePath(panel)}", null);
        }

        foreach (string part in new[] { "GridCell", "RankRow" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartPrefabPath(part));
            if (prefab == null)
                problems += Report($"부품 프리팹이 없습니다: {PartPrefabPath(part)}", null);
            else
                problems += CheckReferences(prefab);
        }

        GameObject canvas = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Canvas_FindHiddenMetal") canvas = root;

            foreach (var controller in root.GetComponentsInChildren<FlowController>(true))
                problems += CheckReferences(controller.gameObject);
        }

        if (canvas == null)
        {
            problems += Report("씬에 Canvas_FindHiddenMetal가 없습니다. '1. 프리팹 생성 및 씬 적용'을 실행하세요.", null);
        }
        else
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(canvas))
                problems += Report("Canvas_FindHiddenMetal가 프리팹 인스턴스가 아닙니다.", canvas);

            foreach (var panel in canvas.GetComponentsInChildren<PanelBase>(true))
            {
                Transform layer = panel.transform.Find(StaticLayerName);
                Image image = layer != null ? layer.GetComponent<Image>() : null;
                if (image == null || image.sprite == null)
                    problems += Report($"{panel.name}: StaticLayer 이미지가 비어 있습니다.", panel);
            }

            problems += CheckReferences(canvas);
        }

        if (problems == 0)
            Debug.Log("숨은 금속 찾기 UI 연결 검사: 문제 없음");
        else
            Debug.LogWarning($"숨은 금속 찾기 UI 연결 검사: 문제 {problems}건 (위 로그 참고)");
        return problems;
    }

    // 하위 모든 컴포넌트에서 비어 있는 오브젝트 참조를 찾는다. (프로젝트 스크립트만)
    private static int CheckReferences(GameObject root)
    {
        int problems = 0;
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                problems += Report($"{root.name}: 스크립트가 없는(Missing) 컴포넌트가 있습니다.", root);
                continue;
            }

            // TMP, UGUI 등 패키지 컴포넌트는 검사하지 않는다
            if (behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;

            var so = new SerializedObject(behaviour);
            SerializedProperty property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.name == "m_Script" || property.objectReferenceValue != null) continue;

                problems += Report($"{GetPath(behaviour.transform)} ({behaviour.GetType().Name}): '{property.displayName}' 연결이 비어 있습니다.", behaviour);
            }
        }
        return problems;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = $"{t.name}/{path}";
        }
        return path;
    }

    private static int Report(string message, Object context)
    {
        Debug.LogWarning($"[UI 검사] {message}", context);
        return 1;
    }
}
