using System.Collections;
using UnityEngine;

/// <summary>
/// 레스토랑 운영 총괄
/// GameManager로부터 Operation Phase 신호를 받아 실제 운영을 담당
/// </summary>
public class RestaurantManager : Singleton<RestaurantManager>
{
    [Tooltip("하루 제한 시간 (초 단위)")]
    [SerializeField] private float roundTimeLimit = 300f;

    [Header("레이아웃")]
    [SerializeField] private Transform entrancePoint;
    [SerializeField] private Transform exitPoint;
    
    [Header("실시간 통계")]
    [SerializeField] private int customersServed;
    [SerializeField] private int customersVisited;
    [SerializeField] private int todayEarnings;
    
    [Header("Debug Info")]
    [SerializeField] private CustomerSpawner customerSpawner;
    [SerializeField] private bool showDebugInfo;
    
    // private fields
    private float currentRoundTime;
    private bool isTimerRunning;
    
    #region property & public methods
    
    public float CurrentRoundTime => currentRoundTime;
    public float RoundTimeLimit => roundTimeLimit;
    public int CustomersServed => customersServed;
    public int CustomersVisited => customersVisited;
    public int TodayEarnings => todayEarnings;
    
    public Vector3 GetEntrancePoint() => entrancePoint.position;
    public Vector3 GetExitPoint() => exitPoint.position;
    public void SpawnTutorialCustomer() => customerSpawner.SpawnTutorialCustomer();
    
    #endregion

    #region Unity events

    protected override void Awake()
    {
        base.Awake();
        if (!customerSpawner) customerSpawner = transform.GetComponent<CustomerSpawner>();

        EventBus.OnGameEvent += HandleGameEvent;
    }

    private void OnDestroy()
    {
        EventBus.OnGameEvent -= HandleGameEvent;
    }

    // 영업 중 타이머 계산
    private void Update()
    {
        if (GameManager.Instance.CurrentPhase != GamePhase.Operation
            || GameManager.Instance.IsTutorialActive || !isTimerRunning) return;
        
        currentRoundTime += Time.deltaTime;

        if (currentRoundTime >= roundTimeLimit)
        {
            currentRoundTime = roundTimeLimit; 
            isTimerRunning = false;
            EventBus.Raise(GameEventType.RoundTimerEnded);
        }
    }

    #endregion

    #region 이벤트 핸들링
    
    private void HandleGameEvent(GameEventType eventType, object data)
    {
        if (eventType == GameEventType.GamePhaseChanged)
        {
            if (data is GamePhase newPhase)
            {
                HandlePhaseChange(newPhase);
            }
        }
    }


    private void HandlePhaseChange(GamePhase newPhase)
    {
        switch (newPhase)
        {
            case GamePhase.Opening:
                InitializeDay();
                EventBus.Raise(UIEventType.ShowRoundTimer);
                break;

            case GamePhase.Operation:
                customerSpawner.StartSpawning();
                break;
            
            case GamePhase.Closing:
                customerSpawner.StopSpawning();
                StartCoroutine(CleanupAndNotify()); 
                break;
        }
    }
    
    /// <summary>
    /// 손님 퇴장 처리, 완료되면 시스템에 알림
    /// </summary>
    private IEnumerator CleanupAndNotify()
    {
        if (showDebugInfo) Debug.Log("[RestaurantManager] 모든 손님이 떠나길 기다리는 중...");
        
        TableManager.Instance.ReleaseAllQueues();
        yield return new WaitUntil(() => CustomerManager.Instance.ActiveCustomerCount == 0);
        
        if (showDebugInfo) Debug.Log("[RestaurantManager] 모든 손님이 퇴장 완료");
        EventBus.Raise(GameEventType.AllCustomersLeft);
    }

    #endregion

    #region 레스토랑 운영 로직

    /// <summary>
    /// 하루 영업 시작 전에 데이터 초기화
    /// </summary>
    private void InitializeDay()
    {
        customersServed = 0;
        customersVisited = 0;
        todayEarnings = 0;
        currentRoundTime = 0f;
        isTimerRunning = true;
        GameManager.Instance.BackupEarningsBeforeDayStart();
    }
    
    public void ReStartRestaurant()
    {
        InitializeDay();
        customersVisited = 1;
        customerSpawner.StartSpawning();
        StationManager.Instance.ResetAllStationsInteractable();
    }

    #endregion
    
    #region 데이터 변경 메서드

    public void OnCustomerEntered() => customersVisited++;
    public void OnCustomerServed() => customersServed++;

    public void AddDailyEarnings(int amount)
    {
        todayEarnings += amount;

        GameManager.Instance.AddMoney(amount);
        EventBus.Raise(UIEventType.UpdateTodayEarnings, todayEarnings);
    }

    #endregion
    
    #region Debug Commands
#if UNITY_EDITOR  
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 400, 300, 700));
        GUILayout.Space(10);
        
        if (GameManager.Instance.CurrentPhase != GamePhase.Operation)
        {
            if (GUILayout.Button("영업 시작"))
            {
                GameManager.Instance.ChangePhase(GamePhase.Operation);
            }
        }
        else
        {
            if (GUILayout.Button("영업 마감"))
            {
                currentRoundTime = roundTimeLimit;
            }
        }
        
        if (GUILayout.Button("영업 강제 종료"))
        {
            currentRoundTime = roundTimeLimit;
            CustomerManager.Instance.EmptyAllPatience();
        }
        
        if (GUILayout.Button("손님 1명 스폰"))
        {
            if (customerSpawner) customerSpawner.SpawnSingleCustomer();
        }
        
        // TODO: 이동 필요
        if (GUILayout.Button("모든 메뉴 해금"))
        {
            MenuManager.Instance.UnlockAllMenus();
        }
        
        GUILayout.EndArea();
    }
#endif    
    #endregion
}