using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [Header("사운드 설정")]
    [SerializeField] private AudioClip _clickSound; // 인스펙터에서 재생할 클릭음 할당
    [SerializeField] private float _volume = 0.75f; // 개별 볼륨 조절 (기본값 1)

    private void Start()
    {
        Button button = GetComponent<Button>();

        // 버튼이 클릭되었을 때 이벤트 버스(SoundEvents)를 통해 사운드 재생 요청
        button.onClick.AddListener(() =>
        {
            if (_clickSound != null)
            {
                // MainSoundManager가 이 이벤트를 듣고 사운드를 재생해 줍니다.
                SoundEvents.PlaySFX?.Invoke(_clickSound, _volume);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 버튼에 클릭 사운드가 할당되지 않았습니다.");
            }
        });
    }
}