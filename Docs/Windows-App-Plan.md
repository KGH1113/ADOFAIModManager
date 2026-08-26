# ADOFAI Mod Manager for Windows 계획

> 문서 상태: 설계 초안  
> 작성일: 2026-08-26  
> 구현 상태: Windows 앱은 아직 구현하지 않았으며, 이 문서는 지금까지 합의한 제품·기술·배포 계획을 정리한다.
> 배포 결정: 공인 Windows 코드 서명과 MSIX를 사용하지 않고, unpackaged self-contained 앱과 Ed25519 검증 기반 자체 업데이트를 사용한다.

## 1. 목표

얼불춤(A Dance of Fire and Ice)만 설치된 새 Windows PC에서도 별도의 개발 도구나 런타임 설치 없이 바로 사용할 수 있는 ADOFAI 전용 UMM 관리 앱을 만든다.

사용자가 경험해야 하는 기본 흐름은 다음과 같다.

1. 공식 다운로드 페이지에서 Windows용 파일을 받는다.
2. 내려받은 실행 파일을 연다.
3. 앱을 실행하면 Steam과 얼불춤을 자동으로 찾는다.
4. 앱 안에서 UMM을 설치하고 모드를 추가한다.
5. 이후 버전은 Windows가 자동으로 업데이트한다.

설치 과정에서 Visual Studio, .NET Runtime, Windows App SDK, PowerShell 스크립트 또는 별도 UMM GUI 설치를 사용자에게 요구하지 않는다.

## 2. 제품 원칙

- ADOFAI 전용 앱으로 만든다.
- 어려운 내부 용어보다 사용자가 하려는 작업을 중심으로 표현한다.
- 앱 전체를 관리자 권한으로 실행하지 않는다.
- 게임 파일을 변경하기 전에 원본을 백업하고 되돌릴 수 있게 한다.
- 게임이 실행 중일 때는 설치나 복원을 진행하지 않는다.
- UMM의 Windows GUI를 다시 띄우는 래퍼가 아니라, 필요한 설치 로직을 앱 내부에서 호출한다.
- Store에 의존하지 않고 공식 웹사이트에서 직접 배포한다.
- 유료 코드 서명 인증서 없이 배포하되 업데이트 파일은 자체 암호화 서명으로 검증한다.
- 설치, 업데이트, 제거가 사용자의 로컬 계정 안에서 예측 가능하게 동작해야 한다.

## 3. 지원 범위

### 운영체제

- Windows 10 version 2004, build 19041 이상
- Windows 11

Windows 10 build 19041을 최소 버전으로 두고 이 환경부터 Windows 11까지 동일한 설치·업데이트·파일 활성화 동작을 검증한다.

### 프로세서

- MVP: x64
- 이후 검토: ARM64 네이티브 빌드

얼불춤 Windows 버전과 일반적인 Steam 설치 환경을 고려해 x64를 우선한다. Windows on ARM에서는 x64 앱 호환성을 먼저 검증한 뒤 네이티브 ARM64 산출물의 필요성을 판단한다.

### 언어

- 한국어
- 영어

macOS 앱과 가능한 한 같은 용어와 화면 흐름을 유지한다.

## 4. 기술 스택

| 영역 | 선택 |
|---|---|
| 언어 | C# |
| UI | WinUI 3 |
| 플랫폼 | Windows App SDK |
| 런타임 | 구현 시작 시점의 안정적인 .NET LTS 버전 사용 |
| 배포 형태 | unpackaged self-contained 앱 |
| 최초 설치 | 단일 실행 파일이 `%LOCALAPPDATA%\Programs`에 사용자 단위로 설치 |
| 업데이트 | MVP에서 제외; 사용자가 새 버전을 직접 다운로드 |
| 주 배포 | 공식 웹사이트 직접 다운로드 |
| 파일 저장소 | 초기에는 GitHub Releases |
| CI/CD | GitHub Actions의 Windows 및 macOS 러너 |

