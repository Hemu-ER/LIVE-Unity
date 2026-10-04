# 라우라 검증 결과

2026-10-04 · Unity6000.6.4f1 · 기준 dccdfd0 · 웹 HEAD2e4b699e961d43984ff0eba88d691038ea163560.
32명 재조사 없이 호환성 문서와 라우라 분기/currentAs/공격 스케줄링만 확인. 현재14명/28개 실행,18명/36개 pending.

## 확인한 웹 규칙

- 황혼의 도둑:10초마다 전체 생존 적 각각 AMP×[1.5,2,3.5] 피해 후 CC1초. CC 중에도 실행. 기본 공격 횟수/예약과 무관.
- 괴도: 초기 예약 없음. 첫 기본 공격 이후 예약하여 현재 AS×2(모든 성급 동일 multiplicative). 다음 기본 피해 후 예약을 false로 만든 뒤 AMP×[1,1.5,2.5] 추가 피해. 예약 소비한 공격에서 재예약하지 않음. 다음 홀수 공격에서 재예약.
- 기본 피해가 대상을 죽이면 추가 피해는 생략하지만 예약/AS는 소비·해제. 외부 대상 사망/재타게팅/이동/CC는 소비 사유가 아님. 정상 소비 전까지 기간 없이 유지.
- 다음 공격 cooldown은 공격 직전 AS로 스냅샷한다. 따라서1AS 예시에서1→2번째 간격1초,2→3번째 간격.5초. 예약 생성 즉시 기존 cooldown을 소급 변경하지 않음.

## 작은 범용 확장

`PrototypeReservationModifier(Stat,Multiplier)` 배열을 스킬 정의에 추가하고 예약이 살아 있는 동안만 ModifiedStat에서 조회한다. 별도 지속시간/복제 buff 상태가 없어 소비·Reset·사망·전투 종료와 함께 사라진다. NextAttackReserved 시점에는 이미 적용,NextAttackConsumed와 추가 피해 시점에는 이미 해제. 기존 이벤트/통계 타입 유지.
`RestartCountOnConsume`은 교대 예약을 위한 선택 옵션. 기존 제니/현우/유키/유스티나 정의와 실행 순서는 그대로이며 캐릭터명 분기 없음. 정의 검증은 비예약 modifier/비정상 배율을 거부한다.
변경 파일: 스킬 정의/검증,UnitAbilities/UnitMechanisms,CharacterSkills의 라우라2개 항목,신규 LauraSmokeCheck와 전체 runner/완료수 기대값,웹 소스 검증기,문서. 기존씬/UI/경제/roster 원본 스탯은 변경하지 않음.

## 자동 결과

Unity batch Play Mode 전체 runner **exit0**, 컴파일 오류 및 테스트 예외 없음. 이벤트 관찰 결과는 callback 바깥에서 assertion 처리하여 Publish가 예외를 기록만 하고 계속하는 경우에도 실패가 숨겨지지 않도록 검증.

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
| 신규 라우라 | 122,947 | PASS |
| 기본 보드/전투/Reset | 별도 집계 없음 | PASS |

기존955,379 + 신규122,947 = **1,078,326 assertions**.
1/2/3성 각각 AP와AMP를 다르게 설정한 정확한 피해,최초/교대 예약,예약 즉시 AS,공격 시작/소비/추가 피해 이벤트 시점의 배율,다른 AS 버프와 곱셈·독립 제거,실제 공격 간격,이동·CC·외부 사망·재타게팅 유지,치명 기본 공격 소비,사망·종료·Reset·다음 전투 정리를 검증.
액티브는9.98초 미발동/10초 피해+CC,CC .98초 유지/1초 만료,19.98초 미발동/20초 재발동을 검사.

실제 상점에서1/3/9개 구매→기존 합성1/2/3성→벤치→배치→Ready→전투3경로 PASS. 테스트 크레딧/전투HP만 임시 적용해 관찰; 실제 Character Definition과 성급별 양쪽 계수 사용,복제 캐릭터 없음.
혼합3v3은 라우라+제니/현우/유키/유스티나,12 seed×2회=24회. 점유 유일성/AP·AS 유한값/양수 AS,동일seed 결과·통계 일치.18회 전멸,6회 기존60초 Draw,시간제한까지 활동한 전투만 Draw로 허용하고 무행동 stall 없음 확인.
`python Tools/verify_web_skills.py ../web-roster-reference` PASS:14명 coefficient 및 라우라10초/CC/교대/AS 배율 규칙.

## 재현과 한계

기존 BattlefieldPrototype 씬 Play→WebRoster 라우라 구매→배치→Ready. 첫 공격 후 AS 상승,다음 공격 추가 피해/배율 해제,10초 전체 피해/CC 확인. 자동 검증 진입점 `LIVE.Prototype.Editor.PrototypeSmokeCheck.Run` (batchmode/nographics,-quit 생략).
웹 전역 AS상한4와 기존 Unity상한100,정수HP/반올림,성급 성장,Manhattan AI,.02초 tick,즉시 전멸 종료에 의한 마지막 후속 효과 중단은 이번 작업에서 변경하지 않았다. 전체 엔진 결과 동등성을 의미하지 않는다.
다음 추천은 나딘 단독: 제한3회 강화와 야성의 주기/AS 연결을 검증하는 후속 작업. 이번 배율 구조만으로 복수 강화 소비까지 지원한다고 표시하지 않는다.
