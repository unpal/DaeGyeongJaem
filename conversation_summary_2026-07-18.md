# 작업 대화 요약

작성일: 2026-07-18 (Asia/Seoul)

## Git 및 브랜치

- 최초 작업 브랜치: `kimminhyeon1`
- `origin/kimminhyeon1`은 삭제되거나 존재하지 않아 `[gone]` 상태였음.
- `prototype-main-fixes` 기준으로 새 로컬 브랜치 `kimminhyeon2`를 생성함.
- 현재 브랜치: `kimminhyeon2`
- 기준 커밋: `26b950c` (`씬 인덱스 수정`)
- 기존 `kimminhyeon1` 브랜치는 보존됨.
- `kimminhyeon2`는 아직 원격에 푸시하지 않음.

## 전달받은 빌드 분석

- `MonoBleedingEdge/DaeGyeongJaem_Data/Managed/Assembly-CSharp.dll`을 분석함.
- 빌드 안의 술래 생성 코드는 `Runner.Spawn()`이 아니라 `Instantiate()`였음.
- 빌드 시간과 커밋 시간을 비교했을 때 `prototype-main-fixes`의 `26b950c` 또는 그 직전 미커밋 상태에서 생성됐을 가능성이 높음.
- 해당 빌드에서 술래가 호스트에게만 보이는 직접 원인은 `Instantiate()` 사용으로 판단함.

## GameManager와 PrototypeRoundManager

### GameManager

- 코루틴과 `Time.deltaTime` 기반의 이전 게임 진행 구조.
- 10초 카운트다운, 술래 생성, 포탈 활성화, 라운드 종료, 최종 우승 처리를 한 파일에서 담당.
- 술래를 `Instantiate()`로 생성하고 `Destroy()`로 제거하고 있었음.

### PrototypeRoundManager

- Fusion `TickTimer`와 `FixedUpdateNetwork()` 기반.
- Phase, 라운드 번호, 우승자, 타이머 등을 `[Networked]` 상태로 관리.
- 플레이어 탐색에 `Runner.ActivePlayers`와 PlayerObject를 사용.
- UI와 테스트 월드는 `PrototypeRoundView`가 담당.

최신 `origin/main`에서는 `PrototypeRoundScene`에 GameManager 관련 작업도 추가된 상태라 이후 병합 시 확인이 필요함.

## 카메라 구조 확인

- 씬의 `Main Camera`에는 처음부터 `CinemachineBrain`이 없고 `PrototypeRoundView`가 실행 중 추가함.
- 플레이어 프리팹 구조: `Capsule/CameraLook/Virtual Camera`.
- `CinemachineVirtualCamera`의 `Follow`와 `LookAt`은 비어 있으며 플레이어의 자식 Transform으로 직접 따라감.
- 좌우 회전은 플레이어 본체, 상하 회전은 `PlayerMove`가 `CameraLook`에 적용함.
- 프로토타입에서는 기존 `CameraMove`를 비활성화함.
- 다른 플레이어의 Virtual Camera는 `playerCamera.gameObject.SetActive(isMine)` 때문에 로컬에서 비활성화됨.
- Priority 100 대신 Priority 10 카메라가 선택된다면 100 카메라가 비활성 상태이거나 `CameraManager.UpdateCameraPriorities()`가 값을 10/0으로 덮어쓰는지 확인해야 함.

## PlayerGameState와 사망 상태

- `IsInPlayground`는 `!IsDead && !HasEscaped`.
- 프로토타입에서는 사망 시 빈 네트워크 입력을 보내고 `PlayerInput`도 비활성화함.
- 로컬 플레이어 상태는 `Runner.TryGetPlayerObject(Runner.LocalPlayer, out NetworkObject playerObject)` 후 `GetComponent<PlayerGameState>()`로 가져올 수 있음.
- PlayerObject 생성 전, 씬 전환 중, 컴포넌트 누락 등의 경우 `PlayerGameState`는 null일 수 있으므로 검사 필요.
- `PlayerGameState.cs`에 실수로 들어갔던 백틱 한 글자는 제거함. 실제 코드 차이는 남기지 않음.

## 사망 후 시체에서 소리가 나는 문제

