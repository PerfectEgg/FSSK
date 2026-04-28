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

