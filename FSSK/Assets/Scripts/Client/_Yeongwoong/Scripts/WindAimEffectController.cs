using System.Collections.Generic;
using UnityEngine;

public static class OmokWindVisualEvents
{
    public static float CurrentVelocityX { get; private set; }
    public static System.Action<float> OnVelocityXChanged;

    public static void PublishVelocityX(float velocityX)
    {
        CurrentVelocityX = velocityX;
        OnVelocityXChanged?.Invoke(velocityX);
    }
}

[DefaultExecutionOrder(10000)]
public sealed class WindAimEffectController : MonoBehaviour
{
    private const float REFERENCE_DRIFT_LEVEL = 2.5f;
    private const float DEFAULT_LEVEL_TWO_POINT_FIVE_DRIFT = 0.35f;

    [Header("Preview Settings")]
    [SerializeField] private bool previewOnStart;
    [SerializeField, Range(0, 3)] private int previewWindLevel = 1;
    [SerializeField] private bool allowKeyboardTesting = true;
    [SerializeField] private bool allowLevelHotkeys = true;
    [SerializeField] private KeyCode clearWindKey = KeyCode.Alpha0;
    [SerializeField] private KeyCode windLevel1Key = KeyCode.Alpha4;
    [SerializeField] private KeyCode windLevel2Key = KeyCode.Alpha5;
    [SerializeField] private KeyCode windLevel3Key = KeyCode.Alpha6;
    [SerializeField] private bool applyInspectorChangesImmediately = true;

    [Header("References")]
    [SerializeField] private OmokTrollInputBridge inputBridge;
    [SerializeField] private OmokStoneDropper stoneDropper;

    [Header("Wave Settings")]
    [SerializeField] private bool useWaveStageEvent = true;
    [SerializeField] private bool useWindLevelEvent = true;
    [SerializeField] private bool clearWindAimOnDisable = true;
    [SerializeField] private bool showWindLog = true;
    [SerializeField] private List<int> _levelProgression = new() { 0, 1, 1, 1, 2, 2, 2, 3, 3, 3 };

    [Header("Level Drift Scale")]
    [SerializeField] private bool scaleDriftFromLevelTwoPointFive = true;
    [SerializeField, Min(0f)] private float levelTwoPointFiveDriftCellsPerSecond = DEFAULT_LEVEL_TWO_POINT_FIVE_DRIFT;

    [Header("Direction Cycle")]
    [SerializeField] private bool cycleHorizontalDirection = true;
    [SerializeField, Min(0.1f)] private float directionChangeSeconds = 15f;
    [SerializeField] private bool randomizeInitialDirection = true;

    [Header("Rain Visual Velocity X")]
    [SerializeField, Min(0f)] private float rainVelocityLevel1X = 5f;
    [SerializeField, Min(0f)] private float rainVelocityLevel2X = 10f;
    [SerializeField, Min(0f)] private float rainVelocityLevel3X = 15f;

    [Header("Level Settings")]
    [SerializeField] private WindAimLevelSettings _level1 = WindAimLevelSettings.Create(new Vector2(1f, 0f), GetDefaultScaledDrift(1));
    [SerializeField] private WindAimLevelSettings _level2 = WindAimLevelSettings.Create(new Vector2(1f, 0f), GetDefaultScaledDrift(2));
    [SerializeField] private WindAimLevelSettings _level3 = WindAimLevelSettings.Create(new Vector2(1f, 0f), GetDefaultScaledDrift(3));

    private int _currentLevel;
    private int _currentDirectionSign = 1;
    private float _directionTimer;
    private bool _hasAppliedLevel;

    [System.Serializable]
    private struct WindAimLevelSettings
    {
        public bool enabled;
        public OmokWindAimSettings aimSettings;

        public static WindAimLevelSettings Create(Vector2 direction, float driftCellsPerSecond)
        {
            return new WindAimLevelSettings
            {
                enabled = true,
                aimSettings = OmokWindAimSettings.CreateDefault(direction, driftCellsPerSecond)
            };
        }

        public void Sanitize()
        {
            aimSettings.Sanitize();
        }
    }

    private void OnEnable()
    {
        TrollEvents.OnWaveStageChanged += HandleWaveStageChanged;
        TrollEvents.OnWindLevelChanged += HandleWindLevelChanged;
    }

    private void OnDisable()
    {
        TrollEvents.OnWaveStageChanged -= HandleWaveStageChanged;
        TrollEvents.OnWindLevelChanged -= HandleWindLevelChanged;

        if (clearWindAimOnDisable && Application.isPlaying)
        {
            ClearWindAim();
        }
    }

    private void Start()
    {
        ResolveReferences();
        _currentDirectionSign = randomizeInitialDirection && Random.value < 0.5f ? -1 : 1;
        _directionTimer = 0f;

        if (previewOnStart)
        {
            ApplyWindLevel(previewWindLevel, true, true);
        }
    }

