# 모드 자동 설정 조사

조사일: 2026-10-01. 입력 문서: 프로젝트 루트의 `mod-settings.md`.

이번 작업은 조사만 수행했다. 앱 코드와 실제 게임의 모드 설정은 변경하지 않았다. 공개 소스의 배포 태그, 현재 앱 코드, 첨부 영상으로 확인한 내용과 구현 제안을 구분해 기록한다. 실제 게임에서의 저장·재실행 검증은 구현 단계에 남아 있다.

## 결과

문서의 주요 설정은 설치 직후 모드별 JSON 파일을 수정하는 방식으로 적용할 수 있다. JRP, KeyboardChatterBlocker, TUFHelper, Enhanced Countdown의 저장 구조와 주요 필드를 확인했다.

- JRP는 오버레이와 키 뷰어 설정을 `Settings.json`의 `Feature` 아래에 저장한다. BPM의 내부 이름은 대문자 `BPM`이다.
- KeyboardChatterBlocker는 단수형 `Setting.json`을 사용한다. 임계값은 밀리초 단위다. JRP의 자동 동기화 기능과 채터 블로커의 키 제한 활성화는 각각 설정해야 한다.
- TUFHelper는 전체 오버레이를 켜고 개별 항목을 조정해야 한다. 현재 앱의 전체 오버레이 끄기는 요구사항과 다르다.
- TUFHelperLite 0.1.9에는 TUFHelper와 같은 리더보드·레벨 정보·점수/배속 오버레이 설정이 없다. Lite에 해당 JSON 필드를 추가해도 같은 기능을 만들 수 없다.
- Enhanced Countdown 0.2.0의 베타 업데이트 설정은 `UpdateSettings.json`의 `ReceiveBetaUpdates`다.
- EnhancedEffectRemover 1.7.0에서는 문서의 일시정지 타일 카운트다운과 게임 중 토글 단축키를 확인하지 못했다. 기본 설정 변경을 요구하는 항목도 없어 자동 설정 대상에서 제외할 수 있다.
- Key10은 위쪽 8개 키와 아래쪽 엄지 키 2개다. 키 박스를 선택해 입력을 기다리는 동안 해당 손가락을 내리고, 등록이 끝나면 올리는 UI로 구현할 수 있다.

## 확인한 버전과 근거