self-contained 산출물에 필요한 .NET 및 Windows App SDK 구성 요소를 포함한다. 파일 크기는 증가하지만 새 컴퓨터에서 추가 런타임 설치 창이 나타나지 않는 것을 우선한다. MSIX 패키지 ID와 Windows App Installer에는 의존하지 않는다.

## 5. 앱 구조

Windows 앱은 다음 계층으로 나눈다.

```text
ADOFAI Mod Manager for Windows
├─ WinUI 3 UI
├─ 앱 상태 및 작업 흐름
├─ Steam/ADOFAI 탐색
├─ UMM 설치·복원 엔진
├─ 모드 ZIP 관리
├─ 로그 읽기
└─ Windows 통합
   ├─ 사용자 단위 설치·제거
   └─ 프로그램 방식 파일 연결
```

현재 macOS 앱에 포함된 C# 설치 엔진과 UMM 포팅 코드 중 운영체제에 독립적인 부분은 최대한 재사용한다. 경로 탐색, 프로세스 확인, 파일 선택, Finder/Explorer 연결 같은 플랫폼 기능은 Windows용으로 분리한다.

`UnityModManager.exe` GUI를 자식 프로세스로 실행하는 방식을 기본 구조로 삼지 않는다. 라이선스 조건을 지키면서 필요한 UMM 코어 로직 또는 현재 프로젝트의 설치 엔진을 직접 호출해 진행 상태와 오류를 우리 UI 안에서 처리한다.

## 6. 화면 구성

Windows에서는 WinUI 3의 Fluent Design과 시스템 컨트롤을 사용한다. macOS의 Liquid Glass를 그대로 흉내 내지 않고, 기능 구조와 쉬운 문구만 공유한다.

### 6.1 설치

- 얼불춤 탐지 상태
  - `Steam에서 찾음`
  - `직접 선택함`
  - `얼불춤을 찾을 수 없음`
- UMM 설치 여부와 버전
- 기본 버튼
  - `UMM 설치`
  - 설치된 경우 `다시 설치`
- 보조 작업
  - `설치 문제 해결`
  - `UMM 제거`
  - `설치 전 상태로 되돌리기`
  - `게임 변경`
  - `상태 다시 확인`

게임 경로, Managed 폴더, 후크 파일명 같은 내부 정보는 기본 화면에 노출하지 않는다. 원본 오류와 상세 경로는 사용자가 직접 여는 기술 로그에만 기록한다.

### 6.2 모드

- 설치된 모드 목록
- `모드 추가`
- 모드 제거
- 모드 폴더 열기
- 필요하면 모드 목록 새로 고침

모드 추가는 앱 안의 파일 선택기와 Windows 파일 연결을 모두 지원한다.

### 6.3 로그

- `게임 로그`
- `UMM 로그`
- 새로 고침
- Explorer에서 로그 위치 열기

기본 선택은 게임 로그로 한다. 일반 화면에서는 쉬운 오류 안내를 보여주고 UMM 로그에서만 내부 파일명과 개발 용어를 허용한다.

## 7. Steam과 얼불춤 탐색

기본 탐색 과정은 다음과 같다.

1. Windows Registry와 Steam 기본 경로에서 Steam 설치 위치를 찾는다.
2. Steam의 library folders 설정에서 추가 라이브러리를 읽는다.
3. 각 라이브러리의 `appmanifest_977950.acf`를 찾는다.
4. 매니페스트와 실제 파일 구조를 함께 확인한다.
5. 자동 탐색에 실패하면 사용자가 얼불춤 폴더를 직접 선택하게 한다.

경로 문자열만 존재한다고 성공으로 간주하지 않고, ADOFAI 실행 파일과 필요한 Unity 데이터 구조가 실제로 있는지 검증한다.

## 8. UMM 설치와 복구

### 설치 전 확인