    private void Update()
    {
        HandleKeyboardTesting();
        UpdateDirectionCycle();
    }

    private void HandleKeyboardTesting()
    {
        if (!allowKeyboardTesting || !allowLevelHotkeys)
        {
            return;
        }

        if (Input.GetKeyDown(clearWindKey))
        {
            ApplyWindLevel(0, true, true);
        }
        else if (Input.GetKeyDown(windLevel1Key))
        {
            ApplyWindLevel(1, true, true);
        }
        else if (Input.GetKeyDown(windLevel2Key))
        {
            ApplyWindLevel(2, true, true);
        }
        else if (Input.GetKeyDown(windLevel3Key))
        {
            ApplyWindLevel(3, true, true);
        }
    }

    private void OnValidate()
    {
        levelTwoPointFiveDriftCellsPerSecond = Mathf.Max(0f, levelTwoPointFiveDriftCellsPerSecond);
        directionChangeSeconds = Mathf.Max(0.1f, directionChangeSeconds);
        rainVelocityLevel1X = Mathf.Max(0f, rainVelocityLevel1X);
        rainVelocityLevel2X = Mathf.Max(0f, rainVelocityLevel2X);
        rainVelocityLevel3X = Mathf.Max(0f, rainVelocityLevel3X);
        _level1.Sanitize();
        _level2.Sanitize();
        _level3.Sanitize();

        if (!applyInspectorChangesImmediately ||
            !Application.isPlaying ||
            !isActiveAndEnabled ||
            !_hasAppliedLevel)
        {
            return;
        }

        ApplyWindLevel(_currentLevel, true, false);
    }

    public void SetKeyboardTestingEnabled(bool enabled)
    {
        allowKeyboardTesting = enabled;
    }

    public void SetLevelHotkeysEnabled(bool enabled)
    {
        allowLevelHotkeys = enabled;
    }

    public void SetWindLevel(int level)
    {
        ApplyWindLevel(level, true, true);
    }

    public void ClearWind()
    {
        ApplyWindLevel(0, true, true);
    }

    [ContextMenu("TEMP/Apply Wind Level 1")]
    private void ApplyWindLevel1()
    {
        ApplyWindLevel(1, true, true);
    }

    [ContextMenu("TEMP/Apply Wind Level 2")]
    private void ApplyWindLevel2()
    {
        ApplyWindLevel(2, true, true);
    }

    [ContextMenu("TEMP/Apply Wind Level 3")]
    private void ApplyWindLevel3()
    {
        ApplyWindLevel(3, true, true);
    }

    [ContextMenu("TEMP/Clear Wind Aim")]
    private void ClearWindAimFromMenu()
    {
        ClearWind();
    }

    private void HandleWaveStageChanged(int stage)
    {
        if (!useWaveStageEvent)
        {
            return;
        }

        ApplyWindLevel(GetWindLevelForStage(stage), false, true);
    }

    private void HandleWindLevelChanged(int level)
    {
        if (!useWindLevelEvent)
        {
            return;
        }

        ApplyWindLevel(level, false, true);
    }

    private void ApplyWindLevel(int level, bool force, bool allowLog)
    {
        int clampedLevel = Mathf.Clamp(level, 0, 3);
        if (!force && _hasAppliedLevel && _currentLevel == clampedLevel)
        {
            return;
        }

        int previousLevel = _currentLevel;
        _currentLevel = clampedLevel;
        _hasAppliedLevel = true;
        if (previousLevel != clampedLevel)
        {
            _directionTimer = 0f;
        }

        if (clampedLevel == 0 ||
            !TryGetLevelSettings(clampedLevel, out WindAimLevelSettings settings) ||
            !settings.enabled)
        {
            ClearWindAim();
            LogWindCleared(clampedLevel, allowLog);
            return;
        }

        OmokWindAimSettings aimSettings = settings.aimSettings;
        if (cycleHorizontalDirection)
        {
            aimSettings.direction = new Vector2(_currentDirectionSign, 0f);
        }

        if (scaleDriftFromLevelTwoPointFive)
        {
            aimSettings.driftCellsPerSecond = GetScaledDrift(clampedLevel);
        }

        aimSettings.Sanitize();
        if (!ApplyWindAim(aimSettings))
        {
            return;
        }

        OmokWindAimEvents.Publish(true, aimSettings.direction, aimSettings.driftCellsPerSecond);
        PublishRainVelocityX(clampedLevel, aimSettings.direction);

        if (showWindLog && allowLog)
        {
            Debug.Log($"[바람] {clampedLevel}단계 바람 조준을 시작합니다. 방향: {aimSettings.direction}, 드리프트: {aimSettings.driftCellsPerSecond:0.###}", this);
        }
    }

