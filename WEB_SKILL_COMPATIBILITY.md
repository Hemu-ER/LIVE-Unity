# WEB SKILL COMPATIBILITY

기준: `Hemu-ER/ER-AutoChess` commit `2e4b699e961d43984ff0eba88d691038ea163560`의 combat-engine.js, game.js, roster.js 직접 조사. 32 playable, 4 PvE. 이름은 game.js skillInfo, 실행 규칙/계수는 combat-engine.js 우선.

A: 작업 전 Unity 범용 시스템만으로 전체 스킬 표현 가능. B: 작은 범용 확장/데이터 조합 필요. C: 전용 상태/정책/훅 필요. B가 이번 패치에서 모두 실행 가능하다는 뜻은 아니다. 실행 정의는 아이솔·비앙카·가넷·샬럿·케네스·아비게일·수아·마커스 8명의 16개이며, 나머지24명/48개는 pending reference이다. 최초 A/B/C 분류와 그 기준 시점은 변경하지 않았다.

AP=현재 공격력, AMP=증폭. 계수 배열은1/2/3성. 행=y,열=x. 웹 일반 거리 Chebyshev,인접 Manhattan1. 아래 별도 기재하지 않은 이동/소환/처치 조건은 해당 스킬 실행에 없다. 사망 시 일반 실행 중단, 예외인 치명 피해 대체는 개별 기재.