- 얼불춤이 실행 중인지 확인
- 대상 폴더에 쓰기 가능한지 확인
- 필요한 게임 파일과 버전 정보 확인
- 설치에 사용할 UMM 구성 요소의 무결성 확인
- 백업을 만들 공간이 충분한지 확인

### 설치

- 앱에 포함된 검증된 UMM 버전을 기본으로 사용한다.
- 인터넷에서 임의의 DLL을 받아 즉시 실행하지 않는다.
- 필요한 파일만 결정적으로 변경한다.
- 원본 파일을 먼저 백업한다.
- 각 작업을 기술 로그에 기록한다.
- 일부 단계가 실패하면 가능한 범위에서 롤백한다.

### 제거와 복원

- UMM이 추가한 파일을 식별해 제거한다.
- 원본 백업을 검증한 뒤 복원한다.
- 사용자 모드와 사용자 설정을 삭제할 때는 범위를 명확히 안내한다.
- 모드 제거는 가능하면 즉시 영구 삭제하지 않고 복구 가능한 위치를 거친다.

## 9. 권한 정책

- 앱 설치는 사용자 계정 단위로 진행한다.
- 기본 실행에는 관리자 권한을 요구하지 않는다.
- 먼저 현재 사용자 권한으로 Steam 라이브러리에 쓰기 가능한지 검사한다.
- 쓰기 권한이 없으면 원본 예외 대신 사용자가 해결할 수 있는 안내를 제공한다.
- 앱 전체를 항상 관리자 권한으로 실행하는 설계는 피한다.
- 별도의 상승 권한 helper가 정말 필요한지는 실제 Steam 설치 환경 테스트 후 결정한다.

관리자 권한이 없다는 이유로 곧바로 UAC를 띄우기보다 Steam 라이브러리 위치와 폴더 권한 문제를 먼저 구분한다.

## 10. 직접 배포 방식

### 결정

- Microsoft Store를 주 배포 경로로 사용하지 않는다.
- MVP에서는 전통적인 `Setup.exe` 설치 마법사를 만들지 않는다.
- MSIX, `.appinstaller`, 공인 코드 서명 인증서에 의존하지 않는다.
- unpackaged self-contained 실행 파일을 GitHub Releases에서 직접 배포한다.
- 최초 실행 파일은 사용자 계정의 `%LOCALAPPDATA%\Programs\ADOFAIModManager`에 앱 본체를 설치한다.
- 설정과 복구 백업은 `%LOCALAPPDATA%\ADOFAIModManager\Data`에 분리해 앱 제거 시에도 보존한다.
- 시작 메뉴 바로가기, 제거 정보, 파일 연결은 사용자 계정 범위에서 등록한다.
- 설치 마법사의 여러 단계를 보여주지 않고 첫 실행 안에서 짧고 명확하게 처리한다.

사용자는 공식 사이트에서 `ADOFAIModManager-Windows-x64.exe`를 내려받아 연다. 앱은 필요한 파일을 사용자 로컬 앱 데이터에 설치하고 정상 실행한다. 관리자 권한과 별도 런타임 설치는 요구하지 않는다.

### Windows 코드 서명을 사용하지 않는 데 따른 제약

공개 릴리스에 공인 Authenticode 또는 MSIX 코드 서명을 적용하지 않는다. 따라서 최초 실행 파일과 업데이트된 실행 파일에 대해 다음 현상이 발생할 수 있음을 제품 제약으로 받아들인다.

- Windows 설치 화면에 검증된 게시자 이름을 표시할 수 없다.
- SmartScreen이 `인식되지 않은 앱` 경고를 표시할 수 있다.
- Chrome 또는 Edge가 흔하지 않은 다운로드라고 안내할 수 있다.
- Smart App Control이나 회사 보안 정책이 실행을 완전히 차단할 수 있다.
- 새 버전의 실행 파일도 보안 제품에서 다시 검사하거나 차단할 수 있다.

