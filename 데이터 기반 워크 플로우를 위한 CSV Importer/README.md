# 📑 CSV 기반 기획자 협업 & 데이터 파이프라인

**Unity + CSV 데이터 파이프라인**을 구축하여, **기획자/디자이너가 작성한 CSV 파일을 그대로 게임 데이터로 변환**하고,  
실시간으로 스토리, 대사, 퀘스트, 상점 데이터를 로드해 사용하는 구조입니다.

<img width="380" height="237" alt="image" src="https://github.com/user-attachments/assets/1c80ca9f-745b-4d05-a7f8-f67d2ba5787f" />


---

## 📂 폴더 구조

```
CSVSystem/
 ├── CSVImporter.cs         # 에디터 CSV → ScriptableObject 변환 툴
 ├── StoreDataManager.cs    # CSV 로드 후 상점 데이터 관리
 ├── DialogueManager.cs     # 대사 데이터 로드 및 표시
 ├── DialogueData.cs        # ScriptableObject: 대화
 ├── StoryData.cs           # ScriptableObject: 스토리
 ├── QuestData.cs           # ScriptableObject: 퀘스트
 ├── SpeakerData.cs         # ScriptableObject: 화자 초상화
```

---

## 1️⃣ 데이터 파이프라인 설계

### **1. CSV → ScriptableObject 변환 (에디터 툴)**

- `CSVImporter.cs`에서 CSV를 읽어 각 데이터 타입별 SO 생성

- **데이터**
  - Customer, Station, Food
  - Dialogue, Story, Quest
  - Speaker (화자/초상화)
    
- **특징**
  - CSV 컬럼 → ScriptableObject 필드 자동 매핑
  - 그룹 데이터(`Dialogue`, `Quest`)는 **ID 기준 그룹화 후 SO 하나로 생성**
  - Editor 메뉴에서 버튼 클릭 한 번으로 **일괄 갱신 가능**

```csharp
[MenuItem("Tools/Import Game Data/Dialogue Data")]
public static void ImportGroupedDialogueData()
{
    ImportGroupedData("Dialogue Data", "Dialogue", ProcessDialogueGroups);
}
```

---

### **2. ScriptableObject 기반 데이터 로딩**

1. **DialogueManager**
   - `Resources/Datas/Dialogue`에서 `DialogueData` 자동 로드
   - 화자(`SpeakerData`) 매칭 후 UI로 표시
   - Phase 전환과 EventBus 연계

2. **StoreDataManager**
   - `Resources/Datas/Store/StoreData.csv` 로드
   - 상점 아이템 Map 구성 → 잠금 조건/해금 설명 생성

3. **StoryManager & QuestManager 연계**
   - CSV에서 스토리 조건(`StoryCondition`)과 액션(`StoryAction`)을 정의
   - `StartQuest`, `ShowGuideUI`, `SpawnCustomer` 등 이벤트 자동화
   - **NoMoreStoriesInPhase → GamePhase 전환까지 연결**

---

### **3. 기획자 협업 프로세스**

1. **기획자**
   - Google Sheet에서 데이터 작성
   - CSV 내보내기
   - 파일명 예시:
     - `DialogueData.csv`
     - `QuestData.csv`
     - `StoryData.csv`

2. **프로그래머**
   - Unity Editor → `Tools/Import Game Data` 실행
   - CSV → ScriptableObject 자동 변환
   - Resources 폴더 내 SO 생성 후 바로 사용 가능

3. **장점**
   - 코드 수정 없이 기획자가 데이터 갱신 가능
   - 실시간으로 신규 콘텐츠 추가 가능 (스토리, 퀘스트)
   - 데이터와 로직 분리 → 유지보수 및 테스트 용이

---

## 2️⃣ 데이터 예시

**실제 DialogueData CSV 예시**

<img width="1716" height="399" alt="image" src="https://github.com/user-attachments/assets/17dbf274-5cd4-43b6-980f-68310b75e983" />


→ `CSVImporter` → `day1_daily (day1_daily.asset)` 생성

---

**실제 FoodData CSV 예시**

<img width="1439" height="661" alt="image" src="https://github.com/user-attachments/assets/d4b87854-229f-42b0-abdd-7fabf1071ec0" />
<img width="268" height="605" alt="image" src="https://github.com/user-attachments/assets/1f5d66de-2d4d-47fc-93fa-f2ec2864dec7" />


→ `CSVImporter` → `TomatoPastaData (TomatoPastaData.asset)` 등 생성

---

## 3️⃣ 코드 예시

**CSV → SO 변환 (공통)**
```csharp
private static void ImportData<T>(string csvName, Func<string[], T> parseFunc, string folderName, Func<string[], string> getFileNameFunc) where T : ScriptableObject
{
    string path = EditorUtility.OpenFilePanel($"Select {csvName} CSV", "", "csv");
    if (string.IsNullOrEmpty(path)) return;

    string fileContent = File.ReadAllText(path, Encoding.UTF8);
    List<string[]> allRows = ParseCsv(fileContent);

    string targetFolder = $"Assets/Resources/Datas/{folderName}/";
    Directory.CreateDirectory(targetFolder);

    for (int i = 1; i < allRows.Count; i++)
    {
        T data = parseFunc(allRows[i]);
        string fileName = getFileNameFunc(allRows[i]);
        string assetPath = $"{targetFolder}/{fileName}.asset";

        AssetDatabase.CreateAsset(data, assetPath);
    }
    AssetDatabase.SaveAssets();
}
```