| ID | 액티브 분류·규칙 | 패시브 분류·규칙 | 실제 coefficient 배열 |
|---|---|---|---|
| hyunwoo | **B 도그파이트**: 일반3회 후 다음 공격 AP×dog, 최대HP×heal 회복 | **B 허세**: 강화 후 방어력+bluffDef 2초; 다음 공격 예약 필요 | `{dog:[1.25,1.8,3],heal:[.05,.07,.10],bluffDef:[10,20,35]}` |
| adela | **C 체크메이트**: 첫6초 이후10초 전체 AMP×(check+대상중첩×pawn), 중첩 삭제; 대상별 소유자 상태 | **B 프로모션**: 매 공격 AMP×promotion, 대상 pawn 최대3 | `{check:[1.8,2.4,4],pawn:[.5,.75,1.5],promotion:[.4,.55,.9]}` |
| dailin | **C 만취**: 취기40에서 AP×entry,3초 AS×2/피해20%감소/공격마다 AP×drunk; 모드 상태 | **B 취기**: 취권 밖 공격당 취기5,10회마다 AP×ten | `{drunk:[1,1.5,2.5],ten:[.5,.75,1],entry:[1.5,2,3.5]}` |
| yuki | **B 머리치기!**: 5회 후 다음 공격 AP×head,CC .5초 | **B 옷매무새 정리**: 단추2개, 공격당 AP×button 및 소비; 고갈 .5초 후2개 보충 | `{head:[2,2.5,4],button:[.5,.75,1.5]}` |
| rio | **C 정사필중**: 첫6초 이후7초,.5초 채널 후 AP×shot,직교 인접 적 절반; CC중단 재예약6초 | **C 카에유미**: 거리1/2/3에 AS+kaeyumiMax×1,2/3,1/3; 대상 연동 능력치 | `{shot:[3.4,4.5,7.5],kaeyumiMax:[.35,.45,.75]}` |
| justina | **B 섬멸 포격**: 2회 공격마다 대상 행 AMP×bomb | **B 부스트 대쉬**: 다음 공격 AMP×boost 예약; 효과 순서 필요 | `{bomb:[.85,1.1,1.9],boost:[.45,.6,1.0]}` |
| jenny | **B 페르소나**: 2회 후 다음 공격 AMP×persona | **B 죽음의 연기**: 첫 치명 피해 최대HP×revive 회복,1.5초 무적,AS×(1+reviveAs) | `{persona:[1.5,2,3],revive:[.2,.3,.5],reviveAs:[.3,.5,1]}` |
| nicky | **B 다혈질**: 생존 HP50%이하 AP/AMP/AS×(1+hot) 영구 | **C 가드&카운터**: guardChance 확률 기본/스킬 피해80%감소,AMP×counter 반격; 피해 전 훅/재귀 방지 | `{hot:[.2,.3,.5],guardChance:[.2,.2,.4],counter:[.75,1,2]}` |
| shurin | **C 만검귀종**: resolve2회 후 직교 인접 AP×aoe,3초 강화 모드 | **C 결심응진**: 일반3회 후 다음 공격 AP×resolve,실제 피해 절반 회복; 모드 상태 | `{aoe:[2,2.5,4.5],resolve:[1,1.5,2.5]}` |
| marcus | **B 지각변동**: 10초마다 전체 AP×quake,CC1초,shock | **B 전사의 투지**: shock 소비 공격 AP×shock,CC .5초 | `{quake:[1.5,2,3],shock:[1.5,2,3.5]}` |
| ian | **B 해방**: 첫 치명 피해 최대HP×revive 회복,이후 AP×1.2/AS×(1+reviveAs)/흡혈lifesteal | **B 사로잡힌 육신**: 시작 AP×.8; 부활 단계 연동 | `{revive:[.4,.5,.7],reviveAs:[.5,.75,1.5],lifesteal:[.2,.3,.5]}` |
| yumin | **B 풍류운산**: 시작 전체 AMP×start,1초에 전체CC .5초 | **C 선풍**: 대상 바람최대2,이미2일 때 AMP×windExtra; 매초 표식 AMP×windDot; 소유자별 표식 | `{start:[1,1.5,2.5],windDot:[.4,.6,1],windExtra:[.75,1,2]}` |
| debi-marlene | **C 트윈즈 러시**: 시작 및5회마다 AP×rush,모드전환,다음5회 현재HP×currentHp 고정 피해 | **C 블루&레드**: Marlene AS+.15,Debi 방어력+10; 전용 모드 | `{rush:[1.5,2,3.5],currentHp:[.03,.04,.07]}` |
| garnet | **B 처형식**: 첫 주기 처리1회: 가까운 적 CC1초,방어력×.9,최대HP×chain 고정 피해; 잔여HP비율<=execute 직접 사망 | **B 익숙한 아픔**: 기본 피해 basicReduce 감소 | `{chain:[.10,.15,.25],basicReduce:[.15,.25,.40],execute:[.10,.15,.25]}` |
| kenneth | **B 업화**: 공격5회마다 AP×shield 보호막,5초 AP/AS×(1+rage) | **B 억압된 분노**: 기본 실제 피해×lifesteal 회복 | `{shield:[1,2,4],lifesteal:[.10,.15,.25],rage:[.20,.30,.60]}` |
| irem | **C 냥냥 펀치**: 대상 방울 교대 부여/소비,소비 AMP×punch; 대상별 상태 | **B 고양이의 습성**: 시작 물고기1,3초마다 재획득; 소비 AMP×fishShield 보호막; 시작AS보너스0 | `{punch:[1.25,2,3.5],fishShield:[.6,1,1.8]}` |
| laura | **B 황혼의 도둑**: 10초마다 전체 AMP×twilight,CC1초 | **B 괴도**: 교대 강화 공격 AMP×thief,강화 대기 AS×2 | `{twilight:[1.5,2,3.5],thief:[1,1.5,2.5]}` |
| bianca | **B 진조의 군림**: 첫4초 이후8초 현재 대상 AMP×dominion+대상최대HP×maxHp | **B 짧은 안식**: 생존HP50%이하 최초1회 즉시 최대HP50%회복,3초 피해90%감소 | `{dominion:[3.3,4.5,7.5],maxHp:[.12,.17,.27]}` |
| cathy | **C 이머전시OP**: 2회마다 외상 후 즉시 OP: AMP×op+대상최대HP×maxHp 고정 피해,전체 아군 최대HP×teamHeal 회복 | **C 외과 전문의**: 후열 타게팅/외상 연결; 전용 타겟 정책 | `{op:[1.5,2.1,3.2],maxHp:[.10,.14,.20],teamHeal:[.08,.13,.24]}` |
| abigail | **B 바이너리 스핀**: 3회 공격마다 전체 AMP×spin | **B 티어링 블레이드**: 명중 생존 대상 방어력×(1-shred),영구 누적 | `{spin:[1,1.5,2.5],shred:[.05,.08,.15]}` |
| leny | **C 스프링! 트랩**: 골트베르2회 적용 후 다음 공격 AMP×trap,CC .5초 | **C 당근! 바주카**: 3초마다 전체 아군 중첩; 살아있는 레니 Chebyshev2칸 내 아군 공격이 소비하여 AMP×goldberg 피해/공격자 회복; 타유닛 공격 훅 | `{trap:[1.5,2,3.5],goldberg:[.35,.55,.90]}` |
| hart | **C Peacemaker**: 첫 치명 피해HP1,양팀 비소환 전체3초 불사,2.8초 후 최대HP×peacemakerHeal 회복; 양팀 범위/지연 생명주기 | **C Feedback**: 공격당 AP×feedback 추가 기본 타격2회; 타격 파이프라인 | `{peacemakerHeal:[.10,.15,.25],feedback:[.30,.40,.60]}` |
| isol | **B Mok제 폭탄**: 10초마다 생존 적 전체 현재AP×bomb; CC중에도 실행 | **B 유격전**: 시작 AP/AS×(1+start) 영구; 이번에 두 정의 이식 | `{bomb:[1,1.5,2.5],start:[.15,.25,.50]}` |
| chloe | **C 생명 공유**: Nina 소환 HP/AP .7,방어력 .8,AS1배; 빈 홈칸 | **C 살아 있는 마리오네트**: HP5% 연결,한쪽 불사,기본/스킬 피해70%전이,연결/주인 사망 연쇄; 소환 기반 후속 | `{ninaHp:[.70,.70,.70],ninaAtk:[.70,.70,.70],ninaDef:[.80,.80,.80],ninaAs:[1,1,1]}` |
| sua | **B 오딧세이**: 4초마다 대상 행 AMP×odyssey,총 실제 피해×lifesteal 회복 | **B 마음의 양식**: 매 공격 AMP×mind 추가,자기최대HP×heal 회복 | `{odyssey:[1.15,1.6,2.8],lifesteal:[.15,.25,.40],mind:[.25,.45,.85],heal:[.008,.015,.03]}` |
| johann | **C 구원의 성역**: 다른 아군2칸/HP45%이하 최소HP비율 대상1회;방어력+sanctuaryDef,4초 매초 최대HP×sanctuaryHp+AMP×sanctuaryAmp;until>time 경계 | **B 빛의 가호**: 자신 CC면역,시작 자신 제외 Chebyshev2칸 아군 AP/AMP/AS×(1+aura); 동적 aura 아님 | `{sanctuaryDef:[20,30,50],sanctuaryHp:[.20,.20,.20],sanctuaryAmp:[1,1.5,2.5],aura:[.10,.15,.25]}` |
| nadine | **B 늑대 맹습**: 야성15에서1회 다음3기본 공격 AP×wolf | **B 야성**: 정수 초 변화마다 야성+2 최대15,중첩당 AS+wild; 제한횟수 예약 필요 | `{wild:[.025,.0375,.05],wolf:[1,1.5,3]}` |
| bernice | **B 레그샷**: 3회마다 대상 행 AP×leg,AS30%감소3초 | **C 산탄**: 기본 AP×pellet,대상 열 다른 적 AP×scatter; 기본 피해 교체 훅 | `{pellet:[.90,.90,.90],scatter:[.50,.50,.50],leg:[1,1.5,2.5]}` |
| rozzi | **C 셈텍스탄 Mk-II**: 실제 기본 타격10회마다 대상최대HP×semtex 고정 피해 | **C 더블샷**: 공격 행동당 실제 기본2타 각각 AP×double; 행동/타격 분리 | `{semtex:[.12,.16,.24],double:[.75,.80,.90]}` |
| aya | **B 공포탄**: 직교 인접 적 존재 시1회 전체 AMP×fear,CC .5초; 조건 확장 | **C 고정 사격**: 3회 후 다음5회 AMP×fixed 추가/AS×2,강화 중에도 횟수 증가; 예약 상태 | `{fear:[1.25,2,3.5],fixed:[.40,.60,1.20]}` |
| mirka | **C 크래시 해머**: 게이지100소모 자기최대HP×shield 보호막,현재+직교인접 자기최대HP×crash,CC1초 | **C 리펄스 게이지**: 손실HP1%당 게이지2/공격당5; 피해량 게이지 훅,설명 초당1은 미구현 | `{shield:[.10,.18,.32],crash:[.06,.10,.18]}` |
| charlotte | **B 기적 실현**: 10초마다 전체 아군1초 무적 | **B 치유의 빛**: 공격3회마다 자신포함 Chebyshev2칸 AMP×heal,AP/AMP×(1+buff)3초 갱신; 치유의노래 시너지 제외 | `{heal:[.60,.90,1.50],buff:[.10,.15,.30]}` |

