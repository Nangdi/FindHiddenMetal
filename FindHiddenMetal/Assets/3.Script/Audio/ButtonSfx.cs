using UnityEngine;
using UnityEngine.UI;

// 버튼을 누르면 효과음을 낸다. 소리 이름(키)은 StreamingAssets/Audio/sound.json 과 같아야 한다.
// 기본은 "button", 체험 시작·정답 확인처럼 따로 소리를 주고 싶은 버튼은 key 를 바꾼다.
[RequireComponent(typeof(Button))]
public class ButtonSfx : MonoBehaviour
{
    [SerializeField] private string key = "button";

    public string Key
    {
        get => key;
        set => key = value;
    }

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => SoundManager.PlaySfx(key));
    }
}
