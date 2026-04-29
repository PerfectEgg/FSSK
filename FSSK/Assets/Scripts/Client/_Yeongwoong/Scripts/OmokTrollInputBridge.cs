using System;
using UnityEngine;

public class OmokTrollInputBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OmokStoneDropper stoneDropper;
    [SerializeField] private OmokMatchManager matchManager;
    [SerializeField] private OmokAiController aiController;

    [Header("Input Gate")]
    [SerializeField] private bool allowOmokInputInExpansionMode;
    [SerializeField] private bool startWithOmokInputEnabled = true;

    private bool _isExpansionMode;
    private bool _isExternallyLocked;
    private float _timedLockRemaining;
    private bool _hasPendingRemovalTarget;
    private OmokStoneRemovalResult _pendingRemovalTarget;

    public OmokStoneDropper StoneDropper => stoneDropper;
    public OmokMatchManager MatchManager => matchManager;
    public OmokAiController AiController => aiController;
    public bool IsOmokInputEnabled { get; private set; }
    public bool HasPendingRemovalTarget => _hasPendingRemovalTarget;
    public Vector2Int PendingRemovalCoordinate => _hasPendingRemovalTarget ? _pendingRemovalTarget.Coordinate : default;
    public OmokStoneColor PendingRemovalColor => _hasPendingRemovalTarget ? _pendingRemovalTarget.StoneColor : OmokStoneColor.None;
    public int PendingRemovalStoneId => _hasPendingRemovalTarget ? GetRemovalStoneId(_pendingRemovalTarget.StoneColor) : 0;
    public Transform PendingRemovalTransform => TryGetPendingRemovalTransform(out Transform stoneTransform) ? stoneTransform : null;
    public event Action<OmokStoneRemovalResult> OnRemovalTargetSelected;

    private void Awake()
    {
        ResolveReferences();
        ApplyInputState();
    }

    private void OnEnable()
    {
        GameEvents.OnExpansionModeChanged += HandleExpansionModeChanged;
        GameEvents.OnStunEffect += HandleStunEffect;
        GameEvents.OnRequestStoneToSteal += HandleStoneStealRequested;
        GameEvents.OnExecuteSteal += HandleExecuteSteal;
        ApplyInputState();
    }

    private void OnDisable()
    {
        GameEvents.OnExpansionModeChanged -= HandleExpansionModeChanged;
        GameEvents.OnStunEffect -= HandleStunEffect;
        GameEvents.OnRequestStoneToSteal -= HandleStoneStealRequested;
        GameEvents.OnExecuteSteal -= HandleExecuteSteal;
    }

    private void Update()
    {
        if (_timedLockRemaining <= 0f)
        {
            return;
        }

        _timedLockRemaining = Mathf.Max(0f, _timedLockRemaining - Time.deltaTime);
        if (_timedLockRemaining <= 0f)
        {
            ApplyInputState();
        }
    }

    public void ResolveReferences()
    {
        if (stoneDropper == null)
        {
            stoneDropper = GetComponentInChildren<OmokStoneDropper>(true);
        }

        if (stoneDropper == null)
        {
            stoneDropper = FindFirstObjectByType<OmokStoneDropper>();
        }

        if (matchManager == null)
        {
            matchManager = GetComponentInChildren<OmokMatchManager>(true);
        }

        if (matchManager == null)
        {
            matchManager = FindFirstObjectByType<OmokMatchManager>();
        }

        if (aiController == null)
        {
            aiController = GetComponentInChildren<OmokAiController>(true);
        }

        if (aiController == null)
        {
            aiController = FindFirstObjectByType<OmokAiController>();
        }
    }

    public void SetOmokInputLocked(bool isLocked)
    {
        _isExternallyLocked = isLocked;
        ApplyInputState();
    }

    public void LockOmokInputForSeconds(float duration)
    {
        _timedLockRemaining = Mathf.Max(_timedLockRemaining, duration);
        ApplyInputState();
    }

    public bool TryRemoveRandomStone(out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        ResolveReferences();
        return matchManager != null && matchManager.TryRemoveRandomStone(out removalResult);
    }

    public bool TryRemoveRandomStone()
    {
        ResolveReferences();
        return matchManager != null && matchManager.TryRemoveRandomStone();
    }

    public bool TrySelectNextRemovalTarget(out OmokStoneRemovalResult removalTarget)
    {
        removalTarget = default;
        ResolveReferences();
        if (matchManager == null || !matchManager.TrySelectNextRemovalTarget(out removalTarget))
        {
            return false;
        }

        StorePendingRemovalTarget(removalTarget);
        OnRemovalTargetSelected?.Invoke(removalTarget);
        return true;
    }

    public bool TryGetNextRemovalCoordinate(out Vector2Int coordinate)
    {
        if (_hasPendingRemovalTarget)
        {
            coordinate = _pendingRemovalTarget.Coordinate;
            return true;
        }

        if (TrySelectNextRemovalTarget(out OmokStoneRemovalResult removalTarget))
        {
            coordinate = removalTarget.Coordinate;
            return true;
        }

        coordinate = default;
        return false;
    }

    public bool TryGetNextRemovalCoordinate(out Vector2Int coordinate, out OmokStoneColor stoneColor)
    {
        if (TryGetNextRemovalCoordinate(out coordinate))
        {
            stoneColor = PendingRemovalColor;
            return true;
        }

        stoneColor = OmokStoneColor.None;
        return false;
    }

    public bool TryGetNextRemovalCoordinate(out Vector2Int coordinate, out int stoneId)
    {
        if (TryGetNextRemovalCoordinate(out coordinate))
        {
            stoneId = PendingRemovalStoneId;
            return stoneId != 0;
        }

        stoneId = 0;
        return false;
    }

    public bool TryGetNextRemovalTransform(out Transform stoneTransform)
    {
        return TryGetNextRemovalTransform(out stoneTransform, out _);
    }

    public bool TryGetNextRemovalTransform(out Transform stoneTransform, out int stoneId)
    {
        stoneTransform = null;
        stoneId = 0;

        if (!TryGetNextRemovalCoordinate(out _, out stoneId))
        {
            return false;
        }

        return TryGetPendingRemovalTransform(out stoneTransform);
    }

    public bool TryGetPendingRemovalTransform(out Transform stoneTransform)
    {
        stoneTransform = null;
        if (!_hasPendingRemovalTarget)
        {
            return false;
        }

        ResolveReferences();
        return stoneDropper != null &&
               stoneDropper.TryGetStoneTransformAt(
                   _pendingRemovalTarget.Coordinate,
                   _pendingRemovalTarget.StoneColor,
                   out stoneTransform);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget)
    {
        return TryConfirmRemoveStone(removalTarget, out _);
    }

    public bool TryConfirmRemoveStone(OmokStoneRemovalResult removalTarget, out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        ResolveReferences();
        if (matchManager == null || !matchManager.TryConfirmRemoveStone(removalTarget, out removalResult))
        {
            return false;
        }

        if (IsPendingRemovalTarget(removalTarget))
        {
            ClearPendingRemovalTarget();
        }

        return true;
    }

    public bool TryConfirmPendingRemoval()
    {
        return TryConfirmPendingRemoval(out _);
    }

    public bool TryConfirmPendingRemoval(out OmokStoneRemovalResult removalResult)
    {
        removalResult = default;
        if (!_hasPendingRemovalTarget)
        {
            return false;
        }

        return TryConfirmRemoveStone(_pendingRemovalTarget, out removalResult);
    }

    public void ClearPendingRemovalTarget()
    {
        _hasPendingRemovalTarget = false;
        _pendingRemovalTarget = default;
    }

    private void StorePendingRemovalTarget(OmokStoneRemovalResult removalTarget)
    {
        _pendingRemovalTarget = removalTarget;
        _hasPendingRemovalTarget = true;
    }

    private bool IsPendingRemovalTarget(OmokStoneRemovalResult removalTarget)
    {
        return _hasPendingRemovalTarget &&
               _pendingRemovalTarget.Coordinate == removalTarget.Coordinate &&
               _pendingRemovalTarget.StoneColor == removalTarget.StoneColor;
    }

    private static int GetRemovalStoneId(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Gold => 1,
            OmokStoneColor.Silver => 2,
            _ => 0
        };
    }

    public void SetAiEnabled(bool isEnabled)
    {
        ResolveReferences();

        if (aiController != null)
        {
            aiController.SetAiEnabled(isEnabled);
        }
    }

    public void SetWindAimEnabled(bool isEnabled)
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.SetWindAimEnabled(isEnabled);
        }
    }

    public void ConfigureWindAim(Vector2 direction, float driftCellsPerSecond)
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.ConfigureWindAim(direction, driftCellsPerSecond);
        }
    }

    public void ClearWindAim()
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.ClearWindAim();
        }
    }

    public void SetPlacementTargetOffset(Vector2Int offset)
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.SetPlacementTargetOffset(offset);
        }
    }

    public void ClearPlacementTargetOffset()
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.ClearPlacementTargetOffset();
        }
    }

    public void SetLocalOmokStoneColor(OmokStoneColor stoneColor)
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.SetLocalManualStoneColor(stoneColor);
        }
    }

    public void ClearLocalOmokStoneColor()
    {
        SetLocalOmokStoneColor(OmokStoneColor.None);
    }

    public void SetOmokTurnDragGateEnabled(bool isEnabled)
    {
        ResolveReferences();

        if (stoneDropper != null)
        {
            stoneDropper.SetRestrictManualDragToCurrentTurn(isEnabled);
        }
    }

    private void HandleExpansionModeChanged(bool isExpansion)
    {
        _isExpansionMode = isExpansion;
        ApplyInputState();
    }

    private void HandleStunEffect(float stunDuration)
    {
        LockOmokInputForSeconds(stunDuration);
    }

    private void HandleStoneStealRequested()
    {
        if (TryGetNextRemovalTransform(out Transform stoneTransform, out int stoneId))
        {
            GameEvents.TriggerStoneTargetCallback(stoneId, stoneTransform);
            return;
        }

        GameEvents.TriggerStoneTargetCallback(0, null);
    }

    private void HandleExecuteSteal()
    {
        TryConfirmPendingRemoval();
    }

    private void ApplyInputState()
    {
        ResolveReferences();

        bool shouldEnable = startWithOmokInputEnabled &&
                            !_isExternallyLocked &&
                            _timedLockRemaining <= 0f &&
                            (allowOmokInputInExpansionMode || !_isExpansionMode);

        IsOmokInputEnabled = shouldEnable;

        if (stoneDropper != null)
        {
            stoneDropper.SetBuiltInMouseInputEnabled(shouldEnable);
        }
    }
}
