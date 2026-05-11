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

# Architecture & Design Patterns

# [아키텍처 기본 원칙 (Managers)]
시스템을 통제하는 매니저 클래스(예: BackendManager, TitleManager)는 싱글톤(Singleton) 패턴을 기본으로 사용한다.

매니저의 상태 데이터는 외부에서 임의로 조작하지 못하도록 public Type Name { get; private set; } 형태의 프로퍼티를 적극 활용하여 캡슐화한다.

💡 주의: 모든 것을 싱글톤으로 만들려는 함정(과엔지니어링)을 경계하고, 꼭 필요한 전역 관리자에만 제한적으로 도입할 것.

# [이벤트 및 통신 규칙 (Event Architecture)]
클래스 간의 스파게티 코드(강한 결합)를 방지하기 위해 옵저버(Observer) 패턴을 기반으로 한 이벤트 주도(Event-Driven) 아키텍처를 지향한다.

중앙집중형 이벤트 (Events.cs 권장):

게임 전체, 또는 거대한 시스템을 관통하는 글로벌 이벤트는 반드시 별도의 [SystemName]Events.cs 파일(정적 클래스)로 묶어서 중앙 관리할 것.

예: LobbyEvents.cs (로그인 성공, 매칭 완료 등), InGameEvents.cs (턴 변경, 승패 결정 등)

지역적 이벤트 (Local Events):

오직 하나의 클래스 내부나 부모-자식 객체 사이에서만 쓰이는 1회성/지역적 이벤트라면 굳이 Events.cs로 빼지 않고 해당 클래스 내부에 선언하는 것을 허용한다. ("아니면 말고" 원칙)

이벤트 네이밍:

이벤트(Action/Delegate) 이름은 반드시 On으로 시작할 것.

예: public static Action<int> OnPlayerTurnChanged;

# [권장 디자인 패턴 (Design Patterns Library)]
Unity 게임 개발 환경에 맞춰 아래의 패턴들을 상황에 맞게 적극 채용한다.

# [생성 패턴 - Creational]

Singleton: 전역 단일 인스턴스 (매니저급 클래스)

Factory: 복잡한 객체(적, 아이템) 생성 로직의 캡슐화

Builder: 매개변수가 많은 복잡한 객체를 단계적으로 조립 (예: 커스텀 캐릭터 스탯 세팅)

Prototype: 기존 객체를 복제하여 새로운 객체 생성 (프리팹 인스턴스화의 근간)

# [구조 패턴 - Structural]

Decorator: 기존 코드 수정 없이 런타임에 기능을 동적으로 추가 (예: 아이템 획득 시 버프 중첩)

Composite: 트리 구조 (예: 복잡한 스킬트리, 다중 파츠 장비 조합)

Flyweight: 동일한 데이터를 공유하여 메모리 절약 (예: 숲의 나무들, 수만 개의 동일한 오목알 데이터)

Facade: 복잡한 서브 시스템들을 묶어 단순한 단일 인터페이스로 제공 (예: NetworkFacade.Connect())

Proxy: 원본 객체에 대한 접근 제어 및 지연 로딩 (메모리가 큰 리소스 로드 대기 시)

# [행동 패턴 - Behavioral]

Observer: 상태 변화를 구독자들에게 브로드캐스트 (UI 업데이트 로직의 핵심)

State: 캐릭터나 AI의 상태(Idle, Attack, Dead)별 행동을 클래스로 분리하여 관리

Strategy: 알고리즘(예: 다양한 무기의 공격 방식, 이동 방식)을 런타임에 교체

Command: 유저의 입력(Input)이나 실행을 객체로 캡슐화 (예: 조작 키 변경, 체스/오목의 무르기(Undo) 기능)

Template Method: 상위 클래스에서 알고리즘의 골격을 정의하고 하위에서 세부 구현

Chain of Responsibility: 요청을 여러 처리 객체(체인)로 순차적으로 넘기며 처리

Mediator: 객체 간의 복잡한 그물망 통신을 중앙 중재자를 통해 단순화

# [게임 특화 패턴 - Game Programming Patterns]

Object Pool: 파괴되고 생성되는 빈도가 높은 객체(총알, 이펙트, 오목알)의 메모리 재사용 (가비지 컬렉터 부하 방지)

Event Queue: 이벤트를 즉시 처리하지 않고 큐에 쌓아두어 프레임/타이밍에 맞춰 순차적(지연) 처리

Type Object: 하드코딩된 상속 대신, 데이터(JSON/DB)를 기반으로 런타임에 유연하게 몬스터/아이템의 타입을 정의

Dirty Flag: 값의 변경이 일어났을 때만 재계산/렌더링하도록 플래그를 세워 최적화 (예: UI 갱신, 물리 연산)

Spatial Partition: 맵을 그리드/쿼드트리로 분할하여 근처에 있는 객체들끼리만 충돌/시야 검사를 수행 (연산량 급감)

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