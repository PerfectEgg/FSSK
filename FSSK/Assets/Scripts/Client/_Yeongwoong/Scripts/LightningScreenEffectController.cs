using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
public sealed class LightningScreenEffectController : MonoBehaviour
{
    private const string TempObjectName = "TEMP Lightning Screen Effect Controller";

    [Header("임시 테스트 설정")]
    [SerializeField] private bool previewOnStart = true;
    [SerializeField, Range(0, 3)] private int previewLightningLevel = 2;
    [SerializeField] private bool triggerLightningOnStart = false;
    [SerializeField, InspectorName("단축키 테스트 사용")] private bool allowKeyboardTesting = true;
    [SerializeField, InspectorName("번개 실행 단축키")] private KeyCode triggerLightningKey = KeyCode.L;
    [SerializeField, InspectorName("1/2/3 레벨 단축키 사용")] private bool allowLevelHotkeys = true;

    [Header("UI 및 연출 설정")]
    [SerializeField] private AudioSource _audioSource;

    [Header("웨이브 연동 설정")]
    [SerializeField] private bool useWaveStageEvent = true;
    [SerializeField] private bool showLightningLog = true;
    [SerializeField] private List<int> _levelProgression = new() { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3 };

    [Header("단계별 패턴 값 (Inspector에서 설정)")]
    [SerializeField] private LightningPattern _pattern1 = new(14f, 22f, 0.075f, 0.14f, 1.35f);
    [SerializeField] private LightningPattern _pattern2 = new(10f, 16f, 0.075f, 0.18f, 1.65f);
    [SerializeField] private LightningPattern _pattern3 = new(7f, 12f, 0.09f, 0.24f, 2f);

    [Header("섬광 설정")]
    [SerializeField] private Color flashColor = new(0.82f, 0.9f, 1f, 1f);
    [SerializeField, Range(0f, 0.6f)] private float flashPeakAlpha = 0.28f;
    [SerializeField, Range(0f, 0.3f)] private float flashAfterAlpha = 0.045f;
    [SerializeField, Min(0.01f)] private float flashRiseSeconds = 0.035f;
    [SerializeField, Min(0.01f)] private float flashDropSeconds = 0.075f;
    [SerializeField, Min(0f)] private float blindedHoldSeconds = 0.14f;

    [Header("시야 회복 연출")]
    [SerializeField] private Color hazeColor = new(0.92f, 0.94f, 0.96f, 1f);
    [SerializeField, Range(0f, 0.75f)] private float hazePeakAlpha = 0.14f;
    [SerializeField, Range(0f, 1f)] private float hazeEdgeBoost = 0.2f;
    [SerializeField, Range(0f, 0.4f)] private float hazeNoiseStrength = 0.04f;
    [SerializeField, Min(0.01f)] private float recoverySeconds = 1.65f;