GitHub에서 호스팅하거나 자체 Ed25519 서명을 사용해도 이 Windows 경고를 제거할 수는 없다. 향후 사용자 경험을 개선할 필요가 생기면 공인 코드 서명 또는 Store 배포를 별도 선택지로 다시 검토한다.

### SmartScreen과 Chrome

무서명 직접 배포에서는 SmartScreen 또는 Chrome의 `흔하지 않은 다운로드` 경고를 피할 수 있다고 보장하지 않는다. 보안 기능을 우회하거나 사용자에게 보안 기능을 끄라고 요구하지 않는다.

경고 가능성을 줄이기 위한 배포 원칙은 다음과 같다.

- 공식 HTTPS 도메인에서 다운로드를 제공한다.
- URL 단축, 불필요한 다중 리디렉션, 암호화 ZIP을 사용하지 않는다.
- 다운로드할 때마다 바이너리를 동적으로 다시 만들지 않는다.
- 릴리스 파일명과 배포 방식을 일관되게 유지한다.
- 난독화 도구, 실행 압축기, 숨겨진 PowerShell 실행을 피한다.
- 공식 사이트에 게시자, 연락처, 소스 저장소, 체크섬을 표시한다.
- 소스 코드와 자동 빌드 과정을 공개한다.
- Windows Defender 등에서 오탐이 발생하면 공식 신고 절차를 사용한다.

## 11. 자체 자동 업데이트

현재 MVP에서는 구현하지 않는다. 아래 내용은 향후 자동 업데이트를 다시
도입할 때를 위한 설계 기록이며, 현재 앱은 네트워크에서 버전을 확인하거나
자체 파일을 교체하지 않는다.

Windows App Installer 대신 앱 내부의 업데이트 확인기와 별도의 `UpdateHelper.exe`를 사용한다. 실행 중인 앱은 자기 파일을 안전하게 교체하기 어렵기 때문에 실제 파일 교체는 앱 종료 후 UpdateHelper가 담당한다.

권장 정책은 다음과 같다.

- 앱 실행 시 최대 12시간에 한 번 업데이트 확인
- GitHub Releases에서 플랫폼별 최신 manifest 확인
- 백그라운드에서 업데이트 payload 다운로드 가능
- 일반 업데이트는 사용자에게 `지금 업데이트`와 `나중에` 제공
- 사용자가 승인하면 앱 종료 후 UpdateHelper가 파일 교체
- 교체 완료 후 새 버전 실행
- 업데이트 서버 장애 때문에 앱 실행을 막지 않음
- 심각한 설치 엔진 결함이 있을 때만 강제 업데이트 검토
- 앱 설정에는 현재 버전, 마지막 확인 상태, `업데이트 확인` 정도만 표시

GitHub Release의 Windows 업데이트 산출물은 개념적으로 다음과 같다.

```text
ADOFAIModManager-Windows-x64-1.1.0.zip
windows-latest.json
windows-latest.json.sig
```

`windows-latest.json`에는 버전, 다운로드 URL, payload 크기와 SHA-256을 기록한다. `windows-latest.json.sig`는 manifest 전체에 대한 Ed25519 서명이다.

앱에는 Ed25519 공개 키만 포함하고 개인 키는 릴리스 환경에만 보관한다. 업데이트 순서는 다음과 같다.

1. `windows-latest.json`과 서명을 받는다.
2. 앱에 내장된 공개 키로 manifest 서명을 검증한다.
3. 현재 버전보다 높은 버전인지 확인한다.
4. ZIP을 다운로드한다.
5. 파일 크기와 SHA-256을 검증한다.
6. 임시 폴더에서 payload 구조를 검사한다.
7. 앱을 종료하고 UpdateHelper를 실행한다.
8. 기존 버전을 임시 백업하고 새 파일로 교체한다.
9. 새 앱을 실행하고 정상 시작을 확인한다.
10. 실패하면 이전 버전으로 롤백한다.

