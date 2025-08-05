using System.Collections.Generic;
using UnityEngine;

public class DialogueManager : Singleton<DialogueManager>
{
    [Header("Debug Info")]
    [SerializeField] public bool showDebugInfo;
    
    private Queue<DialogueLine> linesQueue;
    private DialogueData currentDialogue;
    
    private Dictionary<string, DialogueData> dialogueDatabase;
    private Dictionary<string, SpeakerData> speakerDatabase;
    
    protected override void Awake()
    {
        base.Awake();
        linesQueue = new Queue<DialogueLine>();
        
        LoadDialogueDatabase();
        LoadSpeakerDatabase();
    }

    #region 데이터베이스 관리

    private void LoadDialogueDatabase()
    {
        dialogueDatabase = new Dictionary<string, DialogueData>();
        
        DialogueData[] allDialogues = Resources.LoadAll<DialogueData>("Datas/Dialogue");
        
        foreach (DialogueData dialogue in allDialogues)
        {
            if (!dialogueDatabase.TryAdd(dialogue.id, dialogue))
            {
                Debug.LogWarning($"중복된 Dialogue ID 있음: {dialogue.id}");
            }
        }
        
        if (showDebugInfo) Debug.Log($"총 {dialogueDatabase.Count}개 Dialogue Database 로드 완료");
    }
    
    private void LoadSpeakerDatabase()
    {
        speakerDatabase = new Dictionary<string, SpeakerData>();
        SpeakerData[] allSpeakers = Resources.LoadAll<SpeakerData>("Datas/Speakers");

        foreach (var speaker in allSpeakers)
        {
            speakerDatabase.TryAdd(speaker.id, speaker);
        }
        if (showDebugInfo) Debug.Log($"Speaker Database 로드 완료: {speakerDatabase.Count}명");
    }
    
    public DialogueData FindDialogueDataById(string id)
    {
        return dialogueDatabase.GetValueOrDefault(id);
    }
    
    public SpeakerData FindSpeakerById(string id)
    {
        return speakerDatabase.GetValueOrDefault(id);
    }

    #endregion

    /// <summary>
    /// id값 받아서 새로운 대화 시작
    /// </summary>
    public void StartDialogue(string dataId)
    {
        var data = FindDialogueDataById(dataId);
        
        if (!data)
        {
            if (showDebugInfo) Debug.LogError("시작할 DialogueData 없음");
            // 대화 시작 실패 시, 이전 페이즈 복귀
            GameManager.Instance.ContinueGame();
            return;
        }
        
        GameManager.Instance.EnterDialogue();
        
        linesQueue.Clear();
        foreach (var line in data.lines)
        {
            linesQueue.Enqueue(line);
        }
        
        currentDialogue = data;
        EventBus.Raise(UIEventType.ShowDialoguePanel);
        RequestNextLine();
    }
    
    /// <summary>
    /// 다음 대사 요청
    /// 다음라인 있으면 이벤트로 string 넘겨줌, 없으면 끝
    /// </summary>
    public void RequestNextLine()
    {
        if (linesQueue.Count > 0)
        {
            DialogueLine lineToShow = linesQueue.Dequeue();
            GameManager.Instance.ChangePhase(GamePhase.Dialogue);
            EventBus.Raise(UIEventType.ShowDialogueLine, lineToShow);
        }
        else
        {
            EndDialogue();
        }
    }
    
    private void EndDialogue()
    {
        EventBus.Raise(UIEventType.HideDialoguePanel);
        EventBus.Raise(GameEventType.DialogueEnded, currentDialogue.id);
        GameManager.Instance.ExitDialogue();
    }
    
    public void SkipDialogue()
    {
        linesQueue.Clear(); // 남은 대사 삭제
        EndDialogue();      // 바로 종료 처리
    }
}