64개 분류: {'A': 0, 'B': 38, 'C': 26}. A=0은 기존 단일 대상/가산 버프/캐스트만으로 실제 전체 조합을 그대로 표현할 수 없기 때문이다.

## 추가한 범용 기반
- Periodic 최초시각/간격/cooldown, combat-start instant, 기존 공격횟수/체력/처치 트리거, OnBasicHit/StatusAtLeast.
- keyed 중첩 상한·소비·만료, 가산/비율 버프·디버프, 같은 key/stat 갱신, 영구 버프.
- CC와 이동예약/캐스트 취소, CC면역, 무적, 불사HP1, 전체/기본 피해감소, 기본 HP 피해 흡혈.
- 전투당1회 OnLethalDamage 자기회복 부활: 사망 확정 전 피해 대체. 사망 완료 객체 재생성은 아님.
- 전체 적/아군,행/열,인접 적/인근 아군; ChebyshevRadius 선택. 최대/현재HP 계수,고정 피해,처형,피해 기반 회복,지연 효과.
- 32명64 reference,성급별 계수,정의 복사. CustomHandlerKey 등록 인터페이스,캐릭터명 switch 없음.

## 아직 지원하지 않는 공통/전용 기반
동적 aura 입출입,소유자별 대상 표식,기본 타격 교체/추가타격,가드/반격/전이,조건부 아군 타겟,모드 전환,소환/연쇄사망. death trigger는 치명 피해 전 대체까지이며 사망 완료 후 외부 효과는 후속이다. 시작 범위 버프 형태의 aura만 지원한다. B 분류의 다음 공격 예약·소비 연결도 각 포트별 검증이 필요하다.