HTTPS와 SHA-256만 사용하면 GitHub 계정이나 manifest가 함께 침해됐을 때 악성 업데이트를 막을 수 없다. 따라서 유료 인증서는 사용하지 않더라도 Ed25519 manifest 서명은 필수 보안 요구사항으로 둔다. Ed25519 키는 무료로 생성할 수 있으며 Windows 게시자 인증서와는 관계가 없다.

업데이트 서버에 연결할 수 없거나 검증에 실패하면 현재 설치된 버전을 그대로 실행한다. 서명 실패를 사용자가 무시하고 강제로 설치하는 기능은 제공하지 않는다.

## 12. GitHub Releases 운영

초기에는 공개 GitHub 저장소의 Releases를 설치 및 업데이트 파일 저장소로 사용한다.

한 릴리스에는 macOS와 Windows 산출물을 함께 올릴 수 있다.

```text
GitHub Release v1.2.0
├─ ADOFAIModManager-macOS-universal.dmg
├─ ADOFAIModManager-macOS-universal.zip
├─ appcast.xml
├─ ADOFAIModManager-Windows-x64.exe
├─ ADOFAIModManager-Windows-x64-1.2.0.zip
├─ windows-latest.json
├─ windows-latest.json.sig
└─ SHA256SUMS.txt
```

Windows 최초 다운로드 주소는 다음 형태를 사용한다.

```text
https://github.com/OWNER/REPOSITORY/releases/latest/download/ADOFAIModManager-Windows-x64.exe
```

Windows 앱은 `windows-latest.json`과 그 서명만 확인하고, macOS 자동 업데이트는 별도의 appcast와 macOS 산출물만 확인한다. 한 Release에 함께 있어도 서로 충돌하지 않는다. manifest 안의 payload URL은 버전이 명시된 Release 자산을 가리키게 해 최신 Release가 바뀌는 도중 잘못된 파일 조합을 받지 않게 한다.

### 릴리스 운영 규칙

- 저장소는 공개 상태로 유지한다.
- 저장소 소유자와 이름은 공개 후 가능하면 변경하지 않는다.
- draft와 prerelease는 일반 사용자의 최신 업데이트 대상으로 사용하지 않는다.
- 두 플랫폼이 `/releases/latest/`를 함께 사용한다면 모든 정식 Release에 두 플랫폼 산출물을 포함한다.
- macOS와 Windows 출시 주기가 달라지면 플랫폼별 고정 업데이트 주소를 분리한다.
- Release는 모든 산출물과 서명을 먼저 준비한 뒤 한 번에 공개해 manifest와 payload 사이의 배포 경쟁 상태를 피한다.
- 실제 릴리스 전에 GitHub의 리디렉션 URL을 통한 최초 다운로드와 자체 업데이트를 Windows 10/11에서 검증한다.

장기적으로는 다음과 같은 공식 도메인을 앞에 둘 수 있다.

```text
https://updates.example.com/macos/appcast.xml
https://updates.example.com/windows/latest.json
https://updates.example.com/windows/latest.json.sig
```

업데이트 파일 본체는 계속 GitHub Releases에 둘 수 있다. 고정 도메인을 사용하면 저장소 이전이나 호스팅 변경 시 기존 앱의 업데이트 주소를 유지하기 쉽다.

## 13. GitHub Actions 릴리스 자동화

목표 릴리스 파이프라인은 다음과 같다.

```text
버전 태그 푸시
       │
       ├─ macOS runner
       │  ├─ Universal 앱 빌드
       │  ├─ Developer ID 서명
       │  ├─ Apple 공증 및 staple
       │  └─ DMG/업데이트 산출물 생성
       │
       └─ Windows runner
          ├─ x64 앱 빌드
          ├─ unpackaged self-contained 산출물 생성
          ├─ 최초 실행 파일과 업데이트 ZIP 생성
          ├─ SHA-256이 포함된 manifest 생성
          └─ Ed25519 개인 키로 manifest 서명
       │
       ├─ 체크섬 생성
       ├─ 설치·업데이트 기본 검증
       └─ GitHub Release 업로드
```

