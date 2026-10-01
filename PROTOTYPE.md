# 3 × 6 전장 프로토타입

1. Unity Hub에서 이 저장소 폴더를 Unity **6000.6.4f1**로 엽니다.
2. `Assets/Scenes/BattlefieldPrototype.unity`를 엽니다.
3. Play를 누르고 Game 뷰를 확인합니다. 전장은 Play 시 생성됩니다.

3행 × 6열 중 왼쪽 3열은 파란 A 진영, 오른쪽 3열은 붉은 B 진영입니다.
테스트 유닛은 각각 2행 2열, 2행 5열에 배치되며 초기 체력은 100/100입니다.
유닛은 머리·몸통·발을 조합한 임시 도형으로 표시하며, 위에는 얇은 HP 바만 표시합니다.
설명·좌표·체력 숫자는 표시하지 않습니다. 셀 사이 간격은 0.04이며 전장은 화면 중앙에 배치됩니다.
Game 뷰 종횡비가 바뀌어도 전체 전장이 중앙에 보이도록 카메라가 조절됩니다.

Play 중 Hierarchy에서 `Battlefield Prototype > Test Unit A` 또는 `Test Unit B`를
선택하고 `Prototype Unit` 컴포넌트 메뉴의 `Prototype/Take 25 damage` 또는
`Prototype/Restore health`로 체력바를 확인할 수 있습니다.
Inspector의 Current Health를 수정해도 반영됩니다. 변경은 Play 종료 시 초기화됩니다.

전투, 이동, AI는 아직 구현하지 않았습니다. 외부 이미지나 추가 패키지는 필요하지 않습니다.
기존 LIVE와 SampleScene은 유지하며, 빌드 목록 첫 번째에 새 씬만 추가했습니다.

## 자동 확인

Unity 에디터가 이 프로젝트를 사용하지 않을 때 다음을 실행합니다.

```text
Unity.exe -batchmode -nographics -projectPath "<저장소 절대 경로>" -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile "<로그 절대 경로>"
```

실제 Play 모드에서 타일 수, 진영, 유닛 위치, 체력바 변화와 체력 범위를 검사한 뒤 종료합니다.
성공 시 로그에 `PROTOTYPE_SMOKE_CHECK_PASSED`가 기록됩니다. `-quit`은 넣지 않습니다.
