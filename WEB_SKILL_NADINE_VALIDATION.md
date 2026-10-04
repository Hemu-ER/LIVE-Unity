# 나딘 검증 결과

2026-10-05 · Unity6000.6.4f1 · 기준9147d3c · 웹 HEAD2e4b699e961d43984ff0eba88d691038ea163560.
호환성 문서 및 나딘 관련 combat-engine.js/currentAs/game.js/roster.js 직접 확인. 다른31명 재조사 없음. 현재15명/30개 실행,17명/34개 pending.

## 실제 규칙

- 야성: 시작0,정수 초가 바뀔 때+2,cap15. 정상 진행에서1초2/7초14/8초15. CC 중 획득 가능. 공격/추가 피해로 획득하지 않으며 소모하지 않음. 시간 점프에는 한 검사당+2만 적용하여 과거 초를 소급 적립하지 않음.
- AS: 현재AS×(1+야성×[.025,.0375,.05]). keyed status를 조회하는 multiplicative 배율. 스택 증가/감소를 즉시 반영하고 별도 버프를 누적하지 않음.
- 늑대 맹습: 야성15 최초 도달 시 전투당1회 다음 기본 공격3회 강화. 기본 피해 뒤 잔여 횟수 소비→원래 대상 AP×[1,1.5,3] 추가 스킬 피해. 범위/추가 대상 없음. 기본 피해로 대상이 죽으면 횟수는 소비하고 추가 피해는 생략. 기본 공격 횟수/야성에 추가 타격으로 계산하지 않음.
- Reset/사망/종료에 스택과 잔여 예약 제거. 다음 전투에서 야성0/최초 발동 권한 복구.

## 범용 변경

StackModifiers(Key,Stat,AmountPerStack/ByStar)를 런타임 능력치 조회에 연결. 기존 timed/permanent/reservation 배율과 곱셈으로 합성. 직렬화/성급 적용/유효값 검증 포함.
기존 ReserveNextBasic에 ReservedAttackCount와 잔여 횟수,예약 부여 횟수 도입. OncePerCombat 부여 제한과 실제 소비 횟수를 분리. 기존 정의의 기본값은1회이며 누락된 직렬화 값0도1회로 취급해 호환 유지.
Periodic의 선택적 CoalesceMissedPeriods는 나딘 정수 초 검사와 같은 놓친 주기 병합 방식. 기존 주기 스킬 정책은 변경하지 않음.
기존 쿨다운은 공격 직전 AS로 정하고 중간 AS 변화로 재계산하지 않음. timed 버프 만료/예약 소비는 스택 배율을 지우지 않음.
변경: 정의/검증/능력치 및 예약 런타임,CharacterSkills 나딘2개 항목,신규 NadineSmokeCheck,전체 runner/기대 완료수,소스 검증기,문서. 실제 roster/씬/UI/기존14명 스킬 정의는 불변.

## 자동 결과

Unity batch Play Mode `LIVE.Prototype.Editor.PrototypeSmokeCheck.Run`: **exit0**, 컴파일 오류/테스트 예외 없음.

| Suite | Assertions | 결과 |
|---|---:|---|
| Run 경제/상점/풀/합성/라운드 | 7,391 | PASS |
| Ability U01~U09 | 8,244 | PASS |
| AI/BFS/점유/사거리 | 221,993 | PASS |
| WebRoster | 7,838 | PASS |
| Mechanism/아이솔 | 34,983 | PASS |
| 비앙카/가넷/샬럿 | 130,164 | PASS |
| 케네스/아비게일/수아/마커스 | 151,380 | PASS |
| 제니/이안 | 176,692 | PASS |
| 현우/유키 | 121,983 | PASS |
| 유스티나 | 94,711 | PASS |
| 라우라 | 122,947 | PASS |
| 신규 나딘 | 104,063 | PASS |
| 기본 보드/전투/Reset | 별도 집계 없음 | PASS |

기존1,078,326 + 신규104,063 = **1,182,389 assertions**.
1/2/3성별 야성 초기값/초 경계/반복/cap,스택AS 계수/즉시 감소·복구,야성15 발동/정확히3회 소비/재부여 없음,AP 피해(AMP와 다른 값 사용),CC/재타게팅/종료/Reset 검증.
범용 상호작용 fixture에서 실제 나딘 정의와 라우라 패시브를 결합하고 실제 케네스 성급별 AS effect를 적용해 곱셈·독립 해제·쿨다운 스냅샷 검증. 제품 Character Definition을 복제/등록한 것이 아니며 실제 연결은 나딘 원본 하나다.
실제 상점에서1/3/9개 구매→기존 합성1/2/3성→벤치→배치→Ready→전투3경로 PASS. 테스트용 충분한 크레딧/HP만 임시 적용. 활성 스킬과 패시브 발동 및 성급 계수 확인.
혼합3v3: 나딘+라우라/케네스/제니/이안,12 seed×2회=24회. 모든 전투 전멸 종료,Draw0. 동일seed 결과/통계 일치. 틱마다 점유 유일성/유한 AP·양수 유한AS/실제 cooldown 비음수·유한값/틱당 기본 공격 상한 검사. 종료 시 모든 잔여 예약0 확인.
`python Tools/verify_web_skills.py ../web-roster-reference`: PASS.15명 계수와 나딘 초기값/정수초+2/cap15/최초3회/AP 및AS 공식 확인.

## 재현 / 남은 한계

기존 BattlefieldPrototype 씬 Play→WebRoster에서 나딘 구매→배치→Ready.1초마다야성+2,8초15와다음3회 추가 피해 확인. 자동 검증은 위 runner를 batchmode/nographics로 실행(-quit 생략).
웹 전역 AS상한4와 Unity100,첫 공격 시점,.02초 tick/반올림,기존 성급 성장,Manhattan AI,즉시 전멸 종료에 따른 마지막 후속 효과 중단은 유지했다. 전체 웹 전투 결과가 같다는 의미는 아니다.
새 modifier는 스택 수에 대한 선형 곱셈 배율이다. 임의 곡선/상태별 식을 해석하는 대규모 수식 엔진은 추가하지 않음. 다음 추천은 아야 단독: 제한5회 강화와 인접 적 조건/정확한 타격 순서 추가 검증부터. 다른 캐릭터를 완료로 표시하지 않음.
