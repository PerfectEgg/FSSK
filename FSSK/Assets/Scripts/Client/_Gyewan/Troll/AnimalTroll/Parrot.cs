using UnityEngine;

// 앵무새
public class Parrot : AnimalTroll
{
    private Vector3 currentStartPos;
    private Vector3 startPos;
    private Vector3 endPos;
    private Vector3 targetPos;
    private float flySpeed;
    private float progress = 0f;

    [SerializeField] private float flyHeight = 5f;   // 비행 시 최고 높이
    private float flyDuration; // 비행에 걸리는 총 시간
    private float flyTimer = 0f; // 현재 비행 진행 시간

    private int actionCount = 0;        // 액션 카운팅
    private bool isReturning = false;   // 초기 대기 이후 행동

    void Start()
    {
        startPos = transform.position;
        startPos.y = 0;
        endPos = new Vector3(-startPos.x, startPos.y, Random.Range(-4f, 4f));
        endPos.y = 0;
        currentStartPos = startPos;
        targetPos = endPos;

        flyDuration = Random.Range(1.5f, 2.0f);
        flySpeed = Vector3.Distance(startPos, targetPos) / flyDuration;

        LookAtTarget(targetPos);
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
        switch(currentState)
        {
            case AnimalState.Entering:
                if (currentTime >= enteringTime)
                    ChangeState(AnimalState.Waiting);
                break;
            case AnimalState.Waiting:
                if (currentTime >= waittingTime)
                {
                    // 초기 대기 이후 날았을 때 대기 
                    if(isReturning) Debug.Log("앵무새: 노래 끝! 돌아갑니다.");
                    ChangeState(AnimalState.Action);
                }
                break;
            case AnimalState.Action:
                FlyAction();

                // 목표 위치에 도착했는지 먼저 확인!
                if (progress >= 1f)
                {
                    flyTimer = 0f;
                    progress = 0f;

                    Debug.Log($"[도착 순간 검사] 현재 앵무새 Y: {transform.position.y} / 목표지점(targetPos) Y: {targetPos.y}");
        
                    if (actionCount == 0) // 첫 번째 비행 완료 (도착)
                    {
                        actionCount++;
                        isReturning = true;
                        
                        waittingTime = 1.5f;  // 대기 시간 1.5초로 변경
                        startPos.y = 0;
                        endPos.y = 0;

                        currentStartPos = endPos;
                        startPos.z = Random.Range(-4f, 4f);
                        targetPos = startPos; // 다음 목적지는 원래 위치로 설정
                        LookAtTarget(targetPos); // 고개 돌리기
                        
                        Debug.Log("앵무새: 노래(Party Parrot)를 시작합니다! (1.5초 대기)");
                        ChangeState(AnimalState.Waiting); // 다시 대기 상태로!
                    }
                    else if (actionCount == 1) // 두 번째 비행 완료 (출발)
                    {
                        actionCount++;

                        startPos.y = 0;
                        endPos.y = 0;
                        currentStartPos = startPos;
                        endPos.z = Random.Range(-4f, 4f);
                        targetPos = endPos; // 다음 목적지는 목표 위치로 설정
                        LookAtTarget(targetPos); // 고개 돌리기

                        Debug.Log("앵무새: 노래(Party Parrot)를 시작합니다! (1.5초 대기)");
                        ChangeState(AnimalState.Waiting);
                    }
                    else if (actionCount == 2) // 세 번째 비행 완료 (복귀)
                    {
                        Debug.Log("앵무새: 비행 2회 완료, 퇴장합니다.");
                        ChangeState(AnimalState.Exiting);
                    }
                }
                break;
            case AnimalState.Exiting:
                EndTroll();
                break;
        }
    }

    private void FlyAction()
    {
        // 타이머를 증가시켜 진행률(0.0 ~ 1.0)을 계산합니다.
        flyTimer += Time.deltaTime;
        progress = Mathf.Clamp01(flyTimer / flyDuration);

        // 🟢 2. X와 Z축은 직선(Lerp)으로 부드럽게 이동시킵니다.
        Vector3 currentPos = Vector3.Lerp(currentStartPos, targetPos, progress);

        // 🟢 3. Y축(높이)은 사인 그래프를 더해 포물선을 만듭니다!
        // Mathf.Sin(progress * Mathf.PI)는 progress가 0.5일 때 최대값 1을 반환합니다.
        currentPos.y += Mathf.Sin(progress * Mathf.PI) * flyHeight;

        // 위치 적용
        transform.position = currentPos;
    }
}