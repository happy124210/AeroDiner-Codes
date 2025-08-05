using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomerManager : Singleton<CustomerManager>
{
    [Header("손님 프리팹")]
    [SerializeField] private GameObject customerPrefab;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo;
    
    private CustomerData[] availableCustomerTypes;
    private readonly List<CustomerController> activeCustomers = new();

    public int ActiveCustomerCount => activeCustomers.Count;
    public IReadOnlyList<CustomerData> AvailableCustomerTypes => availableCustomerTypes;

    protected override void Awake()
    {
        base.Awake();
        LoadResourceData();
    }

    private void LoadResourceData()
    {
        customerPrefab = Resources.Load<GameObject>(StringPath.CUSTOMER_PREFAB_PATH);
        availableCustomerTypes = Resources.LoadAll<CustomerData>(StringPath.CUSTOMER_DATA_PATH);
    }
    
    public CustomerController SpawnCustomer(CustomerData data, Vector3 position, Quaternion rotation)
    {
        if (!customerPrefab) return null;

        GameObject customerObj = PoolManager.Instance.Get(customerPrefab, position, rotation);
        if (!customerObj.TryGetComponent<CustomerController>(out var controller))
        {
            PoolManager.Instance.Release(customerPrefab, customerObj);
            return null;
        }

        controller.Setup(data);
        activeCustomers.Add(controller);
        return controller;
    }

    public void DespawnCustomer(CustomerController customer)
    {
        if (!customer) return;
        activeCustomers.Remove(customer);
        PoolManager.Instance.Release(customerPrefab, customer.gameObject);
    }
    
    /// <summary>
    /// 현재 활성화된 모든 손님의 인내심 0으로 만들기
    /// </summary>
    [ContextMenu("Debug/모든 손님 인내심 제거")]
    public void EmptyAllPatience()
    {
        if (activeCustomers.Count == 0)
        {
            Debug.Log("[CustomerManager] 활성화된 손님이 없습니다.");
            return;
        }

        Debug.LogWarning($"[CustomerManager] {activeCustomers.Count}명의 모든 손님의 인내심을 제거합니다.");
        
        foreach (var customer in activeCustomers.ToList())
        {
            customer.EmptyPatience();
        }
    }

    #region 튜토리얼 관리

    /// <summary>
    /// 현재 레스토랑에 있는 첫 번째 손님이 'Eating' 상태인지 확인합니다.
    /// (튜토리얼처럼 손님이 한 명만 있는 상황에서 사용)
    /// </summary>
    /// <returns>조건 충족 시 true</returns>
    public bool IsFirstCustomerEating()
    {
        // 손님이 한 명도 없으면 false
        if (activeCustomers.Count == 0) return false;
        
        // activeCustomers 리스트의 첫 번째 손님
        var customer = activeCustomers[0];
        if (!customer)
        {
            return false;
        }

        // 해당 손님의 상태가 'Eating'인지 확인하여 결과 반환
        return customer.CurrentStateName == CustomerStateName.Eating;
    }

    #endregion

    /// <summary>
    /// 현재 활성화된 모든 손님을 풀로 반환
    /// </summary>
    [ContextMenu("Debug/모든 손님 풀로 반환")]
    public void DespawnAllCustomers()
    {
        if (activeCustomers.Count == 0)
        {
            if (showDebugInfo) Debug.Log("[CustomerManager] 활성화된 손님 없음");
            return;
        }

        Debug.LogWarning($"[CustomerManager] {activeCustomers.Count}명의 모든 손님을 풀로 반환합니다.");
        
        foreach (var customer in activeCustomers.ToList())
        {
            DespawnCustomer(customer);
        }
    }
}