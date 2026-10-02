# LIVE 싱글플레이 통합 프로토타입

Unity **6000.6.4f1**, `Assets/Scenes/BattlefieldPrototype.unity`에서 Play합니다.
기존 `LIVE.unity`, `SampleScene.unity`는 수정하지 않습니다.

## 직접 확인 순서

1. Round 1 Prep에서 Credits 5, 랜덤 1코스트 1성 무료 유닛의 중앙 배치, 상점 5칸을 확인합니다.
2. Shop 카드를 클릭하면 구매한 유닛이 Bench로 들어옵니다.
3. Bench 유닛 클릭 → 왼쪽 파랑 3×3의 빈 칸 클릭으로 배치합니다.
4. 같은 방식으로 Board → Bench, Board → Board, Bench → Bench 이동이 가능합니다.
   목적지는 비어 있어야 합니다. 점유된 칸을 누르면 그 유닛이 새로 선택됩니다. 교환은 하지 않습니다.
5. 선택 후 Sell, Reroll(2 Credits), Mastery 투자(2 Credits / 2 EXP)를 확인합니다.
6. Ready를 누르거나 30초를 기다리면 현재 배치의 복사본으로 자동 전투합니다.
7. 승/패/무 결과, 수입, 자연 숙련도 EXP를 확인합니다. Result 2초 → Transition 1초 → 다음 Prep입니다.
8. 최소 Round 5까지 반복할 수 있으며, 이후에는 Round 5 적 테이블을 반복 사용합니다.
9. Reset Run은 같은 seed로 라운드·경제·상점·유닛·전투를 처음부터 다시 시작합니다.

전투/결과/전환 중 구매·판매·배치·투자·Reroll은 UI와 모델 양쪽에서 차단됩니다.
전투에 유닛을 배치하지 않아도 Ready할 수 있으며, 해당 라운드는 패배로 처리됩니다.
UI의 U01~U09는 Test Unit 01~09의 축약 표기이고, * / ** / ***는 별 등급입니다.
영문 임시 UI를 사용해 추가 폰트 에셋 의존성을 피했습니다.

## 확정 요청 규칙의 구현

- 경제: 시작 5, 라운드 기본 수입 5 + min(3, floor(보상 지급 전 Credits / 10)). 승패와 관계없이 동일 수입.
- 자연 숙련도: 매 라운드 결과 처리 시 +4 EXP. 투자: 2 Credits → +2 EXP.
- 숙련도 구간은 현재 레벨 기준: Lv1→2부터 Lv6→7까지 각 4, Lv7→8부터 Lv13→14까지 각 5,
  Lv14→15부터 Lv19→20까지 각 6 EXP. Lv7 누적 24, Lv14 누적 59, Lv20 누적 95 EXP.
  최대 레벨에서는 EXP를 더 쌓거나 투자 비용을 소비하지 않습니다. 전투 스탯 강화 효과는 없습니다.
- 소유 유닛: Stable ID를 가리키는 별도 Instance ID, 별 등급, Bench/Board 위치를 가집니다.
- 3개 자동 합성: 동일 Stable ID + 별 등급. 배치된 개체를 우선 보존하고, 그다음 낮은 Instance ID를 보존합니다.
  연쇄 합성 가능, 최대 3성. HP/공격력만 1 / 1.8 / 3.2 배율로 증가합니다.
- 공유 풀: 1/2/3코스트 종류마다 18/15/12개. 상점 등장 시 예약 차감, 구매 시 예약을 소유로 이전합니다.
  합성은 원본 개체 수를 보존합니다. 판매 시 1/3/9개의 원본을 반환합니다.
- 판매 가격: 1성은 Cost, 합성 유닛은 ceil(Cost × 원본 개체 수 / 2).
  1코 1/2/5, 2코 2/3/9, 3코 3/5/14입니다.
- Bench 8칸, Shop 5칸. Bench가 가득 차면 합성이 가능하더라도 구매를 먼저 거절합니다.
- Prep 배치는 왼쪽 3×3의 빈 칸만 허용합니다. 숙련도에 따른 배치 수 제한은 없습니다.
- Round마다 우측 3×3의 테스트 적 팀을 사용합니다. 적은 AI 플레이어가 아닌 합성 테스트 상대이므로 공유 풀을 소비하지 않습니다.
- Round 1 무료 유닛은 실제 공유 풀 1개를 소비하며 시작 Credits는 유지합니다.
- Prep 30초, Combat 최대 60초. 전멸은 즉시 결과 처리, 제한시간은 무승부 처리합니다.
- Combat 사망/피해는 소유 상태에 반영하지 않습니다. 다음 Prep에서 모든 배치가 원래 위치·최대 HP로 복원됩니다.
- Prep 시작마다 무료 상점 갱신 1회. 이전 예약을 반환하며, 구매/준비 시작 시 남은 상점 예약은 다음 갱신까지 유지합니다.

