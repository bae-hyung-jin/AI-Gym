# AI GYM — 프로젝트 README

Unity 6 (6000.6.7f1) / URP / Input System 기반의 1인칭 헬스장 프로젝트입니다.
이 문서는 직접 만든 스크립트 4개의 구조와 수정 방법을 정리한 것입니다.

## 1. 조작법

| 입력 | 동작 |
|---|---|
| `W A S D` | 이동 |
| 마우스 | 시점 회전 (좌우는 몸, 상하는 카메라) |
| `Space` | 점프 |
| `F` | 스위치 근처에서 자동문 열기/닫기 |
| `Esc` | 마우스 커서 해제 |
| 화면 클릭 | 커서 다시 잠금 |

## 2. 폴더와 주요 파일

| 위치 | 내용 |
|---|---|
| `Assets/script/FPSController.cs` | 플레이어 이동·시점·애니메이션 |
| `Assets/script/SlidingDoor.cs` | 슬라이딩 도어 |
| `Assets/script/DoorSwitch.cs` | 도어 스위치 |
| `Assets/Editor/GitAutoCommit.cs` | 에디터 메뉴용 Git 도구 |
| `Assets/Characters/EagleHuman.fbx` | 한화 이글스 유니폼 캐릭터 (Humanoid) |
| `Assets/M/` | 직접 만든 Material (`GlassMat`, `SwitchMetalMat`, `SwitchLampMat` 등) |
| `_blender/` (Assets 밖) | 캐릭터를 만든 Blender 스크립트와 `.blend` |
| `_backup_materials/` (Assets 밖) | 원본 Material 백업 |

## 3. 씬 구성 (SampleScene)

```
NestedParentArmature_Unpack
└─ PlayerArmature        ← CharacterController, FPSController
   ├─ EagleHuman         ← Animator (StarterAssetsThirdPerson), 캐릭터 모델
   └─ MainCamera         ← 눈높이 (0, 1.65, 0.1)
DoorRoot                 ← SlidingDoor, Box Collider(Is Trigger)
├─ DoorLeft              ← 유리 문짝 + Frame(금속 테두리, 같이 움직임)
└─ DoorRight             ← 유리 문짝 + Frame
Storefront              ← WallLeft, WallRight, Header (문 양옆 벽과 위 가로대)
DoorSwitch               ← 오른쪽 벽면 스위치. DoorSwitch, Box Collider(Is Trigger)
├─ SwitchPanel / Button / Lamp
```

`PlayerArmature`의 `ThirdPersonController`, `StarterAssetsInputs`, `PlayerInput`은 꺼져 있습니다. 기존 `Geometry`, `Skeleton`도 비활성입니다.

---

## 4. 스크립트 분석

### 4-1. FPSController.cs — 플레이어 이동

**한 줄 요약**: 키보드·마우스 입력을 직접 읽어서 이동, 점프, 시점 회전, 애니메이션 값 전달을 한 번에 처리합니다.

**Inspector 값**

| 변수 | 기본값 | 의미 |
|---|---|---|
| `cameraTransform` | - | 상하로 회전시킬 카메라 (`MainCamera`) |
| `moveSpeed` | 5 | 이동 속도 (m/s) |
| `jumpHeight` | 1.2 | 점프 높이 (m) |
| `gravity` | -20 | 중력 가속도 (음수) |
| `mouseSensitivity` | 0.1 | 마우스 감도 |

**함수**

| 함수 | 역할 |
|---|---|
| `Start()` | `CharacterController`를 찾고, 커서를 잠그고 숨기며, 자식에서 `Animator`를 찾는다. |
| `Update()` | 매 프레임 아래 순서로 처리한다. |

**`Update()` 처리 순서**
1. **입력 장치 확인**: 키보드나 마우스가 없으면 바로 끝낸다.
2. **시점**: 마우스 이동량 × 감도. 좌우는 `transform.Rotate`(몸 전체), 상하는 `pitch`를 -89°~89°로 제한해 카메라만 기울인다.
3. **이동 방향**: WASD를 x(좌우), z(앞뒤)로 바꾸고 몸이 바라보는 방향 기준으로 합친 뒤 `normalized`로 대각선 속도를 맞춘다.
4. **점프와 중력**: 바닥에 있으면 `verticalVelocity`를 -2로 눌러 붙이고, Space를 누르면 `sqrt(점프높이 × -2 × 중력)`으로 초속도를 준다. 매 프레임 중력을 더한다.
5. **이동 적용**: `controller.Move(move * Time.deltaTime)`.
6. **애니메이션 전달**: `Speed`(수평 속도), `Grounded`, `Jump`, `FreeFall`, `MotionSpeed`를 `Animator`에 넣는다.
7. **커서**: 잠금이 풀려 있을 때 클릭하면 다시 잠그고, `Esc`를 누르면 해제한다.

