using UnityEngine;
using Photon.Pun; // 🟢 [멀티플레이] 포톤 네임스페이스 추가

// 앵무새
public class Parrot : AnimalTroll
{
    private Vector3 _currentStartPos;
    private Vector3 _startPos;
    private Vector3 _endPos;
    private Vector3 _finalPos;
    private Vector3 _targetPos;
    private float _flySpeed;
    private float _progress = 0f;

    [SerializeField] private float _flyHeight = 5f;   // 비행 시 최고 높이
    private float _flyDuration; // 비행에 걸리는 총 시간
    private float _flyTimer = 0f; // 연재 비행 진행 시간

    private int _actionCount = 0;        // 액션 카운팅
    private bool _isReturning = false;   // 초기 대기 이후 �동

    protected override void Start()
    {
        base.Start();

        // 애니메이터 컴포넌트 캐싱
        _animator = GetComponent<Animator>();

        // 🟢 [멀티플레이 핵심] 계산은 오직 방장(주인)만!
        if (!photonView.IsMine) return;

        _startPos = transform.position;
        _startPos.y = 0;
        _finalPos = new Vector3(-_startPos.x, _startPos.y, -_startPos.z);
        _finalPos.y = 0;
        _endPos = new Vector3(-_startPos.x, _startPos.y, Random.Range(-4f, 4f));
        _endPos.y = 0;
        _currentStartPos = _startPos;
        _targetPos = _endPos;

        _flyDuration = Random.Range(1.5f, 2.0f);
        _flySpeed = Vector3.Distance(_startPos, _targetPos) / _flyDuration;
    }

    // 상태에 막 진입했을 때 할 일 (무적 판정, 애니메이션 재생 등)
    protected override void OnStateEnter(AnimalState state)
    {
        base.OnStateEnter(state);

        // 🟢 [멀티플레이] 상태 변화 및 애니메이션 트리거는 주인만 실행합니다.
        // (주인이 실행하면 Photon Animator View가 남들의 화면에도 똑같이 애니메이션을 틀어줍니다)
        if (!photonView.IsMine) return;

        if (state == AnimalState.Waiting)
            LookAtTarget(_targetPos);

        // 2. 🟢 상태에 맞는 애니메이션 트리거 단 한 번 실행
        if (_animator != null)
        {
            SendAnimationReset(_enterTrigger);
            SendAnimationReset(_exitTrigger);

            switch(state)
            {
                case AnimalState.Action:
                    SendAnimationSetFloat("Action", 1); // Action 트리거 대신 파라미터로 제어 (연속된 애니메이션 재생 가능)
                    SendAnimationTrigger(_enterTrigger);
                    break;
                case AnimalState.Waiting:
                    SendAnimationTrigger(_exitTrigger);
                    break;
            }
        }
    }

    // 목표 지점을 바라보는 함수
    private void LookAtTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0; // 고개 돌릴 때 위아래로 꺾이지 않게 방지
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
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
                {
                    // 초기 대기 이후 날았을 때 대기 
                    if(_isReturning) Debug.Log("앵무새: 노래 끝! 돌아갑니다.");
                    ChangeState(AnimalState.Action);
                }
                break;
            case AnimalState.Action:
                FlyAction();

                // 목표 위치에 도착했는지 먼저 확인!
                if (_progress >= 1f)
                {
                    _flyTimer = 0f;
                    _progress = 0f;
        
                    if (_actionCount == 0) // 첫 번째 비행 완료 (도착)
                    {
                        _actionCount++;
                        _isReturning = true;
                        
                        _waittingTime = 1.5f;  // 대기 시간 1.5초로 변경
                        _startPos.y = 0;
                        _endPos.y = 0;

                        _currentStartPos = _endPos;
                        _startPos.z = Random.Range(-4f, 4f);
                        _targetPos = _startPos; // 다음 목적지는 원래 위치로 설정
                        LookAtTarget(_targetPos); // 고개 돌리기
                        
                        Debug.Log("앵무새: 노래(Party Parrot)를 시작합니다! (1.5초 대기)");
                        ChangeState(AnimalState.Waiting); // 다시 대기 상태로!
                    }
                    else if (_actionCount == 1) // 두 번째 비행 완료 (출발)
                    {
                        _actionCount++;

                        _startPos.y = 0;
                        _finalPos.y = 0;
                        _currentStartPos = _startPos;
                        _targetPos = _finalPos; // 다음 목적지는 목표 위치로 설정
                        LookAtTarget(_targetPos); // 고개 돌리기

                        Debug.Log("앵무새: 노래(Party Parrot)를 시작합니다! (1.5초 대기)");
                        ChangeState(AnimalState.Waiting);
                    }
                    else if (_actionCount == 2) // 세 번째 비행 완료 (복귀)
                    {
                        Debug.Log("앵무새: 비행 2회 완료, 퇴장합니다.");
                        ChangeState(AnimalState.Hiding);
                    }
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

    private void FlyAction()
    {
        // 타이머를 증가시켜 진행률(0.0 ~ 1.0)을 계산합니다.
        _flyTimer += Time.deltaTime;
        _progress = Mathf.Clamp01(_flyTimer / _flyDuration);

        // 파 둔른 거리 춌 2. X와 Z축은 직선(Lerp)로 부드러운 이동싱낤템니다.
        Vector3 currentPos = Vector3.Lerp(_currentStartPos, _targetPos, _progress);

        // 파 둔른 거리 춌 3. Y축(높이)는 사인 그래프를 더해 포딼선을 만듭니다!
        // Mathf.Sin(progress * Mathf.PI)는 progress가 0.5일 때 최대값 1을 반환합니다.
        currentPos.y += Mathf.Sin(_progress * Mathf.PI) * _flyHeight;

        // 위치 적용
        transform.position = currentPos;
    }

    // 🟢 애니메이션 동기화를 위한 RPC 래퍼 (자식 클래스에서 사용)
    private void SendAnimationSetFloat(string triggerName, float value)
    {
        if (photonView.IsMine)
        {
            photonView.RPC("SyncAnimationSetFloatRPC", RpcTarget.All, triggerName, value);
        }
    }

    [PunRPC]
    public void SyncAnimationSetFloatRPC(string triggerName, float value)
    {
        if (_animator != null) _animator.SetFloat(triggerName, value);
    }
}