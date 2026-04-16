# 02.2 페이즈: 센서 프리팹 구축 및 모의 테스트 가이드

**이 가이드는 로하님이 직접 유니티 에디터에서 센서(Sensor) 오브젝트를 조립하고 모의 테스트를 돌려보기 위한 단계별 가이드입니다.**

---

## 1. 기반 스크립트 연결 확인
1. 방금 생성된 `Assets/02.Script/02.Climbing/Tools/Sensor/SensorDebugger.cs` 에 컴파일 오류가 없는지 확인합니다.

---

## 2. Sensor 임시 오브젝트 (프리팹) 만들기

### A. 모델 추출 및 기본 셋업
1. 유니티 씬(Hierarchy) 빈 공간에 마우스 우클릭 -> `Create Empty`를 눌러 **`Sensor`** 라는 이름의 오브젝트를 만듭니다. (Transform Reset 필수)
2. 기존 씬에 올라와 있는 `XR Origin` (또는 XR Rig) 하위를 열어봅니다.
3. `Camera Offset` -> `Right Controller` -> `Model` (컨트롤러 메쉬가 있는 오브젝트)를 찾아 **Ctrl+D(복사)** 합니다.
4. 복사한 컨트롤러 메쉬(`Model`)를 아까 만든 **`Sensor`** 오브젝트의 자식으로 드래그해 넣습니다.
5. (옵션) Controller의 머리(앞) 부분이 Z축을 향하게 Rotation을 조정해주세요.

### B. 컴포넌트 부착
1. 최상위 **`Sensor`** 오브젝트에 다음 컴포넌트들을 추가합니다.
   - `XRGrabInteractable` (추가 시 Rigidbody가 자동 추가됨. BoxCollider 잊지말고 추가할 것!)
   - `SensorController` (XRGrabInteractable을 요구함)
   - `SensorView` (SensorController를 요구함)
2. `SensorController`의 **Thumbstick Action** 필드에 인벤토리에 쓰시던 기존 우측 컨트롤러 조이스틱 액션 레퍼런스를 연동합니다.

---

## 3. UI 캔버스 부착 (SensorView)

1. **`Sensor`** 오브젝트 하위에 자식으로 **UI > Canvas**를 하나 생성합니다. (UI 패널용)
2. Canvas의 Render Mode를 **World Space**로 변경하고, 사이즈를 아주 작게 줄여 컨트롤러(Sensor) 윗면에 떠 있도록 배치합니다.
3. 캔버스 하위에 **3개의 빈 오브젝트(패널)**를 임시로 만듭니다.
   - `UI_Avalanche` (예: 텍스트로 [===파동===])
   - `UI_Rockfall` (예: 텍스트로 [↑방향↑])
   - `UI_Blizzard` (예: 텍스트로 [@@기상@@] )
4. 텍스트 요소로 **`ModeText`**를 하나 추가합니다.
5. 최상위 **`Sensor`**의 **`SensorView`** 스크립트 슬롯에 방금 만든 3개의 패널과 `ModeText`를 드래그해서 연결합니다.

---

## 4. 모의 환경 검증 (디버거 사용)

1. 하이어라키(Hierarchy)에 빈 오브젝트를 만들고 **`[Debug Manager]`** 로 이름을 짓습니다.
2. 여기에 방금 작성한 **`SensorDebugger`** 컴포넌트를 붙입니다.
3. VR 헤드셋을 끼고 플레이 모드(Play)에 진입합니다!
4. **테스트 1:** 손으로 뻗어서 센서를 쥡니다. 엄지 스틱을 좌/우로 튕겨서 `[===파동===]`, `[↑방향↑]`, `[@@기상@@]` UI가 잘 순환되는지 확인합니다!
5. **테스트 2 (하이라이트):** 헤드셋을 쓴 채로 키보드 주변을 더듬어 유니티 에디터 화면의 `[Debug Manager]` Inspector를 띄워 둡니다. (또는 HMD 벗지 않고 볼 수 있게 배치)
   - 인스펙터의 **`SensorDebugger`** 컴포넌트 우측 상단 `⋮(설정)` 아이콘 우클릭 -> **`Spawn Dummy ... `** 메서드를 실행해 봅니다.
   - (혹은 에디터의 `Scene` 뷰에서 플레이어 머리를 향해 그어지는 빨간 선(Gizmo)과 구체가 생기는지 봅니다.)
   - 발생시킨 가짜 재난의 방향과, 현재 쥐고 있는 센서의 방향을 일치시키면(센서를 적이 나오는 쪽으로 조준하면) **Signal 값이 100%에 가깝게 치솟고 경고 텍스트가 뜨는지 확인**합니다.

---

**테스트가 의도대로 잘 작동했다면, 이 시제품을 이제 인벤토리(`ToolBeltManager`)에 정식으로 포함시키는 `Phase 5` (또는 중간 단계)로 안전하게 진입할 수 있습니다!**