**수정 가이드**
- 더 빠르게/높게 뛰게: `moveSpeed`, `jumpHeight`. 중력이 약하면 `gravity`를 더 작게(예: -30).
- 마우스가 너무 빠르면 `mouseSensitivity`를 줄인다.
- 키 변경: `kb.dKey` 등 `Keyboard.current`의 키 이름을 바꾼다.
- 걷기 모션이 어색하면 `animator.SetFloat("Speed", ...)` 값을 조정한다 (걷기는 약 2, 달리기는 약 5.3 기준).

**알아 둘 점**
- `Esc` 처리에서 `Cursor.visible = false`로 되어 있어, 커서가 풀려도 **화살표가 보이지 않는다**. 화면의 UI를 누르려면 `true`로 바꿔야 한다.
- `cameraTransform`이 비어 있으면 `Update`에서 오류가 난다.
- 달리기, 앉기, 슬라이딩 모션 파라미터는 아직 전달하지 않는다.

---

### 4-2. SlidingDoor.cs — 슬라이딩 도어

**한 줄 요약**: `isOpen` 값 하나를 기준으로 문짝들을 닫힌 위치와 열린 위치 사이로 부드럽게 이동시킵니다.

**구조**
- `Panel` 클래스: 문짝 하나의 정보. `door`(오브젝트), `openOffset`(열릴 때 이동량), `closedPos`/`openPos`(자동 계산).
- `panels` 배열: 문짝을 여러 개 등록할 수 있다.

**Inspector 값**

| 변수 | 기본값 | 의미 |
|---|---|---|
| `panels` | - | 문짝 목록 (현재 `DoorLeft` -1.6, `DoorRight` +1.6. 문짝이 옆 벽 뒤로 들어간다) |
| `speed` | 2 | 이동 속도 (m/s) |
| `playerTag` | Player | 감지할 태그 |
| `autoOpen` | 켜짐 | 켜면 가까이 가면 자동으로 열림. **현재 씬은 꺼져 있음** |

**함수**

| 함수 | 역할 |
|---|---|
| `Start()` | 각 문짝의 현재 `localPosition`을 닫힌 위치로 기억하고, 거기에 `openOffset`을 더해 열린 위치를 계산한다. |
| `Update()` | `isOpen`이면 열린 위치, 아니면 닫힌 위치를 목표로 `Vector3.MoveTowards`로 이동한다. |
| `Toggle()` | `isOpen`을 뒤집는다. 스위치나 UI 버튼이 호출한다. |
| `IsOpen` | 현재 상태를 읽기만 하는 속성. 램프 색에 쓴다. |
| `OnTriggerEnter/Exit()` | `autoOpen`이 켜져 있을 때만 플레이어가 영역에 들어오면 열고, 나가면 닫는다. |

**수정 가이드**
- 열리는 방향과 거리: `Panels`의 `Open Offset` (부호가 방향, 값이 거리).
- 문짝 추가: `Panels`의 Size를 늘리고 `Door`와 `Open Offset`을 채운다.
- 자동 감지 문으로: `Auto Open`을 체크한다.
- 감지 범위: `DoorRoot`의 `Box Collider` `Size`.

**알아 둘 점**
- 문은 **닫은 상태에서 Play를 시작**해야 한다. 시작 시점 위치가 "닫힌 위치"가 된다.
- `Door` 칸이 `None`이면 `Start()`에서 오류가 난다.
- 문이 움직이는 도중에 `Toggle()`을 하면 중간에서 방향이 바뀐다.
- 문짝은 `localPosition`을 쓰므로 `DoorRoot`를 회전해도 같은 방향으로 열린다.

---

### 4-3. DoorSwitch.cs — 도어 스위치

**한 줄 요약**: 플레이어가 스위치 근처에서 지정한 키를 누르면 문을 열고 닫고, 램프 색으로 상태를 알려 줍니다.

**Inspector 값**

| 변수 | 기본값 | 의미 |
|---|---|---|
| `door` | `DoorRoot` | 조작할 문 |
| `binding` | `<Keyboard>/f` | 누를 키 (Input System 경로) |
| `indicator` | `Lamp` | 색이 바뀔 램프 |
| `closedColor` / `openColor` | 빨강 / 초록 | 닫힘 / 열림 색 |
| `playerTag` | Player | 감지할 태그 |

**함수**

