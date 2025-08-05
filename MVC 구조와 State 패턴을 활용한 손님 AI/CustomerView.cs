using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public enum CustomerAnimState
{
    Idle,
    Walking,
    Sitting,
}

/// <summary>
/// 시각적 표현을 담당하는 View
/// </summary>
public class CustomerView : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Canvas customerUI;
    [SerializeField] private Image orderBubble;
    [SerializeField] private Image patienceTimer;
    
    [Header("Animation Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animator emoteAnimator;
    [SerializeField] private CoinPopEffect coin;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo;
    
    // animation hash
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int IsSitting = Animator.StringToHash("IsSitting");
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int DoHappy = Animator.StringToHash("DoHappy");
    private static readonly int DoAngry = Animator.StringToHash("DoAngry");

    #region Initialization

    public void Initialize(CustomerData data)
    {
        SetupComponents();
        ApplyAnimatorOverride(data);
        SetPatienceVisibility(false);
        HideOrderBubble();
    }

    private void SetupComponents()
    {
        customerUI = transform.FindChild<Canvas>("Canvas_Customer");
        orderBubble = transform.FindChild<Image>("Img_OrderBubble");
        patienceTimer = transform.FindChild<Image>("Img_PatienceTimer");
        coin = transform.FindChild<CoinPopEffect>("Coin");
        emoteAnimator = transform.FindChild<Animator>("Emote");

        coin.gameObject.SetActive(false);
    }
    private void ApplyAnimatorOverride(CustomerData data)
    {
        if (animator && data.animator)
        {
            animator.runtimeAnimatorController = data.animator;
        }
    }
    
    #endregion

    #region UI Updates (Controller로부터 호출됨)
    public void UpdatePatienceUI(float patienceRatio)
    {
        if (!patienceTimer) return;
        patienceTimer.fillAmount = patienceRatio;
        patienceTimer.color = Util.ChangeColorByRatio(patienceRatio);
    }

    public void SetPatienceVisibility(bool isActive)
    {
        if (!customerUI || !patienceTimer) return;

        customerUI.gameObject.SetActive(isActive);
        patienceTimer.gameObject.SetActive(isActive);
    }

    public void ShowOrderBubble(FoodData order)
    {
        if (!orderBubble || !order) return;
        
        orderBubble.gameObject.SetActive(true);
        orderBubble.sprite = order.foodIcon;
    }
    
    public void HideOrderBubble()
    {
        if (!orderBubble) return;
        
        orderBubble.gameObject.SetActive(false);
    }

    public void ShowServedEffect()
    {
        EventBus.OnSFXRequested(SFXType.CustomerServe);
        emoteAnimator.SetTrigger(DoHappy);
        if (showDebugInfo) Debug.Log($"[CustomerView]: {gameObject.name} 서빙 이펙트");
    }
    
    public void ShowEatingEffect(Action onComplete)
    {
        StartCoroutine(EatingEffectCoroutine(onComplete));
        if (showDebugInfo) Debug.Log($"[CustomerView]: {gameObject.name} 먹는중 이펙트");
    }

    public void ShowPayEffect()
    {
        coin.Play();
        if (showDebugInfo) Debug.Log($"[CustomerView]: {gameObject.name} 결제 완료 이펙트");
    }

    public void ShowAngryEffect()
    {
        EventBus.OnSFXRequested(SFXType.CustomerAngry);
        emoteAnimator.SetTrigger(DoAngry);
        if (showDebugInfo) Debug.Log($"[CustomerView]: {gameObject.name} 결제 완료 이펙트");
    }
    
    #endregion

    #region Animations

    public void SetAnimationState(CustomerAnimState state)
    {
        if (!animator) return;
        
        animator.SetBool(IsWalking, state == CustomerAnimState.Walking);
        animator.SetBool(IsSitting, state == CustomerAnimState.Sitting);
    }
    
    public void UpdateAnimationDirection(Vector2 direction)
    {
        if (!animator) return;

        animator.SetFloat(MoveX, direction.x);
        animator.SetFloat(MoveY, direction.y);
    }

    #endregion

    #region Coroutines

    private IEnumerator EatingEffectCoroutine(Action onComplete)
    {
        //eatingEffect.SetActive(true);

        yield return new WaitForSeconds(2f);
        
        //eatingEffect.SetActive(false);
        
        onComplete?.Invoke();
    }

    #endregion
    
    #region Cleanup
    public void Cleanup()
    {
        SetPatienceVisibility(false);
        HideOrderBubble();

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        ResetAnimators();
    }
    
    private void ResetAnimators()
    {
        if (animator)
        {
            // 몸 애니메이션 초기화
            animator.SetBool(IsWalking, false);
            animator.SetBool(IsSitting, false);
        }

        if (emoteAnimator)
        {
            // 감정표현 초기화
            foreach (var param in emoteAnimator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    emoteAnimator.ResetTrigger(param.name);
                }
            }

            emoteAnimator.Play("default", 0, 0f);
        }
    }
    
    #endregion
}