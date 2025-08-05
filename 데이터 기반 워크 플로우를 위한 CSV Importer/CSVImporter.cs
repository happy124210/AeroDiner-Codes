using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// 게임 데이터베이스 CSV 임포터
/// 손님, 레시피, 음식/재료 데이터를 CSV에서 ScriptableObject로 변환
/// </summary>
public class CSVImporter
{
    
    #region CustomerData 생성

    [MenuItem("Tools/Import Game Data/Customer Data")]
    public static void ImportCustomerData()
    {
        ImportData("CustomerData", ParseCustomerData, "Customer", cols => ToPascalDataName(cols[1].Trim()));
    }

    private static CustomerData ParseCustomerData(string[] cols)
    {
        var data = ScriptableObject.CreateInstance<CustomerData>();
        
        data.id = cols[0].Trim();
        data.customerName = cols[1].Trim();
        data.displayName = cols[2].Trim();
        data.speed = float.Parse(cols[3]);
        data.waitTime = float.Parse(cols[4]);
        data.eatTime = float.Parse(cols[5]);
        
        return data;
    }
    
    #endregion

    #region StationData 생성

    [MenuItem("Tools/Import Game Data/Station Data")]
    public static void ImportStationData()
    {
        ImportData("StationData", ParseStationData, "Station", cols => ToPascalDataName(cols[1].Trim()));
    }

    private static StationData ParseStationData(string[] cols)
    {
        var data = ScriptableObject.CreateInstance<StationData>();

        data.id = cols[0].Trim();
        data.stationName = cols[1].Trim();
        data.displayName = cols[2].Trim();
        data.stationType = (StationType)Enum.Parse(typeof(StationType), cols[3].Trim());
        data.workType = (WorkType)Enum.Parse(typeof(WorkType), cols[4].Trim());
        data.stationIcon = LoadIcon($"{data.stationName}", "Station");
        data.description = cols[5].Trim();

        return data;
    }

    #endregion

    #region FoodData 생성

    [MenuItem("Tools/Import Game Data/Food Data")]
    public static void ImportFoodData()
    {
        ImportData("FoodData", ParseFoodData, "Food", cols => ToPascalDataName(cols[1].Trim()));
    }

    private static FoodData ParseFoodData(string[] cols)
    {
        var data = ScriptableObject.CreateInstance<FoodData>();
        
        data.id = cols[0].Trim();
        data.foodName = cols[1].Trim();
        data.displayName = cols[2].Trim();
        data.foodType = (FoodType)Enum.Parse(typeof(FoodType), cols[3].Trim());
        data.foodIcon = LoadIcon($"{data.foodName}", "Food"); 
        data.description = cols[4].Trim();
        data.stationType = ParseEnumArray<StationType>(cols[5]);
        data.ingredients = ParseStringArray(cols[6]);
        data.cookTime = float.Parse(cols[7]);
        data.foodCost = int.Parse(cols[8]);
        data.recipeDescription = cols[9].Replace("\\n", "\n");
        
        return data;
    }
    
    #endregion

    #region DialogueData 생성

    [MenuItem("Tools/Import Game Data/Dialogue Data")]
    public static void ImportGroupedDialogueData()
    {
        // 그룹 공통 메서드 호출
        ImportGroupedData("Dialogue Data", "Dialogue", ProcessDialogueGroups);
    }
    
    private static Dictionary<string, DialogueData> ProcessDialogueGroups(Dictionary<string, List<string[]>> groupedById, string targetFolder)
    {
        var dialogueDataMap = new Dictionary<string, DialogueData>();

        // 에셋 생성
        foreach (var dialogueId in groupedById.Keys)
        {
            string assetPath = $"{targetFolder}/{dialogueId}.asset";
            DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }
            dialogueDataMap[dialogueId] = data;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 데이터 채우기
        foreach (var pair in groupedById)
        {
            string dialogueId = pair.Key;
            List<string[]> rows = pair.Value;
            DialogueData data = dialogueDataMap[dialogueId];

            data.id = dialogueId;
            data.lines = new List<DialogueLine>();
            
            rows.Sort((a, b) => int.Parse(a[1]).CompareTo(int.Parse(b[1])));
            
            foreach (var cols in rows)
            {
                Enum.TryParse<Expression>(cols[3].Trim().Trim('"'), true, out var parsedExpression);
                Enum.TryParse<DialoguePosition>(cols[4].Trim().Trim('"'), true, out var parsedPosition); // position 파싱 추가
            
                string processedText = cols[6].Trim().Trim('"').Replace("\\n", "\n");

                data.lines.Add(new DialogueLine
                {
                    speakerId = cols[2].Trim(),
                    text = processedText,
                    expression = parsedExpression,
                    position = parsedPosition
                });
            }
            EditorUtility.SetDirty(data);
        }

        return dialogueDataMap;
    }
    