처음에는 Release를 draft로 만들고 macOS 공증과 Windows 업데이트 manifest 서명·검증이 모두 성공했을 때만 정식 공개한다. 어느 플랫폼의 서명이나 필수 검증이 실패하면 최신 정식 Release를 변경하지 않는다. Windows Ed25519 개인 키는 보호된 GitHub Environment 또는 별도의 릴리스 서명 환경에 두고, 외부 Pull Request 워크플로에는 절대 노출하지 않는다.

## 14. ZIP 파일 연결과 모드 설치

unpackaged 앱이 안정적인 설치 경로에 복사된 뒤 Windows App SDK의 `ActivationRegistrationManager` 또는 동등한 사용자 단위 등록 방식으로 `.zip` 파일 연결을 등록한다. Windows의 `연결 프로그램` 목록에 ADOFAI Mod Manager를 표시하되 ZIP의 기본 앱을 강제로 변경하지 않는다. 제거 시 등록을 함께 해제한다.

사용자 흐름은 다음과 같다.

1. 모드 ZIP을 우클릭한다.
2. `연결 프로그램`에서 ADOFAI Mod Manager를 선택한다.
3. 앱이 실행되거나 이미 열린 앱 창이 앞으로 온다.
4. ZIP 구조를 검사한다.
5. 모드 이름, 버전, 제작자와 설치 대상을 보여준다.
6. 사용자가 `모드 추가`를 누르면 설치한다.

일반 ZIP을 잘못 선택할 수 있으므로 파일을 열자마자 확인 없이 설치하지 않는다. 유효한 UMM 모드가 아니면 쉬운 안내를 표시한다.

앱 실행 시 `AppInstance.GetActivatedEventArgs()`의 파일 활성화 정보를 처리한다. 이미 앱이 실행 중이면 새 인스턴스가 별도 창을 띄우지 않고 기존 인스턴스에 파일을 전달하도록 단일 인스턴스 리디렉션을 구현한다. 앱 파일 위치가 업데이트로 바뀌지 않도록 고정 설치 디렉터리의 launcher 경로를 연결 대상으로 사용한다.

### 전용 확장자

장기적으로 ZIP 컨테이너에 전용 확장자를 추가할 수 있다.

```text
.zip         연결 프로그램을 통한 설치
.adofaimod   더블클릭으로 설치하는 전용 모드 파일
```

기존 UMM 생태계와 호환되도록 `.zip` 지원은 유지한다. `.adofaimod`는 일반 압축 파일과 구분되는 아이콘과 더 명확한 사용자 경험을 제공한다.

## 15. 모드 ZIP 안전성

외부 ZIP을 다루므로 압축을 실제 모드 폴더에 바로 풀지 않는다. 임시 작업 폴더에서 검증한 뒤 최종 위치로 이동한다.

필수 검사는 다음과 같다.

- `../`를 이용한 경로 이탈 차단
- 절대 경로 차단
- 대상 모드 폴더 밖으로 나가는 경로 차단
- 심볼릭 링크 및 재분석 지점 처리
- 압축 해제 총 크기 제한
- 파일 수 제한
- 비정상 압축률과 ZIP bomb 탐지
- 손상된 ZIP 거부
- UMM 모드 구조와 메타데이터 검증
- 중첩된 최상위 폴더 한 단계 자동 인식
- 같은 모드가 이미 있을 때 덮어쓰기 확인
- 설치 도중 실패하면 부분 파일 정리

ZIP 안의 실행 파일을 임시 폴더에서 자동 실행하지 않는다. 모드 설치는 파일 배치로 제한하고, 실제 로딩은 얼불춤과 UMM의 정상 실행 과정에서 이뤄지게 한다.

