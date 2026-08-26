# ADOFAI Mod Manager

ADOFAI 전용 Unity Mod Manager 설치기 MVP입니다. SwiftUI 네이티브 앱과
.NET Native AOT 설치 엔진을 하나의 Universal macOS 앱으로 묶습니다.
사용자 Mac에는 .NET, Mono, Homebrew, Xcode, Rosetta가 필요하지 않습니다.

Windows용 WinUI 3 MVP도 `Windows` 폴더에 함께 있습니다. Steam 게임 탐색,
UMM과 모드 관리, ZIP 연결 프로그램, 로그, 사용자 단위 설치를 제공하며
자동 업데이트는 포함하지 않습니다. 빌드와 배포 방법은 [Windows/README.md](Windows/README.md)를 보세요.

## 기능

- Steam 라이브러리에서 ADOFAI 자동 탐색 및 수동 앱 선택
- UMM Assembly 방식 설치, 복구 설치, 후크 제거, 원본 복원
- 최신 ADOFAI용 UMM 패키지 다운로드
- Harmony가 2.4 미만이면 빌드에 포함한 2.4.2로 교체
- 모드 ZIP 검사 및 안전한 설치, 교체 전 기존 모드 자동 보관
- 앱 창 전체 드래그 앤 드롭과 Finder `다음으로 열기` 지원
- 모드 켜기/끄기, 복구 가능한 제거, Mods 폴더 열기
- 게임 `Player.log` 최근 30줄과 UMM 실제 로그 표시
- 한국어, 영어, 간체 중국어 UI와 앱 내부 언어 선택
- macOS 14 이상, Apple Silicon/Intel Universal 빌드
- macOS 26 Liquid Glass와 macOS 14/15 Material 폴백

## 빌드

요구 환경은 Xcode 26, .NET 10 SDK, Python 3.10 이상입니다. 이 요구사항은
앱을 빌드하는 개발자에게만 적용됩니다.

```sh
./scripts/build-app.sh
./scripts/build-dmg.sh
./scripts/test-engine.sh
```

결과물:

- `build/ADOFAI Mod Manager.app`
- `dist/ADOFAI-Mod-Manager-0.1.0.dmg`

현재 스크립트는 테스트 배포를 위해 ad-hoc 서명합니다. 정식 배포 전에는
Developer ID Application 서명, 공증, staple 절차로 교체해야 합니다.
`build-dmg.sh`는 첫 실행에 프로젝트 전용 도구 폴더를 만들고 무료 빌드
의존성인 `dmgbuild 1.6.7`을 설치합니다. Finder AppleScript를 사용하지 않고
배경, 112pt 아이콘, 창 크기와 위치를 DMG에 직접 기록하므로 macOS 26에서도
동일한 설치 화면을 생성합니다.

Developer ID 없이 배포한 빌드는 처음 실행할 때 macOS가 차단합니다. 사용자는
앱을 한 번 실행한 뒤 `시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기`에서
해당 앱을 승인해야 합니다.

## 안전 동작

설치 엔진은 `UnityEngine.CoreModule.dll`을 교체하기 전에 타임스탬프
백업을 만들며, UMM이 사용하는 `.original_` 원본도 유지합니다. 모드
제거는 즉시 삭제하지 않고 Application Support 아래 복구 폴더로 이동합니다.
외부 ZIP은 설치 전에 `Info.json`, 파일 수, 압축 해제 용량, 심볼릭 링크와
경로 이탈 여부를 확인합니다. 기존 모드를 교체할 때도 이전 폴더를 복구
폴더로 옮긴 다음 새 모드를 배치합니다.

이 프로젝트는 Unity Mod Manager 및 7th Beat Games의 공식 제품이 아닌
커뮤니티 도구입니다. 포팅한 Unity Mod Manager 코어와 제3자 라이선스는
앱의 `ThirdPartyNotices`에 포함됩니다.
