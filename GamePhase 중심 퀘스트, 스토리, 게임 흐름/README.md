# 🎮 GamePhase 기반 게임 흐름 & 튜토리얼 시스템

**Unity 기반 레스토랑 시뮬레이션**의 **게임 진행 관리**를 담당하는 시스템입니다.  
`GameManager`의 **GamePhase**를 중심으로 **튜토리얼 → 게임플레이 → 마감 → 스토리/퀘스트**의 전체 사이클을 제어합니다.

---

## 📂 폴더 구조

```
GameFlow/
 ├── GameManager.cs        # 게임 전체 상태 및 Phase 관리
 ├── RestaurantManager.cs  # 영업/손님 스폰/마감 로직
 ├── QuestManager.cs       # 퀘스트 상태 관리 및 보상 처리
 └── StoryManager.cs       # 스토리 트리거, 튜토리얼/다이얼로그 관리
```

---

## 1️⃣ 핵심 설계

### **1. GamePhase 기반 메인 루프**

`GameManager`는 **게임의 모든 진행 상태**를 Phase 단위로 관리합니다.

```
[Day Scene]
  EditStation → Day → SelectMenu

[Main Scene]
  Opening → Operation → Closing

[Special]
  Dialogue / Paused / GameOver / Loading
```

- **Phase 전환 시 EventBus로 전체 시스템 알림**
  ```csharp
  GameManager.Instance.ChangePhase(GamePhase.Operation);
  EventBus.Raise(GameEventType.GamePhaseChanged, newPhase);
  ```
- 특정 Phase 종료 시 → `StoryManager` 확인 → 스토리 없으면 `NoMoreStoriesInPhase` 이벤트 발행
- `RestaurantManager`는 Operation Phase에서 실제 영업, 손님 스폰 및 라운드 타이머 진행
- `QuestManager`는 게임 이벤트를 구독해 진행도 및 완료 체크
- `StoryManager`는 Phase/이벤트 기반으로 스토리 트리거 및 튜토리얼 실행

---

### **2. 튜토리얼 & 스토리 연계 흐름**

1. **튜토리얼 시작**
   - `GameManager.SetTutorialMode(true)`  
   - 손님 1명만 스폰 → 플레이어 행동 유도

2. **StoryManager 트리거**
   - `GamePhase` 진입 시 스토리 조건 확인
   - Dialogue → Quest → Guide UI → Customer Spawn 등 시나리오 실행
   - 모든 스토리 완료 시 `GameEventType.NoMoreStoriesInPhase` 발생

3. **튜토리얼 종료 후 메인 루프 전환**
   - `GameManager.SetTutorialMode(false)` → `Operation` 시작
   - 실제 영업 루프 시작 + 손님 풀 스폰

---

### **3. 하루 사이클 (Day Cycle)**

1. **Day → EditStation**
   - Day에서 스토리 출력
   - 출력할 스토리가 없거나 전부 출력했다면 자동으로 Day에서 EditStation으로 이동 

2. **Main Scene: Opening (스토리 확인)**
   - `RestaurantManager.InitializeDay()`
   - 스토리/퀘스트 체크

3. **Operation (영업)**
   - `CustomerSpawner.StartSpawning()`
   - `RestaurantManager`가 라운드 타이머 계산
   - 손님 퇴장 완료 시 → `AllCustomersLeft` 이벤트

4. **Closing (영업 종료)**
   - `RestaurantManager` 손님 정리
   - `GameManager` 하루 수익 계산 & 저장

5. **Result Panel 표시 후 다음 날로 전환**
   - Phase: `GamePhase.Loading` → Fade → `Day Scene` 복귀

---

## 2️⃣ 설계 특징

1. **이벤트 기반 Phase 전환**
   - `GameManager` 단일 진입점에서 게임 상태 관리
   - EventBus로 각 매니저 간 결합도 최소화

2. **스토리 & 퀘스트 유기적 연동**
   - 스토리 단계별 퀘스트 자동 시작/완료
   - 다이얼로그 종료 후 다음 행동 자동 트리거

3. **튜토리얼 친화적 구조**
   - `IsTutorialActive`로 스폰, 타이머, 입력 처리 제어
   - 튜토리얼 종료 후 실시간 게임 루프 자연스러운 전환

4. **세이브/로드 통합 관리**
   - 하루 종료 시:
     - `GameManager.SaveData()`
     - `QuestManager.SaveQuestData()`
     - `StoryManager` & `StationManager` 상태 저장

---

## 3️⃣ 코드 예시

**Phase 전환 & 이벤트 알림**
```csharp
public void ChangePhase(GamePhase newPhase)
{
    if (currentPhase == newPhase) return;
    currentPhase = newPhase;
    EventBus.Raise(GameEventType.GamePhaseChanged, newPhase);

    Time.timeScale = (currentPhase is GamePhase.Paused or GamePhase.GameOver) ? 0f : 1f;
}
```

**스토리 트리거 → 퀘스트/다이얼로그**
```csharp
private void CheckAndTriggerStory(string endedDialogueId = null)
{
    var currentPhase = GameManager.Instance.CurrentPhase;
    var nextStory = storyDatabase.FirstOrDefault(story =>
        !executedStoryIds.Contains(story.id) &&
        story.triggerPhase == currentPhase &&
        AreConditionsMet(story.conditions, endedDialogueId));

    if (nextStory)
    {
        executedStoryIds.Add(nextStory.id);
        StartCoroutine(ExecuteActions(nextStory.actions));
    }
    else
    {
        EventBus.Raise(GameEventType.NoMoreStoriesInPhase, currentPhase);
    }
}
```

**하루 종료 및 저장**
```csharp
private void EndDayCycle(int earningsFromDay)
{
    IncreaseDay();
    SaveData();
    ChangePhase(GamePhase.Loading);
}
```
