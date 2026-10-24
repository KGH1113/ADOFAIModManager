# 모드 자동 설정 구현과 검증

2026-10-02 기준 구현이에요. [설정 사양](/Users/kgh/dev/src/ADOFAIModManager/mod-settings.md)과 [사전 조사](/Users/kgh/dev/src/ADOFAIModManager/docs/mod-settings-research.md)를 바탕으로 적용했어요.

## 실행할 앱

[ADOFAI Mod Manager.app](</Users/kgh/dev/src/ADOFAIModManager/build/ADOFAI Mod Manager.app>)에 변경 사항을 반영했어요. macOS Apple Silicon용 빌드예요. 기존 앱을 종료한 뒤 이 빌드를 열어서 확인하면 돼요.

## 다운로드 오류 수정

`The selected file is not a ZIP archive.` 오류는 다운로드 임시 파일의 이름에 `.zip` 확장자가 없어서 발생했어요. Rust 다운로드 함수는 ZIP 서명을 확인했지만, C# 설치 엔진은 확장자를 먼저 확인하고 해당 파일을 거부하고 있었어요.

다운로드 임시 파일에 `.zip` 확장자를 붙이도록 수정했어요. 공식 JALib·JRP ZIP을 Rust 함수로 내려받고, 그 임시 파일을 그대로 배포용 엔진의 `inspectmod` 요청에 전달해 두 모드 모두 검사에 성공하는 것을 확인했어요.

## 공식 다운로드 주소

추천 설치는 TUF 목록 조회 없이 아래 주소를 사용해요. JRP의 JALib을 먼저 설치하고, 모든 ZIP의 모드 ID를 검사한 뒤 설치를 시작해요. TUFHelperLite는 통신 기능이 내장되어 별도 ADOFAI IPC 설치가 필요하지 않아요.

주소와 버전은 [official-downloads.ts](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/models/catalog/official-downloads.ts)에 모았어요. 새 릴리스를 연결할 때는 주소와 버전을 함께 갱신하면 돼요. TUF 목록을 불러오지 못해도 이 추천 모드 10개는 목록과 업데이트 확인에 사용할 수 있어요.