## 아이솔과 기존 전투 규칙
이름/기본스탯/계수를 변경하지 않았다. Mok제 폭탄:10,20,30…초 전체 생존 적 현재AP×[1,1.5,2.5]. 유격전:시작 AP/AS×[1.15,1.25,1.5]. 소수 AP는 피해 계산까지 유지. game.js의 미완성/QA 문구보다 실제 engine 구현을 따른다.
전체 웹 전투 결과와 동일하다는 뜻은 아니다. 기존 Unity 정수HP/피해 반올림,HP/AP 성급1/1.8/3.2,Manhattan AI,첫 기본 공격 시점,.02초 tick,60초 Draw를 유지했다. 웹 역할/배치/시너지/mastery 및 성급 전체 성장 이식은 이번 범위 밖. 한 진영 소멸 시 즉시 종료하므로 마지막 피해 후 후속회복이 중단될 수 있다.

## 설명/실행 차이
현우3회 후 다음 공격,다이린 취기40,슈린 resolve2회,유스티나·버니스 행 판정,수아4초 주기,나딘 초당2,미르카 초당게이지1 없음. 아델라 설명의 채널/무적도 해당 실제 주기 분기에 없다. 설명만 보고 추정 기능을 추가하지 않았다.

## 다음 이식 묶음
1. 비앙카·가넷·샬럿: 이번 묶음 구현 및 자동 검증 완료.
2. 케네스·아비게일·수아·마커스: 두 번째 묶음 구현/검증 완료.
3. 제니·이안:치명 피해 대체/통계 이벤트 순서 보강.
4. C는 별도 작은 패치,클로에는 소환 생명주기부터.

