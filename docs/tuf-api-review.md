# TUF API 조사

이 문서는 전환 전 조사 기록입니다. 이후 TUF 카탈로그·설치 시 다운로드·설치된 모드의 업데이트 확인을 구현했으며, 현재 사용 방법은 [README](../README.md)에 정리했습니다. 다운로드 주소의 종류와 버전 차이는 기록으로 남겨 두었습니다.

2026-09-30 기준. 앱 코드는 수정하지 않았으며, 공개 OpenAPI와 로그인 없는 읽기 요청으로 확인했습니다. 파일 다운로드 및 실제 설치는 실행하지 않았습니다.

## 결론

모드 목록·검색·상세·버전 조회를 TUF API로 통일하는 것은 가능합니다. 추천 모드의 정보와 다운로드 진입점도 TUF를 기준으로 바꿀 수 있습니다.

다만 현재 API와 등록 데이터만으로 모든 모드의 자동 설치를 보장할 수는 없습니다. 다운로드 API는 등록된 URL로 리다이렉트하며, 그 URL이 ZIP인 모드도 있고 GitHub 릴리스 페이지인 모드도 있습니다. OS별 설치 파일·의존성·체크섬 정보도 확인한 공개 응답에는 없습니다. 앱에서 이 부분을 보완하거나 TUF API와 등록 데이터를 확장해야 합니다.

TUF는 모드 정보를 조회하고 파일을 얻는 경로를 맡습니다. 내려받은 ZIP 검사, 게임 폴더에 설치, 백업, 활성화·삭제는 현재 로컬 설치 엔진이 계속 수행해야 합니다.

## 확인한 API

