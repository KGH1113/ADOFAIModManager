# Contributing

## 커밋 규칙

이 프로젝트는 [Conventional Commits](https://www.conventionalcommits.org/) 형식을 사용합니다.

```text
<type>(<scope>): <summary>
```

`scope`는 변경 범위를 명확히 할 때만 사용합니다. 예: `windows`, `macos`, `mods`, `installer`.

사용하는 `type`은 다음과 같습니다.

- `feat`: 사용자에게 보이는 기능 추가
- `fix`: 버그 수정
- `perf`: 성능 개선
- `refactor`: 동작을 바꾸지 않는 코드 구조 변경
- `test`: 테스트 추가 또는 수정
- `docs`: 문서 변경
- `build`: 빌드 시스템이나 의존성 변경
- `ci`: CI 설정 변경
- `chore`: 위 분류에 포함되지 않는 유지보수

### 작성 원칙

- `type`과 `scope`는 영문 소문자로 작성합니다.
- 요약은 명령형 영문으로 간결하게 작성하고 마침표를 붙이지 않습니다.
- 하나의 커밋에는 하나의 논리적 변경만 담습니다.
- 호환성을 깨는 변경은 type 뒤에 `!`를 붙이고 본문에 `BREAKING CHANGE:`를 적습니다.

예시:

```text
fix(windows): foreground redirected file activations
feat(mods): install zip files from Open with
docs: document Windows release process
refactor(installer): separate game discovery from installation
```
