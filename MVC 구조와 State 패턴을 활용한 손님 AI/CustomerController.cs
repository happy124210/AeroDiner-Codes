using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Model과 View를 연결하고 상태 관리 및 외부 시스템과의 상호작용을 담당
/// </summary>
public class CustomerController : MonoBehaviour, IPoolable
{
    [Header("Movement")]
    [SerializeField] private NavMeshAgent navAgent;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo;
    public CustomerStateName CurrentStateName => currentState?.Name ?? default;

    // 상태 관리
    private Customer model;
    private CustomerView view;
    private CustomerState currentState; // 상태머신의 State

    private bool isTutorialMode;
    
    #region Unity Functions
    
    private void Awake()
    {
        model = new Customer();
        view = GetComponent<CustomerView>();
        navAgent = GetComponent<NavMeshAgent>();
        
        if (!view) Debug.LogError($"[CustomerController]: {gameObject.name} CustomerView 없음 !!!");
        if (!navAgent) Debug.LogError($"[CustomerController]: {gameObject.name} NavMeshAgent 없음 !!!");
    }
    
    private void Update()
    {
        if (currentState == null) return;
        
        UpdateTimers(Time.deltaTime);
        UpdateAnimation();
        
        var nextState = currentState?.Update(this);
        if (nextState != null && nextState.Name != currentState.Name)
        {
            ChangeState(nextState);
        }
    }
    
