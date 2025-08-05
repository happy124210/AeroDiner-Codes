using UnityEngine;

public enum CustomerRarity
{
    Normal,
    Rare,
    Special
}

/// <summary>
/// 손님 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Customer Data", menuName = "Game Data/Customer Data")]
public class CustomerData : ScriptableObject
{
    [Header("손님 정보")]
    public string id;
    public string customerName;
    public CustomerRarity rarity;
    public string displayName; // UI용 이름
    public float speed; // 이동 속도
    public float waitTime; // 기다리는 시간
    public float eatTime; // 먹는 시간
    
    public AnimatorOverrideController animator;
}