    [Header("눈꺼풀 암전 마스크")]
    [SerializeField] private Color blinkColor = new(0.13f, 0.17f, 0.21f, 1f);
    [SerializeField, Range(0f, 1f)] private float blinkPeakAlpha = 0.44f;
    [SerializeField, Range(0f, 0.45f)] private float upperBlinkHeight = 0.34f;
    [SerializeField, Range(0f, 0.3f)] private float lowerBlinkHeight = 0.14f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("카메라 블러")]
    [SerializeField] private bool enableCameraBlur = true;
    [SerializeField, Range(0f, 1f)] private float blurPeakWeight = 1f;

    private Image _flashLayer;
    private RawImage _hazeLayer;
    private RawImage _upperBlinkLayer;
    private RawImage _lowerBlinkLayer;
    private RectTransform _upperBlinkRect;
    private RectTransform _lowerBlinkRect;
    private Texture2D _hazeTexture;
    private Texture2D _upperBlinkTexture;
    private Texture2D _lowerBlinkTexture;
    private Volume _blurVolume;
    private VolumeProfile _blurProfile;
    private DepthOfField _depthOfField;
    private Coroutine _lightningRoutine;
    private Coroutine _waveLightningRoutine;
    private int _lightningLevel;
    private int _waveLightningLevel;
    private bool _hasCapturedPostProcessingStates;
    private readonly List<CameraPostProcessState> _cameraPostProcessStates = new();

    [System.Serializable]
    private struct LightningPattern
    {
        [Min(0.1f)] public float minInterval;
        [Min(0.1f)] public float maxInterval;
        [Min(0f)] public float fadeOutTime;
        [Min(0f)] public float blockTime;
        [Min(0f)] public float fadeInTime;
        public AudioClip thunderSound;

        public LightningPattern(float minInterval, float maxInterval, float fadeOutTime, float blockTime, float fadeInTime)
        {
            this.minInterval = minInterval;
            this.maxInterval = maxInterval;
            this.fadeOutTime = fadeOutTime;
            this.blockTime = blockTime;
            this.fadeInTime = fadeInTime;
            thunderSound = null;
        }

        public float GetDelay()
        {
            float min = Mathf.Max(0.1f, minInterval);
            float max = Mathf.Max(min, maxInterval);
            return Random.Range(min, max);
        }
    }

    private readonly struct LightningIntensity
    {
        public readonly float FlashScale;
        public readonly float AfterFlashScale;
        public readonly float HazeScale;
        public readonly float BlinkAlphaScale;
        public readonly float UpperBlinkScale;
        public readonly float LowerBlinkScale;
        public readonly float BlurScale;
        public readonly float HoldScale;
        public readonly float RecoveryScale;

        public LightningIntensity(
            float flashScale,
            float afterFlashScale,
            float hazeScale,
            float blinkAlphaScale,
            float upperBlinkScale,
            float lowerBlinkScale,
            float blurScale,
            float holdScale,
            float recoveryScale)
        {
            FlashScale = flashScale;
            AfterFlashScale = afterFlashScale;
            HazeScale = hazeScale;
            BlinkAlphaScale = blinkAlphaScale;
            UpperBlinkScale = upperBlinkScale;
            LowerBlinkScale = lowerBlinkScale;
            BlurScale = blurScale;
            HoldScale = holdScale;
            RecoveryScale = recoveryScale;
        }
    }

    private readonly struct CameraPostProcessState
    {
        public readonly UniversalAdditionalCameraData CameraData;
        public readonly bool WasPostProcessingEnabled;

        public CameraPostProcessState(UniversalAdditionalCameraData cameraData, bool wasPostProcessingEnabled)
        {
            CameraData = cameraData;
            WasPostProcessingEnabled = wasPostProcessingEnabled;
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForTrollTestPreview()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "TrollTest" ||
            FindFirstObjectByType<LightningScreenEffectController>() != null)
        {
            return;
        }

        GameObject controller = new(TempObjectName);
        controller.AddComponent<LightningScreenEffectController>();
    }
