using UnityEngine;

// 화면 단위 패널 베이스. 전환 연출을 넣을 때는 여기만 바꾼다.
public class PanelBase : MonoBehaviour
{
    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
