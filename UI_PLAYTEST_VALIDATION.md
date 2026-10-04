# Playtest UI 검증

2026-10-05 · Unity6000.6.4f1 · 기준1359555 · codex/battlefield-prototype.
게임 기능/캐릭터 추가 없이 기존 작업 트리의 uGUI 구현을 완성했다.15명/30스킬 유지.

## UI 기술과 구조

프로젝트에 이미 설치된 uGUI(Canvas,Text,Button,GraphicRaycaster)+Input System UI module 사용. 월드 전장 카메라와 화면 좌표를 연결하기 쉽고 기존 패키지로 동작하며 향후 카드/인스펙터를 prefab으로 분리할 수 있다. 폰트는 기존과 같이 설치된 한국어 OS font를 사용하고 외부 에셋/이미지를 다운로드하지 않았다.
1920×1080 기준 RectTransform 레이아웃을 화면에 균등 축소. 상단 Round/Phase/Credits/Mastery EXP/Timer와 작은 Reset,중앙 대형3×6,바로 아래8칸 벤치,하단5개 상점 카드와 액션 패널,선택 시에만 왼쪽 상세 패널. 인스펙터는 이름/성급/Cost/HP/AP/AMP/DEF/AS/Range를 표시한다.
전장에는 기존 placeholder와 HP/Shield/Gauge 표시 유지,이름은 Canvas Text. 셀 크기/Position/논리 그리드/거리 계산 불변. 보드 확대는 viewport/orthographic camera 표시만 조정. 진영 타일 색은 채도를 낮췄다.
기존 OnGUI 구현 제거. 중복 UI/fallback은 없다. Seed/상대 종류/긴 개발 설명은 기본 화면에서 제외. Result에는 짧은 기존 결과 메시지 표시.
보드 밖 UI가 꺼졌을 때 잔상이 남지 않도록 cullingMask0의 전체 화면 배경 카메라를 보드 카메라보다 먼저 렌더한다. 이 문제는 실제 플레이어 캡처에서 발견·수정하고 별도 assertion을 추가했다.

## 변경 파일

- Assets/Prototype/PrototypeRunHud.cs: Canvas presenter,레이아웃/표시/선택/버튼,전체 배경 clear.
- Assets/Prototype/BattlefieldPrototype.cs: 타일 tint와 orthographic 표시 크기만 변경.
- Assets/Prototype/PrototypeVisualProbe.cs: 기존 개발 플레이어 캡처 도구의 해상도/선택 예시/자동 HUD 갱신 검사/Canvas 숨김 보완.
- Assets/Prototype/Editor/PrototypeUiSmokeCheck.cs 및 .meta: 신규 UI 검증.
- Assets/Prototype/Editor/PrototypeSmokeCheck.cs: 신규 suite 등록1줄.
- PROTOTYPE.md,이 문서,UI_VALIDATION/*.png: 사용법 및 검증 결과.

컨트롤러/모델/경제/숙련도/상점/공유 풀/벤치/합성/배치/Ready/라운드/전투/AI/스킬/피해 계산/Character Definition/WebRoster 원본은 수정하지 않았다. 기존 UI와 같은 public 읽기값으로 버튼 상태를 표시하고 Buy/Reroll/Invest/Move/Sell/Ready/ResetRun 공개 API만 호출한다. 실제 규칙과 최종 유효성 검사는 기존 모델이 담당한다. LIVE.unity/SampleScene.unity/BattlefieldPrototype.unity도 불변. 에디터/빌드가 자동 변경한 프로젝트 설정은 백업 후 변경 대상에서 제외.

## 검증

Unity 전체 batch Play Mode: exit0,컴파일 오류/테스트 예외 없음.
기존 **1,182,389 assertions PASS** (경제/상점/풀/합성/라운드/U01~U09/AI/BFS/점유/사거리/WebRoster/15명 스킬).
신규 **140 assertions PASS**,총1,182,529. 기존 gameplay assertion 삭제/완화 없음.

신규 UI 검사:
- 1920×1080/1600×900/1280×720 주요 영역 경계·비겹침,5상점/8벤치,최소 클릭 크기.
- 실제 DisplayName/Credits/Mastery EXP/Timer,선택 상세/outline 갱신.
- GraphicRaycaster가 화면 상점 좌표에서 정확한 카드에 도달.
- uGUI pointer click event로 구매/벤치 선택/보드 배치/보드 이동/빈 벤치 이동/판매/Reroll/숙련도 투자/Ready/Reset.
- Combat 중 부적절한 버튼 차단 및 실제 disabled pointer 무효,Credits 부족 비활성화.
- Prep→Combat→Result→다음 Prep 및 버튼 회복.
- 전체 clear 카메라가 partial viewport보다 먼저 렌더.

개발 Windows 플레이어 빌드 성공. 기존 VisualProbe 실행 exit0/VISUAL_PROBE_PASSED,자동 LateUpdate의 Prep/Combat/Result 상태 갱신과 선택 패널 해제를 캡처 직전에 검사. 생성 PNG는 실제 플레이어 렌더링이며 가공/합성하지 않았다.
최종 세 해상도의 Prep 및 Combat/Result를 눈으로 검수했다. Mastery 비용의 두 번째 줄 잘림을 한 줄로 수정했고,전투 전환 후 선택 패널 잔상 제거를 확인했다.

## 캡처

- [1920×1080 Prep](UI_VALIDATION/prep-1920x1080.png)
- [1600×900 Prep](UI_VALIDATION/prep-1600x900.png)
- [1280×720 Prep](UI_VALIDATION/prep-1280x720.png)
- [1280×720 Combat](UI_VALIDATION/combat-1280x720.png)
- [1280×720 Result](UI_VALIDATION/result-1280x720.png)
- [기존 능력 표시 fixture](UI_VALIDATION/abilities-1280x720.png)

## 확인 방법

Assets/Scenes/BattlefieldPrototype.unity를 열고 Play. 상점 카드 구매→벤치 선택→좌측 보드 클릭으로 배치,다른 빈 칸/벤치로 이동,판매/투자/Ready/Reset을 확인한다.
자동 회귀: 기존 `LIVE.Prototype.Editor.PrototypeSmokeCheck.Run`을 batchmode/nographics로 실행(-quit 생략).
시각 검사: `PrototypePlayerBuild.BuildVisualCheck`로 개발 빌드 후 `LIVEPrototype.exe -screen-fullscreen 0 -live-visual-check <output> -logFile <log>` 실행. 일반 Play에서는 VisualProbe가 실행되지 않는다.

## 임시 요소와 후속

단색 패널/도형 캐릭터/문자 * 성급/영문 조작 라벨/OS 한국어 폰트는 플레이테스트용이다.16:9 데스크톱만 시각 검증했으며 모바일/세로/울트라와이드 최적화는 범위 밖.
최종 UI에서 아트·아이콘·폰트 에셋·현지화·접근성/키보드 탐색·카드 prefab/style 분리를 개선할 수 있다. 현재 배치는 코드로 생성하며 매 프레임 표시를 갱신한다. 대규모 유닛 목록에 대한 UI pooling/변경 기반 갱신은 후속이다. 아이템/시너지/스킬 상세 tooltip은 추가하지 않았다.