    private void UpdateTimers(float deltaTime)
    {
        // 인내심 타이머
        if (ShouldPatienceDecrease() && !isTutorialMode)
        {
            float newPatience = Mathf.Max(0, model.RuntimeData.CurrentPatience - deltaTime);
            model.UpdatePatience(newPatience);
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromModelEvents();
    }
    
    #endregion

    private void ChangeState(CustomerState newState)
    {
        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 상태 변경: {currentState?.Name} -> {newState?.Name}");
        
        currentState?.Exit(this);
        currentState = newState;
        currentState?.Enter(this);
    }
    
    #region 초기화 & 이벤트 구독
    public void Setup(CustomerData data)
    {
        model.Initialize(data);
        view.Initialize(data);
        isTutorialMode = false;
        
        SetupNavMeshAgent(model.Data.speed);
        SubscribeToModelEvents();
        ChangeState(new MovingToEntranceState());

        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 초기 설정 완료 - {data.customerName}");
    }

    private void SubscribeToModelEvents()
    {
        model.OnPatienceChanged += HandlePatienceChanged;
        model.OnOrderPlaced += HandleOrderPlaced;
        model.OnMenuServed += HandleMenuServed;
        model.OnEating += HandleEating;
        model.OnPayment += HandlePayment;
        model.OnLeaving += HandleLeaving;
    }

    private void UnsubscribeFromModelEvents()
    {
        model.OnPatienceChanged -= HandlePatienceChanged;
        model.OnOrderPlaced -= HandleOrderPlaced;
        model.OnMenuServed -= HandleMenuServed;
        model.OnEating -= HandleEating;
        model.OnPayment -= HandlePayment;
        model.OnLeaving -= HandleLeaving;
    }
    
    private void SetupNavMeshAgent(float speed)
    {
        if (!navAgent) return;
        
        navAgent.updateRotation = false;
        navAgent.updateUpAxis = false;
        navAgent.speed = speed;
        navAgent.stoppingDistance = 0.1f;
        navAgent.angularSpeed = 120f;
        navAgent.acceleration = 8f;
    }
    #endregion
    
    #region Model Event Handlers (Controller가 Model 변경사항받고 model / view 조정)
    
    private void HandlePatienceChanged(float currentPatience)
    {
        view.UpdatePatienceUI(model.GetPatienceRatio());
    }

    private void HandleOrderPlaced(FoodData order)
    {
        view.SetPatienceVisibility(true);
        view.ShowOrderBubble(order);
        
        RestaurantManager.Instance.OnCustomerEntered();
        EventBus.Raise(UIEventType.ShowOrderPanel, model);
        
        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 주문 접수됨!");
    }

    private void HandleMenuServed(FoodData servedMenu)
    {
        view.SetPatienceVisibility(false);
        view.ShowServedEffect();
        
        MenuManager.Instance.OnMenuServed(servedMenu.id);
        EventBus.Raise(UIEventType.HideOrderPanel, model);
        
        ChangeState(new EatingState());
        
        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 음식 서빙됨!");
    }

    private void HandleEating()
    {
        view.ShowEatingEffect(() => 
        {
            ChangeState(new PayingState());
        });
        
        EventBus.Raise(GameEventType.CustomerServed);
    }

    private void HandlePayment()
    {
        view.ShowPayEffect();
        
        RestaurantManager.Instance.AddDailyEarnings(GetCurrentOrder().foodCost);
        RestaurantManager.Instance.OnCustomerServed();
    }

    private void HandleLeaving()
    {
        Vector3 exit = RestaurantManager.Instance.GetExitPoint();
        SetDestination(exit);
        SetAnimationState(CustomerAnimState.Walking);
        
        view.SetPatienceVisibility(false);
        
        TableManager.Instance.ReleaseSeat(this);
        TableManager.Instance.RemoveCustomerFromQueue(this);
        EventBus.Raise(UIEventType.HideOrderPanel, model);
    }
    
    public void RequestDespawn() => CustomerManager.Instance.DespawnCustomer(this);
    
    #endregion

    #region 외부 연결 함수
    
    // model
    public void PlaceOrder() => model.PlaceOrder();
    public void ResetPatience() => model.ResetPatience();
    public void ReceiveFood(FoodData servedMenu) =>  model.ReceiveFood(servedMenu);
    public void EatFood() => model.EatFood();
    public void SetAssignedTable(Table table) => model.SetAssignedTable(table);
    public void ProcessPayment() => model.PayMoney();
    public void LeaveSeat() => model.LeaveSeat();
    
    // view
    public void SetPatienceVisibility(bool isActive) => view.SetPatienceVisibility(isActive);
    public void ShowAngryEffect() => view.ShowAngryEffect();
    
    // state 전환
    public void MoveToAssignedSeat() => ChangeState(new MovingToSeatState());
    public void ForceLeave() => ChangeState(new AngryLeavingState());
    
    #endregion

    #region IPoolable Implementation
    public void OnGetFromPool()
    {
        
        
        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 풀에서 초기화 완료");
    }

    public void OnReturnToPool()
    {
        // 정리 작업
        currentState = null;
        UnsubscribeFromModelEvents();
        view.Cleanup();
        
        // NavMesh 정리
        if (navAgent && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }
        
        gameObject.SetActive(false);
        if (showDebugInfo) Debug.Log($"[CustomerController]: {gameObject.name} 풀로 반환");
    }
    #endregion
    
    #region Movement
    public void SetDestination(Vector3 destination)
    {
        if (!navAgent) return;
        
        navAgent.enabled = true;
        navAgent.isStopped = false;
        navAgent.SetDestination(destination);
    }

    public void StopMovement()
    {
        if (!navAgent || !navAgent.isOnNavMesh) return;
        
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
    }

    public bool HasReachedDestination()
    {
        if (!navAgent || navAgent.pathPending || navAgent.enabled == false) return false;
        if (navAgent.remainingDistance > navAgent.stoppingDistance) return false;
        
        return !navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f;
    }
    
    public void AdjustToSeatPosition()
    {
        navAgent.enabled = false;
        transform.position = GetSeatPosition();
    }
    
    public void AdjustToStopPosition()
    {
        navAgent.enabled = false;
        transform.position = GetStopPosition();
    }
    
    private void UpdateAnimation()
    {
        if (!navAgent || !view) return;
        
        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        Vector2 direction = new Vector2(localVelocity.x, localVelocity.z).normalized;
    
        view.UpdateAnimationDirection(direction);
    }
    public void SetAnimationState(CustomerAnimState state)
    {
        view.SetAnimationState(state);
    }
    
    #endregion
    
    # region property & public methods
    
    public CustomerData CustomerData => model.Data;
    public Table GetAssignedTable() => model.RuntimeData.AssignedTable;
    public FoodData GetCurrentOrder() => model.RuntimeData.CurrentOrder;
    public Vector3 GetStopPosition() => GetAssignedTable().GetStopPoint();
    public Vector3 GetSeatPosition() => GetAssignedTable().GetSeatPoint();
    public bool HasPatience() => model.RuntimeData.CurrentPatience > 0;
    public void EmptyPatience() => model.UpdatePatience(0f);
    public void SetTutorialMode(bool value) => isTutorialMode = value;
    
    private bool ShouldPatienceDecrease() => currentState != null && (currentState.Name == CustomerStateName.Ordering || currentState.Name == CustomerStateName.WaitingInLine);
    #endregion
}