### 상점 확률

| Mastery Lv | 1코 | 2코 | 3코 |
|---|---:|---:|---:|
| 1–3 | 80 | 20 | 0 |
| 4–6 | 70 | 30 | 0 |
| 7–9 | 57 | 38 | 5 |
| 10–12 | 43 | 45 | 12 |
| 13–15 | 30 | 47 | 23 |
| 16–18 | 20 | 43 | 37 |
| 19 | 14 | 38 | 48 |
| 20 | 10 | 30 | 60 |

비용별 확률로 선택한 뒤 해당 비용의 남은 원본 개체 수에 비례해 종류를 선택합니다.
고갈된 비용은 제외하고 남은 허용 비용의 가중치를 다시 정규화합니다.
모든 허용 비용이 고갈되면 슬롯은 비워 둡니다. 확률이 0인 상위 비용을 임의로 공급하지 않습니다.
랜덤 지급과 상점은 중앙 xorshift seed 스트림을 사용하며 Reset Run에서 초기화됩니다.

## 데이터 편집

`Assets/Prototype/Resources/PrototypeGameData.json`이 테스트 정의/설정의 원본입니다.

- Units: Stable ID, 이름, Cost, 독립 전투 Stats. Test Unit 01~09, 비용별 3종.
- Rules: 경제, 제한시간, 상점 확률, 풀 수량, 별 배율.
- EnemyRounds: Round별 OpponentType 및 유닛 ID/별 등급/행/열.

새 캐릭터는 Units에 고유 ID로 추가합니다. ID를 변경하면 EnemyRounds 참조도 함께 바꿔야 합니다.
수치는 밸런스 확정값이 아닙니다. OpponentType은 Player/Wildlife로 확장 가능하나 실제 야생동물 규칙은 없습니다.

## 코드 구조

- PrototypeGameData: JSON 정의·검증·조회·별 등급 전투 스탯 생성.
- PrototypeEconomyMastery: 경제/판매 공식, 숙련도, 중앙 RNG.
- PrototypePoolShop: 독립 공유 풀과 예약 기반 상점.
- PrototypePlayerRoster: 영구 소유·배치·벤치·합성.
- PrototypeRunModel: Phase/라운드/타이머/결과와 구매·판매·배치·투자 명령 경계.
- PrototypeRunController: 모델과 기존 전투 코어를 연결하는 씬 어댑터. 고정 틱을 한 곳에서 전달.
- PrototypeRunHud: 임시 반응형 IMGUI 표시와 클릭 명령 전달. 경제/전투 규칙을 변경하지 않음.
- BattlefieldPrototype: 기존 3×6 보드, 임시 캐릭터, HP 바, 카메라/인스턴스 구성.
- PrototypeCombatController / Grid / Stats / DamageCalculator / Unit: 기존 전투 코어 유지.

경제·숙련도·상점·풀·소유·RunModel은 GameObject 생성 없이 검사 가능합니다.
정의 로딩과 화면/전투 어댑터만 Unity 씬에 의존합니다. 저장/네트워크 프로토콜은 아직 없습니다.

## 전투 규칙 유지

맨해튼 거리, 동률은 row → column → 생성 순서, 상하좌우 한 칸 이동,
BFS 우회 경로, 출발 칸 점유 + 도착 칸 예약, 이동 중 공격 금지,
공격 간격 1 / AttackSpeed, 피해 AttackPower × 100 / (100 + max(0, Defense − DefensePenetration)), 기본 공격 치명타 2배, 정수 반올림(.5 올림), 최소 1.
죽은 개체는 즉시 점유/예약 해제 및 렌더러 숨김. 타겟 재선택. 종료 후 이동/공격 정지.
경로가 없으면 제자리에서 재탐색하며 전투는 60초 안에 반드시 종료합니다.

전투 단독 디버그용 ContextMenu는 남아 있지만 통합 루프 반복 테스트에는 **Reset Run**을 사용하십시오.
유닛별 Take 25 damage / Restore health도 디버그 목적으로 유지하며 사망자는 Reset으로만 부활합니다.

## 자동 검사

프로젝트를 사용하는 Unity 에디터가 종료된 상태에서 실행합니다. `-quit`을 넣지 않습니다.

```text
Unity.exe -batchmode -nographics -projectPath "<저장소 절대 경로>" -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile "<로그 절대 경로>"
```

기존 전투 회귀 검사를 유지하고 PrototypeRunSmokeCheck로 경제/이자/숙련도 경계/확률/풀 보존/
Reroll/구매/벤치 가득 참/판매/연쇄 합성/배치/적 팀/3v3/5라운드/60초 무승부를 검사합니다.
성공 로그: `PROTOTYPE_SMOKE_CHECK_PASSED`, `RUN_SMOKE_CHECK_PASSED`.
반복문 안의 불변식까지 포함하므로 assertion 개수는 테스트 수와 다릅니다.

