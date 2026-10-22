# macOS Backslash 홀드 상태 소실 조사 및 개발사 제보 초안

## 결론

사용자가 확인한 현상은 **Backslash의 첫 눌림은 인식하지만, 계속 누르고 있는 상태를 게임 자체와 JRP가 유지하지 못한다**는 것이다. JRP 표시만의 문제로 범위를 제한할 수 없다.

다만 **“down 이벤트가 한 번만 발생한다”는 것은 SkyHook 버그의 증거가 아니다.** 설치된 macOS SkyHook은 물리 키의 눌림·해제 전환을 전달하고 중복 down을 억제한다. 정상적인 홀드는 `down 1회 → 눌림 상태 유지 → 실제 손을 뗐을 때 up 1회`다. OS의 문자 자동 반복 이벤트가 계속 오는 것을 요구하는 구조가 아니다.

현재 정확히 확인한 문제 범위는 **Backslash의 비동기 입력 경로에서 홀드 상태가 유지되지 않는 현상**이다. SkyHook 네이티브 계층이 잘못된 up을 보내는지, 게임이 홀드 상태를 초기화하거나 조회하는 과정에서 잃는지까지는 실제 재현 시점의 이벤트 기록 없이 확정할 수 없다. “입력기가 가짜 key-up을 만든다”는 설명 역시 이번 조사에서 입증되지 않았다.

이번 조사 중 JRP 입력 처리 코드나 게임 바이너리는 수정하지 않았다.

## 확인한 환경

- macOS 27.0, 빌드 26A428, Apple Silicon.
- 게임 앱 메타데이터: CFBundleShortVersionString `3.4.0`, CFBundleVersion `25`. 게임 내부 릴리스 번호나 Steam 브랜치 번호를 뜻한다고 단정하지 않는다.
- JRP 1.5.3.0 로컬 빌드와 다른 모드가 설치된 환경이다. 모드를 모두 끈 순정 환경에서 재현되는지는 아직 확인하지 않았다.
- 키보드 모델, ANSI/ISO/JIS 구분, 입력 소스, 게임의 비동기 입력 옵션 상태는 아직 기록하지 않았다.

## 코드와 바이너리에서 확인한 사실

### 1. macOS SkyHook은 IOHID 값을 읽는다

설치된 `skyhook.bundle`의 ARM64 네이티브 코드에서 `skyhook::platforms::macos::keyboard_callback`은 다음 순서로 동작한다.

1. `IOHIDValueGetElement`로 요소를 얻는다.
2. `IOHIDElementGetUsage`로 키의 HID usage를 얻는다.
3. `IOHIDValueGetIntegerValue`로 상태 값을 읽는다.
4. 값이 `1`이면 `down`, `0`이면 `up`을 호출한다. 다른 값은 이 경로에서 처리하지 않는다.

이 경로는 macOS의 입력 문자나 Unity의 `Input.GetKeyDown`을 읽는 방식이 아니다.

관련 ARM64 주소는 `keyboard_callback`의 시작 `0x64a0`, HID 상태 읽기 `0x64c4`, 값 1 비교 `0x64e4`, `up` 호출 `0x64f4`, `down` 호출 `0x6500`이다. 주소는 아래 해시의 바이너리에만 적용된다.

### 2. 중복 down 억제는 구현에 존재한다

네이티브 `down` 함수는 키의 raw usage를 내부 상태에 삽입하고 이미 존재하면 콜백을 다시 보내지 않는다. `up` 함수는 raw usage를 제거할 수 있을 때만 해제 콜백을 보낸다.

따라서 Backslash를 계속 누르는 동안 새 down 이벤트가 없는 것 자체는 정상이다. 이상 여부를 판단하려면 **물리적으로 손을 떼기 전에 up이 전달되는지**와 **게임의 held mask가 유지되는지**를 봐야 한다.

### 3. 일반 Backslash의 native mapping은 정상값을 반환했다

설치된 네이티브 라이브러리의 `key_code_to_vk`를 직접 호출한 결과:

| 입력 HID usage | 반환 KeyLabel 값 | 의미 |
| --- | --- | --- |
| `0x31` / 49 | 52 | `KeyLabel.BackSlash` |
| `0x32` / 50 | 120 | `KeyLabel.IgnoredInternal` |

일반 Backslash usage `0x31`의 변환 자체가 잘못되었다는 증거는 없다. 실제 키보드가 어떤 usage를 보고하는지는 재현 로그로 확인해야 한다. 특히 다른 usage가 들어오는 키보드에서는 이 매핑 차이를 점검할 필요가 있으나, 이것이 사용자의 원인이라고 확정하지 않는다.

여기서 SkyHook의 raw code는 **HID usage**다. Carbon/Quartz의 가상 키 코드 `0x2A`와 혼동하면 안 된다.

### 4. 게임은 down으로 held mask를 채우고 up으로 제거한다

