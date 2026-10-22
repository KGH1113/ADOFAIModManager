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
변환했습니다. 원본의 60fps 재생 시각을 유지하며 영상 아래 키캡은 126F(2.100초)에
Ctrl, 143F(약 2.383초)에 F10을 누르고 167F에 함께 놓습니다.