개발용 Windows 플레이어 빌드 및 스크린샷 검수 도구도 있습니다.

```text
Unity.exe -batchmode -nographics -quit -projectPath "<저장소 절대 경로>" -executeMethod LIVE.Prototype.Editor.PrototypePlayerBuild.BuildVisualCheck -logFile "<빌드 로그 절대 경로>"
LIVEPrototype.exe -screen-fullscreen 0 -live-visual-check "<이미지 저장 폴더>" -logFile "<플레이어 로그 절대 경로>"
```

빌드 결과는 저장소의 상위 폴더 `PrototypePlayer`에 생성됩니다.
VisualProbe는 명시적 실행 인자가 있는 개발 빌드에서만 실행되며 일반 Play에는 개입하지 않습니다.
1280×720, 800×600, 세로 화면에서 Prep, Combat, Result 화면을 저장합니다.

## 범위 및 기술 부채

- 실제 캐릭터/아트, 아이템, 시너지, 마나, 지역/날씨, 드론, PvP/네트워크/로그인은 없습니다.
- 결과 후 탈락/런 종료/승리 조건은 정의하지 않았습니다. Round 5 이후 마지막 적 테이블을 재사용합니다.
- 적 테이블·무료 라운드 상점 갱신·동률 행동 순서 등은 프로토타입 검증용이며 최종 게임 규칙이 아닙니다.
- 동일 틱은 생성 순서대로 처리하므로 A에게 선공 이점이 있습니다. 고정 틱이지만 서버/플랫폼 간 완전 결정론은 아닙니다.
- IMGUI는 임시 UI입니다. 좁은 화면에서는 전체 UI를 축소해 겹침을 방지하지만 터치 목표 크기 개선이 필요합니다.
- Prep 데이터 변경마다 소규모 표시 인스턴스를 재생성합니다. 대규모 확장 시 뷰 재사용/이벤트 기반 갱신을 권장합니다.
- BFS는 18칸으로 제한되며 경로가 막히면 대기합니다. 다수 유닛의 이동 공정성·경로 할당 최적화는 후속 작업입니다.
- 런 저장/불러오기, 리플레이, 네트워크 명령 검증, 로컬라이징은 아직 구현하지 않았습니다.

## 캐릭터·스킬 확장

- **Unit Definition**: `PrototypeGameData.json`의 스탯, Archetype, Abilities. 역할은 분류 정보이며 자동 스탯 보정이 없습니다.
- **Owned Unit**: 기존 `PrototypePlayerRoster`의 Stable ID/Instance ID/별/배치. 전투 사망으로 삭제되지 않습니다.
- **Combat Unit**: 라운드마다 생성하는 `PrototypeUnit`과 `PrototypeUnitAbilities`에 HP, Shield, Gauge, 타겟, 행동, 버프, 스킬 런타임, 통계를 둡니다. 능력 정의도 깊은 복사하여 원본에 쓰지 않습니다.
- `PrototypeSkillDefinition`은 트리거/타겟/효과 데이터와 런타임 카운터를 정의합니다. 캐릭터 이름 분기는 없습니다.
- `PrototypeDamageCalculator`가 관통/방어/치명타/스킬 계수/반올림을 통합합니다.
- `PrototypeCombatEvents`의 10종 이벤트를 `PrototypeUnitView`가 관찰합니다. 연출 완료는 전투 규칙의 조건이 아닙니다.

게이지 기본값은 최대 100, 기본 공격 성공 +20, 피격 +10입니다. 캐릭터별로 변경할 수 있습니다.
보호막만 맞은 경우도 피격 게이지를 얻으며, 치명적인 피해로 죽으면 얻지 않습니다.
이동 완료 후 발동 가능 조건을 평가하고, GaugeFull → 나머지 정의 순서로 스킬을 우선합니다.
모든 스킬 사용 시작 시 게이지를 0으로 소비합니다. 기본 캐스팅은 0.15초이며 이동·기본 공격을 중단합니다.
사망은 캐스팅을 취소합니다. 시전 중 타겟이 죽으면 타겟을 다시 선택하고, 사거리 밖이면 효과 없이 끝납니다.
기본 공격 쿨다운은 캐스팅 동안 멈춥니다. 공격속도 변경은 다음 공격의 쿨다운부터 반영됩니다.

스킬 원시값은 `BaseDamage + AttackPower × AttackRatio + SkillAmplification × SkillRatio`입니다.
피해 효과는 동일한 방어·관통 공식을 거칩니다. 이번 스킬에는 치명타를 적용하지 않습니다.
별 등급은 HP/AP만 1/1.8/3.2배이며, 스킬 계수와 SkillAmplification은 그대로입니다.
LowestHealthAlly는 자신을 포함하여 **현재 HP 비율**이 가장 낮은 아군을 선택하고, 동률은 생성 순서입니다.
아군/자신 효과는 거리 제한이 없습니다. 적 타겟은 스킬 Range(0이면 AttackRange)를 사용합니다.