| 함수 | 역할 |
|---|---|
| `OnEnable()` | 코드로 `InputAction`을 만들어 키에 연결하고 `Enable()`한다. 플레이어의 `PlayerInput`을 꺼 두었기 때문에 직접 켜야 한다. |
| `OnDisable()` | 이벤트 등록을 해제하고 `Dispose()`로 정리한다. |
| `OnToggle()` | 키를 눌렀을 때 실행된다. 플레이어가 근처일 때만 `door.Toggle()`을 부른다. |
| `Press()` | 범위와 상관없이 `door.Toggle()`을 부른다. UI 버튼의 `On Click`에 연결하는 용도. |
| `Update()` | 문 상태에 따라 램프 색을 바꾼다. |
| `OnTriggerEnter/Exit()` | 스위치의 트리거 범위(약 3m)에 플레이어가 들어오고 나가는 것을 `playerNear`에 기록한다. |

**수정 가이드**
- 키 변경: `binding`을 바꾼다. 예: `<Keyboard>/e`, `<Gamepad>/buttonSouth`.
- 반응 범위: `DoorSwitch` 오브젝트의 `Box Collider` `Size`.
- 램프 색: `Closed Color`, `Open Color`.
- 버튼으로 열기: UI 버튼의 `On Click`에 `DoorSwitch > Press()`를 연결한다 (1인칭에서는 `Esc`로 커서를 풀어야 클릭할 수 있다).

**알아 둘 점**
- 입력은 `Keyboard`에 바인딩된 `InputAction`이다. `InputSystem_Actions` 에셋의 액션과는 별개다.
- `indicator.material`은 램프 전용 복사본을 만든다. 램프 한 개 정도는 괜찮다.

---

### 4-4. GitAutoCommit.cs — Git 자동 커밋 도구

**한 줄 요약**: 에디터 메뉴 `Tools > Git Auto Commit`에서 `git add`, `commit`, `push`를 버튼으로 실행합니다. `Assets/Editor` 폴더에 있어야 빌드 오류가 나지 않습니다.

**함수**

| 함수 | 역할 |
|---|---|
| `ShowWindow()` | 메뉴 항목 (`[MenuItem]`)에서 창을 연다. |
| `OnGUI()` | 창 화면. 커밋 메시지 입력칸, 단계별 버튼 3개, 한 번에 실행 버튼. |
| `RunGitCommand()` | `git`을 프로젝트 루트에서 실행하고 성공/실패를 Console에 출력한다. 단계별 버튼이 사용. |
| `ExecuteAll()` | add → commit → push를 차례로 실행한다. |
| `RunGitCommand_Silent()` | `RunGitCommand`와 비슷하지만 성공 메시지 없이 출력만 보여 준다. |

**동작 원리**
- `Directory.GetParent(Application.dataPath)`로 `Assets`의 상위 폴더(프로젝트 루트)를 구한다.
- `ProcessStartInfo`로 `git`을 실행하고 표준 출력과 오류를 읽는다. 종료 코드가 0이면 성공이다.

**수정 가이드**
- 커밋 메시지 기본값: `commitMessage` 초기값.
- 푸시할 브랜치 지정: `"push"`를 `"push origin main"` 등으로 바꾼다.

**알아 둘 점**
- 커밋 메시지에 큰따옴표(`"`)가 있으면 명령이 깨진다.
- 변경 사항이 없으면 `commit`이 실패해 `[Git Error]`가 뜨는 것은 정상이다.
- `ExecuteAll`은 앞 단계가 실패해도 다음 단계를 계속 실행한다.
- `GUI.backgroundColor`가 초록으로 바뀐 뒤 되돌리지 않아, 이후 그려지는 UI가 있다면 초록으로 보인다.
- Git이 PATH에 있어야 한다. 원격 저장소와 인증은 따로 설정되어 있어야 한다.

---

## 5. 캐릭터 (EagleHuman)

- Mixamo 캐릭터(`Ch38`)를 기반으로 Blender 스크립트(`_blender/make_eagle_human.py`)가 한화 이글스 스타일로 가공했다.
- 상의는 검정 반팔 저지(흰 파이핑, `99`, `Eagles`, `RYU`) 위에 네이비 긴팔 언더셔츠, 하의는 흰 야구 바지, 네이비 양말, 검정 모자다.
- Rig는 Humanoid이며 `StarterAssetsThirdPerson` 컨트롤러의 애니메이션이 그대로 재생된다.
- 다시 만들려면 `blender -b --python make_eagle_human.py`를 실행한다 (원본 `Ch38_nonPBR.fbx`가 필요).
- `make_avatar.py`는 초기 시도 스크립트이며 사용하지 않는다.

## 6. 알려진 문제와 할 일

- [ ] 1인칭에서 모자 챙과 머리카락이 카메라에 보일 수 있다.
- [ ] `Esc`로 커서를 풀면 화살표가 보이지 않는다 (`FPSController`의 `Cursor.visible`).
- [ ] 씬에 `EventSystem`이 2개, `AudioListener`가 중복될 수 있다.
- [ ] 문틀이 없다 (유리만 있음).
- [ ] 신발이 축구화 모양이다.
