using UnityEngine;

// 아이템의 경우 (마우스로 던질 수 있음)
public class Octopus : ItemTroll
{
    private Animator _animator;

    private static readonly int _dragTrigger = Animator.StringToHash("Drag");

    void Start()
    {
        _animator = GetComponent<Animator>();

        _grabbedScale = new Vector3(2f, 2f, 2f); // 🟢 잡았을 때 원래 크기
    }

    protected override void OnDragStart(int actorNumber)
    {
        base.OnDragStart(actorNumber);

        if (_animator != null)
        {
            _animator.SetTrigger(_dragTrigger);
        }
    }
}

