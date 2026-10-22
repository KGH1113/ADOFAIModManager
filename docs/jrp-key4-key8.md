# JRP Key4·Key8 구현 초안

매니저의 키 바인딩 선택 목록을 Key4·Key8·Key10·Key12·Key16으로 변경하고 Key20을 제거했다. 포크 `https://github.com/KGH1113/JipperResourcePack`은 `/Users/kgh/dev/src/ADOFAIModManager/JipperResourcePack`에 clone했다. 이 디렉터리는 별도 Git 저장소이며 매니저 저장소의 `.gitignore`에 추가했다.

## 배치

| 스타일 | 기본 키 (왼쪽부터) | 손가락 | 배치 |
| --- | --- | --- | --- |
| Key4 | 2, E, P, = | 왼손 중지·검지, 오른손 검지·중지 | Key8 가운데 네 자리(x=108·162·216·270), 한 줄 |
| Key8 | Tab, 1, 2, E, P, =, Backspace, Backslash | 양손 엄지 제외 | 50 크기 키 8개, 한 줄 |

JRP 게임 내 Key4 키뷰어는 KPS → 키 4개 → Total을 아래쪽 한 줄에 표시한다. Key8은 키 아래 줄에 KPS·Total을 표시한다. 매니저는 기존 방식대로 입력 키만 표시하며 선택한 손가락의 호버와 입력 대기 동작을 연결했다.

Key4의 키 X 좌표는 Key8의 슬롯 2~5와 일치한다. KPS는 x=0, Total은 x=324에 각각 너비 104로 놓고, 키 4개와 모두 동일한 y=25+YLocation을 사용한다. Key8의 키는 y=79+YLocation을 유지한다. 매니저 미리보기는 두 스타일에 동일한 428 너비를 사용한다.

## 구현

- JRP에 `key4`·`key8`, 각 스타일의 사용자 지정 텍스트와 Ghost 키 설정을 추가했다.
- 일반 키·Ghost 키·텍스트 편집 및 키 제한 갱신에 새 스타일을 연결했다. Key4 편집은 4개 슬롯만 생성한다.
- 스타일 간 키 공유에서 Key4의 중지·검지와 큰 배치의 슬롯 2~5를 대응시켰다.
- 기존 enum 번호 0~3은 유지하고 Key4는 4, Key8은 5를 사용한다.
- 매니저의 레이아웃, 응답 스키마, 저장·불러오기와 KeyboardChatterBlocker 동기화를 변경했다.
- 현재 프로젝트 버전인 1.5.3.0을 엔진 설정 지원 목록에 추가했다. Key4·Key8은 원본 JRP에 PR로 반영할 기능이며, 별도의 포크 버전 저장 제한을 두지 않는다.
- Key20은 양쪽 설정 선택 목록에서 제거했다. 기존 설정을 읽는 JRP 코드와 enum은 호환성을 위해 유지했다. 매니저에서 기존 Key20 설정을 열면 Key16을 선택하며, 저장 전까지 파일을 수정하지 않고 저장 후에도 `key20` 배열을 보존한다.

## 검증 결과와 제약

- 프런트엔드 TypeScript 검사와 Biome 검사 통과.
- 프런트엔드 테스트 11개 파일, 79개 테스트 통과.
- 이전 구현의 엔진 검사 123개 통과. 새 스타일 enum 왕복 저장, 키 제한 동기화, Key20 선택 차단과 기존 배열 보존을 포함한다. 포크 제한 제거 후 엔진 검사 재실행은 완료되지 않았다.
- 두 저장소의 `git diff --check` 통과.
- 화면 검증은 요청대로 수행하지 않았다.
- JRP Release 빌드 완료: 오류 0개, 경고 67개. 빌드용 임시 MSBuild 설정으로 .NET Framework 참조 검색을 우회하고, 공식 Harmony 2.3.6 및 KeyboardChatterBlocker 0.1.2 배포 DLL을 참조했다. 게임에 설치된 Harmony는 변경하지 않았다.
- 요청한 키 공유 코드 주석과 “Key4·Key8은 포크 필요” 예외·테스트·README 안내를 제거했다.
- 2026-10-04 21:40 KST에 기존 게임 모드 폴더의 JRP를 덮어썼다. 버전은 1.5.3.0이며 메인 DLL/PDB, R136·R141·R146·R149 버전별 DLL/PDB, Info.json 총 11개 파일의 복사 후 SHA-256 일치를 검증했다. 기존 assembly_cache는 백업 후 제거해 새 DLL로 다시 생성하도록 했다.
- Settings.json, KeyCount.dat, Plays.dat, JAModInfo.json의 해시가 설치 전후 동일함을 확인했다. 화면 검증은 수행하지 않았으며 커밋·push도 수행하지 않았다.

## 설치 및 백업 위치

- Key4 KPS·Total 한 줄 배치 변경 후 2026-10-04 22:21 KST에 재빌드·재설치했다. Release 빌드 오류 0개, 설치 파일 11개 해시 검증과 설정·카운트 보존 확인을 완료했다. 게임 실행 중 파일을 원자적으로 교체했으며 새 배치는 게임 재시작 후 적용된다.
- Key4 중앙 정렬 변경 후 2026-10-04 22:06 KST에 재빌드·재설치했다. 레이아웃 테스트 8개 및 Release 빌드(오류 0개)가 통과했고, 설치 파일 11개의 해시 일치와 설정·카운트 보존을 다시 확인했다.
- 설치: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/Mods/JipperResourcePack`
- 최초 설치 전 백업: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ModBackups/JipperResourcePack-key4-key8-20261004-214011`
- 중앙 정렬 변경 전 백업: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ModBackups/JipperResourcePack-key4-key8-20261004-220638`
- KPS·Total 한 줄 배치 변경 전 백업: `/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ModBackups/JipperResourcePack-key4-key8-20261004-222157`

주요 소스는 매니저의 `frontend/src/models/onboarding/key-bindings.ts`, `frontend/src/models/onboarding/key-viewer-layout.ts`, `Engine/NativeUmm.Infrastructure/Presets/OnboardingSetupService.cs`와 포크의 `JipperResourcePack/KeyViewerContents/KeyViewer.cs`, `KeyViewerSetting.cs`, `KeyviewerStyle.cs`다.
