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

    public OmokStoneDropper StoneDropper => stoneDropper;
    public OmokMatchManager MatchManager => matchManager;
    public OmokAiController AiController => aiController;
    public bool IsOmokInputEnabled { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        ApplyInputState();
    }

    private void OnEnable()
    {
        GameEvents.OnExpansionModeChanged += HandleExpansionModeChanged;
        GameEvents.OnStunEffect += HandleStunEffect;
        ApplyInputState();
    }

    private void OnDisable()
    {
        GameEvents.OnExpansionModeChanged -= HandleExpansionModeChanged;
        GameEvents.OnStunEffect -= HandleStunEffect;
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

    private void HandleExpansionModeChanged(bool isExpansion)
    {
        _isExpansionMode = isExpansion;
        ApplyInputState();
    }

    private void HandleStunEffect(float stunDuration)
    {
        LockOmokInputForSeconds(stunDuration);
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
