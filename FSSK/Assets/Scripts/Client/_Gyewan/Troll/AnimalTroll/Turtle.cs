using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

// 거북이
public class Turtle : AnimalTroll
{
    [SerializeField] private float _moveSpeed = 1.25f;  // 속도 측정
    private Vector3 _targetDirection;
    private Vector3 _targetPosition;

    protected override void Start()
    {
        base.Start();

        // 애니메이터 컴포넌트 캐싱
        _animator = GetComponent<Animator>();
    }

    // 목표 지점을 바라보는 함수
    private void LookAtTarget()
    {
        _targetPosition = new Vector3(-transform.position.x, transform.position.y, -transform.position.z);

        _targetDirection = _targetPosition.normalized;

        if (_targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_targetDirection);
        }
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected override void OnStateEnter(AnimalState state)
    {
        base.OnStateEnter(state);

        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        if (state == AnimalState.Waiting)
            LookAtTarget();

        // 2. 🟢 상태에 맞는 애니메이션 트리거 단 한 번 실행
        if (_animator != null)
        {
            // 사용하시는 트리거 변수들을 여기서 모두 Reset 해줍니다.
            _animator.ResetTrigger(_enterTrigger);
            _animator.ResetTrigger(_exitTrigger);

            switch(state)
            {
                case AnimalState.Action:
                    _animator.SetTrigger(_enterTrigger);
                    break;
                case AnimalState.Hiding:
                    _animator.SetTrigger(_exitTrigger);
                    break;
            }
        }
    }

    protected override void UpdateState()
    {
        switch(_currentState)
        {
            case AnimalState.Entering:
                EnterAction();
                break;
            case AnimalState.Waiting:
                if (_currentTime >= _waittingTime)
                    ChangeState(AnimalState.Action);
                break;
            case AnimalState.Action:
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPosition) <= 0.05f)
                {
                    ChangeState(AnimalState.Hiding);
                }
                break;
            case AnimalState.Hiding:
                SetHide();
                HideAction();
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }
}