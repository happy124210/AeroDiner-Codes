using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TableManager : Singleton<TableManager>
{
    [Header("테이블 설정")]
    [SerializeField] private Table[] tables;
    [SerializeField] private bool[] seatOccupied;
    
    [Header("줄서기 설정")]
    [SerializeField] private Transform queueStartPosition;
    [SerializeField] private float queueSpacing = -1f;
    [SerializeField] private int maxQueueLength = 6;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // 줄서기 큐
    private Queue<CustomerController> waitingQueue = new();
    private readonly Dictionary<CustomerController, Vector3> customerQueuePositions = new();

    #region Unity Events

    protected override void Awake()
    {
        base.Awake();
        InitializeTables();
    }

    #endregion

    #region 초기화

    private void InitializeTables()
    {
        tables = GetComponentsInChildren<Table>();
        if (tables == null || tables.Length == 0)
        {
            Debug.LogError("[TableManager] Table 배열 설정 안 됨 !!!");
            return;
        }

        seatOccupied = new bool[tables.Length];
        for (int i = 0; i < tables.Length; i++)
        {
            seatOccupied[i] = false;
            if (tables[i] != null)
            {
                tables[i].SetSeatIndex(i);
            }
        }
        
        waitingQueue.Clear();
        customerQueuePositions.Clear();
        
        if (showDebugInfo) Debug.Log($"[TableManager]: 테이블 초기화 완료 - 총 {tables.Length}개");
    }

    #endregion

    #region 좌석 관리

    /// <summary>
    /// 좌석 할당 시도. 자리가 없으면 줄에 세운다.
    /// </summary>
    /// <returns> 레스토랑에 들어올 수 있으면 true, 꽉 찼으면 false </returns>
    public bool TryAssignSeat(CustomerController customer)
    {
        // 바로 앉을 자리가 있는지 확인
        if (AssignSeatToCustomer(customer))
        {
            return true;
        }
        
        // 자리가 없으면 줄에 추가
        return AddCustomerToQueue(customer);
    }

    /// <summary>
    /// 빈 좌석에 손님을 직접 할당
    /// </summary>
    private bool AssignSeatToCustomer(CustomerController customer)
    {
        if (tables == null || seatOccupied == null)
        {
            Debug.LogError("[TableManager]: 테이블 또는 좌석 배열이 null입니다!");
            return false;
        }

        for (int i = 0; i < tables.Length; i++)
        {
            if (!seatOccupied[i] && tables[i])
            {
                seatOccupied[i] = true;
                
                customer.SetAssignedTable(tables[i]);
                tables[i].AssignCustomer(customer);
                
                if (showDebugInfo)
                   Debug.Log($"[TableManager]: 테이블 {i}번 할당 완료 - {customer.name}");
                
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 좌석 해제, 다음 손님 할당
    /// </summary>
    public void ReleaseSeat(CustomerController customer)
    {
        if (!customer) return;
        
        Table customerTable = customer.GetAssignedTable();
        if (!customerTable) return;
        
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] == customerTable)
            {
                if (!seatOccupied[i]) return; // 이미 비어있는 좌석이면 무시

                seatOccupied[i] = false;
                tables[i].ReleaseCustomer();
                customer.SetAssignedTable(null);
                
                if (showDebugInfo)
                    Debug.Log($"[TableManager]: 테이블 {i}번 해제 완료 - {customer.name}");
                
                AssignNextCustomerFromQueue();
                break;
            }
        }
    }

    #endregion

    #region 줄서기 관리

    /// <summary>
    /// 줄에 손님 추가
    /// </summary>
    private bool AddCustomerToQueue(CustomerController customer)
    {
        if (waitingQueue.Count >= maxQueueLength)
        {
            if (showDebugInfo) Debug.LogWarning("[TableManager]: 줄 꽉 참");
            return false;
        }
        
        waitingQueue.Enqueue(customer);
        Vector3 queuePosition = CalculateQueuePosition(waitingQueue.Count - 1);
        customerQueuePositions[customer] = queuePosition;
        
        customer.SetDestination(queuePosition);
        
        if (showDebugInfo) 
            Debug.Log($"[TableManager]: {customer.name} 대기열 {waitingQueue.Count}번째에 추가");
        
        return true;
    }

    /// <summary>
    /// 줄에서 다음 손님을 자동으로 좌석에 할당
    /// </summary>
    private void AssignNextCustomerFromQueue()
    {
        if (waitingQueue.Count == 0) return;
        
        // 바로 앉을 자리가 있는지 다시 확인
        if (!HasAvailableSeat()) return;

        CustomerController nextCustomer = waitingQueue.Dequeue();
        customerQueuePositions.Remove(nextCustomer);
        
        if (AssignSeatToCustomer(nextCustomer))
        {
            if (showDebugInfo)
                Debug.Log($"[TableManager]: 줄에서 대기하던 {nextCustomer.name}에게 좌석 할당");
            
            // 다음 손님 좌석이동
            nextCustomer.MoveToAssignedSeat();
            ReorganizeQueue();
        }
        else
        {
            Debug.LogError("[TableManager]: 좌석 할당 실패함 ..");
        }
    }

    /// <summary>
    /// 줄에서 손님 제거
    /// </summary>
    public void RemoveCustomerFromQueue(CustomerController customer)
    {
        if (!customerQueuePositions.Remove(customer))
        {
            return;
        }
        
        // 리스트 변환해서 바로 찾기
        var customerList = new List<CustomerController>(waitingQueue);
        if (customerList.Remove(customer))
        {
            // 다시 Queue 생성
            waitingQueue = new Queue<CustomerController>(customerList);
        
            if (showDebugInfo) 
                Debug.Log($"[TableManager]: 줄에서 손님 제거 - {customer.name}");
            
            ReorganizeQueue();
        }
    }

    /// <summary>
    /// 줄 재정렬
    /// </summary>
    private void ReorganizeQueue()
    {
        customerQueuePositions.Clear();
        var queueArray = waitingQueue.ToArray();
        
        for (int i = 0; i < queueArray.Length; i++)
        {
            var customer = queueArray[i];
            if (!customer) continue;
            
            Vector3 newPosition = CalculateQueuePosition(i);
            customerQueuePositions[customer] = newPosition;
            
            customer.SetDestination(newPosition);
        }
    }

    /// <summary>
    /// 줄 위치 계산
    /// </summary>
    private Vector3 CalculateQueuePosition(int queueIndex)
    {
        if (!queueStartPosition)
        {
            Debug.LogError("[TableManager]: 줄 시작 위치 없음 !!!");
            return Vector3.zero;
        }
        
        Vector3 basePosition = queueStartPosition.position;
        return basePosition + queueStartPosition.right * (queueIndex * queueSpacing);
    }

    public Vector3 GetCustomerQueuePosition(CustomerController customer)
    {
        if (customerQueuePositions.TryGetValue(customer, out Vector3 position))
        {
            return position;
        }
        Debug.LogError($"[TableManager] {customer.name}의 대기열 위치 없음");
        
        return queueStartPosition.position;
    }

    #endregion

    #region 상태 체크
    
    public bool CanAcceptNewCustomer() 
    {
        // 빈 좌석이 있거나, 줄에 자리가 있으면 새로운 손님을 받을 수 있음
        return HasAvailableSeat() || !IsQueueFull;
    }
    
    #endregion
    
    public void ReleaseAllQueues()
    {
        var queuedCustomers = waitingQueue.ToArray();
        foreach (var customer in queuedCustomers)
        {
            if (customer)
            {
                customer.ForceLeave();
            }
        }
        
        waitingQueue.Clear();
        customerQueuePositions.Clear();
    
        if (showDebugInfo) Debug.Log("[TableManager]: 모든 좌석과 줄 해제됨");
    }
    
    private bool IsQueueFull => waitingQueue.Count >= maxQueueLength;
    private bool HasAvailableSeat() => seatOccupied.Any(occupied => !occupied);
    
    #region Public Getters & methods

    public int TotalSeatCount => tables?.Length ?? 0;
    public int CurrentQueueLength => waitingQueue.Count;
    public int MaxQueueLength => maxQueueLength;
    

    #endregion
}