- `PlayerNoise.PeriodicNoiseRoutine()`이 사망 후에도 계속 실행되어 시체 위치에서 주기적으로 소리를 발생시키는 원인을 확인함.
- `MakeNoise()`와 `PeriodicNoiseRoutine()`에서 `PlayerGameState.IsInPlayground` 검사가 필요함.
- 이 문제는 원인과 수정 방향만 확인했고 아직 코드 수정은 적용하지 않음.

## 해결 과제와 적용한 수정

별도 목록: `prototype_issues.txt`

### 1. 라운드 전환 시 간헐적인 낙하 피해

수정 파일: `Assets/Script/FallDamage.cs`

- `waitForInitialGrounding` 상태를 추가함.
- `ResetForNextRound()` 이후에는 처음 접지될 때까지 낙하 피해 계산을 중단함.
- 텔레포트 직후 이전 높이를 실제 낙하로 오인하는 문제를 방지함.

### 2. 라운드 시작 후 10초 동안 용암 피해 차단

수정 파일: `Assets/Script/LavaBurn.cs`

- `activationDelay = 10f`를 추가함.
- GameManager 또는 PrototypeRoundManager를 찾아 라운드 `Starting` 진입마다 타이머를 초기화함.
- 10초가 지나고 Phase가 `Playing`일 때만 피해를 적용함.
- 다음 라운드가 시작되면 다시 10초를 기다림.

### 3. 술래가 호스트에게만 보이는 문제

수정 파일:

- `Assets/Script/dotori/GameManager.cs`
- `Assets/Prefabs/Boss.prefab`

수정 내용:

- `Instantiate()`를 `Runner.Spawn()`으로 변경함.
- 술래 프리팹에 `NetworkObject`가 없으면 오류 로그를 출력함.
- `Destroy()`를 `Runner.Despawn()`으로 변경함.
- Boss 프리팹의 `ObjectInterest`를 Area Of Interest에서 Global로 변경함.
- Boss 프리팹에는 기존부터 `NetworkObject`와 `NetworkTransform`이 있었음.

Fusion 네트워크 객체 사용 원칙:

- 생성: `Runner.Spawn()`
- 생성 완료 초기화: `NetworkBehaviour.Spawned()`
- 제거: `Runner.Despawn()`
- 네트워크 생성은 State Authority에서 한 번만 실행해야 함.

## 병합 충돌 위험 분석

- `FallDamage.cs`: 낮음. 최신 origin/main과 수정 전 내용이 동일했음.
- `LavaBurn.cs`: 낮음. 최신 origin/main과 수정 전 내용이 동일했음.
- `GameManager.cs`: 중간. 최신 커밋이 파일을 건드렸지만 당시 실질 차이는 공백 수준이었음. 같은 생성·제거 구간을 동료가 다시 수정하면 충돌 가능.
- `Boss.prefab`: 낮음~중간. 동료는 Hitbox 태그를 수정했고 이번 작업은 NetworkObject 관심도 설정을 수정하여 서로 다른 구간임.
- `PrototypeRoundScene.unity`: 충돌 위험이 높아 이번 작업에서 수정하지 않음.
- `PrototypeRoundManager.cs`: 동료 작업과 겹칠 수 있어 이번 작업에서 수정하지 않음.
- `PlayerGameState.cs`: 이번 버그 수정에서 수정하지 않음.

## 검증 상태

- `git diff --check` 통과.
- 명령줄 `dotnet build`는 로컬에 .NET Framework 4.7.1 타기팅 팩이 없어 실행하지 못함.
- Unity Editor에서 스크립트 컴파일과 Host/Client 실행 검증이 필요함.
- 수정 사항은 아직 커밋하지 않은 상태.

## 남은 확인 사항

- Unity Editor 컴파일 오류 여부 확인.
- 1라운드 종료 후 2라운드 스폰에서 낙하 피해가 발생하지 않는지 확인.
- 각 라운드 시작 후 10초 전에는 용암 피해가 없고 이후에는 정상 적용되는지 확인.
- Host와 Client 모두 술래를 볼 수 있는지 확인.
- 라운드 종료 시 술래가 모든 클라이언트에서 제거되고 다음 라운드에 하나만 재생성되는지 확인.
- 술래 AI가 클라이언트별로 중복 실행되어 위치가 흔들리지 않는지 확인. 필요하면 State Authority에서만 AI를 실행하도록 후속 수정.
- 사망한 플레이어의 `PlayerNoise`가 봇 어그로를 발생시키지 않도록 후속 수정.
