using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuestManager : Singleton<QuestManager>
{
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo;
    
    private Dictionary<string, QuestData> questDatabase;
    private Dictionary<string, QuestStatus> playerQuestStatus;
    private Dictionary<string, Dictionary<string, int>> playerQuestProgress;

    #region 외부 사용 함수
    
    public List<QuestData> GetInProgressQuests() => 
        playerQuestStatus.Where(p => p.Value == QuestStatus.InProgress)
            .Select(p => questDatabase[p.Key])
            .ToList();
    
    public List<QuestData> GetCompletedQuests() => 
        playerQuestStatus.Where(p => p.Value == QuestStatus.Completed)
            .Select(p => questDatabase[p.Key])
            .ToList();
    
    public int GetQuestObjectiveProgress(string questId, string targetId)
    {
        return playerQuestProgress.TryGetValue(questId, out var progress) 
            ? progress.GetValueOrDefault(targetId, 0) 
            : 0;
    }
    
    public QuestStatus GetQuestStatus(string questId) => playerQuestStatus.GetValueOrDefault(questId, QuestStatus.Inactive);
    public QuestData FindQuestByID(string questID) => questDatabase.GetValueOrDefault(questID);
    
    #endregion
    
    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this);
        LoadQuestDatabase();
        LoadQuestData();
        EventBus.OnGameEvent += HandleGameEvent;
    }

    private void OnDestroy()
    {
        EventBus.OnGameEvent -= HandleGameEvent;
    }

    private void LoadQuestDatabase()
    {
        questDatabase = Resources.LoadAll<QuestData>(StringPath.QUEST_DATA_PATH).ToDictionary(q => q.id);
        if (showDebugInfo) Debug.Log($"[QuestManager] 총 {questDatabase.Count}개 Quest Database 로드 완료");
    }
    
    #region 데이터 저장 및 불러오기

    private void LoadQuestData()
    {
        playerQuestStatus = new Dictionary<string, QuestStatus>();
        playerQuestProgress = new Dictionary<string, Dictionary<string, int>>();
        var data = SaveLoadManager.LoadGame();
        if (data == null) return;

        for (int i = 0; i < data.playerQuestStatusKeys.Count; i++)
        {
            if (data.playerQuestStatusKeys[i].StartsWith("t_")) continue;
            playerQuestStatus[data.playerQuestStatusKeys[i]] = data.playerQuestStatusValues[i];
        }

        foreach (var progressData in data.playerQuestProgress)
        {
            if (progressData.questId.StartsWith("t_")) continue;
            var innerDict = new Dictionary<string, int>();
            for (int i = 0; i < progressData.objectiveTargetIds.Count; i++)
            {
                innerDict[progressData.objectiveTargetIds[i]] = progressData.objectiveCurrentAmounts[i];
            }
            playerQuestProgress[progressData.questId] = innerDict;
        }
    }

    public void SaveQuestData()
    {
        var data = SaveLoadManager.LoadGame() ?? new SaveData();

        data.playerQuestStatusKeys = playerQuestStatus.Keys.Where(k => !k.StartsWith("t_")).ToList();
        data.playerQuestStatusValues = playerQuestStatus.Where(p => !p.Key.StartsWith("t_")).Select(p => p.Value).ToList();
        
        data.playerQuestProgress.Clear();
        foreach (var pair in playerQuestProgress.Where(p => !p.Key.StartsWith("t_")))
        {
            data.playerQuestProgress.Add(new SerializableQuestProgress
            {
                questId = pair.Key,
                objectiveTargetIds = pair.Value.Keys.ToList(),
                objectiveCurrentAmounts = pair.Value.Values.ToList()
            });
        }
        SaveLoadManager.SaveGame(data);
    }

    public void ResetQuestData()
    {
        playerQuestStatus.Clear();
        playerQuestProgress.Clear();
    }

    #endregion
    
    #region 퀘스트 상태 관리
    
    public void StartQuest(string questId)
    {
        if (!questDatabase.ContainsKey(questId) || GetQuestStatus(questId) != QuestStatus.Inactive) return;

        playerQuestStatus[questId] = QuestStatus.InProgress;
        playerQuestProgress[questId] = new Dictionary<string, int>();
        if (showDebugInfo) Debug.Log($"[QuestManager] 퀘스트 시작: {questId}");
        
        CheckAndCompleteQuest(questId);
    }
    
    // StoryManager 등에서 수동으로 퀘스트 완료를 시도할 때 호출
    public void EndQuest(string questId)
    {
        if (GetQuestStatus(questId) != QuestStatus.InProgress) return;
        
        if (CheckQuestCompletion(questId))
        {
            CompleteQuest(questId);
        }
        else
        {
            FailQuest(questId);
        }
    }
    
    private void CompleteQuest(string questId)
    {
        if (GetQuestStatus(questId) != QuestStatus.InProgress) return;
        
        QuestData quest = FindQuestByID(questId);
        QuestStatus finalStatus = quest.id.StartsWith("t_") ? QuestStatus.Finished : QuestStatus.Completed;
        playerQuestStatus[questId] = finalStatus;
        
        if (quest.rewardMoney > 0) GameManager.Instance.AddMoney(quest.rewardMoney);
        foreach (var itemId in quest.rewardItemIds)
        {
            if (itemId.StartsWith("s")) StationManager.Instance.UnlockStation(itemId);
            else if (itemId.StartsWith("f")) MenuManager.Instance.UnlockMenu(itemId);
        }

        if (showDebugInfo) Debug.Log($"[QuestManager] 퀘스트 {finalStatus}: {quest.questName}");
        EventBus.Raise(GameEventType.QuestStatusChanged, new KeyValuePair<string, QuestStatus>(questId, finalStatus));
    }
    
    private void FailQuest(string questId)
    {
        if (GetQuestStatus(questId) != QuestStatus.InProgress) return;

        playerQuestStatus[questId] = QuestStatus.Failed;

        if (showDebugInfo) Debug.Log($"[QuestManager] 퀘스트 실패: {FindQuestByID(questId)?.questName}");
        EventBus.Raise(GameEventType.QuestStatusChanged, new KeyValuePair<string, QuestStatus>(questId, QuestStatus.Failed));
    }

    #endregion

    #region 퀘스트 진행도 및 완료 체크
    
    // 진행도 누적이 필요한 퀘스트
    public void UpdateQuestProgress(string questId, string objectiveTargetId, int amount = 1)
    {
        if (GetQuestStatus(questId) != QuestStatus.InProgress) return;

        var objective = FindQuestByID(questId)?.objectives.FirstOrDefault(o => o.targetId == objectiveTargetId);
        if (objective == null) return;
        
        int currentAmount = playerQuestProgress[questId].GetValueOrDefault(objectiveTargetId, 0) + amount;
        playerQuestProgress[questId][objectiveTargetId] = currentAmount;
        
        if (showDebugInfo) Debug.Log($"[QuestManager] 진행도 업데이트: {questId} - {objectiveTargetId} ({currentAmount}/{objective.requiredIds.FirstOrDefault()})");
        
        CheckAndCompleteQuest(questId);
    }

    // 퀘스트 완료 조건 확인
    private bool CheckQuestCompletion(string questId)
    {
        QuestData quest = FindQuestByID(questId);
        if (!quest) return false;

        foreach (var objective in quest.objectives)
        {
            bool isObjectiveMet = false;
            switch (objective.objectiveType)
            {
                // --- 특정 상태달성형 퀘스트 ---
                case QuestObjectiveType.EarnMoney:
                    if (int.TryParse(objective.targetId, out int money)) isObjectiveMet = GameManager.Instance.TotalEarnings >= money;
                    break;
                case QuestObjectiveType.HoldFood:
                    isObjectiveMet = PlayerController.Instance.IsHoldingFood(objective.targetId);
                    break;
                case QuestObjectiveType.HoldStation:
                    isObjectiveMet = PlayerController.Instance.IsHoldingStation(objective.targetId);
                    break;
                case QuestObjectiveType.CheckIngredients:
                    isObjectiveMet = StationManager.Instance.CheckIngredients(objective.targetId, objective.requiredIds.ToArray());
                    break;
                case QuestObjectiveType.CheckFood:
                    isObjectiveMet = StationManager.Instance.CheckObjectOnStation(objective.targetId, objective.requiredIds[0]);
                    break;
                case QuestObjectiveType.DeliverFood:
                    isObjectiveMet = CustomerManager.Instance.IsFirstCustomerEating();
                    break;
                case QuestObjectiveType.PlaceStation:
                    isObjectiveMet = StationManager.Instance.CheckStationPlacedOnCell(objective.targetId, objective.requiredIds[0]);
                    break;
                    
                // --- 횟수/수량 누적형 퀘스트 ---
                default:
                    if (int.TryParse(objective.requiredIds.FirstOrDefault(), out int required))
                    {
                        int current = playerQuestProgress[questId].GetValueOrDefault(objective.targetId, 0);
                        isObjectiveMet = current >= required;
                    }
                    break;
            }
            if (!isObjectiveMet) return false;
        }
        return true;
    }

    // 완료 체크와 실행
    private void CheckAndCompleteQuest(string questId)
    {
        if (GetQuestStatus(questId) == QuestStatus.InProgress && CheckQuestCompletion(questId))
        {
            CompleteQuest(questId);
        }
    }

    #endregion

    #region 이벤트 관리

    // 모든 게임 이벤트를 받아 관련 퀘스트들의 상태 확인
    private void HandleGameEvent(GameEventType eventType, object payload)
    {
        var inProgressQuests = playerQuestStatus
            .Where(p => p.Value == QuestStatus.InProgress)
            .Select(p => FindQuestByID(p.Key))
            .ToList();

        foreach (var quest in inProgressQuests)
        {
            bool needsRecheck = quest.objectives.Any(obj =>
            {
                switch (eventType)
                {
                    case GameEventType.PlayerPickedUpItem:
                        return obj.objectiveType == QuestObjectiveType.HoldFood ||
                               obj.objectiveType == QuestObjectiveType.HoldStation;
                    case GameEventType.StationUsed:
                        return obj.objectiveType == QuestObjectiveType.CheckIngredients ||
                               obj.objectiveType == QuestObjectiveType.CheckFood;
                    case GameEventType.StationLayoutChanged:
                        return obj.objectiveType == QuestObjectiveType.PlaceStation;
                    case GameEventType.CustomerServed:
                        return obj.objectiveType == QuestObjectiveType.DeliverFood;
                }

                return false;
            });
            
            if (needsRecheck)
            {
                CheckAndCompleteQuest(quest.id);
            }
        }
    }
    
    #endregion
}