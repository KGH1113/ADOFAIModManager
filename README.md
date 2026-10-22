# ADOFAI Mod Manager

ADOFAI용 Unity Mod Manager 설치 및 모드 관리 앱입니다. macOS·Windows·Linux에서 하나의 **Tauri 2 + React + TypeScript** 앱을 사용하며, 설치 작업은 번들에 포함된 .NET Native AOT 엔진이 수행합니다. 설치된 앱을 실행하는 사용자에게 Bun이나 .NET SDK는 필요하지 않습니다.

## 기능

- 처음 실행 시 웰컴, 모딩 경험 선택, Steam alpha 안내, UMM 설치, 게임 내 설정 안내, 추천 모드 설치와 Key4·Key8·Key10·Key12·Key16 키 배치 설정
- 기존 남색·파랑·분홍 디자인과 앱 아이콘 유지. 궤도 그래픽에는 앱 아이콘을 넣지 않음
- Motion의 포인터 반응·스프링과 GSAP의 단계별 등장·설치 완료 연출
- Steam 라이브러리 자동 탐색, 수동 게임 선택, UMM 설치·복구·후크 제거·원본 복원
- 모드 카탈로그 검색, ZIP 검사·설치, 활성화, 삭제, 폴더 열기
- TUF API 기반 모드 탐색, 추천·의존성 모드 다운로드, 설치된 모드의 업데이트 확인과 다운로드
- ZIP 드래그 앤 드롭 및 파일 연결, 설치 전에 게임과 모드 정보 확인
- 게임 로그와 UMM 로그, 한국어·영어·간체 중국어, 동작 줄이기 지원
- 메인 화면 하단 메뉴에서 웰컴 다시 열기

## 개발

개발 환경에는 Bun 1.3.11, Rust stable, .NET 10 SDK와 각 플랫폼의 네이티브 빌드 도구가 필요합니다. macOS는 Xcode Command Line Tools, Windows는 C++ 데스크톱 빌드 도구와 WebView2를 사용합니다. macOS 최소 버전은 14입니다.

프론트엔드 테스트는 Node.js 22.12 이상으로 실행합니다. Windows에서 사용할 수 있는 Node.js가 없으면 테스트 실행기가 공식 Node.js 22 배포본을 내려받아 SHA-256을 검증하고 `%LOCALAPPDATA%/ADOFAIModManager/tools/`에 캐시합니다. 최초 다운로드에는 네트워크가 필요하며 소스 작업 폴더는 변경하지 않습니다. 직접 설치한 실행 파일은 `AMM_NODE_BINARY`로 지정할 수 있습니다.

