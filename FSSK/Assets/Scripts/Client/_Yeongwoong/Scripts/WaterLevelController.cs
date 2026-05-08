using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(9100)]
public sealed class WaterLevelController : MonoBehaviour
{
    private static readonly int WaveScalePropertyId = Shader.PropertyToID("_WaveScale");

    [System.Serializable]
    private sealed class WaterLayer
    {
        public string label;
        [FormerlySerializedAs("waterRoot")]
        [FormerlySerializedAs("root")]
        public Transform root = null;
        public GameObject activeTarget = null;

        private Renderer[] _renderers = null;
        private Transform _cachedRoot = null;
        private GameObject _cachedActiveTarget = null;
        private MaterialPropertyBlock _propertyBlock = null;

        public WaterLayer(string label)
        {
            this.label = label;
        }

        public void ApplyY(float y, bool active)
        {
            if (root != null)
            {
                Vector3 position = root.position;
                position.y = y;
                root.position = position;
            }

            GameObject target = activeTarget != null
                ? activeTarget
                : root != null
                    ? root.gameObject
                    : null;

            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        public void ApplyWaveScale(float waveScale)
        {
            Renderer[] renderers = GetRenderers();
            if (renderers == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(WaveScalePropertyId, waveScale);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private Renderer[] GetRenderers()
        {
            if (_renderers != null && _cachedRoot == root && _cachedActiveTarget == activeTarget)
            {
                return _renderers;
            }

            _cachedRoot = root;
            _cachedActiveTarget = activeTarget;

            GameObject rendererRoot = activeTarget != null
                ? activeTarget
                : root != null
                    ? root.gameObject
                    : null;

            _renderers = rendererRoot != null
                ? rendererRoot.GetComponentsInChildren<Renderer>(true)
                : null;

            return _renderers;
        }
    }

    [Header("Stage")]
    [SerializeField, Min(0)] private int appearStage = 4;
    [SerializeField, Min(1)] private int targetStage = 19;
    [SerializeField, Min(0.01f)] private float stageDurationSeconds = 30f;
    [SerializeField] private bool syncStageDurationFromWaveManager = true;

    [Header("Water Root Y")]
    [SerializeField] private float hiddenY = -214.4f;
    [FormerlySerializedAs("lowerStartY")]
    [SerializeField] private float startY = -214.4f;
    [FormerlySerializedAs("deckTargetY")]
    [SerializeField] private float targetY = -16.6f;

    [Header("Wave Scale")]
    [SerializeField] private float defaultWaveScale = 1f;
    [SerializeField] private float reducedWaveScale = 0.3f;
    [SerializeField] private float scaleDownStartY = -60f;
    [SerializeField] private float scaleRestoreStartY = -38.5f;
    [SerializeField] private float scaleRestoreEndY = -16.6f;

    [Header("Water")]
    [FormerlySerializedAs("lowerWater")]
    [SerializeField] private WaterLayer water = new("Water");

    private int _currentStage;
    private float _stageStartedAt;
    private bool _hasStage;
    private FieldInfo _waveManagerStageDurationField;

    private void OnEnable()
    {
        TrollEvents.OnWaveStageChanged += HandleWaveStageChanged;
        HideWater();

        int stage = WaveManager.Instance != null ? WaveManager.Instance.currentStage : 0;
        SetStage(stage, true);
    }

    private void OnDisable()
    {
        TrollEvents.OnWaveStageChanged -= HandleWaveStageChanged;
        water?.ApplyWaveScale(defaultWaveScale);
        _hasStage = false;
    }

    private void Update()
    {
        if (_hasStage)
        {
            ApplyStage();
        }
    }

    private void OnValidate()
    {
        targetStage = Mathf.Max(targetStage, appearStage + 1);
        stageDurationSeconds = Mathf.Max(0.01f, stageDurationSeconds);
    }

    public void SetStage(int stage)
    {
        SetStage(stage, true);
    }

    private void HandleWaveStageChanged(int stage)
    {
        SetStage(stage, false);
    }

    private void SetStage(int stage, bool force)
    {
        int clampedStage = Mathf.Max(0, stage);

        if (!force && _hasStage && _currentStage == clampedStage)
        {
            return;
        }

        _currentStage = clampedStage;
        _stageStartedAt = Time.time;
        _hasStage = true;
        ApplyStage();
    }

    private void ApplyStage()
    {
        float stagePosition = GetStagePosition();

        if (stagePosition < appearStage)
        {
            HideWater();
            return;
        }

        float y = GetWaterY(stagePosition);
        water?.ApplyY(y, true);
        water?.ApplyWaveScale(GetWaveScale(y));
    }

    private float GetStagePosition()
    {
        int stage = Mathf.Max(0, _currentStage);
        float duration = GetStageDurationSeconds();
        float progress = Mathf.Clamp01((Time.time - _stageStartedAt) / duration);
        return stage + progress;
    }

    private float GetStageDurationSeconds()
    {
        float fallbackDuration = Mathf.Max(0.01f, stageDurationSeconds);

        if (!syncStageDurationFromWaveManager || WaveManager.Instance == null)
        {
            return fallbackDuration;
        }

        _waveManagerStageDurationField ??= typeof(WaveManager).GetField(
            "_stageDuration",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (_waveManagerStageDurationField == null)
        {
            return fallbackDuration;
        }

        object value = _waveManagerStageDurationField.GetValue(WaveManager.Instance);
        return value is float duration && duration > 0f ? duration : fallbackDuration;
    }

    private float GetWaterY(float stagePosition)
    {
        if (stagePosition >= targetStage)
        {
            return targetY;
        }

        float t = Mathf.InverseLerp(appearStage, targetStage, stagePosition);
        return Mathf.Lerp(startY, targetY, t);
    }

    private float GetWaveScale(float waterY)
    {
        if (waterY < scaleDownStartY)
        {
            return defaultWaveScale;
        }

        if (waterY < scaleRestoreStartY)
        {
            float t = Mathf.InverseLerp(scaleDownStartY, scaleRestoreStartY, waterY);
            return Mathf.Lerp(defaultWaveScale, reducedWaveScale, t);
        }

        if (waterY < scaleRestoreEndY)
        {
            float t = Mathf.InverseLerp(scaleRestoreStartY, scaleRestoreEndY, waterY);
            return Mathf.Lerp(reducedWaveScale, defaultWaveScale, t);
        }

        return defaultWaveScale;
    }

    private void HideWater()
    {
        water?.ApplyY(hiddenY, false);
        water?.ApplyWaveScale(defaultWaveScale);
    }
}
