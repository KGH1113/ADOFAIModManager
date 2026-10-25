# 추천 모드 소개 영상

이 폴더에 다음 이름의 MP4 파일을 넣으면 해당 온보딩 화면에 자동으로 표시됩니다.

- jrp.mp4 — JipperResourcePack
- chatter.mp4 — Keyboard Chatter Blocker
- tufLite.mp4 — TUFHelperLite
- tuf.mp4 — TUFHelper
- modSettings.mp4 — UMM 설치 후 Ctrl+F10 및 모드 개별 설정 가이드

`steam-properties.png`와 `steam-alpha.png`는 UMM 설치 전 알파 버전 안내에 사용하는
Steam 스크린샷입니다. 속성 열기 → alpha 선택 순서로 표시합니다.

파일이 없으면 영상 영역은 공란이며 네트워크 요청이나 재생 버튼이 생기지 않습니다.
H.264 MP4, 16:9 비율을 권장합니다. 파일 추가 후 앱을 다시 빌드하세요.
화면이 열리면 음소거 상태로 자동 재생하고 반복 재생합니다. 페이지가 숨겨지면
잠시 멈추고 다시 보이면 이어서 재생합니다. 재생 버튼과 하단 오버레이 바는 표시하지 않습니다.

현재 영상은 `/Users/kgh/Movies/adofai-mod-manager-showcase`의 원본 MOV에서
H.264 영상 스트림을 그대로 MP4로 옮겼습니다. 1920×1080 해상도와 60fps를
유지하며 자동재생에 필요 없는 오디오와 데이터 트랙은 제외했습니다.
원본 파일 대응은 `jrp.mov → jrp.mp4`, `kcb.mov → chatter.mp4`,
`tufhelperlite.mov → tufLite.mp4`, `tufhelper.mov → tuf.mp4`입니다.

`modSettings.mp4`는 `2026-10-04 20-58-26.mp4`에서 오디오를 제외하고 H.264로
변환했습니다. `PreviewVideo`의 `startTime`으로 앞 144F(2.400초)를 건너뛰며,
반복 재생할 때도 같은 위치부터 시작합니다. 원본의 60fps 재생 시각을 기준으로
키캡은 160F에 Ctrl, 실제 UMM 메뉴가 열리는 177F에 F10을 누르고 201F에
함께 놓습니다. 화면에 보이는 재생 구간에서는 각각 약 0.267초, 0.550초,
0.950초입니다. 키캡은 타이머 대신 영상 프레임 시각을 따라갑니다.

## TUF 소개 스크린샷

다음 이미지는 2026-10-24에 TUF 공개 웹사이트의 실제 화면을 Chromium으로
캡처한 WebP 이미지입니다. 로그인하지 않은 영어 UI를 사용했습니다.

- `tuf-home.webp` — <https://tuforums.com/>. 첫 방문 안내를 닫은 1440×900 홈페이지입니다.
- `tuf-levels.webp` — <https://tuforums.com/levels>. Likes 내림차순으로 정렬하고
  Q Slider를 활성화한 뒤 정렬 설정 패널을 접었습니다. 일반 난이도 P1–U20 및
  Qq–UQ4 전체 범위를 포함하며, 검색 입력창, 두 난이도 슬라이더, 다운로드 버튼이
  있는 실제 레벨 두 행을 함께 보여 주도록 원본 화면을 1160×660 영역으로 캡처했습니다.
- `tuf-leaderboard.webp` — <https://tuforums.com/leaderboard>. Ranked Score 순의
  1440×900 플레이어 랭킹이며, 소개 화면에는 `Oct 24, 2026` 기준임을 표시합니다.

사이트 UI, 로고, 레벨 및 플레이어 이미지는 TUF와 각 원저작자의 자료입니다.
이미지는 사이트 소개를 위한 정적 캡처이며 현재 랭킹이나 실시간 데이터가 아닙니다.

## 커뮤니티와 자료실 아이콘

- `adofai-community.png` — 사용자가 제공한 ADOFAI Community Server 아이콘 원본.
  중앙 별에서 두 공 쪽으로 퍼지는 그림자는 `tuf-intro.css`에서 적용합니다.
- `tuf-icon.png` — 사용자가 제공한 `logo.RzD9CQJq.png` 원본 TUF 아이콘.