Linux CI 기준은 Ubuntu 22.04 x64입니다. WebKitGTK 4.1과 .NET Native AOT용 Clang·zlib가 필요합니다. Ubuntu에서는 다음 패키지를 설치하세요. 다른 배포판은 [Tauri 개발 환경 문서](https://v2.tauri.app/start/prerequisites/#linux)를 참고하세요.

```sh
sudo apt-get update
sudo apt-get install -y libwebkit2gtk-4.1-dev build-essential curl wget file libxdo-dev libssl-dev librsvg2-dev libayatana-appindicator3-dev patchelf clang zlib1g-dev libfuse2
```

```sh
bun install --frozen-lockfile
bun run dev
```

`dev`는 설치 엔진과 리소스를 준비한 뒤 Tauri를 실행합니다. 프론트엔드만 확인하려면 `bun run prepare:desktop`, `bun run frontend:dev`를 실행하고 개발 주소에 `?preview=1`을 붙입니다. 이 프리뷰는 명시적으로 표시된 샘플 데이터만 사용하며 실제 게임에 접근하지 않습니다. 배포 번들에는 샘플 API가 포함되지 않습니다.

## 빌드와 검증

```sh
bun run check
bun run check:engine
bun run prepare:desktop
bun run check:desktop
bun run build --bundles app              # macOS
bun run build --bundles app,dmg          # macOS 배포 이미지
bun run build --bundles nsis             # Windows에서 실행
bun run build:linux                     # Linux에서 .deb와 AppImage 생성
```

macOS 앱은 `build/ADOFAI Mod Manager.app`에 복사됩니다. 원본 번들과 플랫폼별 설치 파일은 `src-tauri/target/release/bundle/`에 생성됩니다. `--target`을 명시하면 `src-tauri/target/<target>/release/bundle/`에 생성됩니다. 각 플랫폼의 빌드는 해당 OS에서 실행하세요. macOS ARM64·x64, Windows x64·ARM64, Linux GNU x64·ARM64 엔진 타깃을 지원합니다. Linux ARM64에서는 `bun run build --target aarch64-unknown-linux-gnu --bundles deb`를 사용하세요. [AppImage 도구는 ARM 교차 컴파일을 지원하지 않습니다.](https://v2.tauri.app/distribute/appimage/)

CI는 macOS ARM64, Windows x64, Linux x64에서 프론트엔드·설치 엔진·Rust 테스트 및 앱 패키징을 수행합니다. Linux 결과물은 `.deb`와 AppImage입니다. 로컬 macOS 빌드는 개발용 서명이며, 배포용 Developer ID 서명과 공증은 별도로 구성합니다.

Linux 게임 탐색은 일반 Steam·Flatpak Steam·추가 Steam 라이브러리와 수동 선택을 지원하며, 네이티브 Unity Mono 및 Proton의 Windows 게임 폴더를 인식합니다. 실제 게임 실행 및 모드 호환성은 별도 검증이 필요합니다.

## 구조

- `frontend/`: React 화면, 기능별 API·스키마·모델·상태·훅과 테스트
- `src-tauri/`: 데스크톱 앱, 플랫폼 연동, 다운로드 및 번들 설정
- `Engine/`: .NET 설치 엔진, 플랫폼별 게임 탐색, 모드 관리 및 검증
- `Resources/`: 앱 아이콘 원본, UMM 페이로드와 라이선스 고지
- `scripts/`: 공통 앱 실행·리소스 준비·엔진 검증과 Harmony 페이로드 유지보수

## 스크립트

- `desktop.mjs`: `bun run dev`와 `bun run build`의 공통 진입점
- `prepare-desktop.mjs`: 대상 OS의 Native AOT 엔진, 설치 페이로드, 아이콘 준비
- `test-native-engine.py`: 번들 엔진의 JSON 프로토콜·프리셋·모드 가져오기·삭제 검증. 실제 게임 대신 임시 폴더를 사용합니다. `python3 scripts/test-native-engine.py <engine-path>`로 실행하며 CI에서도 사용합니다.
- `build-harmony-compat.sh`: macOS ARM64 호환 Harmony 페이로드를 다시 만드는 유지보수 도구. 일반 앱 빌드에는 필요하지 않습니다.

## 설치 엔진

모드 탐색과 온보딩 추천·의존성 설치는 모두 `https://api.tuforums.com/v2/mods`에서 조회한 목록과 버전을 사용합니다. ZIP은 TUF의 `/v2/mods/{slug}/{version}/download` 엔드포인트를 통해 설치할 때 내려받습니다. TUF 조회 실패는 오류로 표시하며, 하드코딩된 목록이나 제작자의 직접 다운로드 주소로 대체하지 않습니다. 개발용 브라우저 미리보기는 별도의 mock 데이터를 사용합니다.

모드 리스트의 **업데이트 확인** 버튼은 TUF 목록의 버전과 설치 버전을 비교하며, 새 버전도 TUF 다운로드 엔드포인트로 받습니다. 다운로드 파일은 `.zip` 임시 파일로 저장하고 실제 ZIP 서명·메타데이터를 검사한 뒤 설치합니다. 로컬 ZIP 가져오기와 드래그 앤 드롭도 지원합니다.

UMM 0.32.5 호환 payload와 Harmony 2.3.6 ABI/MonoMod.Core 1.3.3 ARM64 호환 빌드를 포함합니다. 엔진은 게임 실행 중 변경을 차단하고, 게임 어셈블리와 모드 교체 전에 백업합니다. 모드 삭제는 확인 후 영구 삭제됩니다. ZIP은 메타데이터·파일 수·압축 해제 용량·심볼릭 링크·경로 이탈을 검사합니다.

새로 설치하는 JipperResourcePack 1.5.2.0·1.5.3.0, KeyboardChatterBlocker 0.1.2, TUFHelper 3.1.1, Enhanced Countdown 0.2.0–0.2.3에는 추천 기본값을 적용합니다. JRP와 KCB의 키 제한 프로필을 동기화하고, JRP가 함께 활성화되어 있으면 TUF의 중복 표시를 끕니다. 기존 설정의 키 배치·스타일·다운로드 경로와 알 수 없는 항목을 보존하고, 첫 설정 수정 전에 `.amm-backup`을 만듭니다. 재설치와 업데이트에서도 설정 파일을 유지합니다. JRP 설치 다음 화면에서 Key4·Key8·Key10·Key12·Key16을 고르고 키뷰어 배치에 맞춰 입력을 편집할 수 있습니다. Key4는 양손 중지·검지, Key8은 엄지를 제외한 손가락을 사용합니다. 입력 대기 중 해당 손가락이 내려갔다가 등록 후 올라옵니다. 엄지 키 두 개는 같은 엄지 동작을 사용하고, 손날 키는 호버 색만 표시합니다. 선택한 스타일과 키 배열만 저장하고 다른 스타일의 배열과 발·Ghost·사용자 지정 키 텍스트를 보존합니다. 기존 Key20 설정은 보존하되 편집 화면에서는 Key16을 선택합니다. JRP의 JALib과 TUFHelperLite의 ADOFAI IPC 의존성도 자동으로 설치합니다.

모드 목록의 별도 프리셋 편집은 ADOFAI Tweaks 2.9.3과 Jipper Key Viewer 1.7.2를 지원합니다. Jipper Key Viewer 프리셋은 게임에서 생성한 기존 프로필을 사용합니다.

이 프로젝트는 Unity Mod Manager 및 7th Beat Games의 공식 제품이 아닌 커뮤니티 도구입니다. 제3자 라이선스는 앱의 `ThirdPartyNotices`에 포함됩니다.

Linux 앱 데이터는 `$XDG_DATA_HOME/ADOFAIModManager` 또는 `~/.local/share/ADOFAIModManager`에 저장됩니다. 모든 플랫폼에서 `ADOFAI_MOD_MANAGER_DATA`로 엔진 데이터 경로를 지정할 수 있습니다.
