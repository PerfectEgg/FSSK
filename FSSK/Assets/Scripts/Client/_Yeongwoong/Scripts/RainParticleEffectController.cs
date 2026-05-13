using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
public sealed class RainParticleEffectController : MonoBehaviour
{
    private const string RainParticlesObjectName = "rain particles";
    private const string BoardLayerName = "Board";
    private const int ScreenRainTextureSize = 512;
    private const int ScreenRainRandomSeed = 2137;
    private const float ScreenRainLevel1Alpha = 0.13f;
    private const float ScreenRainLevel2Alpha = 0.23f;
    private const float ScreenRainLevel3Alpha = 0.34f;
    private const int ScreenRainLevel1StreakCount = 70;
    private const int ScreenRainLevel2StreakCount = 125;
    private const int ScreenRainLevel3StreakCount = 205;
    private const int ScreenRainMinStreakLength = 14;
    private const int ScreenRainMaxStreakLength = 42;
    private const float ScreenRainDropLengthScale = 1f;
    private const float ScreenRainFadeSpeed = 3.5f;
    private const float ScreenRainScrollSpeed = 3.05f;
    private const float ScreenRainNearTiling = 1.85f;
    private const float ScreenRainFarTiling = 2.55f;
    private const float ScreenRainSlant = -0.07f;
    private const float ScreenRainWindReferenceVelocityX = 15f;
    private const float ScreenRainWindHorizontalScrollScale = 0.025f;
    private const float ScreenRainWindSpeedBoost = 0.1f;
    private const float ScreenRainLayerOverscanPixels = 320f;
    private const float ScreenRainFarLayerAlphaScale = 0.55f;
    private const bool InvertScreenRainWindDirection = true;

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

    [Header("Camera Coverage")]
    [SerializeField] private bool followActiveCamera = true;
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool resolveMainCameraAsTarget = true;
    [SerializeField] private bool keepInitialEmitterHeight = true;
    [SerializeField, Min(0f)] private float emitterHeightOffset = 22f;
    [SerializeField, Min(0f)] private float emitterLookAheadDistance = 8f;
    [SerializeField] private Vector3 emitterWorldOffset;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.5f;
    [SerializeField] private bool forceWorldSimulationSpace = true;

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

    [Header("Rain Collision")]
    [SerializeField] private bool ignoreBoardLayerForCollision = true;

    [Header("Wind Velocity Transition")]
    [SerializeField, Min(0f)] private float windVelocityTransitionSeconds = 1.5f;

    [Header("Screen Rain Overlay")]
    [SerializeField] private bool enableScreenRainOverlay = true;
    [SerializeField] private bool showScreenRainOverlayOnlyInPlacementMode = true;
    [SerializeField] private Color screenRainColor = new(0.72f, 0.84f, 1f, 1f);
    [SerializeField, Range(0.25f, 1.25f)] private float screenRainAmountMultiplier = 1f;
    [SerializeField] private bool enableScreenRainWindTilt = true;
    [SerializeField, Range(0f, 20f)] private float screenRainWindAngleAtReference = 8f;
    [SerializeField, Range(0f, 3f)] private float screenRainWindFollowStrength = 1.6f;

    private readonly List<ParticleSystem> _allParticleSystems = new();
    private readonly List<ParticleSystem> _rainParticleSystems = new();
    private GameObject _rainInstance;
    private float _initialEmitterWorldY;
    private bool _hasInitialEmitterWorldY;
    private bool _missingRainInstanceWarningLogged;
    private Camera _cachedMainCamera;
    private float _targetRefreshElapsed;
    private int _currentRainLevel;
    private bool _hasAppliedRainLevel;
    private bool _windVelocityTransitionActive;
    private float _windVelocityStartX;
    private float _currentWindVelocityX;
    private float _targetWindVelocityX;
    private float _windVelocityTransitionElapsed;
    private Canvas _screenRainCanvas;
    private RawImage _screenRainNearLayer;
    private RawImage _screenRainFarLayer;
    private Texture2D _screenRainTexture;
    private int _screenRainTextureStreakCount = -1;
    private int _screenRainTextureSeed;
    private float _screenRainCurrentAlpha;
    private float _screenRainUvOffset;
    private float _screenRainWindUvOffset;
    private bool _isExpansionMode;

