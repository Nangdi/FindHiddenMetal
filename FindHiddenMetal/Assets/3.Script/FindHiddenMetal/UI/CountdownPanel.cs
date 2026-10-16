using System;
using System.Collections;
using TMPro;
using UnityEngine;

// 체험 시작 전 카운트다운 (3, 2, 1). 숫자마다 커졌다 줄어드는 효과를 준다.
// 제목, 안내 문구, 원 배경은 정적 레이어.
public class CountdownPanel : PanelBase
{
    [SerializeField] private TMP_Text numberText;
    [Tooltip("숫자가 나타날 때 시작 배율")]
    [SerializeField] private float popScale = 1.4f;
    [Tooltip("배율이 1로 돌아오는 시간(초)")]
    [SerializeField] private float popSeconds = 0.25f;

    public event Action OnDone;

    private Coroutine routine;

    public void Begin(int seconds)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(Mathf.Max(0, seconds)));
    }

    public override void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        base.Hide();
    }

    private IEnumerator Run(int seconds)
    {
        for (int n = seconds; n > 0; n--)
        {
            if (numberText != null) numberText.text = n.ToString();
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / popSeconds);
                if (numberText != null) numberText.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
        }
        routine = null;
        OnDone?.Invoke();
    }
}