#endif

    private void OnEnable()
    {
        TrollEvents.OnWaveStageChanged += HandleWaveStageChanged;
        TrollEvents.OnLightningLevelChanged += HandleLightningLevelChanged;
        TrollEvents.OnLightningStrikeRequested += HandleLightningStrikeRequested;
    }

    private void OnDisable()
    {
        TrollEvents.OnWaveStageChanged -= HandleWaveStageChanged;
        TrollEvents.OnLightningLevelChanged -= HandleLightningLevelChanged;
        TrollEvents.OnLightningStrikeRequested -= HandleLightningStrikeRequested;

        StopWaveLightningRoutine();
    }

    private void Start()
    {
        EnsureOverlay();

        if (!previewOnStart)
        {
            return;
        }

        SetLightningLevel(previewLightningLevel);

        if (triggerLightningOnStart)
        {
            TriggerLightning();
        }
    }

    private void Update()
    {
        if (allowKeyboardTesting && Input.GetKeyDown(triggerLightningKey))
        {
            TriggerLightning();
        }

        if (!allowKeyboardTesting || !allowLevelHotkeys)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TriggerLightning(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TriggerLightning(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TriggerLightning(3);
        }
    }

    public void SetKeyboardTestingEnabled(bool enabled)
    {
        allowKeyboardTesting = enabled;
    }

    public void SetLevelHotkeysEnabled(bool enabled)
    {
        allowLevelHotkeys = enabled;
    }

    [ContextMenu("TEMP/Enable Test Hotkeys")]
    private void EnableTestHotkeys()
    {
        SetKeyboardTestingEnabled(true);
    }

    [ContextMenu("TEMP/Disable Test Hotkeys")]
    private void DisableTestHotkeys()
    {
        SetKeyboardTestingEnabled(false);
    }

    [ContextMenu("TEMP/Trigger Lightning Screen Effect")]
    public void TriggerLightning()
    {
        TriggerLightning(GetRandomPattern(_lightningLevel));
    }

    private void TriggerLightning(LightningPattern pattern)
    {
        EnsureOverlay();

        if (_lightningLevel <= 0)
        {
            return;
        }

        if (_lightningRoutine != null)
        {
            StopCoroutine(_lightningRoutine);
            ResetEffectState(false);
        }

        _lightningRoutine = StartCoroutine(PlayLightningEffect(pattern));
    }

    public void TriggerLightning(int level)
    {
        SetLightningLevel(level);
        TriggerLightning(GetRandomPattern(_lightningLevel));
    }

    private void TriggerLightning(int level, LightningPattern pattern)
    {
        SetLightningLevel(level);
        TriggerLightning(pattern);
    }

    private void HandleWaveStageChanged(int stage)
    {
        if (!useWaveStageEvent)
        {
            return;
        }

        ApplyWaveLightningLevel(GetLightningLevelForStage(stage));
    }

    private void HandleLightningLevelChanged(int level)
    {
        if (useWaveStageEvent)
        {
            ApplyWaveLightningLevel(level);
            return;
        }

        SetLightningLevel(level);
    }

    private void HandleLightningStrikeRequested(int level, int patternIndex)
    {
        int clampedLevel = Mathf.Clamp(level, 0, 3);
        if (clampedLevel <= 0)
        {
            return;
        }

        TriggerLightning(clampedLevel, GetPatternByIndex(patternIndex));
    }

    private void SetLightningLevel(int level)
    {
        _lightningLevel = Mathf.Clamp(level, 0, 3);
    }

    private void ApplyWaveLightningLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 0, 3);

        if (_waveLightningLevel == clampedLevel && _waveLightningRoutine != null)
        {
            return;
        }

        _waveLightningLevel = clampedLevel;
        SetLightningLevel(clampedLevel);
        StopWaveLightningRoutine();

        if (_lightningRoutine != null)
        {
            StopCoroutine(_lightningRoutine);
            _lightningRoutine = null;
        }

        ResetEffectState(false);

        if (clampedLevel == 0)
        {
            return;
        }

        if (!CanScheduleLightningStrikes())
        {
            return;
        }

        if (showLightningLog)
        {
            Debug.Log($"[번개] {clampedLevel}단계 번개 코루틴을 시작합니다!", this);
        }

        _waveLightningRoutine = StartCoroutine(WaveLightningRoutine(clampedLevel));
    }

    private IEnumerator WaveLightningRoutine(int level)
    {
        while (_waveLightningLevel == level && level > 0)
        {
            int patternIndex = GetRandomPatternIndex(level);
            LightningPattern timing = GetPatternByIndex(patternIndex);
            yield return new WaitForSeconds(timing.GetDelay());

            if (_waveLightningLevel != level || level <= 0)
            {
                continue;
            }

            RequestLightningStrike(level, patternIndex);

            yield return null;
            while (_lightningRoutine != null)
            {
                yield return null;
            }
        }
    }

    private void StopWaveLightningRoutine()
    {
        if (_waveLightningRoutine == null)
        {
            return;
        }

        StopCoroutine(_waveLightningRoutine);
        _waveLightningRoutine = null;
    }

    private LightningPattern GetRandomPattern(int level)
    {
        return GetPatternByIndex(GetRandomPatternIndex(level));
    }

    private static int GetRandomPatternIndex(int level)
    {
        int maxPatternIndex = Mathf.Clamp(level, 1, 3);
        return Random.Range(1, maxPatternIndex + 1);
    }

    private LightningPattern GetPatternByIndex(int patternIndex)
    {
        int clampedPatternIndex = Mathf.Clamp(patternIndex, 1, 3);

        return clampedPatternIndex switch
        {
            1 => _pattern1,
            2 => _pattern2,
            _ => _pattern3
        };
    }

    private void RequestLightningStrike(int level, int patternIndex)
    {
        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.BroadcastLightningStrike(level, patternIndex);
                return;
            }

            Debug.LogWarning("[LightningScreenEffectController] NetworkGameManager missing; lightning strike will only play locally.", this);
        }

        HandleLightningStrikeRequested(level, patternIndex);
    }

    private static bool CanScheduleLightningStrikes()
    {
        return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    }

    private IEnumerator PlayLightningEffect(LightningPattern pattern)
    {
        if (_flashLayer == null || _hazeLayer == null || _upperBlinkLayer == null || _lowerBlinkLayer == null)
        {
            yield break;
        }

        LightningIntensity intensity = GetIntensityForLevel(_lightningLevel);
        float flashPeak = flashPeakAlpha * intensity.FlashScale;
        float flashAfter = flashAfterAlpha * intensity.AfterFlashScale;
        float hazePeak = hazePeakAlpha * intensity.HazeScale;
        float blinkAlpha = blinkPeakAlpha * intensity.BlinkAlphaScale;
        float upperHeight = upperBlinkHeight * intensity.UpperBlinkScale;
        float lowerHeight = lowerBlinkHeight * intensity.LowerBlinkScale;
        float blurPeak = blurPeakWeight * intensity.BlurScale;
        float flashDropDuration = GetDurationOrDefault(pattern.fadeOutTime, flashDropSeconds);
        float holdSeconds = GetDurationOrDefault(pattern.blockTime, blindedHoldSeconds) * intensity.HoldScale;
        float recoverDuration = GetDurationOrDefault(pattern.fadeInTime, recoverySeconds) * intensity.RecoveryScale;

        CaptureAndEnablePostProcessing();
        SetHaze(0f);
        SetBlink(0f, 0f, 0f);
        SetBlur(0f);

        yield return FadeFlash(0f, flashPeak, flashRiseSeconds);
        PlayThunderSound(pattern);
        SetHaze(hazePeak);
        SetBlink(blinkAlpha, upperHeight, lowerHeight);
        SetBlur(blurPeak);
        yield return FadeFlash(flashPeak, flashAfter, flashDropDuration);

        if (holdSeconds > 0f)
        {
            yield return new WaitForSeconds(holdSeconds);
        }

        SetFlash(0f);
        yield return FadeRecovery(hazePeak, 0f, blinkAlpha, 0f, upperHeight, 0f, lowerHeight, 0f, blurPeak, 0f, recoverDuration);

        ResetEffectState(false);
        _lightningRoutine = null;
    }

    private IEnumerator FadeFlash(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            SetFlash(Mathf.Lerp(fromAlpha, toAlpha, eased));
            yield return null;
        }

        SetFlash(toAlpha);
    }

    private void PlayThunderSound(LightningPattern pattern)
    {
        if (_audioSource == null || pattern.thunderSound == null)
        {
            return;
        }

        _audioSource.PlayOneShot(pattern.thunderSound);
    }

    private static float GetDurationOrDefault(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }

    private IEnumerator FadeRecovery(
        float fromHazeAlpha,
        float toHazeAlpha,
        float fromBlinkAlpha,
        float toBlinkAlpha,
        float fromUpperHeight,
        float toUpperHeight,
        float fromLowerHeight,
        float toLowerHeight,
        float fromBlurWeight,
        float toBlurWeight,
        float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float openT = openCurve != null ? openCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            float hazeT = Mathf.SmoothStep(0f, 1f, t);

            SetHaze(Mathf.Lerp(fromHazeAlpha, toHazeAlpha, hazeT));
            SetBlink(
                Mathf.Lerp(fromBlinkAlpha, toBlinkAlpha, openT),
                Mathf.Lerp(fromUpperHeight, toUpperHeight, openT),
                Mathf.Lerp(fromLowerHeight, toLowerHeight, openT));
            SetBlur(Mathf.Lerp(fromBlurWeight, toBlurWeight, hazeT));

            yield return null;
        }

        SetHaze(toHazeAlpha);
        SetBlink(toBlinkAlpha, toUpperHeight, toLowerHeight);
        SetBlur(toBlurWeight);
    }

    private void EnsureOverlay()
    {
        if (_flashLayer != null && _hazeLayer != null && _upperBlinkLayer != null && _lowerBlinkLayer != null)
        {
            return;
        }

        Canvas canvas = CreateCanvas();
        _hazeTexture = CreateHazeTexture(256);
        _upperBlinkTexture = CreateBlinkTexture(512, 192, true);
        _lowerBlinkTexture = CreateBlinkTexture(512, 192, false);

        _hazeLayer = CreateRawImage(canvas.transform, "Lightning Haze Recovery", _hazeTexture, hazeColor);
        _upperBlinkLayer = CreateBlinkLayer(canvas.transform, "Lightning Upper Soft Blink", _upperBlinkTexture, blinkColor, true, out _upperBlinkRect);
        _lowerBlinkLayer = CreateBlinkLayer(canvas.transform, "Lightning Lower Soft Blink", _lowerBlinkTexture, blinkColor, false, out _lowerBlinkRect);
        _flashLayer = CreateImage(canvas.transform, "Lightning Flash", flashColor);

        SetHaze(0f);
        SetBlink(0f, 0f, 0f);
        SetFlash(0f);
        SetBlur(0f);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new("Lightning Screen Effect Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = CreateFullScreenUiObject(parent, objectName);
        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(Transform parent, string objectName, Texture texture, Color color)
    {
        GameObject imageObject = CreateFullScreenUiObject(parent, objectName);
        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = texture;
        image.color = color;
        return image;
    }

    private static RawImage CreateBlinkLayer(Transform parent, string objectName, Texture texture, Color color, bool upper, out RectTransform rectTransform)
    {
        GameObject imageObject = new(objectName);
        imageObject.transform.SetParent(parent, false);

        rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = upper ? new Vector2(0f, 1f) : Vector2.zero;
        rectTransform.anchorMax = upper ? Vector2.one : new Vector2(1f, 0f);
        rectTransform.pivot = upper ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        RawImage image = imageObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.texture = texture;
        image.color = color;
        return image;
    }

    private static GameObject CreateFullScreenUiObject(Transform parent, string objectName)
    {
        GameObject uiObject = new(objectName);
        uiObject.transform.SetParent(parent, false);
        RectTransform rectTransform = uiObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return uiObject;
    }

    private Texture2D CreateHazeTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((float)x / (size - 1) - 0.5f) * 2f;
                float ny = ((float)y / (size - 1) - 0.5f) * 2f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = Mathf.SmoothStep(0.28f, 1.04f, distance);
                float verticalEdge = Mathf.SmoothStep(0.16f, 1f, Mathf.Abs(ny));
                float noise = Mathf.PerlinNoise(x * 0.052f + 18.3f, y * 0.052f + 71.9f) - 0.5f;
                float alpha = Mathf.Clamp01(0.35f + edge * hazeEdgeBoost + verticalEdge * 0.08f + noise * hazeNoiseStrength);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateBlinkTexture(int width, int height, bool upper)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float y01 = (float)y / (height - 1);

            for (int x = 0; x < width; x++)
            {
                float x01 = (float)x / (width - 1);
                float side = Mathf.Abs(x01 - 0.5f) * 2f;
                float center = 1f - Mathf.Clamp01(side);
                float arch = Mathf.Pow(center, 1.55f) * 0.17f;
                float softWave = Mathf.Sin((x01 * 4.2f + (upper ? 0.2f : 1.05f)) * Mathf.PI) * 0.018f;
                float edge = upper
                    ? 0.25f + arch + softWave
                    : 0.75f - arch - softWave;

                float alpha = upper
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.12f, edge + 0.07f, y01))
                    : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.07f, edge + 0.12f, y01));

                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private void SetFlash(float alpha)
    {
        SetGraphicAlpha(_flashLayer, alpha);
    }

    private void SetHaze(float alpha)
    {
        SetGraphicAlpha(_hazeLayer, alpha);
    }

    private void SetBlur(float weight)
    {
        EnsureBlurVolume();

        if (_blurVolume == null)
        {
            return;
        }

        _blurVolume.weight = enableCameraBlur ? Mathf.Clamp01(weight) : 0f;
    }

    private void SetBlink(float alpha, float upperHeight01, float lowerHeight01)
    {
        float height = 1080f;

        if (_upperBlinkRect != null)
        {
            _upperBlinkRect.sizeDelta = new Vector2(0f, height * Mathf.Clamp01(upperHeight01));
        }

        if (_lowerBlinkRect != null)
        {
            _lowerBlinkRect.sizeDelta = new Vector2(0f, height * Mathf.Clamp01(lowerHeight01));
        }

        SetGraphicAlpha(_upperBlinkLayer, alpha);
        SetGraphicAlpha(_lowerBlinkLayer, alpha);
    }

    private void ResetEffectState(bool restorePostProcessing)
    {
        SetFlash(0f);
        SetHaze(0f);
        SetBlink(0f, 0f, 0f);
        SetBlur(0f);

        if (restorePostProcessing)
        {
            RestorePostProcessing();
        }
    }

    private void EnsureBlurVolume()
    {
        if (!enableCameraBlur || _blurVolume != null)
        {
            return;
        }

        GameObject volumeObject = new("Lightning Temporary Blur Volume");
        volumeObject.transform.SetParent(transform, false);

        _blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _blurProfile.name = "Lightning Temporary Blur Profile";

        _depthOfField = _blurProfile.Add<DepthOfField>(true);
        _depthOfField.active = true;
        _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
        _depthOfField.gaussianStart.Override(0f);
        _depthOfField.gaussianEnd.Override(0.01f);
        _depthOfField.gaussianMaxRadius.Override(1.5f);
        _depthOfField.highQualitySampling.Override(true);
        _depthOfField.focusDistance.Override(0.1f);
        _depthOfField.aperture.Override(1f);
        _depthOfField.focalLength.Override(190f);

        _blurVolume = volumeObject.AddComponent<Volume>();
        _blurVolume.isGlobal = true;
        _blurVolume.priority = 1000f;
        _blurVolume.weight = 0f;
        _blurVolume.sharedProfile = _blurProfile;
    }

    private void CaptureAndEnablePostProcessing()
    {
        if (!enableCameraBlur)
        {
            return;
        }

        if (_hasCapturedPostProcessingStates)
        {
            return;
        }

        _cameraPostProcessStates.Clear();

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            _cameraPostProcessStates.Add(new CameraPostProcessState(cameraData, cameraData.renderPostProcessing));
            cameraData.renderPostProcessing = true;
        }

        _hasCapturedPostProcessingStates = true;
    }

    private void RestorePostProcessing()
    {
        for (int i = 0; i < _cameraPostProcessStates.Count; i++)
        {
            CameraPostProcessState state = _cameraPostProcessStates[i];
            if (state.CameraData != null)
            {
                state.CameraData.renderPostProcessing = state.WasPostProcessingEnabled;
            }
        }

        _cameraPostProcessStates.Clear();
        _hasCapturedPostProcessingStates = false;
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = Mathf.Clamp01(alpha);
        graphic.color = color;
    }

    private int GetLightningLevelForStage(int stage)
    {
        if (_levelProgression == null || _levelProgression.Count == 0)
        {
            if (stage < 3) return 0;
            if (stage < 6) return 1;
            if (stage < 9) return 2;
            return 3;
        }

        int targetIndex = Mathf.Clamp(stage, 0, _levelProgression.Count - 1);
        return Mathf.Clamp(_levelProgression[targetIndex], 0, 3);
    }

    private static LightningIntensity GetIntensityForLevel(int level)
    {
        return Mathf.Clamp(level, 0, 3) switch
        {
            0 => new LightningIntensity(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f),
            1 => new LightningIntensity(0.62f, 0.65f, 0.45f, 0.58f, 0.64f, 0.55f, 0.55f, 0.55f, 0.82f),
            2 => new LightningIntensity(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f),
            _ => new LightningIntensity(1.28f, 1.22f, 1.28f, 1.16f, 1.18f, 1.14f, 1f, 1.5f, 1.32f)
        };
    }

    private void OnDestroy()
    {
        RestorePostProcessing();

        if (_hazeTexture != null)
        {
            Destroy(_hazeTexture);
        }

        if (_upperBlinkTexture != null)
        {
            Destroy(_upperBlinkTexture);
        }

        if (_lowerBlinkTexture != null)
        {
            Destroy(_lowerBlinkTexture);
        }

        if (_blurProfile != null)
        {
            Destroy(_blurProfile);
        }
    }
}