    private void OnEnable()
    {
        TrollEvents.OnWaveStageChanged += HandleWaveStageChanged;
        TrollEvents.OnRainLevelChanged += HandleRainLevelChanged;
        OmokWindVisualEvents.OnVelocityXChanged += HandleWindVisualVelocityXChanged;
        TrollEvents.OnExpansionModeChanged += HandleExpansionModeChanged;
        GameEvents.OnGameOverTriggered += HandleGameOver;
    }

    private void OnDisable()
    {
        TrollEvents.OnWaveStageChanged -= HandleWaveStageChanged;
        TrollEvents.OnRainLevelChanged -= HandleRainLevelChanged;
        OmokWindVisualEvents.OnVelocityXChanged -= HandleWindVisualVelocityXChanged;
        TrollEvents.OnExpansionModeChanged -= HandleExpansionModeChanged;
        GameEvents.OnGameOverTriggered -= HandleGameOver;

        StopRainParticles(true);
        HideScreenRainOverlay();
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
        UpdateEmitterFollow();
        UpdateWindVelocityTransition();
        UpdateScreenRainOverlay();
        HandleKeyboardTesting();
    }

    private void OnValidate()
    {
        rainLevel1RateOverTime = Mathf.Max(0f, rainLevel1RateOverTime);
        rainLevel2RateOverTime = Mathf.Max(0f, rainLevel2RateOverTime);
        rainLevel3RateOverTime = Mathf.Max(0f, rainLevel3RateOverTime);
        windVelocityTransitionSeconds = Mathf.Max(0f, windVelocityTransitionSeconds);
        emitterHeightOffset = Mathf.Max(0f, emitterHeightOffset);
        emitterLookAheadDistance = Mathf.Max(0f, emitterLookAheadDistance);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
        screenRainAmountMultiplier = Mathf.Clamp(screenRainAmountMultiplier, 0.25f, 1.25f);
        screenRainWindAngleAtReference = Mathf.Clamp(screenRainWindAngleAtReference, 0f, 20f);
        screenRainWindFollowStrength = Mathf.Clamp(screenRainWindFollowStrength, 0f, 3f);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            InvalidateScreenRainTexture();
            ApplyRainCollisionMask();
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
        if (TrollEvents.IsGameplayEventBlocked)
        {
            return;
        }

        if (useWaveStageEvent)
        {
            ApplyRainLevel(GetLevelForStage(stage, rainLevelProgression), false, true);
        }
    }

    private void HandleRainLevelChanged(int level)
    {
        if (TrollEvents.IsGameplayEventBlocked)
        {
            return;
        }

        if (useRainLevelEvent)
        {
            ApplyRainLevel(level, false, true);
        }
    }

    private void HandleWindVisualVelocityXChanged(float velocityX)
    {
        if (TrollEvents.IsGameplayEventBlocked)
        {
            return;
        }

        if (useWindVisualVelocityEvent)
        {
            SetWindVelocityTarget(velocityX);
        }
    }

    private void HandleExpansionModeChanged(bool isExpansionMode)
    {
        _isExpansionMode = isExpansionMode;

        if (!ShouldShowScreenRainOverlay())
        {
            HideScreenRainOverlay();
        }
    }

    private void HandleGameOver()
    {
        ApplyRainLevel(0, true, false);
        StopRainParticles(true);
        HideScreenRainOverlay();
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
            if (!_missingRainInstanceWarningLogged)
            {
                Debug.LogWarning(
                    "[RainParticleEffect] Place the Rain Particles prefab in the scene, or assign Existing Rain Instance.",
                    this);
                _missingRainInstanceWarningLogged = true;
            }

            return false;
        }

