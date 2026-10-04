# 현우·유키 예약/자원 검증

2026-10-04, Unity **6000.6.4f1**, `codex/battlefield-prototype`, 기준 커밋 `b7fb0ca`.
웹 참조 HEAD: `2e4b699e961d43984ff0eba88d691038ea163560`. 기존 호환성 문서를 사용하고 현우·유키 실행 분기만 재확인했다.

## 구현

- 현우: 3회 예약/4번째 도그파이트 AP×[1.25,1.8,3] + 자기 최대HP×[.05,.07,.10] 회복, 후속 허세 DEF+[10,20,35] 2초. 웹처럼 예약 시 카운트를 초기화하여 이후7/10번째 강화.
- 유키: 5회 예약/6번째 머리치기 AP×[2,2.5,4] + CC .5초, 이후11/16번째 강화. 단추2개 시작, 기본 명중마다1개 소비/AP×[.5,.75,1.5], 고갈 .5초 후2개 재충전.
- 기존 ReserveNextBasic을 재사용. OnReservedBasicResolved/RequiredSkillId로 실제 예약 실행 후 다른 정의의 효과 연결. 캐릭터명 분기 없음.
- Charge 정의는 keyed status + key당 단일 refill 시각. CC 중 시간 진행, Reset/사망/전투 종료 시 제거. JSON 왕복 보존.
- 같은 공격에서 기본 피해→머리치기 피해→CC→단추 소비→단추 피해를 이벤트 순서로 검증. 기존 이벤트 타입/통계 유지.
- 실제 CharacterSkills 정의4개만 pending→implemented. 기존10명의 실행 정의,32명 binding,roster 원본 스탯은 불변. 현재12명/24개 실행,20명/40개 pending.

## 자동 검증 결과

배치 Play Mode `LIVE.Prototype.Editor.PrototypeSmokeCheck.Run`: **exit 0**, C# 컴파일 오류/테스트 실패 없음.

| Suite | Assertions | 결과 |
|---|---:|---|
| Run: 경제/상점/풀/합성/벤치/라운드 | 7,391 | PASS |
| Ability: U01~U09/스탯/게이지/이벤트 | 8,244 | PASS |
| AI: 타겟/BFS/점유/사거리/64 simulations | 221,993 | PASS |
| WebRoster | 7,838 | PASS |
| Mechanism/아이솔/직렬화 | 34,983 | PASS |
| 비앙카/가넷/샬럿 | 130,164 | PASS |
| 케네스/아비게일/수아/마커스 | 151,380 | PASS |
| 제니/이안/치명 피해 대체 | 176,692 | PASS |
| 신규 Charge: 현우/유키 | 121,983 | PASS |
| 최상위 보드/기본 전투/Reset | 별도 집계 없음 | PASS |

신규 검증: 두 캐릭터1/2/3성, 예약/소비/회복/버프/CC 계수와 경계 시간, CC/이동 중 예약 유지, 버프 갱신, 재충전 대기 중 추가 공격/중복 방지, 직렬화, Reset/사망/종료 정리.
혼합3v3은12 seed 각각2회(24회), 틱마다 생존 점유 중복/비정상 수치 검사, 결과/통계 일치.22회 전멸 종료,2회 기존60초 Draw; 무행동 stall 없음. 프레임 기반 무한 실행 없이3000 tick 상한에서 기존 FinishAsDraw 계약 사용.

실제 게임 경로6개(현우/유키×1/2/3성): 실제 유료 reroll/구매로1/3/9개 확보→기존 재귀 합성→벤치→배치→Ready→전투. 액티브·패시브 발동과 합성 성급의 런타임 계수 확인. 테스트 실행을 위해 시작 크레딧과 전투 HP만 fixture에서 높였으며, 복제 캐릭터나 제품 데이터 변경은 없다.

`python Tools/verify_web_skills.py ../web-roster-reference`: PASS. 핀된 실제 웹12명 계수,현우/유키 예약·효과 순서·재충전 규칙 검증.

## 재현

Unity에서 기존 BattlefieldPrototype 씬을 열고 Play. 기본 WebRoster 모드에서 현우(roster 이름 멧현우)/유키(roster 이름 유키멍)를 구매하고 벤치에서 배치한 뒤 Ready. 각3/5회 이후 다음 공격 효과를 확인한다. 전체 자동 검증은 기존 SmokeCheck 실행 진입점을 사용하며 신규 suite가 자동 포함된다.

CLI: Unity6000.6.4f1에 `-batchmode -nographics -projectPath <LIVE-Unity> -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile <log>`를 전달한다. runner가 종료하므로 `-quit`를 추가하지 않는다.

## 남은 한계 / 다음 작업

- 현우 원시 웹은 빠른 재발동 때 DEF를 반복 가산하고 만료 때 한번만 빼는 누적 문제가 있다. 요청된 기존 timed buff 계약을 우선해 Unity에서는 같은 key/stat 지속시간 갱신을 사용한다.
- 기존 Unity 정수HP/반올림,Manhattan 이동,성급 성장,.02초 tick,60초 Draw 유지. 전체 웹 전투 결과까지 동일하다는 의미는 아니다.
- 기존 즉시 전멸 종료가 마지막 공격의 후속 효과를 중단할 수 있는 계약은 유지했다.
- charge는 기본 명중 소비/고갈 후 전체 보충 범위다. 조건부 획득,자동 재충전 없는 유한 강화,공유 자원 정책은 후속. 새 enum은 마지막에 추가해 기존 직렬화 값 보존.
- 다음에는 유스티나를 작은 단독 묶음으로 권장: 즉시 행 피해와 다음 기본 공격 예약의 연결 순서를 먼저 검증한다. 다른 캐릭터를 완료 처리하지 않았다.