    #endregion

    #region SpeakerData 생성

    [MenuItem("Tools/Import Game Data/Speaker Data")]
    public static void ImportSpeakerData()
    {
        string path = EditorUtility.OpenFilePanel("Select Speaker CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= 1)
        {
            Debug.LogWarning("CSV 파일에 데이터 없음");
            return;
        }

        string targetFolder = "Assets/Resources/Datas/Speakers/";
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        
        // 에셋 생성
        foreach (var line in lines.Skip(1))
        {
            string[] cols = line.Split(',');
            if (cols.Length < 2 || string.IsNullOrEmpty(cols[0].Trim())) continue;
            string id = cols[0].Trim();
            string assetPath = $"{targetFolder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<SpeakerData>(assetPath) == null)
            {
                var newAsset = ScriptableObject.CreateInstance<SpeakerData>();
                AssetDatabase.CreateAsset(newAsset, assetPath);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 데이터 채우기 및 초상화 연결
        int successCount = 0;
        foreach (var line in lines.Skip(1))
        {
            try
            {
                string[] cols = line.Split(',');
                if (cols.Length < 2 || string.IsNullOrEmpty(cols[0].Trim())) continue;

                string id = cols[0].Trim();
                string assetPath = $"{targetFolder}/{id}.asset";
                SpeakerData data = AssetDatabase.LoadAssetAtPath<SpeakerData>(assetPath);
                
                data.id = id;
                data.speakerName = cols[1].Trim();
                data.portraits = new List<PortraitEntry>(); 
                
                // 초상화 연결
                foreach (Expression expression in Enum.GetValues(typeof(Expression)))
                {
                    string portraitPath = $"Icons/Portrait/{id}_{expression}";
                    Sprite portraitSprite = Resources.Load<Sprite>(portraitPath);

                    if (portraitSprite == null) continue;
                    
                    data.portraits.Add(new PortraitEntry 
                    { 
                        expression = expression, 
                        portrait = portraitSprite 
                    });
                }
                
                EditorUtility.SetDirty(data);
                successCount++;
            }
            catch (Exception e)
            {
                Debug.LogError($"Speaker ID '{lines[successCount + 1].Split(',')[0]}' 처리 중 오류 발생: {e.Message}");
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"총 {successCount}명의 SpeakerData CSV 임포트 완료");
    }

    #endregion
    
    #region StoryData 생성

    [MenuItem("Tools/Import Game Data/Story Data")]
    public static void ImportStoryData()
    {
        ImportData("StoryData", ParseStoryData, "Story", cols => cols[0].Trim());
    }
    
    private static StoryData ParseStoryData(string[] cols)
    {
        var data = ScriptableObject.CreateInstance<StoryData>();
        string id = cols[0].Trim();
        
        data.id = id;
        
        if (Enum.TryParse<GamePhase>(cols[1].Trim(), true, out var phase))
        {
            data.triggerPhase = phase;
        }
        else
        {
            Debug.LogWarning($"Story ID '{id}'의 triggerPhase '{cols[1].Trim()}'없음");
            data.triggerPhase = GamePhase.Day;
        }
        
        data.conditions = ParseConditions(cols[2]); // 조건 파싱
        data.actions = ParseActions(cols[3]);     // 액션 파싱

        return data;
    }

    private static List<StoryCondition> ParseConditions(string conditionsString)
    {
        var result = new List<StoryCondition>();
        if (string.IsNullOrEmpty(conditionsString)) return result;

        string[] conditionParts = conditionsString.Split('|');
        foreach (var part in conditionParts)
        {
            string[] attrs = part.Split(';').Select(s => s.Trim()).ToArray();
            if (attrs.Length < 2) continue;

            if (Enum.TryParse<ConditionType>(attrs[0], true, out var type))
            {
                var condition = new StoryCondition { conditionType = type };
                switch (type)
                {
                    case ConditionType.Day:
                        condition.@operator = attrs[1];
                        condition.rValue = attrs[2];
                        break;
                    case ConditionType.QuestStatus:
                        condition.lValue = attrs[1];
                        condition.@operator = attrs[2];
                        condition.rValue = attrs[3];
                        break;
                    case ConditionType.DialogueEnded:
                        condition.@operator = attrs[1];
                        condition.lValue = attrs[2];
                        break;
                }
                result.Add(condition);
            }
        }
        return result;
    }

    private static List<StoryAction> ParseActions(string actionsString)
    {
        var result = new List<StoryAction>();
        if (string.IsNullOrEmpty(actionsString)) return result;

        string[] actionParts = actionsString.Split('|');
        foreach (var part in actionParts)
        {
            string[] attrs = part.Split(';').Select(s => s.Trim()).ToArray();
            if (attrs.Length < 1) continue;

            if (Enum.TryParse<StoryType>(attrs[0], true, out var type))
            {
                var action = new StoryAction { storyType = type };
                switch (type)
                {
                    case StoryType.StartDialogue:
                    case StoryType.StartQuest:
                    case StoryType.UnlockRecipe:
                    case StoryType.UnlockStation:
                        action.targetId = attrs.Length > 1 ? attrs[1] : "";
                        break;
                    case StoryType.GiveMoney:
                    case StoryType.SetMoney:
                        action.targetId = "";
                        action.value = attrs.Length > 1 ? attrs[1] : "0";
                        break;
                }
                result.Add(action);
            }
        }
        return result;
    }

    #endregion

    #region QuestData 생성

    [MenuItem("Tools/Import Game Data/Quest Data")]
    public static void ImportQuestData()
    {
        ImportGroupedData("Quest Data", "Quest", ProcessQuestGroups);
    }
    
    private static Dictionary<string, QuestData> ProcessQuestGroups(Dictionary<string, List<string[]>> groupedById, string targetFolder)
    {
        var questDataMap = new Dictionary<string, QuestData>();

        foreach (var pair in groupedById)
        {
            string id = pair.Key;
            List<string[]> rows = pair.Value;
            
            // 딕셔너리에서 기존 퀘스트 데이터를 찾거나, 없으면 새로 생성/로드
            string assetPath = $"{targetFolder}/{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<QuestData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<QuestData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }
            questDataMap[id] = data;
            
            // 퀘스트 기본 정보는 첫 번째 줄 데이터를 사용
            string[] firstRow = rows.First();
            data.id = id;
            data.questName = firstRow[1].Trim();
            data.description = firstRow[2].Trim();
            data.rewardDescription = firstRow[3].Trim();
            data.rewardMoney = int.TryParse(firstRow[7], out int money) ? money : 0;
            data.rewardItemIds = string.IsNullOrEmpty(firstRow[8]) ? Array.Empty<string>() : firstRow[8].Split('|').Select(s => s.Trim()).ToArray();
            
            // 목표(objectives) 정보 채우기
            data.objectives = new List<QuestObjective>();
            foreach (var cols in rows)
            {
                if (cols.Length < 7 || !Enum.TryParse<QuestObjectiveType>(cols[5].Trim(), true, out var type)) continue;
                
                string[] objectiveParts = cols[6].Split(';');
                var objective = new QuestObjective
                {
                    description = cols[4].Trim(),
                    objectiveType = type,
                    targetId = objectiveParts.Length > 0 ? objectiveParts[0].Trim() : "",
                    requiredIds = objectiveParts.Length > 1 && !string.IsNullOrEmpty(objectiveParts[1]) 
                        ? objectiveParts[1].Split('|').Select(s => s.Trim()).ToArray() 
                        : Array.Empty<string>()
                };
                data.objectives.Add(objective);
            }
            EditorUtility.SetDirty(data);
        }
        return questDataMap;
    }

    #endregion
    
    #region 공통 Import 메서드
    
    /// <summary>
    /// CSV 한 줄당 하나의 에셋을 생성/업데이트하는 제네릭 데이터 임포트 메서드
    /// </summary>
    private static void ImportData<T>(string csvName, Func<string[], T> parseFunc, string folderName, Func<string[], string> getFileNameFunc) where T : ScriptableObject
    {
        string path = EditorUtility.OpenFilePanel($"Select {csvName} CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;
        
        // 따옴표와 줄바꿈을 지원하는 파서 사용
        string fileContent = File.ReadAllText(path, Encoding.UTF8);
        List<string[]> allRows = ParseCsv(fileContent);

        if (allRows.Count <= 1) 
        {
            Debug.LogWarning("CSV has no data.");
            return;
        }
        
        string targetFolder = $"Assets/Resources/Datas/{folderName}/";
        if (!Directory.Exists(targetFolder)) 
        {
            Directory.CreateDirectory(targetFolder);
        }
        
        int successCount = 0;
        for (int i = 1; i < allRows.Count; i++)
        {
            try
            {
                string[] cols = allRows[i];
                if (cols.Length < 2 || string.IsNullOrEmpty(cols[0].Trim())) continue;

                T data = parseFunc(cols);
                string fileName = getFileNameFunc(cols);
                string assetPath = $"{targetFolder}/{fileName}.asset";
                
                data.name = fileName;

                var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(data, existing);
                    
                    if (existing.name != fileName)
                    {
                        existing.name = fileName;
                    }
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(data, assetPath);
                }
                successCount++;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1}: {string.Join(",", allRows[i])}\nError: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{csvName} CSV import completed! {successCount} items processed.");
    }
    
    /// <summary>
    /// CSV 여러 줄을 그룹화하여 하나의 에셋을 생성/업데이트하는 제네릭 데이터 임포트 메서드
    /// </summary>
    private static void ImportGroupedData<T>(string csvName, string folderName, 
        Func<Dictionary<string, List<string[]>>, string, Dictionary<string, T>> processFunc) where T : ScriptableObject
    {
        string path = EditorUtility.OpenFilePanel($"Select {csvName} CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        string fileContent = File.ReadAllText(path, Encoding.UTF8);
        List<string[]> allRows = ParseCsv(fileContent);
        
        if (allRows.Count <= 1)
        {
            Debug.LogWarning("CSV 파일에 데이터가 없습니다.");
            return;
        }

        var groupedById = new Dictionary<string, List<string[]>>();
        for (int i = 1; i < allRows.Count; i++)
        {
            string[] cols = allRows[i];
            if (cols.Length < 1 || string.IsNullOrEmpty(cols[0])) continue;

            string id = cols[0].Trim();
            if (!groupedById.ContainsKey(id))
            {
                groupedById[id] = new List<string[]>();
            }
            groupedById[id].Add(cols);
        }
        
        string targetFolder = $"Assets/Resources/Datas/{folderName}/";
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        
        // 각 데이터 타입에 맞는 그룹 처리 로직 호출
        Dictionary<string, T> processedData = processFunc(groupedById, targetFolder);
        
        foreach (var data in processedData.Values)
        {
            EditorUtility.SetDirty(data);
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"총 {processedData.Count}개의 {csvName} CSV 임포트/업데이트 완료");
    }

    #endregion
    
    #region 유틸리티 메서드들
    
    /// <summary>
    /// 이름을 PascalCase + "Data" 형태의 파일명으로 변환
    /// ex: "some_food_name" -> "SomeFoodNameData"
    /// </summary>
    private static string ToPascalDataName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "UnknownData";
        
        string[] parts = name.Split(new [] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        string pascal = string.Concat(parts.Select(part =>
        {
            if (string.IsNullOrEmpty(part)) return "";
            return char.ToUpper(part[0]) + part.Substring(1).ToLower();
        }));

        return pascal + "Data";
    }
    
    /// <summary>
    /// Resources 폴더에서 아이콘 로드
    /// </summary>
    private static Sprite LoadIcon(string name, string category = "")
    {
        if (string.IsNullOrEmpty(name)) return null;
        string iconName = name.PascalToSnake();
        
        string path = string.IsNullOrEmpty(category) 
            ? $"Icons/{iconName.Trim()}" 
            : $"Icons/{category}/{iconName.Trim()}";
            
        Sprite icon = Resources.Load<Sprite>(path);
        if (icon == null)
        {
            Debug.LogWarning($"[LoadIcon] Icon not found: Resources/{path}");
        }
        return icon;
    }
    
    private static string[] ParseStringArray(string value)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
        return value.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
    }
    
    public static float[] ParseFloatArray(string value)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<float>();
        return value.Split('|').Select(s => float.Parse(s.Trim())).ToArray();
    }

    private static TEnum[] ParseEnumArray<TEnum>(string value) where TEnum : struct
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<TEnum>();
        
        return value.Split('|')
            .Select(t => t.Trim())
            .Where(t => Enum.TryParse<TEnum>(t, true, out _))
            .Select(t => (TEnum)Enum.Parse(typeof(TEnum), t, true))
            .ToArray();
    }

    private static List<string[]> ParseCsv(string fileContent)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        StringBuilder currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < fileContent.Length; i++)
        {
            char c = fileContent[i];

            if (c == '\r') continue;

            if (inQuotes)
            {
                if (c == '"' && i + 1 < fileContent.Length && fileContent[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }
    
    #endregion
}

#endif