| 모드 | ZIP에서 확인한 버전 | 직접 지정한 주소 |
| --- | --- | --- |
| JALib | 1.0.0.45 beta45 | [GitHub ZIP](https://github.com/Jongye0l/JALib/releases/download/v1.0.0.45/JALib.zip) |
| JipperResourcePack | 1.5.2.0 | [GitHub ZIP](https://github.com/Jongye0l/JipperResourcePack/releases/download/v1.5.2.0/JipperResourcePack.zip) |
| Keyboard Chatter Blocker | 0.1.2 | [GitHub ZIP](https://github.com/fangshenghan/KeyboardChatterBlocker/releases/download/0.1.2/KeyboardChatterBlocker.v0.1.2.zip) |
| TUFHelperLite | 0.1.9 | [GitHub ZIP](https://github.com/KGH1113/TUFHelperLite/releases/download/v0.1.9/TUFHelperLite.zip) |
| TUFHelper | 3.1.1 | [macOS](https://github.com/coyami-ke/TUFHelper/releases/download/v3.1.1/TUFHelper.OSX.3.1.1.zip) · [Windows](https://github.com/coyami-ke/TUFHelper/releases/download/v3.1.1/TUFHelper.Windows.3.1.1.zip) · [Linux](https://github.com/coyami-ke/TUFHelper/releases/download/v3.1.1/TUFHelper.Linux.3.1.1.zip) |
| Enhanced Countdown | 0.2.3 | [GitHub ZIP](https://github.com/KGH1113/enhanced-countdown/releases/download/v0.2.3/EnhancedCountdown.zip) |
| Better Calibration | 1.2.2.0 | [GitHub ZIP](https://github.com/Jongye0l/BetterCalibration/releases/download/v1.2.2.0/BetterCalibration.zip) |
| YouTubeStream | 1.0.5 | [제작자의 고정 다운로드 링크](https://adof.ai/mods/YouTubeStream) |
| Enhanced Effect Remover | 1.7.0 | [GitHub ZIP](https://github.com/WsbiMango/EnhancedEffectRemover/releases/download/1.7.0/EnhancedEffectRemover_1.7.0.zip) |
| ADOFAI IPC | 0.4.1 | [GitHub ZIP](https://github.com/KGH1113/adofai-ipc/releases/download/v0.4.1/AdofaiIpc.zip) |

macOS에서 선택하는 10개 ZIP은 실제로 내려받아서 `Info.json`의 모드 ID와 버전을 확인했어요. TUFHelper의 세 운영체제용 주소는 [공식 릴리스](https://github.com/coyami-ke/TUFHelper/releases/tag/v3.1.1)의 자산을 확인해서 지정했어요. YouTubeStream의 링크는 요청할 때마다 현재 Discord CDN 주소로 연결되므로 만료되는 CDN 주소를 저장하지 않아요.

## 설치 시 적용하는 설정

| 모드 | 적용 내용 |
| --- | --- |
| JRP | Progress, XAccuracy, Music Time, Progress Bar, BPM, Combo, Timing Scale, Attempt, Full Attempt를 켜요. Checkpoint, Best, Auto Combo는 꺼요. Rabbit·Ball Color·Tile Color 변경도 꺼요. |
| JRP Key Viewer | Rain·Ghost Rain·Auto Setup Key Limit를 켜요. 새 설정은 Key10·Foot None으로 시작하고, 기존 손·발 스타일과 키 배열은 보존해요. Music Time 언어는 시스템 UI 언어가 한국어면 한국어, 그 외에는 영어로 설정해요. |
| Keyboard Chatter Blocker | 입력 간격을 35ms로 설정해요. JRP가 활성화되어 있으면 키 제한을 켜고 현재 손·발 키 배열을 JRP 프로필에 동기화해요. 기존 개인 프로필과 제외 키는 보존해요. 기존 XML 설정도 읽어서 JSON으로 옮겨요. |
| TUFHelper | 전체 오버레이, Leaderboard, Level Info, Score & Speed를 켜요. JRP가 함께 활성화되어 있으면 Progress·Combo·Judgements·Current BPM·Real BPM의 중복 표시를 꺼요. 새 요소에는 모드의 기본 위치를 넣고, 기존 위치와 다운로드 경로는 보존해요. |
| Enhanced Countdown | `UpdateSettings.json`에서 베타 업데이트를 켜요. 확인한 0.2.0–0.2.3 설정 형식을 지원해요. |
| TUFHelperLite | 통신 기능을 내장하므로 별도 IPC 모드를 설치하지 않아요. Lite는 전체 TUFHelper의 오버레이 설정을 제공하지 않으므로 다운로드 경로를 보존해요. |
| Enhanced Effect Remover | 게임 내 모드 설정에서 조정할 수 있도록 안내해요. 사양 문서의 추가 기능 두 항목은 게임 내에서 나중에 설정하도록 남겨 두었어요. |

위 기본값은 온보딩뿐 아니라 새 로컬 ZIP 설치에도 적용돼요. 재설치·업데이트에서는 `Settings.json`, `Setting.json`, `Setting.xml`, `UpdateSettings.json`과 첫 백업을 보존해요. 설정 파일에 있는 다른 필드도 유지하며, 이미 설치한 모드에는 새 설치용 기본값을 반복 적용하지 않아요.

JRP가 게임을 초기화할 때 운영체제별 네이티브 키 코드도 동기화하도록 `AutoSetupKeyLimit`를 켜요. 설치기가 작성한 Unity 키 배열과 프로필은 자동 검증했고, 실제 게임에서 입력 제한이 적용되는지는 게임 실행 후 확인할 수 있어요.

## JRP 키 입력 화면

JRP 다음에 키 배치 화면을 넣었어요. JRP를 설치하지 않으면 이 단계를 건너뛰어요.

제목은 `Key bindings / 키 설정 / 按键设置`으로 표시하고, 키 박스에는 순번 없이 키 이름만 보여줘요. 아래 표의 키 번호는 설정 배열의 순서를 설명하기 위한 번호예요.

창 왼쪽의 세로 선택 목록에서 Key10·Key12·Key16·Key20을 고를 수 있어요. 원본 JRP 1.5.2.0의 `KeyViewer.Initialize0/1/2/3KeyViewer` 좌표와 `BackSequence16/20` 순서를 반영해 키의 위치·너비·간격을 맞췄어요. KPS와 Total은 표시하지 않아요.

| 스타일 | 원본 키 배치와 손 안내 |
| --- | --- |
| Key10 | 윗줄에 손가락 키 8개, 아랫줄 가운데에 넓은 엄지 키 2개를 표시해요. |
| Key12 | 윗줄은 동일하고, 아랫줄 가운데에 엄지 키 4개를 놓아요. 키 번호 순서는 10·9·11·12이며 가운데 두 키가 더 넓어요. 각 엄지의 두 키는 같은 엄지 동작과 호버 색을 사용해요. |
| Key16 | 8개씩 두 줄로 표시해요. 아랫줄 순서는 13·14·10·9·11·12·15·16이에요. 양손 손날의 키는 호버 색만 표시하고 입력 대기 중에도 손을 움직이지 않아요. |
| Key20 | Key16의 두 줄 아래 가운데에 자유 키 4개를 추가해요. 아랫줄 순서는 18·17·19·20이며 가운데 두 키가 더 넓어요. 자유 키는 손 표시와 연결하지 않고 이미 쓰는 키도 할당할 수 있어요. |

손 영역은 220px을 확보해 기본 손 그래픽 크기인 175×196px을 유지하고, 키뷰어는 나머지 높이 안에서 원본 비율을 유지해요. Key20으로 바꿔도 손이 아래로 밀리지 않아요. 스타일별 설명과 호버 안내·KCB 동기화 안내 문장은 요청대로 제거했고, 입력 대기·오류·취소 안내는 하단 버튼 옆에 표시해요. 긴 키 이름은 글자를 조금 작게 표시해요. 화면 크기는 `index.css`의 `.key-viewer-editor`에 있는 `--hand-area-height`, `.finger-hands`의 `--hand-max-height`, `.key-viewer-layout`에서 조절해요.

1. 키 박스를 누르면 해당 손가락이 내려가고 입력을 기다려요.
2. 키를 누르면 박스에 등록되고 손가락이 다시 올라와요.
3. 중복 키나 지원하지 않는 키는 설명을 보여주고 입력 대기를 유지해요.
4. Esc, 창의 포커스 해제, 박스 밖 클릭으로 입력을 취소할 수 있어요.
5. **Save & continue / 저장하고 계속**으로 JRP에 저장하고 KCB 프로필도 갱신해요. 저장 오류가 나면 화면에 남아서 다시 시도할 수 있어요.
6. **Skip / 건너뛰기**는 키 편집 내용을 저장하지 않고 다음으로 이동해요.

물리 키 코드를 사용해서 한국어 IME 상태에서도 영문 키 배치를 등록할 수 있어요. 기존에 사용하는 스타일과 네 가지 키 배열을 모두 불러와요. 스타일을 오갈 때 각 배열의 편집 내용은 유지하고 입력 대기는 취소해요. 저장하면 선택한 스타일을 활성화하고 그 배열만 갱신해요. 다른 스타일의 저장된 배열, 발 스타일, Ghost Key 배열, 사용자 지정 키 텍스트는 보존해요. Key20의 자유 키 네 개는 JRP 기본값처럼 다른 키와 중복해서 사용할 수 있어요. 손가락 애니메이션은 동작 줄이기 설정을 따라요.

손 그림은 첨부한 영상처럼 손등을 비스듬히 보는 시점이에요. 연결된 손 메시를 사용해서 손등·손가락·엄지 사이에 틈이 없고, 흰 표면과 회색 옆면이 매끈하게 이어져요. 손목이 중앙 쪽으로 너무 꺾여 보이지 않게 기울기를 줄였어요. 이 구도를 유지하면서 카메라를 조금 높여 손등을 더 위에서 내려다보도록 조정했어요. 손목은 화면 아래로 이어져서 잘린 단면이 보이지 않아요.

카메라는 기존 직교 투영에서 원근 투영으로 바꿨어요. 화각을 좁히고 카메라를 멀리 두어 망원 카메라처럼 앞뒤의 크기 차이가 작게 보이도록 했어요. 카메라 각도와 화각은 `hand-rig.ts`의 `azimuth`, `elevation`, `verticalFieldOfView`에서 조정할 수 있어요. 손 크기와 구도는 편안한 기본 자세를 기준으로 한 번 정하므로, 손가락이 움직이는 동안 카메라가 확대·축소되지 않아요. SVG 대체 화면도 같은 카메라 계산을 사용해요.

그림의 기본 메시는 MIT로 공개된 [WebXR Input Profiles](https://github.com/immersive-web/webxr-input-profiles/tree/main/packages/assets)의 손 모델이에요. 앱에 좌표와 관절 정보를 포함하므로 실행 중 다운로드하지 않아요. WebGL에서 표면의 법선을 보간해 줄무늬나 삼각형 경계가 드러나지 않게 그려요. WebGL을 사용할 수 없으면 같은 자세를 SVG로 표시해요. 출처와 원본 라이선스는 [LICENSE.md](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/assets/hand/LICENSE.md)에 있어요.

기본 자세부터 타이핑을 준비하는 것처럼 손가락의 세 관절을 살짝 굽혀요. 새끼·약지·중지·검지는 각각 조금씩 다른 각도를 사용해요. 누를 때는 손등·손목·손가락 뿌리의 위치를 유지하면서 첫 관절도 조금 함께 움직이고, 중간 마디와 끝마디가 추가로 접혀요. 등록하거나 취소한 뒤에도 이 편안한 기본 자세로 돌아와요. 각 마디 길이는 유지하고, 관절을 따라 손의 표면도 함께 움직여요. 접힌 끝마디와 옆면이 보이고 겹치는 부분은 깊이에 따라 가려져요. 손의 기본 색은 흰색이에요.

엄지는 기본 자세에서 뿌리 관절을 검지 쪽으로 조금 더 모아, 두 손가락 사이의 벌어진 각도를 줄였어요. 이 각도는 `hand-rig.ts`의 `thumbRestAdduction`에서 조정할 수 있어요. 누를 때는 손바닥에 붙은 뿌리 관절에서 전체가 손바닥 쪽으로 내려가고, 엄지의 긴 축을 따라 조금 회전해요. 나머지 두 관절은 작게 굽혀서 끝마디만 꺾이는 모양을 줄였어요. 엄지에만 별도의 관절 축과 움직임을 사용해요.

입력 대기 중에는 해당 손가락이 눌린 자세를 유지해요. 누를 때는 0.22초, 등록하거나 취소해서 펴질 때는 0.28초 동안 ease-out으로 움직여요. 처음에 빠르게 움직이고 목표 자세에 가까워질수록 천천히 멈춰요. 도중에 다른 키 박스를 누르거나 취소해도 현재 자세에서 새 목표로 이동해요. 동작 줄이기가 켜져 있으면 자세를 즉시 바꿔요. 시간과 곡선은 `finger-hands.tsx`의 `fingerMotion.pressDuration`, `releaseDuration`, `ease`에서 조절해요.

키 박스에 마우스를 올리면 대응하는 손가락에 앱의 포인트 컬러를 섞어서 표시해요. 실제 색은 `--primary` 테마 값을 읽고, 색의 강도는 `hand-rig.ts`의 `handHighlightStrength`에서 조정할 수 있어요. 손가락의 피부 가중치를 사용해 뿌리 쪽 경계를 부드럽게 흐리며, 손목과 다른 손가락은 기본 색을 유지해요. 손날 키는 새끼손가락 아래의 손바닥 바깥 가장자리만 표시해요. 마우스를 옮기거나 떼면 현재 색에서 부드럽게 전환해요. 호버 색과 키 입력 대기 자세는 독립적으로 처리하고, 동작 줄이기가 켜져 있으면 색도 즉시 바꿔요. WebGL과 SVG는 같은 색 계산을 사용해요.

키 위치와 크기는 [key-viewer-layout.ts](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/models/onboarding/key-viewer-layout.ts)에서 조정해요. 화면은 [jrp-keys-step.tsx](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/components/onboarding/jrp-keys-step.tsx), [finger-hands.tsx](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/components/onboarding/finger-hands.tsx), [hand-rig.ts](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/models/onboarding/hand-rig.ts), [hand-renderer.ts](/Users/kgh/dev/src/ADOFAIModManager/frontend/src/components/onboarding/hand-renderer.ts)에 있고, 설정 저장은 [OnboardingSetupService.cs](/Users/kgh/dev/src/ADOFAIModManager/Engine/NativeUmm.Infrastructure/Presets/OnboardingSetupService.cs)에 있어요.

## 기존 화면 요청

- UMM 전에 Steam alpha 안내를 추가했고, 첨부한 두 사진을 넓게 배치했어요.
- public에서는 계속 버튼이 비활성화되고, alpha 업데이트 완료를 감지하면 활성화돼요. 화면에 진입한 뒤에는 사용자가 버튼을 눌러 넘어가요. 이미 alpha 설치가 완료된 상태에서 시작하면 안내를 건너뛰어요.
- Check again과 하단 안내 문장·버튼 위 구분선을 없애고, 강조하지 않는 Skip을 추가했어요.
- UMM 다음에 Ctrl+F10과 모드별 설정 버튼을 설명하는 안내를 넣었어요. 왼쪽 영상 자리는 요청대로 비워 두고, 오른쪽에 설명과 Understand / 이해했어요 버튼을 배치했어요.
- `asdf.md`의 문장을 반영하고, 한국어는 기존 해요체를 유지했어요.
- TUFHelperLite의 통신 기능 내장에 맞춰 별도 IPC 자동 설치와 안내를 제거했어요. More mods에서 PACL을 제외했어요.

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| 프런트엔드 TypeScript·Biome | 통과 |
| 프런트엔드 테스트 | 78개 통과 |
| 엔진 설정·ZIP 설치·설정 보존·경로 검증 | 112개 통과 |
| Rust 기본 테스트 | 4개 통과 |
| 공식 JALib·JRP 다운로드 → 배포 엔진 ZIP 검사 | 통과 |
| 배포용 macOS AOT 설정 프로토콜 | 37개 통과 |
| macOS Apple Silicon 앱 빌드 | 성공 |

네이티브 검증은 임시 게임·모드 폴더에서 진행했어요. 실제 화면과 게임 안의 표시·입력 동작은 사용자가 확인하기로 했어요. 빌드에는 기존 dnlib 트리밍 경고와 Vite의 번들 크기 알림이 남아 있어요.

손가락 검증은 애니메이션 중 뿌리의 위치와 각 마디 길이가 유지되는지, 첫 관절도 함께 움직이는지, 손끝이 손바닥 쪽으로 굽혀지는지 확인해요. 빠르게 손가락을 바꾸는 동안 전체 표면 좌표와 법선이 유효한지, SVG의 깊이 순서가 맞는지, 범위 밖 애니메이션 값을 처리하는지도 검사해요. 새 카메라는 앞뒤의 크기 차이가 작게 유지되는지, 기본·중간·눌린 자세의 모든 표면이 카메라 앞의 표시 범위에 놓이는지도 검사해요. 손 그림 자체는 이 세 자세를 별도로 렌더링해서 확인했어요. 실제 앱 화면은 사용자가 확인하기로 했어요. 기존 키 등록·중복 키·엄지 전환·취소·저장 재시도 검증도 통과했어요. 전체 검사 중 발견한 기존 업데이트 조회 테스트의 재시도 지연과 대기 시간 충돌은 테스트용 재시도 지연을 0으로 설정해서 해결했어요.

호버 검증에서는 양쪽 엄지를 포함한 10개 키의 손가락 연결과 마우스를 뗐을 때의 해제를 확인했어요. 다른 키에 마우스를 올려도 기존 입력 대기가 유지되는지, 손끝의 색만 바뀌고 다른 손가락·손목·관절 위치는 그대로인지도 검사했어요. 기본·검지 호버·엄지 호버의 손 그림을 앱 화면과 별도로 렌더링해서 색의 범위를 확인했어요.

네 스타일의 원본 좌표·키 순서·서로 겹치지 않는 배치도 검사해요. Key12 이상에서 엄지 키 두 개가 같은 엄지에 연결되는지, 손날 키는 호버만 표시하고 자유 키는 손과 연결하지 않는지도 확인했어요. 스타일별 배열 저장·기존 설정 보존·KCB 동기화와 Key20 자유 키의 중복 허용은 임시 설정 파일과 API 테스트로 검사했어요.

다시 실행할 수 있는 배포 엔진 검증은 [published-engine-checks.py](/Users/kgh/dev/src/ADOFAIModManager/Engine/NativeUmm.Checks/published-engine-checks.py)에 있어요.
