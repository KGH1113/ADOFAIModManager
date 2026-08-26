# ADOFAI Mod Manager for Windows

Windows 10 build 19041 이상과 Windows 11을 대상으로 하는 WinUI 3 MVP입니다.
자동 업데이트는 이번 구현 범위에 포함되지 않습니다.

## 포함된 기능

- Steam 라이브러리와 `appmanifest_977950.acf`를 통한 얼불춤 자동 탐색
- 게임 폴더 직접 선택
- UMM 설치, 복구 설치, 제거, 원본 복원
- 모드 ZIP 검사, 설치, 교체, 활성화, 제거, 복원, 영구 삭제
- ZIP 드래그 앤 드롭과 Windows `연결 프로그램` 등록
- 게임 로그 최근 30줄, UMM 로그, 작업 기록
- `%LOCALAPPDATA%\Programs\ADOFAIModManager` 사용자 단위 설치
- 설정과 복구 백업은 `%LOCALAPPDATA%\ADOFAIModManager\Data`에 별도 보존
- 시작 메뉴 바로가기와 Windows 프로그램 제거 등록
- x64 unpackaged self-contained 단일 파일 게시

## 빌드

Visual Studio 2026의 WinUI 앱 개발 워크로드와 .NET 10 SDK가 필요합니다.
저장소 루트의 `ADOFAIModManager.Windows.slnx`를 Visual Studio에서 열면 앱과
검사 프로젝트가 함께 로드됩니다. 명령행에서는 다음과 같이 빌드할 수 있습니다.

```powershell
dotnet restore .\ADOFAIModManager.Windows\ADOFAIModManager.Windows.csproj
dotnet build .\ADOFAIModManager.Windows\ADOFAIModManager.Windows.csproj -c Debug -p:Platform=x64
```

## 단일 파일 게시

```powershell
.\scripts\publish.ps1
```

게시 결과는 `dist\win-x64\ADOFAIModManager.Windows.exe`와 검증용
`ADOFAIModManager.Windows.exe.sha256`입니다. EXE 파일 하나를
배포하면 첫 실행 시 현재 사용자의 `%LOCALAPPDATA%\Programs` 아래로 복사한 후
시작 메뉴, 프로그램 제거, ZIP `연결 프로그램`을 등록합니다. 개발 중에는
`--portable` 인수로 사용자 폴더에 복사하지 않고 현재 위치에서 실행할 수 있습니다.

## 현재 제약

- 공인 Authenticode 코드 서명을 사용하지 않으므로 SmartScreen, Smart App Control,
  Chrome 또는 Edge에서 경고나 차단이 발생할 수 있습니다.
- macOS에서는 WinUI 앱을 실행 검증할 수 없습니다. Windows 실기기에서 게시 산출물,
  시작 메뉴, 제거, 파일 연결과 Steam 기본 설치 경로를 반드시 확인해야 합니다.
- 자동 업데이트는 의도적으로 구현하지 않았습니다.