설치된 `Assembly-CSharp.dll`을 확인한 결과:

- `AsyncInputManager.Setup`은 `SkyHookManager.KeyUpdated` 이벤트를 큐에 넣는다.
- `scrController.UpdateInput`은 이벤트의 `Key`와 `Label`로 `AsyncKeyCode`를 만들고, `KeyPressed`이면 `keyMask`와 `frameDependentKeyMask`에 추가한다.
- 해제 이벤트이면 두 held mask에서 제거한다.
- `AsyncInput.GetKey(...)`는 이 held mask에 키가 있는지를 조회한다. 다음 down이 계속 들어와야 true를 유지하는 방식이 아니다.
- `RDInputType_AsyncKeyboard.Main(ButtonState.IsDown)`도 `AsyncInputManager.keyMask`를 사용한다.
- 포커스를 잃거나 비동기 입력 상태가 바뀌는 경로에는 `AsyncInputManager.ClearKeys()` 호출이 있다.

따라서 down이 정상적으로 등록된 뒤 홀드가 끊기면, 해제 이벤트 전달·상태 초기화·키 식별 또는 이후 소비 경로를 추적해야 한다. 반복 down 부재만으로 설명할 수 없다.

### 5. JRP도 별도로 down부터 up까지 상태를 유지한다

JRP의 `KeyViewer.Update.cs`는 `SkyHookManager.KeyUpdated`를 직접 구독한다. `_keyState[index]`를 눌림·해제 이벤트에 맞춰 유지하고, 해제일 때 키 레인을 끝낸다. 게임의 held mask를 매 프레임 복사하는 구조가 아니다.

게임과 JRP에서 동시에 동일한 현상이 관찰된다는 사용자 설명은 두 시스템이 공유하는 SkyHook 이벤트를 점검할 이유가 된다. 그러나 이것만으로 네이티브 SkyHook 내부 결함을 확정하거나, 다른 설치 모드의 영향을 제외할 수는 없다.

## 개발사가 기록하면 원인을 구분할 수 있는 값

Backslash를 3초 정도 누르고 실제로 손을 떼는 한 번의 재현에서 아래 값을 같은 타임라인으로 기록하면 된다.

1. IOHID callback: device/sender 식별자, usage page, usage, integer value, timestamp.
2. SkyHook native 상태: raw usage의 내부 상태 삽입·제거 및 callback 전달 여부.
3. `SkyHookManager.KeyUpdated`: `Type`, `Key`, `Label`, timestamp.
4. `scrController.UpdateInput`: 두 held mask의 Backslash 존재 여부와 `ClearKeys` 호출 위치·이유.
5. `Application.isFocused`와 비동기 입력 활성 상태.

판별 기준:

- 물리적으로 누르는 동안 HID 값이 0으로 들어옴: 장치 보고 또는 IOHID 처리 경로를 조사한다.
- HID 상태는 유지되는데 SkyHook이 `KeyReleased`를 전달함: 네이티브 변환·상태 추적 경로를 조사한다.
- SkyHook은 올바른 down/up을 전달하지만 게임 held mask만 사라짐: 게임의 상태 초기화·소비 경로를 조사한다.
- held mask도 유지되는데 홀드 동작만 실패함: 키 필터·키 제한 또는 홀드 판정의 이후 호출 경로를 조사한다.

## 개발사에 보낼 영어 초안

> On macOS, holding the Backslash key registers the initial press, but the game does not continue to recognize the held state. The same symptom is visible in JipperResourcePack's key viewer.
>
> I understand that SkyHook is transition-based and intentionally suppresses duplicate key-down events. The issue is loss of the held state, rather than the absence of OS key-repeat events.
>
> Inspection of the installed build shows that the macOS native hook uses IOHID input values (1 = down, 0 = up), and HID usage 0x31 maps to KeyLabel.BackSlash. The game's scrController.UpdateInput adds KeyPressed events to its held masks and removes them on release; AsyncInput.GetKey queries those masks.
>
> Could you trace the IOHID value, raw usage, SkyHook event type/key/label, held-mask contents, and ClearKeys calls while Backslash is physically held? This would distinguish an unexpected release in the native hook from a held-state reset or a problem further along the game's input path.
>
> Environment: macOS 27.0 (26A428), Apple Silicon. The app metadata reports version 3.4.0, build 25. This observation was made with JipperResourcePack and other mods installed; a clean, mod-free reproduction has not yet been established. The exact keyboard model/layout, input source and asynchronous-input option should be included when submitting the report.

## 조사 대상 바이너리

- `skyhook.bundle` SHA-256: `c3a55d7883e9110ae8e7559e552a198686b6b18e2a9a9fd67880009fafb751e4`
- `SkyHook.Unity.dll` SHA-256: `64c5d43619896a53085281f5bf12a9b42696f94a3d7d5b81c8d8f41676953c33`
- 설치 경로: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app`