## 16. 로그와 오류 처리

일반 오류는 사용자가 다음 행동을 알 수 있는 문장으로 변환한다.

예시:

- `얼불춤이 실행 중입니다. 게임을 종료한 다음 다시 시도해 주세요.`
- `얼불춤 폴더를 변경할 수 없습니다. Steam의 게임 폴더를 확인한 다음 다시 시도해 주세요.`
- `이 파일은 설치할 수 있는 UMM 모드가 아닙니다.`
- `업데이트를 확인할 수 없습니다. 현재 버전은 계속 사용할 수 있습니다.`

원본 예외, 스택 트레이스, 내부 경로, UMM 파일명은 기술 로그에 남긴다. 로그에는 인증 토큰, 사용자 개인 정보 또는 업데이트 서명 개인 키가 기록되지 않게 한다.

## 17. 보안과 신뢰

- 앱과 함께 배포하는 UMM 구성 요소의 출처와 라이선스를 문서화한다.
- 포함 파일의 해시를 빌드 시 고정하고 실행 전 검증한다.
- 릴리스 체크섬을 공개한다.
- 다운로드한 업데이트 manifest는 앱에 내장된 Ed25519 공개 키로 검증한다.
- 업데이트 payload는 서명된 manifest의 크기와 SHA-256으로 검증한다.
- 업데이트는 임시 디렉터리에서 검증하고 교체 실패 시 이전 버전으로 롤백한다.
- 게임 프로세스 메모리에 코드를 주입하지 않는다.
- 난독화된 별도 로더를 사용하지 않는다.
- 사용자 동의 없이 게임 파일을 변경하지 않는다.
- 설치 전 변경 대상과 복원 가능 여부를 알린다.
- 백신 오탐이 발생하면 탐지 우회를 시도하지 않고 해당 보안 업체의 공식 오탐 신고 절차를 사용한다.

## 18. 테스트 계획

### 지원 환경

- 깨끗한 Windows 10 22H2 x64
- 깨끗한 Windows 11 x64
- Steam 기본 경로
- 추가 드라이브의 Steam Library
- 한글 및 영문 Windows 사용자 계정
- 인터넷 연결 없음 또는 불안정
- 표준 사용자 권한
- Windows on ARM의 x64 호환 실행은 별도 확인

### 설치와 권한

- 최초 실행 파일의 사용자 단위 설치
- `%LOCALAPPDATA%\Programs\ADOFAIModManager` 설치 경로
- 시작 메뉴 바로가기와 프로그램 제거 항목 등록
- 추가 런타임 설치 요구가 없는지 확인
- UAC 없이 실행되는지 확인
- Steam 기본 폴더와 사용자 라이브러리 쓰기 가능 여부
- 제거 후 앱 설치 파일과 등록 정보 정리

### 향후 자동 업데이트를 도입할 때

- 1.0.0에서 1.1.0 업데이트
- 여러 버전을 건너뛴 업데이트
- 동일 버전 재설치 및 복구
- GitHub 리디렉션을 통한 다운로드
- 업데이트 중 네트워크 단절
- GitHub 장애 시 기존 앱 실행
- 변조되거나 서명이 잘못된 manifest 거부
- SHA-256이 다른 payload 거부
- UpdateHelper 교체 실패 후 이전 버전 롤백
- 업데이트 도중 앱 또는 PC가 종료된 뒤 복구
- 다운그레이드 기본 차단

### 게임과 UMM

- ADOFAI 자동 탐색과 직접 선택
- 게임 실행 중 설치 차단
- UMM 최초 설치와 다시 설치
- UMM 제거
- 설치 전 상태 복원
- 설치 중 실패 후 롤백
- 백업 손상 또는 누락 안내

### 모드 파일

