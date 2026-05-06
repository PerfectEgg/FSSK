using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class RainParticleEffectController : MonoBehaviour
{
    private const string RainParticlesObjectName = "rain particles";

    [Header("Preview Settings")]
    [SerializeField] private bool previewOnStart;
    [SerializeField, Range(0, 3)] private int previewRainLevel = 1;
    [SerializeField] private bool allowKeyboardTesting = true;
    [SerializeField] private KeyCode clearRainKey = KeyCode.Keypad4;
    [SerializeField] private KeyCode rainLevel1Key = KeyCode.Keypad5;
    [SerializeField] private KeyCode rainLevel2Key = KeyCode.Keypad6;
    [SerializeField] private KeyCode rainLevel3Key = KeyCode.Keypad7;

    [Header("References")]
    [SerializeField] private GameObject existingRainInstance;

    [Header("Wave Settings")]
    [SerializeField] private bool useWaveStageEvent = true;
    [SerializeField] private bool useRainLevelEvent = true;
    [SerializeField] private bool useWindVisualVelocityEvent = true;
    [SerializeField] private bool showRainLog = true;
    [SerializeField] private List<int> rainLevelProgression = new() { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3 };

    [Header("Rain Rate Over Time")]
    [SerializeField, Min(0f)] private float rainLevel1RateOverTime = 250f;
    [SerializeField, Min(0f)] private float rainLevel2RateOverTime = 500f;
    [SerializeField, Min(0f)] private float rainLevel3RateOverTime = 750f;

    [Header("Wind Velocity Transition")]
    [SerializeField, Min(0f)] private float windVelocityTransitionSeconds = 1.5f;

    private readonly List<ParticleSystem> _allParticleSystems = new();
    private readonly List<ParticleSystem> _rainParticleSystems = new();
    private GameObject _rainInstance;
    private int _currentRainLevel;
    private bool _hasAppliedRainLevel;
    private bool _windVelocityTransitionActive;
    private float _windVelocityStartX;
    private float _currentWindVelocityX;
    private float _targetWindVelocityX;
    private float _windVelocityTransitionElapsed;

    private void OnEnable()
    {
        TrollEvents.OnWaveStageChanged += HandleWaveStageChanged;
        TrollEvents.OnRainLevelChanged += HandleRainLevelChanged;
        OmokWindVisualEvents.OnVelocityXChanged += HandleWindVisualVelocityXChanged;
    }

    private void OnDisable()
    {
        TrollEvents.OnWaveStageChanged -= HandleWaveStageChanged;
        TrollEvents.OnRainLevelChanged -= HandleRainLevelChanged;
        OmokWindVisualEvents.OnVelocityXChanged -= HandleWindVisualVelocityXChanged;

        StopRainParticles(true);
    }

    private void Start()
    {
        ResolveRainInstance();

        if (!_hasAppliedRainLevel)
        {
            ApplyRainLevel(previewOnStart ? previewRainLevel : 0, true, previewOnStart);
        }

        HandleWindVisualVelocityXChanged(OmokWindVisualEvents.CurrentVelocityX);
    }

    private void LateUpdate()
    {
        UpdateWindVelocityTransition();
        HandleKeyboardTesting();
    }

    private void OnValidate()
    {
        rainLevel1RateOverTime = Mathf.Max(0f, rainLevel1RateOverTime);
        rainLevel2RateOverTime = Mathf.Max(0f, rainLevel2RateOverTime);
        rainLevel3RateOverTime = Mathf.Max(0f, rainLevel3RateOverTime);
        windVelocityTransitionSeconds = Mathf.Max(0f, windVelocityTransitionSeconds);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            ApplyRainLevel(_currentRainLevel, true, false);
            SetWindVelocityTarget(OmokWindVisualEvents.CurrentVelocityX);
        }
    }

    public void SetRainLevel(int level)
    {
        ApplyRainLevel(level, true, true);
    }

    public void ClearRain()
    {
        ApplyRainLevel(0, true, true);
    }

    [ContextMenu("TEMP/Apply Rain Level 1")]
    private void ApplyRainLevel1()
    {
        ApplyRainLevel(1, true, true);
    }

    [ContextMenu("TEMP/Apply Rain Level 2")]
    private void ApplyRainLevel2()
    {
        ApplyRainLevel(2, true, true);
    }

    [ContextMenu("TEMP/Apply Rain Level 3")]
    private void ApplyRainLevel3()
    {
        ApplyRainLevel(3, true, true);
    }

    [ContextMenu("TEMP/Clear Rain")]
    private void ClearRainFromMenu()
    {
        ClearRain();
    }

    private void HandleWaveStageChanged(int stage)
    {
        if (useWaveStageEvent)
        {
            ApplyRainLevel(GetLevelForStage(stage, rainLevelProgression), false, true);
        }
    }

    private void HandleRainLevelChanged(int level)
    {
        if (useRainLevelEvent)
        {
            ApplyRainLevel(level, false, true);
        }
    }

    private void HandleWindVisualVelocityXChanged(float velocityX)
    {
        if (useWindVisualVelocityEvent)
        {
            SetWindVelocityTarget(velocityX);
        }
    }

    private void ApplyRainLevel(int level, bool force, bool allowLog)
    {
        int clampedLevel = Mathf.Clamp(level, 0, 3);
        if (!force && _hasAppliedRainLevel && _currentRainLevel == clampedLevel)
        {
            return;
        }

        _currentRainLevel = clampedLevel;
        _hasAppliedRainLevel = true;

        if (!ResolveRainInstance())
        {
            return;
        }

        ApplyRainRate(GetRainRateOverTime(clampedLevel));

        if (clampedLevel == 0)
        {
            StopRainParticles(false);
            LogRain("cleared", allowLog);
            return;
        }

        PlayRainParticles();
        LogRain($"level {clampedLevel}, rate {GetRainRateOverTime(clampedLevel):0.#}", allowLog);
    }

    private bool ResolveRainInstance()
    {
        if (_rainInstance != null)
        {
            return true;
        }

        _rainInstance = existingRainInstance != null
            ? existingRainInstance
            : FindRainParticlesInstance();

        if (_rainInstance == null)
        {
            Debug.LogWarning(
                "[RainParticleEffect] Place the Rain Particles prefab in the scene, or assign Existing Rain Instance.",
                this);
            return false;
        }

        CaptureParticleSystems();
        ApplyWindVelocityX(_currentWindVelocityX);
        return true;
    }

    private GameObject FindRainParticlesInstance()
    {
        GameObject childInstance = FindRainParticlesInChildren(transform);
        if (childInstance != null)
        {
            return childInstance;
        }

        GameObject attachedInstance = FindRainParticlesRoot(transform);
        if (attachedInstance != null)
        {
            return attachedInstance;
        }

        ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null ||
                particleSystem.GetComponentInParent<RainParticleEffectController>() != null)
            {
                continue;
            }

            GameObject candidate = FindRainParticlesRoot(particleSystem.transform);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindRainParticlesInChildren(Transform parent)
    {
        ParticleSystem[] childParticleSystems = parent.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < childParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = childParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            GameObject candidate = FindRainParticlesRoot(particleSystem.transform);
            if (candidate != null && candidate.transform.IsChildOf(parent))
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindRainParticlesRoot(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            if (IsRainParticlesRoot(current))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool IsRainParticlesRoot(Transform candidate)
    {
        return candidate != null &&
               candidate.name.ToLowerInvariant().Contains(RainParticlesObjectName) &&
               candidate.GetComponentsInChildren<ParticleSystem>(true).Length > 0;
    }

    private void CaptureParticleSystems()
    {
        _allParticleSystems.Clear();
        _rainParticleSystems.Clear();

        ParticleSystem[] particleSystems = _rainInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            _allParticleSystems.Add(particleSystem);
            if (IsRainParticleSystem(particleSystem))
            {
                _rainParticleSystems.Add(particleSystem);
            }
        }

        if (_rainParticleSystems.Count == 0)
        {
            _rainParticleSystems.AddRange(_allParticleSystems);
        }
    }

    private static bool IsRainParticleSystem(ParticleSystem particleSystem)
    {
        return particleSystem != null &&
               particleSystem.gameObject.name.ToLowerInvariant().Contains("rain");
    }

    private void ApplyRainRate(float rateOverTime)
    {
        for (int i = 0; i < _rainParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _rainParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rateOverTime);
        }
    }

    private void SetWindVelocityTarget(float targetVelocityX)
    {
        if (!ResolveRainInstance())
        {
            return;
        }

        if (Mathf.Approximately(_targetWindVelocityX, targetVelocityX) &&
            Mathf.Approximately(_currentWindVelocityX, targetVelocityX))
        {
            return;
        }

        _targetWindVelocityX = targetVelocityX;
        _windVelocityStartX = _currentWindVelocityX;
        _windVelocityTransitionElapsed = 0f;

        if (windVelocityTransitionSeconds <= 0f)
        {
            _currentWindVelocityX = _targetWindVelocityX;
            _windVelocityTransitionActive = false;
            ApplyWindVelocityX(_currentWindVelocityX);
            return;
        }

        _windVelocityTransitionActive = true;
    }

    private void UpdateWindVelocityTransition()
    {
        if (!_windVelocityTransitionActive)
        {
            return;
        }

        _windVelocityTransitionElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_windVelocityTransitionElapsed / windVelocityTransitionSeconds);
        _currentWindVelocityX = Mathf.Lerp(_windVelocityStartX, _targetWindVelocityX, t);
        ApplyWindVelocityX(_currentWindVelocityX);

        if (t >= 1f)
        {
            _currentWindVelocityX = _targetWindVelocityX;
            _windVelocityTransitionActive = false;
            ApplyWindVelocityX(_currentWindVelocityX);
        }
    }

    private void ApplyWindVelocityX(float velocityX)
    {
        for (int i = 0; i < _rainParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _rainParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.VelocityOverLifetimeModule velocityModule = particleSystem.velocityOverLifetime;
            velocityModule.enabled = true;
            velocityModule.x = new ParticleSystem.MinMaxCurve(velocityX);
        }
    }

    private void PlayRainParticles()
    {
        for (int i = 0; i < _allParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _allParticleSystems[i];
            if (particleSystem != null && !particleSystem.isPlaying)
            {
                particleSystem.Play(true);
            }
        }
    }

    private void StopRainParticles(bool clear)
    {
        ParticleSystemStopBehavior behavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < _allParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _allParticleSystems[i];
            if (particleSystem != null)
            {
                particleSystem.Stop(true, behavior);
            }
        }
    }

    private void HandleKeyboardTesting()
    {
        if (!allowKeyboardTesting)
        {
            return;
        }

        if (Input.GetKeyDown(clearRainKey))
        {
            ApplyRainLevel(0, true, true);
        }
        else if (Input.GetKeyDown(rainLevel1Key))
        {
            ApplyRainLevel(1, true, true);
        }
        else if (Input.GetKeyDown(rainLevel2Key))
        {
            ApplyRainLevel(2, true, true);
        }
        else if (Input.GetKeyDown(rainLevel3Key))
        {
            ApplyRainLevel(3, true, true);
        }
    }

    private float GetRainRateOverTime(int level)
    {
        switch (level)
        {
            case 1:
                return rainLevel1RateOverTime;
            case 2:
                return rainLevel2RateOverTime;
            case 3:
                return rainLevel3RateOverTime;
            default:
                return 0f;
        }
    }

    private static int GetLevelForStage(int stage, List<int> progression)
    {
        if (progression == null || progression.Count == 0)
        {
            return 0;
        }

        int index = Mathf.Clamp(stage, 0, progression.Count - 1);
        return Mathf.Clamp(progression[index], 0, 3);
    }

    private void LogRain(string message, bool allowLog)
    {
        if (showRainLog && allowLog)
        {
            Debug.Log($"[RainParticleEffect] {message}.", this);
        }
    }
}