## 검증 방법
기존 PrototypeSmokeCheck에 PrototypeMechanismSmokeCheck를 연결했다. catalog/JSON,주기/cooldown,공격·체력·중첩,CC/만료/부활/범위,아이솔1~3성/10·20초,실제 상점→구매→배치→전투를 자동 검증한다. 결과는 SKILL_VALIDATION.md 참조.

## 현재 실행 상태 (두 번째 묶음 반영)

| 실험체 | 액티브 | 패시브 | 근거 |
|---|---|---|---|
| 아이솔 | implemented | implemented | 기존 두 정의/회귀 유지 |
| 비앙카 | implemented | implemented | 첫4초/이후8초; 성급 AMP/최대HP 계수; 비치명 피해 직후50% 판정/회복/3초90%감소 |
| 가넷 | implemented | implemented | 최초 주기1회,nearest selector,CC→DEF×.9→고정 피해→처형; 성급별 기본 피해 감소 |
| 샬럿 | implemented | implemented | 10초마다 전체 아군1초 무적; 세 번째 공격 직후 Chebyshev2,대상마다 회복→AP/AMP 버프,3초 갱신 |
| 케네스 | implemented | implemented | 기본5회 보호막/AP·AS5초; 실제 기본 HP피해 흡혈 |
| 아비게일 | implemented | implemented | 생존 대상 명중3회 스핀; 명중 대상 현재 방어력 비율 누적 감소 |
| 수아 | implemented | implemented | 4초 행 피해/실제 합산 회복; 매 기본 공격 추가 피해/최대HP 회복 |
| 마커스 | implemented | implemented | 10초 전체 피해/CC/shock; CC 중 주기 지연,실제 명중 대상 shock1회 소비 |
| 위8명을 제외한24명 | pending | pending | reference만 있고 실행 정의 없음 |

최초 A0/B38/C26 분류는 유지한다. 첫 묶음3명은 당시 B였으며 작은 범용 확장과 데이터만으로 구현했다. 캐릭터별 C# 분기/전용 핸들러는 추가하지 않았다. 표현 범위 확장: 최대HP/처형 성급 배열,명시적 nearest/사거리 무시,피해 직후 health trigger,공격 직후 count trigger,대상별 효과 순서,절대 전투시각 기반 버프 만료. 효과/상태 이벤트와 감소/처형 통계를 추가했다.

웹 재확인은 이 세 캐릭터의 실행 순서/계수에만 한정했다. 샬럿은 `currentAmp(u)`를 아군마다 다시 평가하므로 자신의 버프 이후 아군 회복량이 커진다. Unity도 이 순서를 보존하되 대상 순서는 기존 stable CombatId로 고정한다(웹은 units 배열 순서). 기존 Manhattan nearest/현재 타겟 잠금·정수 피해·성급 기본 성장 차이는 그대로다. 샬럿 치유의 노래 시너지는 추가하지 않았다.

