using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryManager : Singleton<StoryManager>
{
    [Header("Debug Info")]
    [SerializeField] bool showDebugInfo;
    
    private List<StoryData> storyDatabase;
    private HashSet<string> executedStoryIds = new();
    
    private bool isCurrentSceneUIReady;
    
    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this);
        LoadStoryDatabase();
        EventBus.OnGameEvent += HandleGameEvent;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        EventBus.OnGameEvent -= HandleGameEvent;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    
    private void OnActiveSceneChanged(Scene current, Scene next)
    {
        isCurrentSceneUIReady = false;
        if (showDebugInfo) Debug.Log($"[StoryManager] 씬 변경: {next.name}. UI 준비 상태 초기화");
    }

    private void LoadStoryDatabase()
    {
        storyDatabase = Resources.LoadAll<StoryData>(StringPath.STORY_DATA_PATH).ToList();
    }
    
    private void HandleGameEvent(GameEventType eventType, object data)
    {
        switch (eventType)
        {
            case GameEventType.GamePhaseChanged:
                GamePhase newPhase = (GamePhase)data;
                if (newPhase == GamePhase.Closing || newPhase == GamePhase.Paused || newPhase == GamePhase.Dialogue) return;
                
                if (showDebugInfo) Debug.Log($"[StoryManager] {newPhase} Phase 진입");
                CheckAndTriggerStory();
                break;

            case GameEventType.AllCustomersLeft:
                if (showDebugInfo) Debug.Log("[StoryManager] AllCustomersLeft 이벤트 수신, 마감 스토리 확인");
                CheckAndTriggerStory();
                break;
            
            case GameEventType.UISceneReady:
                isCurrentSceneUIReady = true;
                if (showDebugInfo) Debug.Log("[StoryManager] UISceneReady 이벤트 수신, 스토리 트리거 확인");
                CheckAndTriggerStory();
                break;
            
            case GameEventType.DialogueEnded:
                string endedDialogueId = data as string;
                if (showDebugInfo) Debug.Log($"[StoryManager] Dialogue '{endedDialogueId}' 종료. 다음 할 일을 확인합니다.");
                CheckAndTriggerStory(endedDialogueId);
                break;
            
            case GameEventType.QuestStatusChanged:
                if (showDebugInfo) Debug.Log("[StoryManager] QuestStatusChanged 이벤트 수신, 스토리 트리거 확인");
                CheckAndTriggerStory();
                break;
        }
    }
    
    /// <summary>
    /// 실행할 다음 스토리를 찾고 없으면 이벤트를 발생
    /// </summary>
    private void CheckAndTriggerStory(string endedDialogueId = null)
    {
        if (!isCurrentSceneUIReady)
        {
            if (showDebugInfo) Debug.Log("[StoryManager] UI가 아직 준비되지 않아 스토리 확인 보류");
            return;
        }

        var currentPhase = GameManager.Instance.CurrentPhase;
    
        var nextStory = storyDatabase.FirstOrDefault(story =>
        {
            if (executedStoryIds.Contains(story.id)) return false;

            bool isPhaseCompatible = (story.triggerPhase == currentPhase) || 
                                     (story.triggerPhase == GamePhase.None && endedDialogueId != null);

            return isPhaseCompatible && AreConditionsMet(story.conditions, endedDialogueId);
        });

        if (nextStory)
        {
            if (showDebugInfo) Debug.Log($"[StoryManager] 다음 스토리 트리거: {nextStory.id}");
            executedStoryIds.Add(nextStory.id);
            StartCoroutine(ExecuteActions(nextStory.actions));
        }
        else 
        {
            if (showDebugInfo) Debug.Log($"[StoryManager] {currentPhase} Phase에서 실행할 스토리가 더 이상 없음");
            EventBus.Raise(GameEventType.NoMoreStoriesInPhase, currentPhase);
        }
    }
    
    private IEnumerator ExecuteActions(List<StoryAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action.storyType)
            {
                case StoryType.StartDialogue:
                    DialogueManager.Instance.StartDialogue(action.targetId);
                    break;
                case StoryType.StartQuest:
                    QuestManager.Instance.StartQuest(action.targetId);
                    break;
                case StoryType.EndQuest:
                    QuestManager.Instance.EndQuest(action.targetId);
                    break;
                case StoryType.GiveMoney:
                    GameManager.Instance.AddMoney(int.Parse(action.value));
                    break;
                case StoryType.SetMoney:
                    GameManager.Instance.SetMoney(int.Parse(action.value));
                    break;
                case StoryType.ChangeGamePhase:
                    GameManager.Instance.ChangePhase((GamePhase)Enum.Parse(typeof(GamePhase), action.targetId));
                    break;
                
                // 튜토리얼용
                case StoryType.ShowGuideUI:
                    UIEventCaller.CallUIEvent(action.targetId);
                    break;
                case StoryType.ActivateStation:
                    StationManager.Instance.ActivateStation(action.targetId, bool.Parse(action.value));
                    break;
                case StoryType.SetTutorialMode:
                    bool tutorialState = bool.Parse(action.targetId);
                    GameManager.Instance.SetTutorialMode(tutorialState);
                    break;
                case StoryType.SpawnCustomer:
                    RestaurantManager.Instance.SpawnTutorialCustomer();
                    break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 주어진 모든 조건이 충족되었는지 확인
    /// </summary>
    private bool AreConditionsMet(List<StoryCondition> conditions, string endedDialogueId = null)
    {
        if (conditions == null || !conditions.Any()) return true;

        foreach (var c in conditions)
        {
            bool result = false;
            switch (c.conditionType)
            {
                case ConditionType.Day:
                    result = CheckNumericCondition(GameManager.Instance.CurrentDay, c.@operator, c.rValue);
                    break;
                case ConditionType.QuestStatus:
                    result = CheckQuestStatusCondition(c.lValue, c.@operator, c.rValue);
                    break;
                case ConditionType.DialogueEnded:
                    result = (c.lValue == endedDialogueId);
                    break;
            }
            if (!result) return false;
        }
        
        return true;
    }

    public void ResetStoryData()
    {
        executedStoryIds =  new HashSet<string>();
    }
    
    #region helper
    
    /// <summary>
    /// 숫자 값을 연산자 기반으로 비교하는 헬퍼 메서드
    /// </summary>
    private bool CheckNumericCondition(int currentValue, string op, string requiredValueStr)
    {
        if (!int.TryParse(requiredValueStr, out int requiredValue))
        {
            Debug.LogWarning($"[StoryManager] 숫자 값 비교 실패: '{requiredValueStr}' 유효하지 않음");
            return false;
        }

        switch (op)
        {
            case "==": return currentValue == requiredValue;
            case "!=": return currentValue != requiredValue;
            case ">":  return currentValue > requiredValue;
            case ">=": return currentValue >= requiredValue;
            case "<":  return currentValue < requiredValue;
            case "<=": return currentValue <= requiredValue;
            default:
                Debug.LogWarning($"[StoryManager] 알 수 없는 연산자: '{op}'");
                return false;
        }
    }
    
    /// <summary>
    /// 퀘스트 상태를 연산자 기반으로 비교하는 헬퍼 메서드
    /// </summary>
    private bool CheckQuestStatusCondition(string questId, string op, string requiredStatusStr)
    {
        QuestStatus currentStatus = QuestManager.Instance.GetQuestStatus(questId);
        
        switch (op)
        {
            case "==":
                return currentStatus.ToString().Equals(requiredStatusStr, StringComparison.OrdinalIgnoreCase);
            case "!=":
                return !currentStatus.ToString().Equals(requiredStatusStr, StringComparison.OrdinalIgnoreCase);
            default:
                Debug.LogWarning($"[StoryManager] 퀘스트 상태에 사용할 수 없는 연산자: '{op}'");
                return false;
        }
    }
    
    #endregion
}