using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum Expression
{
    Default,
    Happy,
    Sad,
    Angry,
    Surprised,
}

[System.Serializable]
public class PortraitEntry
{
    public Expression expression;
    public Sprite portrait;
}

[CreateAssetMenu(fileName = "SpeakerData", menuName = "Data/Speaker Data")]
public class SpeakerData : ScriptableObject
{
    public string id;
    public string speakerName;
    
    public List<PortraitEntry> portraits = new();
    private Dictionary<Expression, Sprite> portraitDict;
    private bool isDictInitialized;
    
    private void InitializeDictionary()
    {
        if (isDictInitialized) return;

        portraitDict = new Dictionary<Expression, Sprite>();
        foreach (var entry in portraits)
        {
            portraitDict[entry.expression] = entry.portrait;
        }
        isDictInitialized = true;
    }
    
    public Sprite GetPortraitByExpression(Expression expression)
    {
        InitializeDictionary();
        return portraitDict.TryGetValue(expression, out Sprite portrait) 
            ? portrait 
            : portraitDict.GetValueOrDefault(Expression.Default);
    }
}