고정 피해는 방어력을 우회하지만 무적/피해감소/보호막은 거친다. 가넷 처형은 웹처럼 직접 사망이므로 DamageDealt에 잔여HP를 가산하지 않고 UnitExecuted/UnitDied와 Kills/Executions에 기록한다. 비앙카의 치명 피해에는 짧은 안식이 발동하지 않는다. 첫 묶음 검증은 WEB_SKILL_BATCH_VALIDATION.md 참조.

## 두 번째 묶음의 실행 순서와 범용 확장

최신 웹 HEAD는 기준 문서와 같은 `2e4b699e961d43984ff0eba88d691038ea163560`이었다. 전체 조사를 반복하지 않고 네 캐릭터 관련 분기와 damage의 실제 HP 피해 반환만 확인했다.

- 케네스: 기본 피해 후 실제 HP 손실 기준 흡혈 → 5회마다 현재 AP 기준 보호막 → rage5초 갱신. 보호막 흡수량과 초과 피해는 흡혈에 포함하지 않는다. 이전 rage가 유효하면 다음 보호막은 강화 AP로 계산되며 버프는 중복 곱하지 않는다.
- 아비게일: 웹의 `!t.dead` 분기 안에서 방어력 감소와 횟수 증가가 함께 일어난다. 따라서 치명 기본 공격은 스핀 횟수에서 제외한다. 세 번째 생존 명중의 shred가 먼저 적용된 다음 스핀이 피해를 준다. 원래 명중 대상이 죽었다고 다음 타겟에 shred를 옮기지 않는다.
- 수아: 기본 공격 원래 대상에 추가 피해 후 자기 최대HP 회복. 죽은 대상 추가 피해는 생략하지만 다른 생존 적이 있어 전투가 계속되면 자기 회복은 실행한다. 오딧세이는 대상 행 생존 적의 실제 HP 손실을 합산하여 회복한다. 마지막 처치의 기본 흡혈/이미 해결한 오딧세이 피해 회복도 정산한다.
- 마커스: `nextQuake` 기한과 CC 종료를 모두 만족해야 주기가 발동한다. 피해 후 생존 적에게 CC1초와 공유 boolean에 해당하는 `shock` 1중첩을 부여한다. 후속 기본 공격 직후 실제 명중한 살아있는 대상의 shock를 먼저 제거하고 추가 피해/CC .5초. 여러 마커스도 같은 shock를 두 번 소비할 수 없다.

작은 범용 확장: 원래 BasicHitTarget 효과 anchor, 생존 명중만 세는 attack-count 옵션, OnBasicHit의 대상 상태 조건/원자적 소비, 성급별 회복 계수. 상태 소비 이벤트를 append하고 보호막/회복에 SkillId를 전달했다. 캐릭터명 C# 분기나 custom handler는 없다. 기존 keyed status,영구 비율 modifier,Periodic,all-enemy/row,CC,shield,lifesteal를 재사용했다.

새 네 정의는 `PreserveAuthoredPrecision`을 사용한다. float 저장 스키마를 유지하면서 작성된 십진 계수를 double 계산에 사용하여 수아2성 `100×0.45/2=22.5`가23으로 반올림되게 한다. 이전4명/U01~U09의 반올림 변경을 피하려 기본값은 legacy 경로다. 전체 정의의 정밀도 정책 통일은 별도 검증 과제이다.

최초 A0/B38/C26 분류 기준은 변경하지 않는다. 이번 네 명도 최초 B이며 이제 범용 실행 정의와1/2/3성 독립·게임 흐름 테스트가 존재한다. 신규151,380 assertions 및 기존 전체 회귀 통과. 24회 혼합 시뮬레이션 중22회 전멸 종료,2회는 피해/행동이 계속되는 장기전으로 기존60초 Draw. 상세: WEB_SKILL_SECOND_BATCH_VALIDATION.md.