    private void UpdateDirectionCycle()
    {
        if (!cycleHorizontalDirection || _currentLevel <= 0 || directionChangeSeconds <= 0f)
        {
            return;
        }

        _directionTimer += Time.deltaTime;
        if (_directionTimer < directionChangeSeconds)
        {
            return;
        }

        _directionTimer %= directionChangeSeconds;
        _currentDirectionSign *= -1;
        ApplyWindLevel(_currentLevel, true, true);
    }

    private void PublishRainVelocityX(int level, Vector2 direction)
    {
        float velocityMagnitude = GetRainVelocityXForLevel(level);
        float directionSign = Mathf.Abs(direction.x) > 0.0001f
            ? Mathf.Sign(direction.x)
            : 0f;

        OmokWindVisualEvents.PublishVelocityX(velocityMagnitude * directionSign);
    }

    private bool TryGetLevelSettings(int level, out WindAimLevelSettings settings)
    {
        switch (level)
        {
            case 1:
                settings = _level1;
                return true;
            case 2:
                settings = _level2;
                return true;
            case 3:
                settings = _level3;
                return true;
            default:
                settings = default;
                return false;
        }
    }

    private bool ApplyWindAim(OmokWindAimSettings settings)
    {
        ResolveReferences();

        if (inputBridge != null)
        {
            inputBridge.ConfigureWindAim(settings);
            return true;
        }

        if (stoneDropper != null)
        {
            stoneDropper.ConfigureWindAim(settings);
            return true;
        }

        if (showWindLog)
        {
            Debug.LogWarning("[바람] OmokTrollInputBridge 또는 OmokStoneDropper를 찾지 못해 바람 조준을 적용하지 못했습니다.", this);
        }

        return false;
    }

    private void ClearWindAim()
    {
        ResolveReferences();

        if (inputBridge != null)
        {
            inputBridge.ClearWindAim();
        }
        else if (stoneDropper != null)
        {
            stoneDropper.ClearWindAim();
        }

        OmokWindAimEvents.Publish(false, Vector2.zero, 0f);
        OmokWindVisualEvents.PublishVelocityX(0f);
    }

    private void ResolveReferences()
    {
        if (inputBridge == null)
        {
            inputBridge = GetComponent<OmokTrollInputBridge>();
        }

        if (inputBridge == null)
        {
            inputBridge = GetComponentInParent<OmokTrollInputBridge>();
        }

        if (inputBridge == null)
        {
            inputBridge = GetComponentInChildren<OmokTrollInputBridge>(true);
        }

        if (inputBridge == null)
        {
            inputBridge = FindFirstObjectByType<OmokTrollInputBridge>();
        }

        if (inputBridge != null)
        {
            inputBridge.ResolveReferences();
            stoneDropper = inputBridge.StoneDropper;
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponent<OmokStoneDropper>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponentInParent<OmokStoneDropper>();
        }

        if (stoneDropper == null)
        {
            stoneDropper = GetComponentInChildren<OmokStoneDropper>(true);
        }

        if (stoneDropper == null)
        {
            stoneDropper = FindFirstObjectByType<OmokStoneDropper>();
        }
    }

    private void LogWindCleared(int level, bool allowLog)
    {
        if (showWindLog && allowLog && level == 0)
        {
            Debug.Log("[바람] 바람 조준을 종료합니다.", this);
        }
    }

    private int GetWindLevelForStage(int stage)
    {
        if (_levelProgression == null || _levelProgression.Count == 0)
        {
            if (stage < 1) return 0;
            if (stage < 4) return 1;
            if (stage < 7) return 2;
            return 3;
        }

        int targetIndex = Mathf.Clamp(stage, 0, _levelProgression.Count - 1);
        return Mathf.Clamp(_levelProgression[targetIndex], 0, 3);
    }

    private float GetScaledDrift(int level)
    {
        return GetScaledDrift(level, levelTwoPointFiveDriftCellsPerSecond);
    }

    private float GetRainVelocityXForLevel(int level)
    {
        switch (level)
        {
            case 1:
                return rainVelocityLevel1X;
            case 2:
                return rainVelocityLevel2X;
            case 3:
                return rainVelocityLevel3X;
            default:
                return 0f;
        }
    }

    private static float GetDefaultScaledDrift(int level)
    {
        return GetScaledDrift(level, DEFAULT_LEVEL_TWO_POINT_FIVE_DRIFT);
    }

    private static float GetScaledDrift(int level, float levelTwoPointFiveDrift)
    {
        return Mathf.Max(0f, levelTwoPointFiveDrift) * Mathf.Max(0, level) / REFERENCE_DRIFT_LEVEL;
    }
}
