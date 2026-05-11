# Project Overview
- Engine: Unity 6
- Language: C#
- Main Server/BaaS: TheBackend (뒤끝 서버)
- UI System: UGUI & TextMeshPro
- In-Game Network: Photon (PUN 2)

# Code Style & Naming Conventions

# [클라이언트 & C# 기본 규칙]
- 클래스, 함수, public 속성(Property): `PascalCase` (예: `GameManager`, `LoginPanel`)
- private 내부 변수: `_camelCase` (언더바 포함, 예: `_hp`, `_loginIdText`)
- 지역 변수, 매개 변수: `camelCase` (예: `moveSpeed`, `targetPanel`)
- UI 하이어라키 오브젝트 이름: `PascalCase` (예: `LoginIdText`, `SignupButton`)
- 상수(Constant): `UPPER_SNAKE_CASE` (예: `MAX_TURN_TIME`, `BOARD_SIZE_X`)

# [서버 & C# 네트워크 특화 규칙]
- DB 테이블 = `PascalCase` (예: `UserData`)
- DB 컬럼 = `camelCase` (예: `winCount`)
- 포톤 RPC 함수: `PascalCase` 뒤에 Rpc를 붙여서 일반 함수와 구별할 것. (예: `PlaceStoneRpc()`)
- 이벤트/액션: On으로 시작할 것. (예: `Action OnLoginSuccess`, `public void OnClickLoginButton()`)

# Unity Specific Rules
1. 변수 노출 (Inspector): - 변수를 인스펙터에 노출할 때는 절대 `public`을 남발하지 말고, 무조건 `[SerializeField] private`를 사용할 것.
2. 성능 최적화: - `Update()` 함수 안에서 `GetComponent<T>()`나 `GameObject.Find()`를 절대 호출하지 말 것. 반드시 `Start()`나 `Awake()`에서 캐싱(`_camelCase` 변수에 저장)해서 사용할 것.
   - 문자열 비교보다 `CompareTag("Player")`를 사용할 것.
3. UI 컴포넌트:
   - 텍스트를 다룰 때는 기본 `Text` 대신 무조건 `TextMeshProUGUI`를 사용할 것. (네임스페이스: `using TMPro;`)
4. 아키텍처 (Managers):
   - 시스템을 관리하는 매니저 클래스(예: `BackendManager`, `TitleManager`)는 싱글톤(Singleton) 패턴을 사용하며, 상태 관리는 `public Type Name { get; private set; }` 형태의 프로퍼티를 적극 활용할 것.


# Workflow Guidelines
- 코드를 수정하거나 제안할 때는 기존의 네이밍 규칙(특히 `_camelCase`와 `PascalCase` 구분)을 엄격하게 지킬 것.
- 새로운 UI 스크립트를 작성할 때는 Header 어트리뷰트(`[Header("UI Elements")]`)를 활용하여 인스펙터를 깔끔하게 정리해 줄 것.

# Debugging & Logging Conventions
1. 말머리(Prefix) 필수
   - 모든 `Debug.Log` 호출 시 문자열 맨 앞에 `[클래스명]`을 반드시 포함하여 출처를 명확히 할 것. 
   - 예: `Debug.Log("[BackendManager] ...");`

2. 로그 언어 하이브리드 정책 (Language Policy)
   - `Debug.Log` (일반 정보/상태 전환): 기획/아트 팀원과의 원활한 소통 및 빠른 테스트를 위해 직관적인 **한글**로 작성할 것.
     - 예: `Debug.Log("[LobbyManager] 로비 씬 진입 완료");`
   - `Debug.LogWarning` & `Debug.LogError` (예외/에러): 서버 전송 시 인코딩 깨짐을 방지하고 빠른 검색을 위해, 객체명과 에러 원인을 포함하여 반드시 **영어(English)**로 건조하고 명확하게 작성할 것.
     - 예: `Debug.LogError("[CommonUIManager] _gameSettingPanel NullReference.");`

3. 로그 타격 지점 (Log Placement)
   - 씬(Scene) 이동, 서버 네트워크 통신(요청 및 응답), 치명적 예외 상황(Null) 등 **상태가 크게 변하는 마일스톤(Milestone)** 지점에만 로그를 배치할 것.
   - 가독성 파괴 및 성능 저하(Log Spam)를 막기 위해, `Update()` 내부나 초당 여러 번 호출되는 자잘한 연산 함수에는 절대 로그를 남기지 말 것.

4. 예외 처리와 방어적 프로그래밍 (Defensive Programming)
   - `[SerializeField]`로 할당받는 UI 패널이나 필수 컴포넌트를 메서드에서 사용할 때는, 로직 실행 전 반드시 `null` 체크를 할 것.
   - 컴포넌트가 비어있을 경우 `Debug.LogError`로 원인을 출력한 뒤, `return;`으로 함수를 강제 종료(Fail-Fast)하여 게임이 크래시되는 것을 막을 것.