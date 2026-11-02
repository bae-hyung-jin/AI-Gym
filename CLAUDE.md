# MiriWaBom (AI GYM)

Unity 6000.6.7f1 / URP / Input System 기반 1인칭 헬스장 프로젝트. 구조와 할 일은 `Assets/README.md`를 본다.

## 맥과 Windows를 같이 쓸 때

- 같은 저장소를 두 컴퓨터에서 쓴다. Claude 대화 기록과 메모리는 공유되지 않으므로, 계속 필요한 내용은 이 파일이나 README에 적는다.
- 작업을 시작할 때 `git pull`을 먼저 하고, 끝나면 커밋하고 푸시한다.
- `.unity` 씬 파일은 합치기 어렵다. 한 컴퓨터에서 작업한 씬을 푸시하기 전에 다른 컴퓨터에서 같은 씬을 고치지 않는다.
- `.vscode/settings.json`은 Unity가 폴더 이름에 맞춰 다시 쓴다. 커밋하지 않는다.
- Git Auto Commit 버튼은 받지 않은 변경이 있는지 확인한 뒤 쓴다.

## 작업 방식

- 수정, 커밋, 푸시, `main` 병합은 승인을 묻지 않고 진행하고, 끝나면 결과만 알린다.
- 데이터를 지우거나 되돌리기 어려운 작업, 다른 컴퓨터에서 받을 때 파일이 지워지는 작업은 먼저 확인한다.
- 씬과 에셋은 열려 있는 에디터가 있으면 YAML을 직접 고치지 말고 `unity command`로 바꾼다.
- 플레이 테스트는 에디터가 뒤에 있으면 멈춘다. 움직임을 확인할 때는 프레임과 위치가 실제로 바뀌는지 본다.
- ProBuilder 모드(`PositionToolContext`)는 에디터가 앞에 있고 Inspector가 선택을 인식한 뒤에만 켜진다. `unity command editor focus` 후 벽을 선택하고 몇 초 기다린다.

## 인수인계 (2026-11-02, 맥 → Windows)

맥에서 한 작업은 모두 `main`에 있다. Windows에서는 `git pull`부터 한다.

- 문 위 벽 마감: 오른쪽 앞면 벽을 왼쪽과 같은 평면으로 맞추고, 벽 4개의 UV를 월드 좌표로 바꿨다. 꺼 둔 `WALL/DoorHeader`는 지우지 않았다.
- 콘솔 정리: `UI_EventSystem`을 껐고, `Road/R (2)`~`R (7)`의 X 스케일을 양수로 바꿨다.
- 이동 오류: `EagleHuman`에 `AnimationEventReceiver`를 붙였다. 함수가 비어 있어 발소리는 없다.
- `Esc` 커서: `Cursor.visible = true`로 바꿨다. 플레이 모드에서 직접 눌러 보지는 않았다.
- 남은 일: README `6. 알려진 문제와 할 일`, 벽 두 겹 사이 윗부분 틈, `build_web`·`Data`·`Assets/_Recovery`를 Git 추적에서 뺄지 결정(빼면 다른 컴퓨터에서 받을 때 파일이 지워지므로 사용자 확인 필요), 헬스장 내부 제작.

## 인수인계 (2026-11-02, 클라우드 → Windows)

클라우드에는 Unity 에디터가 없어 C#과 문서만 고쳤다. 플레이 테스트는 하지 않았다.

- 1인칭 시점: `FPSController.Start()`에서 `CapDome`, `CapBrim`, `Hair`, `Eyelashes` 렌더러를 `ShadowsOnly`로 바꾼다. 씬 수정은 필요 없다.
- 발소리: `AnimationEventReceiver`가 걷기·달리기 비중 0.5 이상인 이벤트에서 `Player_Footstep_*`, 착지 때 `Player_Land`를 재생한다. 클립은 에디터 `OnValidate`가 자동으로 채운다.
- Windows에서 할 일: `git pull` → 씬 열기 → `EagleHuman`의 `AnimationEventReceiver`에 클립이 채워졌는지 확인 → 씬 저장 후 커밋 → 플레이로 모자가 안 보이는지, 발소리가 나는지, 콘솔 오류가 없는지 확인.
