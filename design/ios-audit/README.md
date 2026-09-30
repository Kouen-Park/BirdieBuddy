# BirdieBuddy iOS 디자인 검사 — 2026-10-01

`frontend-design-direction`, `design-system`, `make-interfaces-feel-better` 스킬의 시각 검사 기준을 SwiftUI 앱에 적용했습니다. 방향은 골프장에서 빠르게 읽는 기록 앱입니다. 기존 짙은 녹색, 밝은 종이색, 점수용 숫자 글꼴을 유지하고, 브랜드와 화면 제목을 구분했습니다.

점수는 코드 검사와 수정 내용에 따른 디자인 판단입니다. 수정 후 점수는 실제 화면 재검증 전의 잠정 평가이며, 인증이나 자동 접근성 검사 결과가 아닙니다.

| 검사 항목 | 수정 전 → 수정 후 / 10 | 확인 및 수정 위치 |
| --- | --- | --- |
| 색상 일관성 | 6 → 9 | 깃발·노랑 장식 색을 상태 글자색과 분리. 시스템 빨강·초록·주황을 공통 의미 색으로 교체. `ios/BirdieBuddyApp/BirdieTheme.swift:16`, `AccountView.swift:47` |
| 글자 위계 | 6 → 8 | 시스템 폰트로 보이던 상단 이름을 벡터 로고로 통일. 반복되던 브랜드 문구를 본문 제목에서 제거. 통계 라벨은 11pt에서 13pt로 확대. `BirdieTheme.swift:69`, `BirdieTheme.swift:94`, `BirdieTheme.swift:121` |
| 여백 리듬 | 7 → 8 | 기존 16/18/20pt 간격 유지. 로그인 바깥 여백을 16pt, 카드 안쪽을 20pt로 정리. `LoginView.swift:73` |
| 컴포넌트 일관성 | 6 → 8 | 로그인·복원·상단 바에서 `BirdieBrand` 사용. 라운드/연습 카드 주변의 기본 목록 구분선을 제거. `BirdieTheme.swift:82`, `RoundHistoryView.swift:61`, `PracticeView.swift:67` |
| 좁은 화면·큰 글씨 | 6 → 8 | 접근성 글씨 크기에서 라운드 점수와 카운터를 세로로 배치. 진행 중 라운드 배너를 스크롤 안으로 이동. `RoundHistoryView.swift:140`, `BirdieTheme.swift:152`, `CourseBrowserView.swift:25` |
| 다크 모드 | 해당 없음 | 앱이 명시적으로 밝은 테마를 사용합니다. 별도 다크 팔레트는 추가하지 않았습니다. `BirdieBuddyApp.swift:16` |
| 움직임 | 8 → 8 | 추가 장식 애니메이션 없음. 기본 iOS 화면 이동·입력 동작 유지. 새 의존성 없음. |
| 접근성 | 6 → 8 | 로고 VoiceOver 이름, 검색 필드 이름, 로그인 링크 44pt 영역, 상태 글자 대비, 비활성 카운터 구분을 개선. 조절 가능한 카운터의 VoiceOver 동작 유지. `BirdieTheme.swift:89`, `BirdieTheme.swift:207`, `LoginView.swift:66` |
| 정보 밀도 | 6 → 8 | 본문에 반복되던 브랜드 문구 제거. 연습 입력에 항상 보이는 Focus/Drill 라벨 추가. `BirdieTheme.swift:104`, `PracticeView.swift:19` |
| 마무리 상태 | 6 → 8 | 연습 기록이 없을 때 안내 화면 추가. 오류·경고 색상 통일. 한국어 문자열 추가. `PracticeView.swift:45`, `Localizable.xcstrings:4` |

## 구체적인 변경

