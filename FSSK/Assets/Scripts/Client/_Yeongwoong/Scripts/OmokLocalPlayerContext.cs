using System;
using UnityEngine;

[DisallowMultipleComponent]
public class OmokLocalPlayerContext : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OmokMatchManager matchManager;

    [Header("Local Player")]
    [SerializeField] private OmokStoneColor localStoneColor = OmokStoneColor.None;
    [SerializeField] private bool allowManualInput = true;

    private OmokMatchManager _subscribedMatchManager;

    public OmokStoneColor LocalStoneColor => localStoneColor;
    public bool HasLocalSeat => localStoneColor != OmokStoneColor.None;
    public bool IsSpectator => localStoneColor == OmokStoneColor.None;
    public bool AllowManualInput => allowManualInput;
    public bool IsLocalTurn => HasLocalSeat &&
                               matchManager != null &&
                               matchManager.CanTakeTurn(localStoneColor);

    public event Action<OmokLocalPlayerContext> OnLocalPlayerStateChanged;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToMatchManager();
        NotifyStateChanged();
    }

    private void OnDisable()
    {
        UnsubscribeFromMatchManager();
    }

    private void OnValidate()
    {
        localStoneColor = NormalizeStoneColor(localStoneColor);
        if (matchManager == null)
        {
            matchManager = GetComponent<OmokMatchManager>();
        }
    }

    public void SetMatchManager(OmokMatchManager nextMatchManager)
    {
        if (matchManager == nextMatchManager)
        {
            return;
        }

        UnsubscribeFromMatchManager();
        matchManager = nextMatchManager;

        if (isActiveAndEnabled)
        {
            SubscribeToMatchManager();
        }

        NotifyStateChanged();
    }

    public void ConfigureLocalPlayer(OmokStoneColor stoneColor, bool canUseManualInput = true)
    {
        localStoneColor = NormalizeStoneColor(stoneColor);
        allowManualInput = canUseManualInput;
        NotifyStateChanged();
    }

    public void SetLocalStoneColor(OmokStoneColor stoneColor)
    {
        OmokStoneColor normalizedColor = NormalizeStoneColor(stoneColor);
        if (localStoneColor == normalizedColor)
        {
            return;
        }

        localStoneColor = normalizedColor;
        NotifyStateChanged();
    }

    public void ClearLocalStoneColor()
    {
        SetLocalStoneColor(OmokStoneColor.None);
    }

    public void SetManualInputAllowed(bool isAllowed)
    {
        if (allowManualInput == isAllowed)
        {
            return;
        }

        allowManualInput = isAllowed;
        NotifyStateChanged();
    }

    public bool CanControlStone(OmokStoneColor stoneColor)
    {
        if (!allowManualInput ||
            !HasLocalSeat ||
            stoneColor == OmokStoneColor.None ||
            stoneColor != localStoneColor)
        {
            return false;
        }

        return matchManager == null || matchManager.CanTakeTurn(stoneColor);
    }

    public OmokManualPlacementState GetManualPlacementState()
    {
        if (!allowManualInput || !HasLocalSeat)
        {
            return new OmokManualPlacementState(false, false);
        }

        if (matchManager != null)
        {
            return matchManager.GetManualPlacementState(localStoneColor, allowManualInput);
        }

        return new OmokManualPlacementState(
            localStoneColor == OmokStoneColor.Gold,
            localStoneColor == OmokStoneColor.Silver);
    }

    private void ResolveReferences()
    {
        if (matchManager == null)
        {
            matchManager = GetComponent<OmokMatchManager>();
        }

        if (matchManager == null)
        {
            matchManager = GetComponentInParent<OmokMatchManager>();
        }

        if (matchManager == null)
        {
            matchManager = FindFirstObjectByType<OmokMatchManager>();
        }
    }

    private void SubscribeToMatchManager()
    {
        if (_subscribedMatchManager == matchManager)
        {
            return;
        }

        UnsubscribeFromMatchManager();

        if (matchManager == null)
        {
            return;
        }

        matchManager.OnTurnChanged += HandleTurnChanged;
        matchManager.OnMatchEnded += HandleMatchEnded;
        _subscribedMatchManager = matchManager;
    }

    private void UnsubscribeFromMatchManager()
    {
        if (_subscribedMatchManager == null)
        {
            return;
        }

        _subscribedMatchManager.OnTurnChanged -= HandleTurnChanged;
        _subscribedMatchManager.OnMatchEnded -= HandleMatchEnded;
        _subscribedMatchManager = null;
    }

    private void HandleTurnChanged(OmokStoneColor nextTurn)
    {
        NotifyStateChanged();
    }

    private void HandleMatchEnded(OmokStoneColor winner)
    {
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnLocalPlayerStateChanged?.Invoke(this);
    }

    private static OmokStoneColor NormalizeStoneColor(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Gold => OmokStoneColor.Gold,
            OmokStoneColor.Silver => OmokStoneColor.Silver,
            _ => OmokStoneColor.None
        };
    }
}