현재 앱이 조회하는 [TUF 모드 카탈로그](https://api.tuforums.com/v2/mods?limit=100&offset=0)의 버전과 다운로드 링크를 확인했다. 아래는 조사에 사용한 소스다.

| 모드 | 카탈로그/배포 버전 | 확인한 소스 |
| --- | --- | --- |
| JipperResourcePack | 카탈로그 `1.5.2`, 배포 태그 `v1.5.2.0` | [배포 소스](https://github.com/Jongye0l/JipperResourcePack/tree/v1.5.2.0), 커밋 `0ba3ec08cb650c3d848b544cc6d0bde943730b5f` |
| KeyboardChatterBlocker | `0.1.2` | [배포 소스](https://github.com/fangshenghan/KeyboardChatterBlocker/tree/0.1.2), 커밋 `160270ee1b29fb40a36688eee77d3b6ef46a8812` |
| TUFHelper | `3.1.1` | [배포 소스](https://github.com/coyami-ke/TUFHelper/tree/v3.1.1), 커밋 `59c16adbac14cbeb010f31831b5a5d660af20f4f` |
| TUFHelperLite | `v0.1.9` | [배포 소스](https://github.com/KGH1113/TUFHelperLite/tree/v0.1.9), 커밋 `891fb9cb87462aabf71662f33dd457152a83b26a` |
| Enhanced Countdown | `0.2.0` | [배포 소스](https://github.com/KGH1113/enhanced-countdown/tree/v0.2.0), 커밋 `83d47e0186353d5e92c91119d5231b96532636ea` |
| EnhancedEffectRemover | `1.7.0` | [배포 소스](https://github.com/WsbiMango/EnhancedEffectRemover/tree/1.7.0), 커밋 `4ab61bb464c65a8f290eb8c7263e84875a283ade` |

JRP의 JSON 래퍼 구조는 [JALib의 JAFeatureSetting](https://github.com/Jongye0l/JALib/blob/1c19f2541c09b8300504eda476a5aff3abd8daf8/JALib/Core/Setting/JAFeatureSetting.cs)과 [JASetting 직렬화](https://github.com/Jongye0l/JALib/blob/1c19f2541c09b8300504eda476a5aff3abd8daf8/JALib/Core/Setting/JASetting.cs)도 대조했다. 이 라이브러리 소스 조사는 JRP 배포 바이너리의 실행 검증을 대신하지 않는다.

## JipperResourcePack

파일: `<게임>/Mods/JipperResourcePack/Settings.json`.

아래 경로는 JSON 루트 기준이며 대소문자를 그대로 사용해야 한다. 개별 토글뿐 아니라 해당 기능의 `Enabled`도 켜져 있어야 표시된다. [Status 설정](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/OverlayContents/Status.cs), [기능 등록](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/Main.cs)이 근거다.

| 문서 항목 | JSON 경로 | 적용값 |
| --- | --- | --- |
| 오버레이 상태 기능 | `Feature.Status.Enabled` | `true` |
| Display Progress | `Feature.Status.Setting.ShowProgress` | `true` |
| Display X-Acc | `Feature.Status.Setting.ShowXAccuracy` | `true` |
| Display Music Time | `Feature.Status.Setting.ShowMusicTime` | `true` |
| 음악 시간 언어 | `Feature.Status.Setting.TimeTextType` | 한국어 `0`, 영어 `1` |
| Display Progress Bar | `Feature.Status.Setting.ShowProgressBar` | `true` |
| Show Checkpoint | `Feature.Status.Setting.ShowCheckpoint` | `false` |
| Show Best Record | `Feature.Status.Setting.ShowBest` | `false` |
| Display BPM | `Feature.BPM.Enabled` | `true` |
| Display Combo | `Feature.Combo.Enabled` | `true` |
| Autoplay Combo | `Feature.Combo.Setting.EnableAutoCombo` | `false` |
| Timing Scale | `Feature.TimingScale.Enabled` | `true` |
| 시도 횟수 기능 | `Feature.Attempt.Enabled` | `true` |
| Show Attempt | `Feature.Attempt.Setting.ShowAttempt` | `true` |
| Show Full Attempt | `Feature.Attempt.Setting.ShowFullAttempt` | `true` |
| Change Rabbit | `Feature.ResourceChanger.Setting.ChangeRabbit` | `false` |
| Change Planet Color | `Feature.ResourceChanger.Setting.ChangeBallColor` | `false` |
| Change Tile Color | `Feature.ResourceChanger.Setting.ChangeTileColor` | `false` |

언어 enum은 [TimeTextType](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/OverlayContents/TimeTextType.cs)의 순서다. 앱에서 선택한 표시 언어와 PC 시스템 언어는 구분해야 한다. 구현 시 OS의 사용자 언어를 받아 `ko`이면 한국어, 그 외에는 영어로 설정하는 것을 제안한다. 이는 제안한 매핑 정책이며, JRP에 시스템 언어 자동 전환 모드가 있다는 뜻은 아니다.

[BPM](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/OverlayContents/Bpm.cs), [Combo](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/OverlayContents/Combo.cs), [Attempt](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/OverlayContents/Attempt.cs), [ResourceChanger](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/ResourceChanger.cs)의 필드를 각각 확인했다. 색상·폰트·위치·추가 오버레이 등 문서에 없는 설정은 보존한다.

### 키 뷰어

공통 접두사는 `Feature.KeyViewer.Setting`이다. [KeyViewerSetting](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/KeyViewerContents/KeyViewerSetting.cs)과 [KeyViewer](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/KeyViewerContents/KeyViewer.cs)이 근거다.

| 항목 | 접두사 아래 필드 | 적용값/동작 |
| --- | --- | --- |
| 키 뷰어 기능 | 별도 경로 `Feature.KeyViewer.Enabled` | `true` |
| Use Rain | `useRain` | `true` |
| Use Ghost Rain | `useGhostRain` | `true` |
| Automatically Set Key Limit | `AutoSetupKeyLimit` | `true` |
| Key10 | `KeyViewerStyle` | `3` |
| Foot None | `FootKeyViewerStyle` | `0` |
| Input Keys | `key10` | Unity `KeyCode` 값 10개 배열 |
| Ghost Rain Keys | `GhostKey10` 등 | 자동 수정하지 않음, 설정 UI 생략 제안 |
| Change Text | `key10Text` 등 | 기존 값 보존, 온보딩 UI 생략 |
| Display Total / KPS | 별도 Key10 토글 없음 | Key10 레이아웃에서 둘 다 생성됨 |

스타일 숫자는 [KeyviewerStyle](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/KeyViewerContents/KeyviewerStyle.cs)과 [FootKeyviewerStyle](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/KeyViewerContents/FootKeyviewerStyle.cs)의 enum 순서다. `ShowTotalKpsKey16`은 Key16 계열용 설정이므로 Key10의 Total/KPS를 켜는 필드로 취급하지 않는다.

문서의 Key10/Foot None 지정과 “customize setting, don't touch”는 적용 범위를 확정해야 한다. **제안:** 새 설치에는 Key10/Foot None을 기본값으로 적용하고, 기존 설치의 사용자 스타일과 키 배치는 보존한다. 현재 JRP 기본값은 Key16/Foot Key4다.

“Use Ghost Rain — On”과 “Ghost Rain Keys — Disable this feature”도 구분이 필요하다. **제안:** Ghost Rain은 켜되, 복잡한 Ghost Rain 키 편집 화면을 온보딩에서 생략한다. 실제 Ghost Rain을 끄라는 의도라면 앞의 On 요구와 충돌한다. 아직 이 해석을 확정된 요구로 간주하지 않는다.

## KeyboardChatterBlocker

파일: `<게임>/Mods/KeyboardChatterBlocker/Setting.json`. [Setting](https://github.com/fangshenghan/KeyboardChatterBlocker/blob/0.1.2/KeyboardChatterBlocker/Setting.cs)은 기존 `Setting.xml`을 읽어 JSON으로 이전하는 경로도 가진다. JSON 파일이 있으면 XML보다 우선하므로, 기존 XML 사용자의 설정을 보존하려면 이전 내용을 읽은 뒤 병합해야 한다.

| 항목 | JSON 경로 | 적용값/처리 |
| --- | --- | --- |
| Chatter Threshold | `inputInterval` | `35` |
| Key Limiter | `enableKeyLimiter` | JRP 연동 조건 충족 시 `true` |
| 제한 키 프로필 | `keyLimiterProfiles` | JRP 프로필과 기존 프로필 보존 |
| 선택된 프로필 | 각 프로필의 `isSelected` | 선택된 프로필이 유효하도록 처리 |
| 일반 입력 허용 키 | 프로필의 `allowedKeys` | Unity KeyCode 목록 |
| 비동기 입력 허용 키 | 프로필의 `allowedAsyncKeys` | 게임의 플랫폼별 네이티브 키 코드 목록 |

임계값은 [Patch](https://github.com/fangshenghan/KeyboardChatterBlocker/blob/0.1.2/KeyboardChatterBlocker/Patch.cs)의 `ToUnixTimeMilliseconds()` 비교로 밀리초임을 확인했다. 프로필 구조는 [KeyLimiterProfile](https://github.com/fangshenghan/KeyboardChatterBlocker/blob/0.1.2/KeyboardChatterBlocker/KeyLimiterProfile.cs)에 있다.

JRP는 [KeyboardChatterBlockerAPI](https://github.com/Jongye0l/JipperResourcePack/blob/v1.5.2.0/JipperResourcePack/KeyViewerContents/OtherModApi/KeyboardChatterBlockerAPI.cs)를 통해 `JipperResourcePack`이라는 프로필을 선택/생성하고 일반·비동기 허용 키를 갱신한다. 이 API는 `enableKeyLimiter`를 켜지 않는다. 따라서 현재 앱처럼 JRP의 `AutoSetupKeyLimit`만 켜면 채터 블로커의 제한 기능까지 활성화되지는 않는다.

동기화 코드는 게임 빌드에 따라 분기한다. 이전 빌드의 경로에는 Windows 조건이 있고, 최신 빌드에서는 SkyHook의 플랫폼별 키 변환을 사용한다. `allowedAsyncKeys`를 모든 OS에서 Windows 가상 키 값으로 저장하면 안 된다.

**구현 제안:** 게임이 종료된 상태에서 저장하고, JRP와 채터 블로커가 설치·활성화돼 있으며 지원 버전인지 확인한 뒤 연동한다. 임계값 35는 독립적으로 적용 가능하다. 제한 기능을 켤 때는 빈 허용 목록으로 시작하는 문제를 해결해야 하므로, 유효한 프로필 생성과 최초 게임 실행의 JRP 동기화 시점을 함께 검증한다. JRP가 없을 때 제한 키 목록을 임의로 추정하지 않는다.

## TUFHelper와 TUFHelperLite

### TUFHelper 3.1.1

파일: `<게임>/Mods/TUFHelper/Settings.json`. [Setting](https://github.com/coyami-ke/TUFHelper/blob/v3.1.1/Assets/TUFHelper/ModScripts/Helpers/Setting.cs)의 `IngameElementsSettings`는 ID별 [IngameElementModel](https://github.com/coyami-ke/TUFHelper/blob/v3.1.1/Assets/TUFHelper/Assets/Scripts/OverlayerSystem/IngameElementModel.cs)을 저장한다.

| 항목 | JSON 경로 | 적용값 |
| --- | --- | --- |
| 전체 오버레이 | `ShowTUFHelperOverlayer` | `true` |
| Leaderboard | `IngameElementsSettings.Leaderboard.IsShowed` | `true` |
| Level Info | `IngameElementsSettings.LevelInfo.IsShowed` | `true` |
| Score & Speed | `IngameElementsSettings.PPDisplayer.IsShowed` | `true` |
| 개별 점수/배속 | `PPDisplayer` 모델의 `Categories.PPDisplayer` | 기존 모델이면 `IsShowScore`, `IsShowSpeed` 모두 `true` |
| JRP와 겹치는 오버레이 | `Progress`, `Combo`, `Judgements`, `CurrentBPM`, `RealBPM` 각각의 `IsShowed` | JRP 설치 시 `false` |
| 다운로드 경로 | `LevelSaveFolder` | 수정하지 않음 |

`Score & Speed`의 내부 ID는 `PPDisplayer`다. [PPDisplayerScript](https://github.com/coyami-ke/TUFHelper/blob/v3.1.1/Assets/TUFHelper/Assets/Scripts/PrefabsScript/PPDisplayerScript.cs)에서 확인했고, 점수·배속 세부 옵션의 모드 기본값도 모두 On이다. 다른 ID는 [Mod 스크립트 목록](https://github.com/coyami-ke/TUFHelper/tree/v3.1.1/Assets/TUFHelper/Assets/Scripts/Mod)에 있다.

JRP가 없을 때는 문서가 지정한 세 항목만 켜고 다른 요소는 원래 설정을 보존하는 것을 제안한다. JRP가 있는 조건에서는 나열한 중복 요소를 끈다. 나중에 JRP가 설치되는 순서도 있으므로, TUF 설치 직후 한 번만 검사하는 방식으로는 부족하다. JRP 설치 완료 후에도 이 관계를 다시 계산해야 한다.

**초기 파일 생성 시 주의:** [BasicIngameElement](https://github.com/coyami-ke/TUFHelper/blob/v3.1.1/Assets/TUFHelper/Assets/Scripts/OverlayerSystem/BasicIngameElement.cs)는 ID가 아직 없는 경우에만 모드의 기본 위치·앵커를 적용한다. `{ "IsShowed": true }`만 미리 넣으면 그 초기화를 건너뛰고 모델의 기본 중앙 배치가 사용될 수 있다. 새 요소는 해당 버전의 기본 배치를 함께 복원해야 한다. 기존 요소는 위치·크기·카테고리를 그대로 보존한다.

확인한 기본 배치: Leaderboard는 RightMiddle, Level Info는 RightTop, Score & Speed는 LeftMiddle이며 위치 `(5, 0)`이다. 카테고리 JSON에는 타입 정보가 사용되므로 기존 `$type`을 보존하고, 새 카테고리의 전체 형식을 임의로 만들지 않는다. 오버레이는 해당 모드의 표시 조건도 충족해야 하므로, TUFHelper로 연 레벨에서 표시 확인이 필요하다.

### TUFHelperLite 0.1.9

[DownloadStorageSettings](https://github.com/KGH1113/TUFHelperLite/blob/v0.1.9/TUFHelperLite/Infrastructure/Settings/DownloadStorageSettings.cs)의 설정은 `DownloadRoot`다. [저장소](https://github.com/KGH1113/TUFHelperLite/blob/v0.1.9/TUFHelperLite/Infrastructure/Settings/DownloadStorageSettingsStore.cs)는 모드 폴더의 `Settings.json`을 사용한다.

이 버전에는 Full의 `IngameElementsSettings`나 리더보드·레벨 정보·점수/배속 오버레이에 대응하는 설정이 없다. 다운로드 상태 알림용 UI는 별도 기능이다. 문서대로 다운로드 경로는 나중에 게임 안에서 정하도록 그대로 두고, Full의 오버레이 프리셋을 Lite에 쓰지 않는다. ADOFAI IPC 의존성 자동 설치는 현재 앱의 설치 계획에 유지하면 된다.

## Enhanced Countdown / EnhancedEffectRemover

Countdown 파일: `<게임>/Mods/EnhancedCountdown/UpdateSettings.json`.

```json
{
  "ReceiveBetaUpdates": true
}
```

[UpdateSettings](https://github.com/KGH1113/enhanced-countdown/blob/v0.2.0/EnhancedCountdown/Infrastructure/Settings/UpdateSettings.cs)와 [Bootstrap/Main](https://github.com/KGH1113/enhanced-countdown/blob/v0.2.0/EnhancedCountdown/Bootstrap/Main.cs)에서 경로와 필드를 확인했다. JALib의 `Beta` 필드를 쓰는 모드가 아니다. 다른 설정 및 런타임 상태 파일은 수정할 필요가 없다.

EER는 [Main](https://github.com/WsbiMango/EnhancedEffectRemover/blob/1.7.0/Main.cs)의 `Settings.json`을 사용한다. [Settings](https://github.com/WsbiMango/EnhancedEffectRemover/blob/1.7.0/Settings.cs)와 해당 태그의 C# 파일 전체에서 일시정지 타일 카운트다운·입력 단축키 구현을 확인하지 못했다. 문서에서도 이 두 항목은 나중에 설정할 사항으로 적혀 있으므로 자동 설정은 생략한다. 지원 기능으로 안내하려면 다른 버전/포크가 있는지 추가 근거가 필요하다.

Ctrl+F10으로 UMM을 열고 개별 모드의 설정을 조정하는 기존 가이드는 유지할 수 있다. EER에는 게임 중 이펙트 제거 설정을 바꿀 수 있다는 설명만 추가하는 것이 현재 확인한 범위에 맞다.

## Key10 입력 UI와 손가락 동작

첨부 영상은 약 1.28초, 398×300, 60fps다. 흰색 손가락이 접히며 내려가는 움직임과 그림자를 참고할 수 있다. 짧은 영상에서 복귀 동작까지는 확인하지 못했지만, 사용자가 요청한 **등록 완료 시 다시 올라오기**를 동작 요건으로 적용한다. 영상의 녹색 배경과 부수 요소를 UI에 재현할 필요는 없다.

### 레이아웃과 저장 순서

`KeyViewer.Initialize3KeyViewer()`는 위쪽 키 8개를 왼쪽부터 index 0–7로 만들고 아래쪽 긴 키를 8–9로 만든다. KPS와 Total은 입력 키 배열에 포함되지 않는다.

| 배열 index | 화면 위치 | 기본 키 | 손가락 안내 제안 |
| --- | --- | --- | --- |
| 0 | 위 1 | Tab | 왼손 새끼 |
| 1 | 위 2 | 1 | 왼손 약지 |
| 2 | 위 3 | 2 | 왼손 중지 |
| 3 | 위 4 | E | 왼손 검지 |
| 4 | 위 5 | P | 오른손 검지 |
| 5 | 위 6 | = | 오른손 중지 |
| 6 | 위 7 | Backspace | 오른손 약지 |
| 7 | 위 8 | Backslash | 오른손 새끼 |
| 8 | 아래 왼쪽 | Space | 왼손 엄지 |
| 9 | 아래 오른쪽 | Comma | 오른손 엄지 |

배열 순서와 기본 키는 소스에서 확인했다. 손가락 매핑은 이 배치와 사용자 요청을 토대로 한 UI 설계 제안이며, 모드가 손가락 정보를 저장하는 것은 아니다. 선택한 실제 키 이름이 바뀌어도 같은 슬롯의 손가락을 안내한다.

### 상태 흐름 제안

| 상태/이벤트 | 키 박스 | 손가락 |
| --- | --- | --- |
| 기본 상태 | 등록 키 표시 | 모두 올라옴 |
| 박스 클릭 → 입력 대기 | 선택 표시 + “키를 눌러주세요” | 해당 손가락 내려감 |
| 유효한 키 입력 → 등록 완료 | 새 키 표시 | 해당 손가락 올라옴 |
| 지원하지 않는 입력/중복 처리 오류 | 안내 표시, 입력 대기 유지 | 내려간 상태 유지 |
| 다른 슬롯 선택 | 새 박스에서 입력 대기 | 이전 손가락 올라오고 새 손가락 내려감 |
| 취소·창 포커스 이탈·화면 이동 | 입력 대기 종료 | 올라옴 |
| 저장 실패 | 실패 안내와 재시도 | 입력 대기는 종료돼 올라옴 |

손가락 상태는 `activeSlot`과 `captureState`에서 계산하고 별도 상태로 중복 저장하지 않는 것이 좋다. 내려가는 상태는 물리 키를 계속 누르는 동안이 아니라 **키 등록을 기다리는 동안** 지속한다. 입력 반복 이벤트는 한 번만 처리한다. 키 입력 등록과 파일 저장 실패도 분리해, 오류 때문에 손가락이 계속 내려가 있는 상태를 피한다.

손은 키 뷰어 아래에 좌우로 두고, SVG의 손바닥과 다섯 손가락을 분리해 움직이는 것을 제안한다. 손가락 길이 축소만으로 끝내기보다 관절이 접히는 형태와 그림자 변화로 영상의 내려가는 느낌을 표현한다. 엄지도 따로 접힐 수 있어야 한다. `prefers-reduced-motion`에서는 자세 변화와 선택 강조만 제공한다.

### 입력 저장

브라우저의 키 이벤트 숫자를 그대로 Unity 설정에 저장하면 안 된다. `KeyboardEvent.code`를 Unity `KeyCode`로 바꾸는 명시적인 테이블이 필요하고, 화면에는 사용자가 이해할 키 이름을 표시한다. 숫자 패드와 상단 숫자 키, 좌우 보조 키를 구분한다. 국제 키보드·IME·OS가 가로채는 키는 별도 검증 대상이다.

등록 결과는 10개 `key10` 배열의 선택 index에만 병합한다. 다른 스타일의 배열과 `key10Text`, Ghost Rain 키는 보존한다. 기존 스타일이 Key10이 아니라면 Key10 배열만 바꿔도 현재 게임 화면에 반영되지 않으므로, 스타일 적용 범위를 앞의 정책과 함께 결정해야 한다.

중복 키를 허용할지, Escape를 취소 키로 쓸지, 입력할 때마다 저장할지 최종 완료 시 저장할지는 아직 제품 정책으로 정해지지 않았다. 구현 기본안은 10개를 편집한 뒤 명시적인 완료 버튼으로 저장하는 방식이다.

## 현재 앱에서 바꿀 지점

| 현재 지점 | 확인한 상태 | 구현 시 변경 제안 |
| --- | --- | --- |
| [OnboardingSetupService](/Users/kgh/dev/src/ADOFAIModManager/Engine/NativeUmm.Infrastructure/Presets/OnboardingSetupService.cs:17) | `jrp-key-sync`와 `tuf-overlay` 두 종류만 지원 | 모드별 프리셋과 Key10 저장 경로 추가 |
| [설정 파일 경로](/Users/kgh/dev/src/ADOFAIModManager/Engine/NativeUmm.Infrastructure/Presets/OnboardingSetupService.cs:37) | 항상 `Settings.json` | KCB `Setting.json`, Countdown `UpdateSettings.json` 선택 지원 |
| [TUF 처리](/Users/kgh/dev/src/ADOFAIModManager/Engine/NativeUmm.Infrastructure/Presets/OnboardingSetupService.cs:26) | 전체 오버레이를 항상 끔 | 전체 On + 세 항목 On + JRP 있을 때 중복 요소 Off |
| [추천 설치 완료 처리](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/models/onboarding/install-recommendation.ts:67) | 세션에서 설치한 모드에만 설정, TUF와 chatter만 호출 | JRP·KCB·TUF·Countdown 각각 완료 후 적용, 설치 순서에 따른 관계 재계산 |
| [모드 설정 가이드](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/components/onboarding/setup-guides.tsx:89) | UMM 이후 Ctrl+F10 안내, 영상 슬롯은 공란 | 가이드 설명 보강, Key10 입력 UI는 별도 단계/영역 추가 |

엔진은 기존 JSON 병합, 최초 백업, 변경 감지, 임시 파일 후 교체를 이미 지원한다. 이를 확장할 수 있다. 버전 검증은 원격 카탈로그의 표시 버전보다 실제 설치 모드의 버전과 스키마를 기준으로 한다.

현재 자동 설정 호출은 추천 설치 흐름에만 있다. 일반 카탈로그 설치/업데이트에도 같은 프리셋을 적용할지는 별도 범위 결정이 필요하다. 기존 설정을 가진 모드의 업데이트마다 사용자 선택을 덮어쓰는 방식은 피하고, 새 설치 기본값과 이후 개인화 설정을 구분하는 것을 제안한다.

## 구현 순서와 검증 제안

1. 모드별 파일명·지원 버전·필드 병합을 엔진에 추가한다. 새 파일과 기존 설정, KCB XML 이전을 구분한다.
2. 설치 완료 처리에 프리셋을 연결하고 JRP/TUF/KCB 조합을 양쪽 설치 순서로 확인한다. 프리셋 실패는 설치 성공과 구분해 재시도할 수 있게 한다.
3. Key10 편집 UI와 손가락 대기/복귀 동작을 연결한다. Unity 키 변환은 UI 텍스트와 분리한다.
4. 게임 최초 실행과 재실행 후 설정 유지, JRP 동기화, KCB 일반/비동기 입력, TUF 요소의 위치 및 표시를 확인한다.

필요한 검증은 필드 이름을 그대로 비교하는 테스트보다 실제 실패 지점을 다뤄야 한다. 기존 사용자 설정 보존, 새 TUF 모델의 기본 배치, 빈 제한 프로필, 설치 순서 반전, 알 수 없는 버전, 저장 중 게임 실행/파일 변경, 키 입력 반복·취소·포커스 이탈이 주요 대상이다.

이번 조사에서 확정하지 않은 항목은 세 가지다: 기존 사용자 스타일의 적용 범위, Ghost Rain 키 항목의 의미, EER 추가 기능을 지원하는 다른 버전/포크의 존재다. 앞의 구현 기본안을 바탕으로 진행할 수 있으나 확인되지 않은 기능을 이미 지원한다고 안내하지 않는다.