| 유닛 | 역할 | 임시 패턴 |
|---|---|---|
| 01 | Fighter | 게이지 충전 후 단일 피해 15 + AP×1.8 |
| 02 | Fighter | HP 40% 이하에서 전투당 한 번 Shield 70 |
| 03 | Fighter | 기본 공격 3회마다 최대 2칸 Dash + 피해 10 + AP×1.4 |
| 04 | RangedAttack | 사거리 3, 공격속도 1.35, 3회마다 다음 기본 공격 1.8배 |
| 05 | RangedAttack | 치명타 45%, 처치 후 3초간 공격속도 +0.5 |
| 06 | RangedSkill | 게이지 충전 후 피해 15 + AP×0.3 + 증폭×1.4 |
| 07 | RangedSkill | 게이지 충전 후 최저 HP 비율 아군 회복 15 + 증폭×1.1 |
| 08 | Fighter | 전투 시작 Shield 50 + 4초간 방어력 +15 |
| 09 | RangedSkill | 게이지 충전 후 혼합 계수 피해 10 + AP×0.8 + 증폭×0.8 |

Shield는 합산하고 HP보다 먼저 흡수하며 MaxHealth를 바꾸지 않습니다. 전투 종료/Reset에서 사라집니다.
HP 초록 바 위의 하늘색 얇은 바는 Shield, 아래 금색 얇은 바는 Gauge입니다.
Shield 표시는 MaxHealth 대비 비율(최대 한 바)입니다. 스킬은 흰 flash/크기 변화, 회복은 초록 flash입니다.
Dash는 점유/예약을 지키는 인접 이동이고 가까우면 짧은 측면 경로를 사용합니다. 자리가 없으면 이동하지 않습니다.
논리 이동은 효과 실행 순간 처리하고 도형만 짧게 보간합니다.

전투 종료 Console의 `LIVE combat statistics`에서 DamageDealt/Taken, HealingDone, ShieldGranted,
Kills, BasicAttackCount, SkillCastCount를 확인합니다. 피해 통계는 **실제로 소모한 Shield+HP**이며 초과 피해는 제외합니다.
회복 통계도 실제 복구량입니다. SkillCastCount는 시작 횟수라서 사망으로 취소된 시전도 포함합니다.
마지막 적 처치 즉시 종료되므로 그 뒤 OnKill 버프는 발동하지 않습니다.
치명타 RNG는 라운드별 seed의 별도 스트림이므로 상점 랜덤 결과를 변경하지 않습니다.

### 신규 자동 검사와 수동 확인

기존 SmokeCheck 실행에 `PrototypeAbilitySmokeCheck`가 추가됩니다.
성공 표식은 기존 두 표식과 `ABILITY_SMOKE_CHECK_PASSED`입니다.
신규 검사는 스탯/별/공격 주기/사거리/방어·관통/0·100% 치명타/게이지/5개 트리거/
5개 효과/보호막/회복 상한/버프 만료/시전 중 사망/재탐색/이벤트/통계/seed 반복 3v3을 확인합니다.

Unity에서 기존 씬 Play → 상점 구매 → 서로 다른 U번호를 배치 → Ready로 확인합니다.
U02의 HP가 줄면 보호막이 생기는지, U04~06이 원거리에서 공격하는지, 금색 게이지가 차면 흰 flash와 함께
스킬을 쓰는지 확인합니다. U07의 회복과 U08의 시작 보호막, 결과 Console 통계도 확인합니다.
Reset Run 후 기존 구매/판매/합성/배치/숙련도/라운드 흐름을 다시 사용할 수 있습니다.

수치는 모두 검증용입니다. AOE, 투사체, 버프 해제/중첩 정책 세분화, 스킬별 성장, CC, 실제 애니메이션은 없습니다.
버프는 현재 합산 방식이며 매 틱 유효 스탯을 복사 계산합니다. 소규모 18칸 검증용이며 대규모 최적화는 후속 과제입니다.
검증 기록 (2026-10-03, Unity 6000.6.4f1): 에디터 Play Mode 기본 전투 검사 통과,
기존 통합 검사 7,601 assertions, 신규 능력 검사 8,788 assertions 통과. Windows 개발 빌드 및
화면 캡처 프로브도 종료 코드 0. 3×6 전장, HP/Shield/Gauge 표시를 렌더링 캡처로 확인했습니다.
assertion 수는 루프 내부 불변식 검사를 포함합니다. 독립 테스트 케이스 수를 의미하지 않습니다.