        _missingRainInstanceWarningLogged = false;
        CaptureParticleSystems();
        CaptureInitialEmitterHeight();
        ApplySimulationSpace();
        ApplyRainCollisionMask();
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

    private void CaptureInitialEmitterHeight()
    {
        if (_rainInstance == null || _hasInitialEmitterWorldY)
        {
            return;
        }

        _initialEmitterWorldY = _rainInstance.transform.position.y;
        _hasInitialEmitterWorldY = true;
    }

    private void ApplySimulationSpace()
    {
        if (!forceWorldSimulationSpace)
        {
            return;
        }

        for (int i = 0; i < _rainParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _rainParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private static bool IsRainParticleSystem(ParticleSystem particleSystem)
    {
        return particleSystem != null &&
               particleSystem.gameObject.name.ToLowerInvariant().Contains("rain");
    }

    private void ApplyRainCollisionMask()
    {
        if (!ignoreBoardLayerForCollision)
        {
            return;
        }

        int boardLayer = LayerMask.NameToLayer(BoardLayerName);
        if (boardLayer < 0)
        {
            return;
        }

        int boardLayerMask = 1 << boardLayer;
        for (int i = 0; i < _rainParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _rainParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.CollisionModule collision = particleSystem.collision;
            if (!collision.enabled)
            {
                continue;
            }

            LayerMask collidesWith = collision.collidesWith;
            collidesWith.value &= ~boardLayerMask;
            collision.collidesWith = collidesWith;
        }
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
        bool hasRainInstance = ResolveRainInstance();

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
            if (hasRainInstance)
            {
                ApplyWindVelocityX(_currentWindVelocityX);
            }
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

    private void UpdateEmitterFollow()
    {
        if (!followActiveCamera)
        {
            return;
        }

        if (_rainInstance == null && _currentRainLevel <= 0)
        {
            return;
        }

        if (!ResolveRainInstance())
        {
            return;
        }

        Transform target = ResolveFollowTarget();
        if (target == null)
        {
            return;
        }

        Vector3 horizontalForward = target.forward;
        horizontalForward.y = 0f;
        if (horizontalForward.sqrMagnitude > 0.001f)
        {
            horizontalForward.Normalize();
        }
        else
        {
            horizontalForward = Vector3.forward;
        }

        Vector3 position = target.position + horizontalForward * emitterLookAheadDistance;
        position.x += emitterWorldOffset.x;
        position.z += emitterWorldOffset.z;
        position.y = keepInitialEmitterHeight && _hasInitialEmitterWorldY
            ? _initialEmitterWorldY + emitterWorldOffset.y
            : target.position.y + emitterHeightOffset + emitterWorldOffset.y;

        _rainInstance.transform.position = position;
    }

    private Transform ResolveFollowTarget()
    {
        if (followTarget != null)
        {
            return followTarget;
        }

        if (!resolveMainCameraAsTarget)
        {
            return null;
        }

        if (_cachedMainCamera != null && _cachedMainCamera.isActiveAndEnabled)
        {
            return _cachedMainCamera.transform;
        }

        _targetRefreshElapsed -= Time.deltaTime;
        if (_targetRefreshElapsed > 0f)
        {
            return null;
        }

        _targetRefreshElapsed = targetRefreshInterval;
        _cachedMainCamera = FindActiveCamera();
        return _cachedMainCamera != null ? _cachedMainCamera.transform : null;
    }

    private static Camera FindActiveCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        return null;
    }

    private void PlayRainParticles()
    {
        ApplyRainCollisionMask();

        for (int i = 0; i < _rainParticleSystems.Count; i++)
        {
            ParticleSystem particleSystem = _rainParticleSystems[i];
            if (particleSystem != null && !particleSystem.isPlaying)
            {
                particleSystem.Play(false);
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

    private void UpdateScreenRainOverlay()
    {
        float targetAlpha = ShouldShowScreenRainOverlay() ? GetScreenRainAlphaForLevel(_currentRainLevel) : 0f;
        if (targetAlpha <= 0f && _screenRainCurrentAlpha <= 0f && _screenRainCanvas == null)
        {
            return;
        }

        int targetStreakCount = targetAlpha > 0f
            ? GetScreenRainStreakCountForLevel(_currentRainLevel)
            : _screenRainTextureStreakCount;

        if ((targetAlpha > 0f || _screenRainCurrentAlpha > 0f) && !EnsureScreenRainOverlay(targetStreakCount))
        {
            return;
        }

        _screenRainCurrentAlpha = ScreenRainFadeSpeed > 0f
            ? Mathf.MoveTowards(_screenRainCurrentAlpha, targetAlpha, ScreenRainFadeSpeed * Time.deltaTime)
            : targetAlpha;

        if (_screenRainCanvas == null)
        {
            return;
        }

        bool visible = _screenRainCurrentAlpha > 0.001f;
        if (_screenRainCanvas.gameObject.activeSelf != visible)
        {
            _screenRainCanvas.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            SetScreenRainOverlayAlpha(0f);
            return;
        }

        float screenWind = GetNormalizedScreenRainWind();
        float scrollSpeed = ScreenRainScrollSpeed * (1f + Mathf.Abs(screenWind) * ScreenRainWindSpeedBoost);
        _screenRainUvOffset += scrollSpeed * Time.deltaTime;
        _screenRainWindUvOffset += (ShouldSyncScreenRainWithWind() ? GetSignedScreenRainWindVelocityX() : 0f) *
                                   ScreenRainWindHorizontalScrollScale *
                                   screenRainWindFollowStrength *
                                   Time.deltaTime;

        ApplyScreenRainWindTransform(screenWind);
        SetScreenRainOverlayAlpha(_screenRainCurrentAlpha);
        SetScreenRainLayerUv(
            _screenRainFarLayer,
            ScreenRainFarTiling,
            _screenRainUvOffset * 0.72f,
            _screenRainWindUvOffset * 0.58f);
        SetScreenRainLayerUv(
            _screenRainNearLayer,
            ScreenRainNearTiling,
            _screenRainUvOffset,
            _screenRainWindUvOffset);
    }

    private bool EnsureScreenRainOverlay(int streakCount)
    {
        if (_screenRainCanvas != null && _screenRainNearLayer != null && _screenRainFarLayer != null)
        {
            return EnsureScreenRainTexture(streakCount);
        }

        GameObject canvasObject = new("Rain Screen Overlay Canvas");
        canvasObject.transform.SetParent(transform, false);

        _screenRainCanvas = canvasObject.AddComponent<Canvas>();
        _screenRainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _screenRainCanvas.sortingOrder = short.MaxValue - 200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _screenRainFarLayer = CreateScreenRainLayer(canvasObject.transform, "Rain Screen Far Layer", _screenRainTexture);
        _screenRainNearLayer = CreateScreenRainLayer(canvasObject.transform, "Rain Screen Near Layer", _screenRainTexture);

        EnsureScreenRainTexture(streakCount);
        SetScreenRainOverlayAlpha(0f);
        canvasObject.SetActive(false);
        return true;
    }

    private bool EnsureScreenRainTexture(int streakCount)
    {
        int safeStreakCount = Mathf.Max(0, streakCount);
        if (_screenRainTexture != null &&
            _screenRainTextureStreakCount == safeStreakCount &&
            _screenRainTextureSeed == ScreenRainRandomSeed)
        {
            AssignScreenRainTexture(_screenRainTexture);
            return true;
        }

        if (_screenRainTexture != null)
        {
            Destroy(_screenRainTexture);
        }

        _screenRainTexture = CreateScreenRainTexture(ScreenRainTextureSize, safeStreakCount, ScreenRainRandomSeed);
        _screenRainTextureStreakCount = safeStreakCount;
        _screenRainTextureSeed = ScreenRainRandomSeed;
        AssignScreenRainTexture(_screenRainTexture);
        return true;
    }

    private void AssignScreenRainTexture(Texture texture)
    {
        if (_screenRainNearLayer != null)
        {
            _screenRainNearLayer.texture = texture;
        }

        if (_screenRainFarLayer != null)
        {
            _screenRainFarLayer.texture = texture;
        }
    }

    private bool ShouldShowScreenRainOverlay()
    {
        return enableScreenRainOverlay &&
               (!showScreenRainOverlayOnlyInPlacementMode || !_isExpansionMode);
    }

    private RawImage CreateScreenRainLayer(Transform parent, string objectName, Texture texture)
    {
        GameObject imageObject = new(objectName);
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        ApplyScreenRainLayerOverscan(rectTransform);

        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = texture;
        image.color = Color.clear;
        return image;
    }

    private void SetScreenRainOverlayAlpha(float alpha)
    {
        Color nearColor = screenRainColor;
        nearColor.a *= Mathf.Clamp01(alpha);
        Color farColor = screenRainColor;
        farColor.a *= Mathf.Clamp01(alpha * ScreenRainFarLayerAlphaScale);

        if (_screenRainNearLayer != null)
        {
            _screenRainNearLayer.color = nearColor;
        }

        if (_screenRainFarLayer != null)
        {
            _screenRainFarLayer.color = farColor;
        }
    }

    private void SetScreenRainLayerUv(RawImage image, float tiling, float verticalOffset, float horizontalOffset)
    {
        if (image == null)
        {
            return;
        }

        float safeTiling = Mathf.Max(1.8f, tiling);
        float activeSlant = ShouldSyncScreenRainWithWind()
            ? ScreenRainSlant * GetNormalizedScreenRainWind() * screenRainWindFollowStrength
            : 0f;
        image.uvRect = new Rect(
            horizontalOffset + verticalOffset * activeSlant,
            verticalOffset,
            safeTiling,
            safeTiling);
    }

    private void ApplyScreenRainWindTransform(float normalizedWind)
    {
        float angle = ShouldSyncScreenRainWithWind()
            ? -normalizedWind * screenRainWindAngleAtReference * screenRainWindFollowStrength
            : 0f;

        ApplyScreenRainLayerTransform(_screenRainFarLayer, angle * 0.72f);
        ApplyScreenRainLayerTransform(_screenRainNearLayer, angle);
    }

    private void ApplyScreenRainLayerTransform(RawImage image, float angle)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rectTransform = image.rectTransform;
        ApplyScreenRainLayerOverscan(rectTransform);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ApplyScreenRainLayerOverscan(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        float overscan = Mathf.Max(0f, ScreenRainLayerOverscanPixels);
        rectTransform.offsetMin = new Vector2(-overscan, -overscan);
        rectTransform.offsetMax = new Vector2(overscan, overscan);
    }

    private void HideScreenRainOverlay()
    {
        _screenRainCurrentAlpha = 0f;

        if (_screenRainCanvas == null)
        {
            return;
        }

        SetScreenRainOverlayAlpha(0f);
        _screenRainCanvas.gameObject.SetActive(false);
    }

    private Texture2D CreateScreenRainTexture(int size, int streakCount, int randomSeed)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        System.Random random = new(randomSeed);
        int configuredMinLength = Mathf.Clamp(ScreenRainMinStreakLength, 1, size);
        int configuredMaxLength = Mathf.Clamp(Mathf.Max(configuredMinLength, ScreenRainMaxStreakLength), configuredMinLength, size);
        int minLength = Mathf.Clamp(Mathf.RoundToInt(configuredMinLength * ScreenRainDropLengthScale), 1, size);
        int maxLength = Mathf.Clamp(Mathf.RoundToInt(configuredMaxLength * ScreenRainDropLengthScale), minLength, size);

        for (int i = 0; i < streakCount; i++)
        {
            int startX = random.Next(0, size);
            int startY = random.Next(0, size);
            int length = random.Next(minLength, maxLength + 1);
            bool accentStreak = random.NextDouble() > 0.78;
            if (accentStreak)
            {
                length = Mathf.Clamp(Mathf.RoundToInt(length * RandomRange(random, 1.15f, 1.55f)), minLength, maxLength + 18);
            }

            int thickness = accentStreak || random.NextDouble() > 0.88 ? 1 : 0;
            float alpha = accentStreak
                ? RandomRange(random, 0.58f, 0.9f)
                : RandomRange(random, 0.32f, 0.62f);
            float diagonal = RandomRange(random, -0.055f, 0.055f);

            for (int step = 0; step < length; step++)
            {
                float progress = step / (float)Mathf.Max(1, length - 1);
                float fade = Mathf.Sin(progress * Mathf.PI);
                fade = 0.22f + fade * 0.78f;
                int x = WrapIndex(startX + Mathf.RoundToInt(step * diagonal), size);
                int y = WrapIndex(startY + step, size);

                for (int width = -thickness; width <= thickness; width++)
                {
                    float widthFade = width == 0 ? 1f : 0.3f;
                    BlendRainPixel(pixels, size, WrapIndex(x + width, size), y, alpha * fade * widthFade);
                }
            }
        }

        int dropletCount = Mathf.RoundToInt(streakCount * 0.45f);
        for (int i = 0; i < dropletCount; i++)
        {
            int x = random.Next(0, size);
            int y = random.Next(0, size);
            float alpha = RandomRange(random, 0.18f, 0.42f);
            BlendRainPixel(pixels, size, x, y, alpha);

            if (random.NextDouble() > 0.72)
            {
                BlendRainPixel(pixels, size, WrapIndex(x + 1, size), y, alpha * 0.45f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void BlendRainPixel(Color[] pixels, int size, int x, int y, float alpha)
    {
        int index = y * size + x;
        float blendedAlpha = Mathf.Max(pixels[index].a, Mathf.Clamp01(alpha));
        pixels[index] = new Color(1f, 1f, 1f, blendedAlpha);
    }

    private static int WrapIndex(int value, int size)
    {
        value %= size;
        return value < 0 ? value + size : value;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return min + (max - min) * (float)random.NextDouble();
    }

    private void HandleKeyboardTesting()
    {
        if (TrollEvents.IsGameplayEventBlocked)
        {
            return;
        }

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

    private float GetScreenRainAlphaForLevel(int level)
    {
        switch (level)
        {
            case 1:
                return ScreenRainLevel1Alpha;
            case 2:
                return ScreenRainLevel2Alpha;
            case 3:
                return ScreenRainLevel3Alpha;
            default:
                return 0f;
        }
    }

    private float GetNormalizedScreenRainWind()
    {
        if (!ShouldSyncScreenRainWithWind())
        {
            return 0f;
        }

        return Mathf.Clamp(
            GetSignedScreenRainWindVelocityX() / Mathf.Max(0.01f, ScreenRainWindReferenceVelocityX),
            -1f,
            1f);
    }

    private bool ShouldSyncScreenRainWithWind()
    {
        return enableScreenRainWindTilt && useWindVisualVelocityEvent;
    }

    private float GetSignedScreenRainWindVelocityX()
    {
        return InvertScreenRainWindDirection ? -_currentWindVelocityX : _currentWindVelocityX;
    }

    private int GetScreenRainStreakCountForLevel(int level)
    {
        switch (level)
        {
            case 1:
                return Mathf.RoundToInt(ScreenRainLevel1StreakCount * screenRainAmountMultiplier);
            case 2:
                return Mathf.RoundToInt(ScreenRainLevel2StreakCount * screenRainAmountMultiplier);
            case 3:
                return Mathf.RoundToInt(ScreenRainLevel3StreakCount * screenRainAmountMultiplier);
            default:
                return 0;
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

    private void InvalidateScreenRainTexture()
    {
        _screenRainTextureStreakCount = -1;
        _screenRainTextureSeed = 0;
    }

    private void OnDestroy()
    {
        if (_screenRainTexture != null)
        {
            Destroy(_screenRainTexture);
        }
    }
}
