# LIVE 전투 코어 1차 프로토타입

Unity **6000.6.4f1**에서 `Assets/Scenes/BattlefieldPrototype.unity`를 열고 Play합니다.
기존 LIVE와 SampleScene은 수정하지 않습니다. 화면은 3행 × 6열, 파랑 A/빨강 B,
임시 캐릭터 도형과 얇은 HP 바만 유지합니다.

## 전투 규칙

- 1초 준비(Ready) 후 자동 Fighting. 한 진영만 남으면 Finished, Console에 `LIVE: A wins.` 또는 `LIVE: B wins.` 출력.
- 기본 스탯: HP 100, 공격력 20, 방어력 5, 초당 공격 1회, 사거리 1칸, 이동속도 초당 2칸.
- 스탯은 PrototypeCombatStats에 분리. Battlefield Prototype의 Test Stats로 양쪽 기본값 설정, 실행 중 각 유닛의 Stats도 Inspector에서 확인/변경 가능.
- 좌표는 0부터 시작하는 (row, column). 거리는 맨해튼 거리, 이동은 상하좌우 한 칸씩.
- 가장 가까운 살아 있는 적을 선택. 동률은 row → column → 생성 순서. 타겟 사망 즉시 재탐색.
- BFS로 공격 가능한 빈 칸까지 최단 경로 탐색. 이웃 후보는 타겟 거리 → row → column 순서로 검사.
- 이동 중 출발 칸 점유를 유지하고 도착 칸을 예약. 도착 시 논리 좌표 갱신. 실제 화면에서는 0.5초간 보간 이동하며 이동 중 공격 불가.
- 사거리에 진입하면 즉시 첫 공격, 이후 1 / AttackSpeed 간격. 공격 시 캐릭터 도형만 짧게 튕김.
- 피해 = AttackPower × 100 / (100 + Defense), 가장 가까운 정수로 반올림(.5 올림), 최소 1. 기본 피해 19.
- 체력 0이면 즉시 사망, 렌더러 숨김, 점유/예약 해제. 살아 있는 유닛만 탐색/행동에 참여.
- Finished에서는 이동/공격 중지. 진행 중이던 이동은 원래 논리 칸으로 복귀.
- 같은 틱의 행동은 생성 순서대로 처리. 따라서 동일 스탯의 기본 1:1 전투는 A가 선공 이점을 가짐.

## 반복 확인

Play 중 `Battlefield Prototype` 오브젝트의 `Prototype Combat Controller` 컴포넌트 메뉴에서
`Prototype/Reset battle`을 선택하면 시작 위치·HP·점유·타겟·쿨다운을 초기화하고 1초 후 재전투합니다.
`Prototype/Start battle`로 준비 시간을 건너뛸 수 있습니다.
유닛의 기존 `Prototype/Take 25 damage`, `Prototype/Restore health` 메뉴도 유지합니다.
사망한 유닛은 Restore health로 부활하지 않으며 전투 Reset으로만 복구합니다.

## 코드 책임

- BattlefieldPrototype: 보드/테스트 유닛 시각 구성과 전투 코어 연결
- PrototypeCombatStats: 확장 가능한 독립 스탯 구조
- PrototypeDamageCalculator: 피해 공식과 정수 변환
- PrototypeCombatGrid: 좌표, 점유, 예약, 경로 탐색
- PrototypeUnit: 개별 상태, 이동/공격/사망, HP 바 갱신
- PrototypeCombatController: 고정 시간 간격의 전투 진행, 타겟 선택, 승패, Reset

## 자동 검증

프로젝트를 사용하는 Unity 에디터를 닫고 실행합니다.

```text
Unity.exe -batchmode -nographics -projectPath "<저장소 절대 경로>" -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile "<로그 절대 경로>"
```

`-quit`은 넣지 않습니다. Play 모드에서 런타임과 동일한 Step을 호출해 전체 전투를 검증합니다.
보드/진영, 좌표/점유, 준비 후 시작, 피해/HP 바, 사망/승패, 자동 전투 완주와 반복 Reset,
이동 중 사망/Reset 예약 해제, 다수 유닛 우회/타겟 교체, 사거리 3/공격 쿨다운을 검사합니다.
성공 로그: `PROTOTYPE_SMOKE_CHECK_PASSED`.

## 범위와 한계

스킬·아이템·마나·시너지·라운드 등은 없습니다. 원거리 공격도 투사체 없이 즉시 피해를 줍니다.
시야/지형/대각선 이동은 없고 경로가 완전히 막히면 기다렸다가 재탐색합니다.
교착 상태의 제한 시간/무승부 처리는 아직 없습니다. 유닛 수가 많은 경우의 성능 최적화와
동시 공격의 공정성은 후속 과제입니다. 로컬 고정 틱 처리이며 네트워크 결정론을 보장하지 않습니다.