문서: [Swagger UI](https://api.tuforums.com/docs/), [OpenAPI JSON](https://api.tuforums.com/openapi.json)

| 기능 | API | 확인 내용 |
| --- | --- | --- |
| 목록·검색 | `GET /v2/mods` | 실제 공개 요청 성공. `{ total, mods, offset, limit, hasMore }` 응답 |
| 태그 목록 | `GET /v2/mods/tags` | 실제 공개 요청 성공. Gameplay, Quality Of Life, Editor, Jokes, Overlay, Dependency 태그 |
| 최신 상세 | `GET /v2/mods/{slug}` | 실제 공개 요청 성공. `{ mod }` 안에 `versions`, `latestVersion` 포함 |
| 특정 버전 | `GET /v2/mods/{slug}/{version}` | 공개 명세에 정의됨. 특정 버전 요청 자체는 이번에 실행하지 않음 |
| 최신 다운로드 | `GET /v2/mods/{slug}/download` | 공개 명세상 HTTP 302 리다이렉트. 이번에는 호출하지 않음 |
| 특정 버전 다운로드 | `GET /v2/mods/{slug}/{version}/download` | 공개 명세상 HTTP 302 리다이렉트. 이번에는 호출하지 않음 |

목록·상세·태그는 인증 없이 조회했습니다. 공개 명세에서도 위 조회·다운로드 작업에는 인증 요구가 선언되어 있지 않습니다. 좋아요 변경·신고 등에는 bearer 인증이 선언되어 있습니다. 모드 탐색과 다운로드를 위해 앱에 로그인부터 추가할 필요는 없어 보입니다.

### 검색과 페이지 처리

- `q=JALib`로 검색하면 전체 159개 중 7개가 반환되었습니다. 이름이 JALib인 항목 외에도 관련 모드가 포함되므로 이름 일치 검색으로 가정하면 안 됩니다.
- `limit=200` 요청은 실제 응답에서 `limit=100`으로 제한되었습니다.
- `offset=100&limit=100`으로 나머지 59개를 받아 `hasMore=false`까지 확인했습니다.
- `search=JALib`, `query=JALib`는 이 확인에서는 검색에 적용되지 않았습니다.
- OpenAPI에는 목록의 query parameter와 응답 스키마가 충분히 정의되어 있지 않습니다. 추가 정렬·태그 필터의 정확한 계약은 별도 확인이 필요합니다.

확인 요청: [검색 예시](https://api.tuforums.com/v2/mods?q=JALib&limit=5), [목록 첫 페이지](https://api.tuforums.com/v2/mods?limit=100), [목록 다음 페이지](https://api.tuforums.com/v2/mods?limit=100&offset=100), [태그](https://api.tuforums.com/v2/mods/tags)

## 데이터 구조와 자동 설치 제한

목록에서 확인한 필드는 `id`, `slug`, `name`, `creatorUsername`, `version`, `description`, `downloadUrl`, `imageUrl`, `projectUrl`, `deprecatedAfter`, `sourceUploadedAt`, `isPinned`, `likes`, `downloadCount`, `tags`, `assignees`, `postedBy`입니다.

상세에는 버전 목록이 추가되고 각 버전은 `id`, `version`, `downloadUrl`, `notes`, `releasedAt`, `source`를 제공합니다. `source`에 `github` 및 `hosted`가 실제로 존재합니다. [JRP 상세](https://api.tuforums.com/v2/mods/jrp), [TUFHelper 상세](https://api.tuforums.com/v2/mods/tufhelper), [PACL2 상세](https://api.tuforums.com/v2/mods/pacl2)

확인한 JRP·TUFHelper·PACL2 상세에는 UMM의 `Info.json`에 해당하는 설치 ID, 구조화된 의존성, OS별 파일 목록, 파일 체크섬 필드가 없습니다. 태그의 Dependency는 라이브러리 모드를 분류하는 태그이며, 의존성 그래프를 대체하지 않습니다. 공개 명세가 간략하므로 API 전체에서 이 기능이 절대로 없다고 단정하지는 않습니다.

GitHub 릴리스 페이지는 다운로드 API를 거쳐도 ZIP으로 바뀐다는 보장이 없습니다. 현재 앱의 다운로드 함수는 ZIP 시그니처를 검사하므로 HTML 페이지가 반환되면 설치를 거부합니다. TUF를 유일한 정보 공급원으로 만들려면 다음 중 하나가 필요합니다.

1. TUF에 직접 다운로드 가능한 ZIP 또는 TUF CDN 파일을 등록하고, OS별 파일 정보를 제공하기.
2. TUF에서 받은 GitHub 주소를 앱이 해석해 릴리스 asset을 선택하기. 이 경우 목록·메타데이터는 TUF로 통일되지만 다운로드 파일 선택을 위해 GitHub 조회가 추가됩니다.
3. 자동 설치가 불가능한 항목은 원본 페이지를 열어 사용자가 파일을 선택하게 하기.

TUF 다운로드 URL로 모든 요청을 시작하더라도 리다이렉트의 최종 파일 호스트는 GitHub 등 외부 서비스일 수 있습니다. 파일 전송까지 TUF 도메인으로 통일하려면 TUF 측 파일 호스팅 또는 프록시 기능이 필요합니다.

## 현재 추천 모드와의 대응

현재 앱의 추천 모드 10개는 모두 공개 목록에서 찾았습니다. 앱에 없는 Jipper Key Viewer도 등록되어 있습니다. 다음은 조사 당시의 목록 응답입니다.

| 앱의 모드 | TUF slug | TUF 등록 버전 | 확인한 다운로드 형태 |
| --- | --- | --- | --- |
| JALib | `jalib` | `1.0 Beta45` | GitHub 릴리스 목록 페이지 |
| JipperResourcePack | `jrp` | `1.5.2` | GitHub 릴리스 목록 페이지 |
| KeyboardChatterBlocker | `keyboardchatterblocker` | `0.1.2` | 직접 ZIP |
| TUFHelperLite | `tufhelperlite` | `v0.1.8` | GitHub 버전 페이지 |
| TUFHelper | `tufhelper` | `3.1.1` | GitHub 버전 페이지 |
| EnhancedCountdown | `enhanced-countdown` | `0.2.0` | 직접 ZIP |
| BetterCalibration | `bettercalibration` | `1.2.2` | JALib modApplicator URL. 파일 형식 미검증 |
| YouTubeStream | `youtubestream` | `1.0.5` | TUF CDN URL. 파일 형식 미검증 |
| EnhancedEffectRemover | `enhancedeffectremover` | `1.7.0` | 직접 ZIP |
| PACL2 | `pacl2` | `2.5.301` | TUF CDN URL. 파일 형식 미검증 |
| Jipper Key Viewer | `jipperkeyviewer` | `1.7.2` | 직접 ZIP |

앱은 TUFHelperLite `0.1.9`, EnhancedCountdown `0.2.3`, PACL2 `2.5.400`을 고정하고 있습니다. TUF 등록 최신 버전으로 단순 교체하면 현재 앱보다 낮은 버전이 선택될 수 있습니다. JRP `1.5.2`와 앱의 `1.5.2.0`처럼 버전 표기도 다르므로 문자열만으로 동일 여부를 판단하면 안 됩니다. 버전 선택과 실제 ZIP의 `Info.json` 검사가 필요합니다.

현재 TUFHelper는 앱에서 macOS·Windows·Linux별 다운로드 주소를 따로 가지고 있지만, TUF의 확인한 상세에는 단일 GitHub 버전 페이지 URL만 있습니다. 이 부분은 OS별 asset 선택 또는 API 확장이 필요합니다.

## 프로젝트 변경 방향

1. `src-tauri/src/downloads.rs`의 기존 `bot.adofai.gg` 목록을 TUF로 교체하고, 검색·페이지·상세·버전 조회를 담당하는 공통 클라이언트를 만듭니다.
2. `frontend/src/schemas/catalog/`와 카탈로그 모델을 TUF 응답에 맞추고 페이지 처리, 태그, 버전 선택을 연결합니다. 현재 배열 응답 스키마에 URL만 바꾸면 동작하지 않습니다.
3. 온보딩 추천 설정은 TUF slug와 앱 설치 ID를 연결하도록 바꿉니다. 고정 GitHub URL과 기존 카탈로그 ID를 제거하고 추천 모드도 공통 조회·다운로드 흐름을 사용합니다.
4. 다운로드 명령은 임의 URL 대신 slug·버전을 받도록 정리하고 TUF 다운로드 API를 통해 파일을 얻습니다. ZIP이 아닌 URL의 처리 방침과 OS별 asset 선택은 먼저 확정해야 합니다.
5. PACL2·YouTubeStream의 번들 ZIP은 TUF 다운로드를 검증한 뒤 제거합니다. PACL2는 현재 번들 버전과 TUF 등록 버전 차이도 해결해야 합니다.
6. JRP·BetterCalibration·PACL2에 대한 JALib 의존성 및 온보딩 프리셋의 버전 제한은 유지합니다. TUF에서 구조화된 정보를 제공할 때 이를 대체합니다.
7. ZIP 검사·임시 파일 토큰·취소·백업·로컬 설치 엔진은 유지하고, 새로운 응답과 URL 처리에 맞는 테스트를 추가합니다.

권장 순서는 **TUF로 탐색·메타데이터 통일 → 추천 모드 공통 경로 연결 → 직접 ZIP·OS별 다운로드 데이터 보완 → 번들 ZIP 제거**입니다. API 조회 구조는 충분하지만, 완전 자동 설치 통일에는 다운로드 데이터 정리가 함께 필요합니다.
