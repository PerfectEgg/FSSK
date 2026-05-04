using UnityEngine;

[DisallowMultipleComponent]
public class OmokTurnAuthorityAdapter : MonoBehaviour
{
    [SerializeField, HideInInspector] private OmokTurnSystem turnSystem;

    protected OmokTurnSystem TurnSystem => turnSystem;

    protected virtual void Reset()
    {
        ResolveReferences();
    }

    protected virtual void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    protected virtual void OnDisable()
    {
        Unsubscribe();
    }

    public virtual void ResolveReferences()
    {
        if (turnSystem == null)
        {
            turnSystem = GetComponent<OmokTurnSystem>();
        }

        if (turnSystem == null)
        {
            turnSystem = GetComponentInParent<OmokTurnSystem>();
        }

        if (turnSystem == null)
        {
            turnSystem = GetComponentInChildren<OmokTurnSystem>(true);
        }

        if (turnSystem == null)
        {
            turnSystem = FindFirstObjectByType<OmokTurnSystem>();
        }
    }

    public bool TryApplyPlacementVisual(OmokStonePlacementRequest request)
    {
        return turnSystem != null && turnSystem.TryExecuteAuthoritativePlacementVisual(request);
    }

    public bool TryApplyPlacementResult(Vector2Int coordinate, OmokStoneColor stoneColor)
    {
        return turnSystem != null && turnSystem.TryApplyAuthoritativePlacementResult(coordinate, stoneColor);
    }

    public bool TryApplyBlockedResult(OmokBlockedStoneResult blockedResult)
    {
        return turnSystem != null && turnSystem.TryApplyAuthoritativeBlockedResult(blockedResult);
    }

    public bool TryApplyRemoval(OmokStoneRemovalResult removalTarget, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        return turnSystem != null && turnSystem.TryApplyAuthoritativeRemoval(removalTarget, out removalResult);
    }

    protected virtual void SendPlacementRequestToAuthority(OmokStonePlacementRequest request)
    {
        Debug.LogWarning($"{nameof(OmokTurnAuthorityAdapter)} received a placement request, but no network transport is implemented.", this);
    }

    protected virtual void SendRandomRemovalRequestToAuthority()
    {
        Debug.LogWarning($"{nameof(OmokTurnAuthorityAdapter)} received a random-removal request, but no network transport is implemented.", this);
    }

    protected virtual void SendRemovalConfirmationToAuthority(OmokStoneRemovalResult removalTarget)
    {
        Debug.LogWarning($"{nameof(OmokTurnAuthorityAdapter)} received a removal confirmation, but no network transport is implemented.", this);
    }

    private void Subscribe()
    {
        if (turnSystem == null)
        {
            return;
        }

        turnSystem.OnPlacementRequestSubmittedToAuthority += SendPlacementRequestToAuthority;
        turnSystem.OnRandomRemovalRequestedFromAuthority += SendRandomRemovalRequestToAuthority;
        turnSystem.OnRemovalConfirmationSubmittedToAuthority += SendRemovalConfirmationToAuthority;
    }

    private void Unsubscribe()
    {
        if (turnSystem == null)
        {
            return;
        }

        turnSystem.OnPlacementRequestSubmittedToAuthority -= SendPlacementRequestToAuthority;
        turnSystem.OnRandomRemovalRequestedFromAuthority -= SendRandomRemovalRequestToAuthority;
        turnSystem.OnRemovalConfirmationSubmittedToAuthority -= SendRemovalConfirmationToAuthority;
    }
}
