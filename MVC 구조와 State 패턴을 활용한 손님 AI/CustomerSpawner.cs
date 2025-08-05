using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class CustomerSpawner : MonoBehaviour
{
    [Header("스폰 세팅")]
    [SerializeField] private float initialSpawnDelay = 3f;
    [SerializeField] private float minSpawnInterval;
    [SerializeField] private float maxSpawnInterval;
    [SerializeField] private int maxCustomers = 10;
    [SerializeField] private Transform[] spawnPoints;
    
    [Header("스폰 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float normalCustomerChance;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Dictionary<CustomerRarity, List<CustomerData>> raritySortedCustomers;
    private Coroutine spawnCoroutine;

    #region Unity events
    
    private void Start()
    {
        var availableCustomers = CustomerManager.Instance.AvailableCustomerTypes;
        SortCustomersByRarity(availableCustomers);
    }
    
    private void OnDestroy()
    {
        StopSpawning();
    }
    
    #endregion
    
    #region 스폰 시스템
    
    public void StartSpawning()
    {
        if (spawnCoroutine != null) return;
        spawnCoroutine = StartCoroutine(SpawnCustomerCoroutine());
    }

    private IEnumerator SpawnCustomerCoroutine()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        while (true)
        {
            if (!GameManager.Instance.IsTutorialActive)
            {
                if (CustomerManager.Instance.ActiveCustomerCount < maxCustomers && 
                    TableManager.Instance.CanAcceptNewCustomer())
                {
                    SpawnRandomCustomer();
                }
            }

            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }
    
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
            if (showDebugInfo) Debug.Log("[CustomerSpawner]: 자동 스폰 중단");
        }
    }

    private void SpawnRandomCustomer()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[CustomerSpawner]: 스폰 지점을 설정해주세요!");
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        CustomerData customerData = SelectRandomCustomerByRarity();
        if (!customerData)
        {
             if (showDebugInfo) Debug.LogWarning("[CustomerSpawner]: 스폰할 손님 데이터를 찾지 못했습니다.");
             return;
        }
        
        CustomerManager.Instance.SpawnCustomer(customerData, spawnPoint.position, spawnPoint.rotation);
        
        if (showDebugInfo) 
            Debug.Log($"[CustomerSpawner]: {customerData.customerName} (등급: {customerData.rarity}) 스폰 요청 완료!"); 
    }
    
    private void SortCustomersByRarity(IReadOnlyList<CustomerData> customers)
    {
        raritySortedCustomers = new Dictionary<CustomerRarity, List<CustomerData>>();

        foreach (var customer in customers)
        {
            if (customer == null) continue;
        
            if (!raritySortedCustomers.ContainsKey(customer.rarity))
            {
                raritySortedCustomers[customer.rarity] = new List<CustomerData>();
            }
            raritySortedCustomers[customer.rarity].Add(customer);
        }
    }

    private CustomerData SelectRandomCustomerByRarity()
    {
        float randomValue = Random.value;
        CustomerRarity selectedRarity = (randomValue < normalCustomerChance) ? CustomerRarity.Normal : CustomerRarity.Rare;
        
        if (raritySortedCustomers.TryGetValue(selectedRarity, out List<CustomerData> candidates) && candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        CustomerRarity fallbackRarity = (selectedRarity == CustomerRarity.Normal) ? CustomerRarity.Rare : CustomerRarity.Normal;
        if (raritySortedCustomers.TryGetValue(fallbackRarity, out List<CustomerData> fallbackCandidates) && fallbackCandidates.Count > 0)
        {
            return fallbackCandidates[Random.Range(0, fallbackCandidates.Count)];
        }
    
        return null;
    }

    #endregion
    
    [ContextMenu("Debug/수동으로 손님 1명 스폰")]
    public void SpawnSingleCustomer()
    {
        if (!Application.isPlaying) return;
        SpawnRandomCustomer();
    }

    public void SpawnTutorialCustomer()
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        CustomerData customerData = SelectRandomCustomerByRarity();
        CustomerController cc = CustomerManager.Instance.SpawnCustomer(customerData, spawnPoint.position, spawnPoint.rotation);
        cc.SetTutorialMode(true);
    }
}