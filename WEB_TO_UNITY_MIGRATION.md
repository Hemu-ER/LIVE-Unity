> 후속 업데이트: 이 문서의 최초 roster 이식 시점 표와 차이는 `WEB_SKILL_COMPATIBILITY.md`를 우선합니다. 현재 32명 모두 64개 skill reference를 가지며 아이솔만 액티브/패시브가 실행됩니다. 시너지/PvE는 여전히 미이식입니다.

# 웹 → Unity 데이터 마이그레이션 체크리스트

## 확인한 원본과 범위

기준 저장소: `Hemu-ER/ER-AutoChess`, HEAD **2e4b699e961d43984ff0eba88d691038ea163560**.
현재 파일을 직접 읽었으며 아래 고정 커밋 링크로 조사 결과를 재현할 수 있다.

- [roster.js](https://github.com/Hemu-ER/ER-AutoChess/blob/2e4b699e961d43984ff0eba88d691038ea163560/roster.js): 정적 데이터의 기준. Git blob `22391aa249142f8033982b732522a69eefe58a3b`.
- [combat-engine.js](https://github.com/Hemu-ER/ER-AutoChess/blob/2e4b699e961d43984ff0eba88d691038ea163560/combat-engine.js): `growth`, `makeUnit`, `roleBuff`, `target`, `move`, `damage`, `periodicAndSkills`, `prepareBattle`, `summonNina`, `tick` 확인.
- [game.js](https://github.com/Hemu-ER/ER-AutoChess/blob/2e4b699e961d43984ff0eba88d691038ea163560/game.js): `playableRoster`, `identityInfo`, `skillInfo`, 상점/풀/합성/숙련도, `setupRoundOpponent`, `finishRound` 확인.

roster 총 **36개 = 플레이 가능 32개 + PvE 4개**. 주석의 “32-character”는 배열 전체 개수가 아니다.
PvE ID는 `wild_boar`, `wild_dog`, `wild_wolf`, `wild_bear`이며 cost 0, pveOnly true이다.
엔진이 생성하는 **니나(nina) 1종**은 roster 항목이 아니다. 클로에 스탯에서 생성되며 이번 import에 포함하지 않는다.
32개 플레이 가능 항목은 모두 implemented=true이지만 이는 **웹의 플래그**이지 Unity 스킬 구현 상태가 아니다.

이번 패치는 기본 정의 데이터와 플레이어 상점 연결만 옮긴다. 웹 전투 코드를 복사하지 않는다.
이미지 파일은 가져오지 않았고 asset.sd 식별 문자열만 보존한다. 실제 스킬/시너지/PvE 동작은 구현하지 않는다.

## 구조 및 필드 대응

`Resources/WebRoster.json`은 원본 roster 배열을 필드 이름까지 유지한 데이터 스냅샷이다.
`PrototypeWebRoster`가 이를 기존 `PrototypeUnitDefinition`으로 변환한다. 소유/전투 모델은 그대로 사용한다.
실험체와 PvE 36개 모두 정의 조회가 가능하지만 `Playable` 필터로 **32개만** 상점/공유 풀/무료 시작 유닛에 사용한다.

| 웹 필드 | Unity 필드 | 변환/보존 규칙 |
|---|---|---|
| id | Id | 원문 그대로. 새 캐릭터 ID 발급 없음 |
| name | DisplayName | 원문 그대로. 멧현우·꿈델라·유키멍도 변경하지 않음 |
| cost | Cost | 0/1/2/3 그대로. 플레이어용은 1~3만 |
| role | Role + Archetype | 한국어 Role 그대로 + 분류 enum의 명시적 대응. 스탯 자동 보정 없음 |
| affiliations[] | Affiliations[] | 순서·문자열·빈 배열 보존. 시너지 효과 없음 |
| baseStats.hp | Stats.MaxHealth | 그대로 |
| baseStats.atk | Stats.AttackPower | 그대로 |
| baseStats.amp | Stats.SkillAmplification | 그대로 |
| baseStats.def | Stats.Defense | 그대로 |
| baseStats.as | Stats.AttackSpeed | 그대로(초당 공격 수, C# float 저장) |
| baseStats.range | Stats.AttackRange | 정수 그대로. 거리 **공식** 차이는 아래 참조 |
| main | MainStat | atk/amp/hp 그대로. 적응형 스킬 계산은 아직 사용하지 않음 |
| pveOnly | PveOnly / Playable | 원본 true 보존. 필드 생략은 웹의 !r.pveOnly와 동일하게 false로 해석 |
| implemented | WebImplemented | 웹 플래그 그대로. UnityImplementationStatus는 별도로 BasicCombatOnly |
| asset.sd | AssetReferenceId | 경로 문자열 그대로. 이미지 미포함 |
| 원본에 별도 definition ID 없음 | ActiveDefinitionReference / PassiveDefinitionReference | 미지정(null). 이름으로 스킬 ID를 추측하지 않음 |

역할 대응: 전사→Fighter, 원거리 평타→RangedAttack, 원거리 스킬→RangedSkill,
탱커→Tank, 근거리 스킬→MeleeSkill, 암살자→Assassin, 서포터→Support, 야생동물→PveReference.
기존 3종 enum의 숫자는 유지하고 뒤에 추가했다.

### 원본에 없는 값과 런타임 정책

- roster에는 CriticalChance, DefensePenetration, MoveSpeed가 없다. 스냅샷에는 이 필드를 추가하지 않았다.
- 기본 전투 어댑터에서는 치명타/관통을 0으로 둔다. 웹 기본 공격에는 해당 스탯 시스템이 없으며 추가 능력을 만들지 않기 위한 중립값이다.
- MoveSpeed 2칸/초는 웹 `QA.moveInterval=.5`에 대응한다. Unity는 보간 이동, 웹은 간격마다 칸을 바꾸므로 시간 처리까지 완전히 같지는 않다.
- Abilities.Skills는 빈 배열이다. Unity의 기존 게이지 기본 설정은 남지만 실제 실험체 스킬은 발동하지 않는다.
- game.js의 identityInfo는 roster name을 일부 다른 표시 이름으로 바꾼다(예: 멧현우→현우). 이번에는 요청된 **roster.js 기준 이름 보존**을 우선한다.

## 모드와 연결

일반 `BattlefieldPrototype.unity` Play는 WebRoster 모드다. 상점·벤치·보드가 실제 roster 이름을 쓴다.
`PrototypeRunController`의 ContextMenu에서 `Use U01-U09 regression fixtures` / `Use imported web roster`를 선택할 수 있다.
모드 전환은 새 Run을 생성한다. Reset Run은 현재 모드를 유지한다.
기존 `PrototypeGameData.Load()`는 의도적으로 기존 fixture 전용으로 유지하여 기존 테스트의 데이터를 바꾸지 않았다.

경제/확률/공유 풀/성급은 기존 Unity Rules를 재사용한다. 별도 웹 경제 엔진은 만들지 않았다.
기존 적 라운드의 수·별·좌표를 유지하고 U01~U09 참조만 같은 비용의 다음 ID로 대체한다:
`hyunwoo, kenneth, abigail, adela, yuki, rio, dailin, nicky, shurin`.
이는 **Unity 테스트 상대 연결용 매핑**이며 웹의 상대 라운드 테이블이나 밸런스를 이식한 것이 아니다.
PvE 4개는 참조 데이터로만 남고 게임의 적 라운드에도 넣지 않는다.

`PrototypeCharacterAssets`는 선택적 ScriptableObject 매핑이다.
`data.Definition(stableId)` → `AssetReferenceId` → `Resolve(definition)` → Sprite 경로를 제공한다.
현재 매핑 에셋은 없고 기존 도형을 유지한다. 웹 경로가 Unity Resources 경로라는 의미는 아니다.

한글 이름에는 설치된 맑은 고딕/Apple SD Gothic Neo/Noto Sans CJK KR를 사용한다.
레이아웃은 유지하고 보드 이름 너비만 텍스트 길이에 맞춘다. 폰트/이미지 파일은 복사하지 않았다.
배포 환경에 한국어 폰트가 없을 수 있으므로 정식 배포 전 라이선스가 확인된 폰트 에셋 준비가 필요하다.

## 기능 비교 체크리스트

분류: **동등**=핵심 규칙 동등, **차이**=양쪽 존재하지만 규칙/구현 다름,
**웹만**, **Unity만**, **양쪽 미완성**. 아래는 이번 패치 적용 후 상태다.
체크된 항목은 이번 범위에서 완료한 이식/보존이며, 나머지는 후속 비교·이식 과제다.

| 확인 | 대상 | 분류 | 웹 실제 구현 → Unity 상태 / 후속 과제 |
|---|---|---|---|
| [x] | 실험체 데이터 | 동등 | roster 32 플레이어 + 4 PvE 원본 필드 보존. PvE는 실행하지 않음 |
| [x] | 기본 스탯 | 동등 | 원본 6개 수치 보존. Crit/Pen/Move는 Unity 어댑터 정책으로 구분 |
| [ ] | 성급 | 차이 | 웹 HP/ATK/AMP 1/1.7/2.8, DEF 1/1.15/1.4, AS 1/1.05/1.15. Unity HP/AP만 1/1.8/3.2 유지 |
| [ ] | 숙련도 | 차이 | 웹 총96 EXP(현 Lv13부터6), Unity 총95(현 Lv14부터6). 웹 전투 스탯에 레벨당1%, Unity 전투 보정 없음 |
| [x] | 역할 | 차이 | 분류 문자열은 보존. 웹 배치 깊이별 roleBuff, Unity 분류만 유지 |
| [x] | 소속/시너지 | 차이 | affiliation 데이터 보존. 웹 lockSynergies/시간·피격 시너지, Unity 효과 없음 |
| [ ] | 기본 공격 | 차이 | 공통 방어 공식. 웹 float HP·캐릭터별 추가타·최초 공격 지연, Unity 정수 반올림/최소1/중립 crit0 기본 공격 |
| [ ] | 타게팅 | 차이 | 웹 행 우선·거리·HP·DEF·행 tie와 캐시 특수 규칙. Unity 거리→HP→CombatId, 타겟 유지 |
| [ ] | 이동/사거리 | 차이 | 웹 거리 Chebyshev(max dx,dy), 단순 전진/행 이동. Unity Manhattan, BFS/예약/보간 유지 |
| [ ] | 액티브 | 웹만 | 32종 이름별 실제 핸들러. Unity 실험체 참조는 미지정, generic fixture 스킬만 있음 |
| [ ] | 패시브 | 웹만 | 실제 실험체 패시브가 웹에 있음. Unity에는 향후 참조 필드만 있음 |
| [ ] | 상태/중첩 | 차이 | 웹 폰/취기/바람 등 개별 상태. Unity 일반 가산 버프·횟수 카운터만 |
| [ ] | 보호막 | 차이 | 양쪽 HP 이전 흡수. Unity 일반 Shield 효과/별도 이벤트, 실험체 연계는 미이식 |
| [ ] | 회복 | 차이 | 양쪽 HP 상한. 웹 흡혈·광역·주기·실험체 회복, Unity 일반 Heal 효과만 |
| [ ] | 행동 불능 | 웹만 | applyCC, 면역/감소/정신집중 취소. Unity CC 상태 없음 |
| [ ] | 부활/불사 | 웹만 | 제니/이안 부활, 하트·생명 공유 불사. Unity 전투 중 부활 없음 |
| [ ] | 소환 | 웹만 | summonNina 런타임 파생 스탯/안전한 빈 칸. Unity 소환 없음 |
| [ ] | 전투 시작 효과 | 차이 | 웹 실제 핸들러, Unity OnCombatStart 일반 트리거(U08 등) |
| [ ] | 공격 횟수 트리거 | 차이 | 웹 실험체별 카운터/추가타, Unity AfterNAttacks 일반 트리거 |
| [ ] | 시간 트리거 | 웹만 | periodicAndSkills의 주기/지연 발동. Unity는 버프 지속시간/시전 시간만 있고 주기 스킬 트리거 없음 |
| [ ] | 체력 조건 | 차이 | 웹 실험체 조건/부활/보호/처형, Unity HealthBelowPercent/OncePerCombat |
| [ ] | 처치/사망 트리거 | 차이 | 웹 죽음 개입 및 소속 연계. Unity OnKill + UnitDied 관찰, 사망 방지 핸들러 없음 |
| [ ] | 전투 이벤트 | 차이 | 웹 drainEvents log/damage, Unity 10종 typed event. 이름/구조/발행 시점 대응 필요 |
| [ ] | 전투 통계 | 차이 | 웹 basic/skill/passive/synergy 및 source/target 세분화, Unity 유닛별 피해/회복/보호막/처치/횟수 |
| [ ] | 3×3 배치→3×6 전투 | 차이 | 웹 B x=5-x, 팀 최대3명. Unity row/column·우측 절대 좌표, 현재 최대9칸 배치 |
| [x] | 경제 | 동등 | 시작5, 기본수입5, 10당이자1/최대3, Reroll2, 투자2→2EXP, 자연4EXP 유지 |
| [x] | 상점 | 차이 | 5칸 및 레벨별 1/2/3 비용 확률 동등. 웹 Math.random/잠금 있음, Unity seeded RNG/잠금 없음 |
| [x] | 공유 풀 | 동등 | 비용별18/15/12, 등장 예약·원본 개체 수 기준 소유/반환. 웹 재계산, Unity 명시적 예약 |
| [x] | 합성 | 동등 | 같은 ID/별 3개→상위별, 최대3성, 배치 개체 우선, 원본3/9개 보존 |
| [x] | 벤치 | 차이 | 양쪽8칸. 웹 가득 차도 즉시 합성 가능하면 구매 허용, Unity는 항상 구매 전 빈 칸 필요 |
| [ ] | 라운드 | 차이 | 웹 Result4초, 플레이어HP/탈락, R1 PvE. Unity Result2+Transition1, 고정 적표 반복, 탈락 없음 |
| [ ] | 전투 시간제한 | 차이 | 둘 다60초. 웹 남은 HP 비율 합으로 판정, Unity 무조건 Draw 유지 |
| [ ] | 아이템 | 양쪽 미완성 | 웹 BASIC_ITEMS 6종(철판/가죽/배터리/옷감/고철/오일), 획득·인벤토리만. Unity 없음. 장착/조합/전투 효과 이식 대상 아님 |
| [ ] | 야생동물/파밍 | 웹만 | 웹 R1 4종 출현표·승리 시 재료1~3개. Unity 정의 참조만 있고 출현/보상 없음 |
| [x] | U01~U09 테스트 fixture | Unity만 | 실제 roster와 분리, 기존 능력/경제/AI 회귀 검사 유지 |
| [x] | 공통 게이지·스킬 definition/runtime | Unity만 | 웹은 캐릭터별 핸들러/상태, Unity 공통 GaugeFull 등 데이터형 구성 |
| [x] | 고정 ID 예약 우선순위·stall 진단 | Unity만 | Unity 고정 순서/BFS/진단, 웹 seeded 행동 셔플/단순 이동 |
| [ ] | 확정 스킬 밸런스·표기 정합성 | 양쪽 미완성 | 웹 QA 상수 및 설명/코드 차이, Unity 실험체 스킬 미이식. 실제 코드·테스트로 규칙 확정 필요 |

### 원본 내부의 주의점

game.js 설명과 combat-engine.js 구현이 일부 다르다. 예를 들어 아이솔 설명은 미완성/QA지만 엔진에는
10초마다 적 전체 피해가 있고, 리오 설명은8초지만 엔진은 초기6초/이후7초 카운터다.
따라서 implemented=true 또는 설명 문장만으로 향후 스킬 이식을 완료 처리하면 안 된다.
이 문서는 이 차이를 기록하며 이번 패치에서 어느 쪽도 임의로 수정하지 않는다.

## 재생성 및 검증

```text
python Tools/import_web_roster.py <고정 커밋 roster.js의 로컬 경로> --commit 2e4b699e961d43984ff0eba88d691038ea163560
python Tools/import_web_roster.py <동일 경로> --commit 2e4b699e961d43984ff0eba88d691038ea163560 --check
```

스크립트는 JS를 실행하지 않고 JSON 배열 literal만 읽는다. 미지원 필드/스탯 변경은 오류로 처리한다.
`--check`는 원본 전체 레코드와 생성 JSON, Git blob hash까지 비교한다. 갱신 시 커밋과 개수 검사를 함께 검토한다.

Unity 6000.6.4f1에서 기존 `PrototypeSmokeCheck.Run` 실행으로 전체 검사를 수행한다.
`PrototypeRosterSmokeCheck`는 원본 필드 대조·32/36개 수·ID 중복·PvE 제외·레벨별 상점 풀 보존과
실제 명령으로 Reroll→구매→벤치→배치→판매→동일2개 추가 구매→2성→전투→다음 Prep를 확인한다.
통합 검사의 충분한 구매 자금은 테스트에서만 설정하며 마지막 모드 재설정으로 시작 Credits5를 복구한다.

## 다음 단계

1. 먼저 위 차이표에서 성급/숙련도/거리/시간초과 승패를 웹과 일치시킬 범위를 명시적으로 결정한다. 이번에는 Unity 규칙을 보존했다.
2. 기존 AfterNAttacks/Damage/Heal/StatBuff로 표현할 수 있는 단일 실험체부터 액티브·패시브를 원본 코드 및 고정 seed 비교 테스트와 함께 이식한다.
3. 그 뒤 주기 트리거·상태 중첩·CC/면역을 추가하고, 부활/불사/소환은 점유·사망 처리 테스트를 확장한 별도 단계로 진행한다.
4. 이미지 연결은 준비된 SD Sprite가 전달된 후 안정 ID 매핑으로만 진행한다. 아이템/시너지/PvE는 이번 체크리스트의 별도 범위다.

## 이번 패치 검증 결과

- 원본 대조: 36개 전체 JSON 레코드 및 roster Git blob 일치, 플레이 가능32/PvE4.
- Unity 6000.6.4f1 Play Mode: 기본 전투 SmokeCheck 통과.
- 기존 Run 7,391 / Ability 8,244 / AI 221,993 assertions 통과(64회 3v3, timeout Draw 0).
- 신규 Roster 7,838 assertions 통과. 원본 필드 보존, PvE 제외, 레벨1~20 상점 풀 보존,
  실제 roster 구매/이동/판매/3개 합성/2성/전투/다음 Prep 및 fixture 모드 분리 검증.
- Windows 개발 플레이어 빌드 및 캡처 프로브 종료 코드0. 실제 한국어 상점·보드 이름 표시 확인.
- 컴파일 오류/런타임 예외 없음. assertion 수는 반복 불변식 검사를 포함하며 독립 테스트 케이스 수와 다름.
