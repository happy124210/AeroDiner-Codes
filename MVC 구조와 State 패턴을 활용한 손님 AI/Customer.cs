using System;

/// <summary>
/// 고객의 모든 데이터를 관리하는 Model
/// View나 Controller에 대한 정보는 알지 못함
/// </summary>
public class Customer
{
    // Events
    public event Action<float> OnPatienceChanged; // 인내심 줄어들고 있음
    public event Action<FoodData> OnOrderPlaced; // 주문함
    public event Action<FoodData> OnMenuServed; // 서빙됨
    public event Action OnEating; // 먹기 시작함
    public event Action OnPayment; // 결제 끝남
    public event Action OnLeaving; // 자리에서 일어남
    
    // 데이터 컨테이너
    private CustomerData _data;
    private CustomerRuntimeData runtimeData;
    
    // === Properties & helper ===
    public CustomerData Data => _data;
    public CustomerRuntimeData RuntimeData => runtimeData;
    public float GetPatienceRatio() => runtimeData.CurrentPatience / _data.waitTime;

    public void Initialize(CustomerData data)
    {
        _data = data;
        runtimeData = new CustomerRuntimeData(data.waitTime, data.eatTime);
    }
    
    // 인내심시간 변경, 알림 보내기
    public void UpdatePatience(float newPatience)
    {
        runtimeData.CurrentPatience = newPatience;
        OnPatienceChanged?.Invoke(GetPatienceRatio());
    }
    
    // 주문하기
    public void PlaceOrder()
    {
        runtimeData.CurrentOrder = MenuManager.Instance.GetRandomMenu();
        OnOrderPlaced?.Invoke(runtimeData.CurrentOrder);
    }
    
    public void ResetPatience()
    {
        UpdatePatience(_data.waitTime);
    }

    // 앉는 테이블 번호 받기
    public void SetAssignedTable(Table assignedTable)
    {
        runtimeData.AssignedTable = assignedTable;
    }
    
    // 음식 받기
    public void ReceiveFood(FoodData servedMenu)
    {
        if (servedMenu == RuntimeData.CurrentOrder)
            OnMenuServed?.Invoke(servedMenu);
    }

    public void EatFood()
    {
        runtimeData.AssignedTable.GetCurrentFood().isPickupable = false;
        OnEating?.Invoke();
    }

    public void PayMoney()
    {
        OnPayment?.Invoke();
    }

    public void LeaveSeat()
    {
        OnLeaving?.Invoke();
        RuntimeData.AssignedTable = null;
        RuntimeData.CurrentOrder = null;
    }
}