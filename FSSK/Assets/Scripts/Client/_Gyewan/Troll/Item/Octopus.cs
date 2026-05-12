using UnityEngine;

// 아이템의 경우 (마우스로 던질 수 있음)
public class Octopus : ItemTroll
{
    [Header("사운드 설정")]
    [SerializeField] private AudioClip _octopusHitSound;   // 적중 사운드

    private Animator _animator;

    private static readonly int _dragTrigger = Animator.StringToHash("Drag");

    void Start()
    {
        _animator = GetComponent<Animator>();

        _grabbedScale = new Vector3(2f, 2f, 2f); // 🟢 잡았을 때 원래 크기

        _hitSound = _octopusHitSound; // 🟢 아이템별로 적중 사운드 설정
    }

    public override void OnDragStart()
    {
        base.OnDragStart();

        if (_animator != null)
        {
            _animator.SetTrigger(_dragTrigger);
        }
    }
}

