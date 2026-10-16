using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 격자 한 칸. 선택 표시(Highlight), 정답/오답 마크, 정답 공개 마크를 담당한다.
// 칸 번호와 격자 선은 정적 레이어 그림에 있고, 칸은 그 위에 투명하게 겹친다.
// 진행 화면 칸(GridCell)과 결과 화면 칸(ResultCell)이 같은 스크립트를 쓴다.
// 정답/오답 마크는 다음 위치 확인까지 남겨 두거나(keep) 잠깐 보였다 사라지게 할 수 있다.
// 마크가 남아 있는 동안 그 칸은 다시 고를 수 없다(IsLocked).
public class GridCellView : MonoBehaviour
{
    [Tooltip("비워두면 누를 수 없음 (결과 화면)")]
    [SerializeField] private Button button;
    [Tooltip("선택 표시. 비워두면 표시하지 않음")]
    [SerializeField] private GameObject highlight;
    [SerializeField] private CanvasGroup foundMark;
    [Tooltip("정답 공개(못 찾은 금속). 비워두면 표시하지 않음")]
    [SerializeField] private GameObject revealMark;
    [Tooltip("오답 표시. 비워두면 표시하지 않음")]
    [SerializeField] private CanvasGroup wrongMark;
    [Tooltip("개발자용 정답 위치 표시. 비워두면 표시하지 않음")]
    [SerializeField] private GameObject debugMark;
    [Tooltip("남기지 않는 마크가 보였다 사라지는 시간(초)")]
    [FormerlySerializedAs("wrongShowSeconds")]
    [SerializeField] private float markShowSeconds = 1f;

    public int X { get; private set; }
    public int Y { get; private set; }
    public bool IsLocked { get; private set; }
    public event Action<GridCellView> OnClick;

    private Coroutine foundRoutine;
    private Coroutine wrongRoutine;

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(() => OnClick?.Invoke(this));
    }

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
        name = $"Cell_{TargetLayout.ToLabel(x, y)}";
        ResetEffect();
    }

    public void SetSelected(bool selected)
    {
        if (highlight != null) highlight.SetActive(selected);
    }

    public void ShowFound(bool keep)
    {
        ShowMark(foundMark, keep, ref foundRoutine);
    }

    public void ShowWrong(bool keep)
    {
        ShowMark(wrongMark, keep, ref wrongRoutine);
    }

    // 정답 공개 (못 찾은 금속)
    public void ShowReveal()
    {
        if (revealMark != null) revealMark.SetActive(true);
    }

    // 정답/오답 마크를 지우고 다시 고를 수 있게 한다 (다음 위치 확인 직전).
    public void ClearMarks()
    {
        StopRoutine(ref foundRoutine);
        StopRoutine(ref wrongRoutine);
        IsLocked = false;
        HideMark(foundMark);
        HideMark(wrongMark);
    }

    // 개발자용 정답 위치 표시
    public void SetDebugTarget(bool on)
    {
        if (debugMark != null) debugMark.SetActive(on);
    }

    public void ResetEffect()
    {
        ClearMarks();
        SetSelected(false);
        SetDebugTarget(false);
        if (revealMark != null) revealMark.SetActive(false);
    }

    private void ShowMark(CanvasGroup mark, bool keep, ref Coroutine routine)
    {
        if (mark == null) return;
        StopRoutine(ref routine);

        mark.gameObject.SetActive(true);
        mark.alpha = 1f;
        if (keep)
        {
            IsLocked = true;
            return;
        }
        if (isActiveAndEnabled) routine = StartCoroutine(FadeOut(mark));
        else HideMark(mark);
    }

    private IEnumerator FadeOut(CanvasGroup mark)
    {
        float t = 0f;
        while (t < markShowSeconds)
        {
            t += Time.unscaledDeltaTime;
            // 앞 30%는 그대로 보이고, 나머지 동안 사라진다.
            float k = Mathf.InverseLerp(markShowSeconds * 0.3f, markShowSeconds, t);
            mark.alpha = 1f - k;
            yield return null;
        }
        HideMark(mark);
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private static void HideMark(CanvasGroup mark)
    {
        if (mark == null) return;
        mark.alpha = 0f;
        mark.gameObject.SetActive(false);
    }
}