- 앱 안에서 ZIP 선택
- ZIP 우클릭 후 `연결 프로그램` 활성화
- 앱 미실행/실행 중 각각 파일 전달
- 정상적인 두 종류의 ZIP 루트 구조
- 일반 ZIP 거부
- 손상 ZIP 거부
- 경로 이탈 ZIP 거부
- ZIP bomb 제한
- 기존 모드 덮어쓰기 확인
- `.adofaimod` 확장자 도입 시 더블클릭 활성화

### 보안과 평판

- Windows Defender 검사
- SmartScreen 표시 상태 기록
- Smart App Control에서 무서명 앱 차단 여부 기록
- Chrome, Edge에서 공식 URL 다운로드 확인
- 릴리스 체크섬 일치 확인

## 19. MVP 범위

MVP에 포함한다.

- WinUI 3 기본 화면
- Steam/ADOFAI 자동 탐색 및 직접 선택
- UMM 설치, 다시 설치, 제거, 복원
- 모드 ZIP 추가와 제거
- `.zip` 연결 프로그램 등록 및 파일 활성화
- 게임 로그와 UMM 로그
- x64 unpackaged self-contained 앱
- 사용자 단위 최초 설치와 제거
- GitHub Releases 배포

MVP 이후로 미룬다.

- ARM64 네이티브 앱
- UpdateHelper 기반 자체 자동 업데이트와 롤백
- Ed25519 서명 manifest 및 업데이트 payload 검증
- 전용 `.adofaimod` 생태계 정착
- Windows 11 전용 Explorer 새 컨텍스트 메뉴 확장
- 여러 모드 ZIP 일괄 설치
- 플랫폼별 독립 릴리스 채널
- GitHub 이외의 업데이트 CDN
- 고급 모드 의존성 해결과 온라인 카탈로그

## 20. 남은 결정 사항

- 사용할 .NET LTS 및 Windows App SDK 버전
- 최초 배포를 단일 self-extracting EXE로 할지 작은 bootstrapper와 payload로 나눌지 여부
- `%LOCALAPPDATA%` 내부의 최종 설치 경로와 launcher 이름
- 프로그램 제거 항목과 파일 연결 등록 구현 방식
- Ed25519 개인 키의 생성·백업·교체·폐기 정책
- GitHub Environment의 릴리스 승인 담당자
- UMM 코드 재사용 범위와 라이선스 검토 결과
- GitHub 공개 저장소의 최종 OWNER/REPOSITORY 이름
- macOS와 Windows 버전을 항상 동기화할지 여부
- 공식 업데이트 도메인을 처음부터 사용할지 여부
- `.adofaimod` 확장자를 MVP에 포함할지 여부
- Steam 기본 설치 폴더에서 권한 상승이 필요한 실제 사례와 대응 방식

## 21. 구현 순서

1. Windows 솔루션과 WinUI 3 셸 생성
2. 현재 C# 설치 엔진의 재사용 가능 부분 분리
3. Steam/ADOFAI 탐색 구현
4. 설치·복원 엔진 연결
5. 설치, 모드, 로그 화면 구현
6. ZIP 파일 활성화와 단일 인스턴스 처리
7. unpackaged self-contained 산출물과 사용자 단위 설치 구현
8. 파일 연결 등록·해제와 프로그램 제거 구현
9. GitHub Actions와 draft Release 자동화
10. 깨끗한 Windows 10/11 실기기 테스트
11. 무서명 배포 경고와 차단 환경을 문서화한 Preview 공개

## 22. 참고 문서

- [Unpackaged WinUI 3 앱 배포](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)
- [Windows App SDK self-contained 배포](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
- [Windows 코드 서명 선택지](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
- [SmartScreen 게시자 평판](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
- [Windows App SDK 앱 활성화](https://learn.microsoft.com/en-us/windows/apps/develop/launch/activate-an-app)
- [ActivationRegistrationManager 파일 형식 등록](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.applifecycle.activationregistrationmanager.registerforfiletypeactivation)
- [GitHub 최신 Release 파일 연결](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases)
