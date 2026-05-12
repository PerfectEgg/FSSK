using UnityEngine;

// 아이템의 경우 (마우스로 던질 수 있음)
public class Rum : ItemTroll
{
    [Header("사운드 설정")]
    [SerializeField] private AudioClip _rumHitSound;   // 적중 사운드

    void Start()
    {
        _grabbedScale = new Vector3(2f, 2f, 2f); // 🟢 잡았을 때 원래 크기
        _hitSound = _rumHitSound; // 🟢 아이템별로 적중 사운드 설정
    }
}