| 이전 | 이후 |
| --- | --- |
| 앱 아이콘은 새, 웹 아이콘·로그인은 깃발, 상단은 일반 텍스트 | 같은 새/골프 티 심볼과 소문자 워드마크. iOS는 벡터 PDF, 웹은 윤곽선 SVG |
| 오류 글자 `#E56A4E`: 종이 배경 대비 3.16:1 | 오류 글자 `#AB3D31`: 5.98:1 |
| 연습 순번 `#E6B85C`: 종이 배경 대비 1.81:1 | 짙은 녹색 순번. 경고·GIR 그래프에는 `#895E1E`: 5.59:1 |
| 큰 글씨에서도 코스 설명과 라운드 점수가 가로로 경쟁 | 접근성 크기에서 점수를 설명 아래로 이동 |
| 큰 카운터 글자·긴 라벨·버튼이 한 줄에 배치 | 접근성 크기에서 라벨과 숫자/버튼을 두 줄로 분리 |
| 큰 글씨의 진행 중 배너가 화면 상단을 계속 차지 | 배너가 코스·라운드 목록과 함께 스크롤 |
| 입력값이 있는 연습 필드에서 입력 의미를 알기 어려움 | 값 위에 Focus/Drill 라벨 표시 |

글자 대비는 sRGB 상대 휘도 공식으로 `#FCFDFB` 배경에서 계산했습니다. 장식용 mint/sun/flag 팔레트는 유지했습니다. 읽는 글자와 실제 작업 버튼을 중심으로 [Apple의 UI 설계 권장사항](https://developer.apple.com/design/tips/) 및 [큰 글씨 평가 기준](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/larger-text-evaluation-criteria)을 참고했습니다.

## 범위와 검증

로그인, 회원가입, 비밀번호 재설정, 코스 목록/상세/등록, 라운드 목록/상세/입력, 통계, 연습, 계정 화면의 SwiftUI 구현과 기존 화면 자료를 검사했습니다. 이번 변경을 적용한 모든 화면의 시각 검증은 완료하지 못했습니다. `CaptureTests.swift`는 실제 SwiftUI 화면을 격리된 API/계정 fixture로 렌더링하는 수동 검토 도구입니다. 프로덕션 계정을 사용하거나 실제 기록을 변경하지 않습니다.

| 검증 | 결과 |
| --- | --- |
| 최종 iOS 앱 빌드 | Xcode 27.0, iOS Simulator 대상으로 `BUILD SUCCEEDED` |
| iOS 테스트 타깃 컴파일 | 성공. 새 큰 글씨 라운드 행 테스트를 포함한 테스트 소스가 컴파일됨 |
| XCTest 실행·화면 캡처 | 미완료. iPhone 17e / iOS 27.0 시뮬레이터에서 앱 시작을 계속 기다림. 재부팅과 디버거 없이 실행해도 동일했고, 앱 단독 실행도 대기하여 중단함. 테스트 케이스 결과 및 새 앱 화면 캡처는 없음 |
| 웹 브라우저 단위 테스트 | `npm run test:browser`: 25/25 통과 |
| 웹 모바일 fixture 테스트 | 첫 실행 4/5 통과 후 실패한 1개를 단독 재실행해 통과. 시뮬레이터 종료 후 머지 전 전체 재실행에서 5/5 통과 (9.2초) |
| 로고 | SVG/PDF의 렌더링을 확인하고 PNG 미리보기 검사 완료. 웹·iOS 자산은 같은 윤곽선 원본 사용 |
| 파일 검증 | `git diff --check` 통과. 문자열 카탈로그, manifest, 이미지 카탈로그 JSON 유효 |

임시 테스트 캡처 코드와 실행 설정은 원복했습니다. 남겨 둔 `CaptureTests.swift`는 XCTest 파일에 추가해 실행하는 검토용 자료이며, 현재 테스트 타깃에는 포함되지 않습니다. 기기 연결은 시뮬레이터 검사에 필요하지 않습니다. 실제 iPhone에서의 레이아웃, VoiceOver, 키보드 입력은 이번 작업에서 확인하지 않았습니다.

로고 원본과 재생성 방법은 [`../brand/README.md`](../brand/README.md), 미리보기는 [`../brand/preview.html`](../brand/preview.html)에 있습니다.
