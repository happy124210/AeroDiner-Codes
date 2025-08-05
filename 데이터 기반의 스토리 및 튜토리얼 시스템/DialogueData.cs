using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 대사 한 줄에 해당하는 정보 (화자, 표정, 텍스트)
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    public string speakerId;  // SpeakerData의 id
    public Expression expression; // Happy, Sad 등 초상화용
    public string text;       // 실제 대사 텍스트
    public DialoguePosition position; // 이 대사의 화자가 표시될 위치
}

public enum DialoguePosition
{
    Left,
    Right
}


/// <summary>
/// CSV의 dialogueData id 하나에 해당하는 전체 대화 묶음 데이터
/// </summary>
[CreateAssetMenu(fileName = "DialogueData", menuName = "Data/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("관리용 ID")]
    public string id;

    [Header("대화 내용")]
    public List<DialogueLine> lines;
}