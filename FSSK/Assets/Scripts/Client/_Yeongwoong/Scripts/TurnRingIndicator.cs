using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class TurnRingIndicator : MonoBehaviour
{
    private const string DiscName = "TurnRing_Disc";
    private const string RingName = "TurnRing_Rotating";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Turn Binding")]
    [SerializeField] private OmokStoneColor ownerColor = OmokStoneColor.Gold;
    [SerializeField] private OmokTurnSystem turnSystem;
    [SerializeField] private bool autoFindTurnSystem = true;
    [SerializeField] private OmokMatchManager matchManager;
    [SerializeField] private bool autoFindMatchManager = true;
    [SerializeField] private bool previewActiveWhenUnbound = true;

    [Header("Visuals")]
    [SerializeField] private Material indicatorMaterial;
    [SerializeField] private Color indicatorColor = new(1f, 0.74f, 0.22f, 1f);
    [SerializeField, Min(0.01f)] private float discRadius = 0.37f;
    [SerializeField, Min(0.01f)] private float ringInnerRadius = 0.4f;
    [SerializeField, Min(0.01f)] private float ringWidth = 0.03f;
    [SerializeField, Range(8, 128)] private int circleResolution = 80;
    [SerializeField, Range(3, 32)] private int ringSegmentCount = 10;
    [SerializeField, Range(0.2f, 0.95f)] private float ringSegmentFill = 0.9f;
    [SerializeField] private float yOffset = 0.025f;

    [Header("State")]
    [SerializeField] private bool alwaysShowDisc = true;
    [SerializeField, Range(0f, 1f)] private float discAlpha = 0.28f;
    [SerializeField, Range(0f, 1f)] private float inactiveRingAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float activeRingAlpha = 0.92f;
    [SerializeField, Min(0f), InspectorName("Active Turn Emission")] private float activeEmission = 0.75f;
    [SerializeField, Min(0f), InspectorName("Inactive Turn Emission")] private float inactiveStateEmission = 0.35f;

    [Header("Countdown")]
    [SerializeField] private bool useTurnTimerCountdown = true;
    [SerializeField] private bool showFullRingWhenInactive = true;
    [SerializeField, Range(-360f, 360f)] private float countdownStartAngleDegrees = -90f;
    [SerializeField] private bool countdownClockwise = true;

    private Transform _discTransform;
    private Transform _ringTransform;
    private MeshFilter _discFilter;
    private MeshFilter _ringFilter;
    private MeshRenderer _discRenderer;
    private MeshRenderer _ringRenderer;
    private Mesh _discMesh;
    private Mesh _ringMesh;
    private MaterialPropertyBlock _propertyBlock;
    private OmokMatchManager _subscribedMatchManager;
    private int _visibleRingSegmentCount = -1;
    private bool _isTurnActive;

    public OmokStoneColor OwnerColor => ownerColor;
    public bool IsTurnActive => _isTurnActive;

    private void OnEnable()
    {
        EnsureVisuals();
        SubscribeToMatchManager();
        RefreshTurnState();
        RefreshCountdownMesh();
    }

    private void OnDisable()
    {
        UnsubscribeFromMatchManager();
        ReleaseGeneratedMeshes();
    }

    private void OnValidate()
    {
        ringInnerRadius = Mathf.Max(0.01f, ringInnerRadius);
        ringWidth = Mathf.Max(0.01f, ringWidth);
        circleResolution = Mathf.Clamp(circleResolution, 8, 128);
        ringSegmentCount = Mathf.Clamp(ringSegmentCount, 3, 32);

        EnsureVisuals();
        RebuildMeshes();
        RefreshTurnState();
    }

    private void Update()
    {
        if (_ringTransform == null)
        {
            EnsureVisuals();
        }

        if (_ringTransform == null)
        {
            return;
        }

        RefreshTurnState();
        RefreshCountdownMesh();
    }

    public void SetOwnerColor(OmokStoneColor nextOwnerColor)
    {
        ownerColor = nextOwnerColor;
        RefreshTurnState();
    }

    public void SetTurnActive(bool isActive)
    {
        if (_isTurnActive == isActive)
        {
            RefreshCountdownMesh();
            return;
        }

        _isTurnActive = isActive;
        ApplyVisualState();
        RefreshCountdownMesh();
    }

    private void HandleTurnChanged(OmokStoneColor nextTurn)
    {
        SetTurnActive(nextTurn == ownerColor);
    }

    private void RefreshTurnState()
    {
        ResolveBindings();

        OmokStoneColor currentTurn = OmokStoneColor.None;
        if (turnSystem != null && turnSystem.CurrentTurn != OmokStoneColor.None)
        {
            currentTurn = turnSystem.CurrentTurn;
        }
        else if (matchManager != null)
        {
            currentTurn = matchManager.CurrentTurn;
        }

        if (currentTurn != OmokStoneColor.None)
        {
            SetTurnActive(currentTurn == ownerColor);
        }
        else
        {
            SetTurnActive(previewActiveWhenUnbound);
        }
    }

    private void ResolveBindings()
    {
        if (turnSystem == null && autoFindTurnSystem)
        {
            turnSystem = FindFirstObjectByType<OmokTurnSystem>();
        }

        if (matchManager == null && autoFindMatchManager)
        {
            matchManager = FindFirstObjectByType<OmokMatchManager>();
        }

        if (matchManager != null)
        {
            SubscribeToMatchManager();
            return;
        }

        UnsubscribeFromMatchManager();
    }

    private void SubscribeToMatchManager()
    {
        if (matchManager == null)
        {
            return;
        }

        if (_subscribedMatchManager == matchManager)
        {
            return;
        }

        UnsubscribeFromMatchManager();
        _subscribedMatchManager = matchManager;
        _subscribedMatchManager.OnTurnChanged += HandleTurnChanged;
    }

    private void UnsubscribeFromMatchManager()
    {
        if (_subscribedMatchManager == null)
        {
            return;
        }

        _subscribedMatchManager.OnTurnChanged -= HandleTurnChanged;
        _subscribedMatchManager = null;
    }

    private void EnsureVisuals()
    {
        _propertyBlock ??= new MaterialPropertyBlock();

        _discTransform = GetOrCreateChild(DiscName);
        _ringTransform = GetOrCreateChild(RingName);

        _discFilter = EnsureComponent<MeshFilter>(_discTransform.gameObject);
        _ringFilter = EnsureComponent<MeshFilter>(_ringTransform.gameObject);
        _discRenderer = EnsureComponent<MeshRenderer>(_discTransform.gameObject);
        _ringRenderer = EnsureComponent<MeshRenderer>(_ringTransform.gameObject);

        _discTransform.localPosition = new Vector3(0f, yOffset, 0f);
        _ringTransform.localPosition = new Vector3(0f, yOffset + 0.002f, 0f);
        _discTransform.localRotation = Quaternion.identity;
        _ringTransform.localRotation = Quaternion.identity;
        _discTransform.localScale = Vector3.one;
        _ringTransform.localScale = Vector3.one;

        if (_discMesh == null || _ringMesh == null)
        {
            RebuildMeshes();
        }

        AssignMaterials();
        ApplyVisualState();
    }

    private Transform GetOrCreateChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new(childName);
        childObject.hideFlags = HideFlags.DontSave;
        childObject.transform.SetParent(transform, false);
        return childObject.transform;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private void AssignMaterials()
    {
        if (indicatorMaterial == null)
        {
            return;
        }

        if (_discRenderer != null)
        {
            _discRenderer.sharedMaterial = indicatorMaterial;
        }

        if (_ringRenderer != null)
        {
            _ringRenderer.sharedMaterial = indicatorMaterial;
        }
    }

    private void RebuildMeshes()
    {
        ReleaseGeneratedMeshes();
        RebuildDiscMesh();
        RebuildRingMesh(GetCurrentVisibleRingSegmentCount());
    }

    private void RebuildDiscMesh()
    {
        DestroyGeneratedMesh(_discMesh);
        _discMesh = BuildDiscMesh(Mathf.Max(0.01f, discRadius), circleResolution);
        _discMesh.name = $"{name}_DiscMesh";
        _discMesh.hideFlags = HideFlags.DontSave;

        if (_discFilter != null)
        {
            _discFilter.sharedMesh = _discMesh;
        }
    }

    private void RebuildRingMesh(int visibleSegmentCount)
    {
        DestroyGeneratedMesh(_ringMesh);
        _visibleRingSegmentCount = Mathf.Clamp(visibleSegmentCount, 0, Mathf.Max(3, ringSegmentCount));

        _ringMesh = BuildSegmentedRingMesh(
            Mathf.Max(0.01f, ringInnerRadius),
            Mathf.Max(0.01f, ringInnerRadius + ringWidth),
            ringSegmentCount,
            _visibleRingSegmentCount,
            ringSegmentFill,
            circleResolution,
            countdownStartAngleDegrees,
            countdownClockwise);
        _ringMesh.name = $"{name}_RingMesh";
        _ringMesh.hideFlags = HideFlags.DontSave;

        if (_ringFilter != null)
        {
            _ringFilter.sharedMesh = _ringMesh;
        }
    }

    private void RefreshCountdownMesh()
    {
        if (_ringFilter == null)
        {
            return;
        }

        int visibleSegmentCount = GetCurrentVisibleRingSegmentCount();
        if (_ringMesh != null && _visibleRingSegmentCount == visibleSegmentCount)
        {
            return;
        }

        RebuildRingMesh(visibleSegmentCount);
        ApplyVisualState();
    }

    private int GetCurrentVisibleRingSegmentCount()
    {
        int safeSegmentCount = Mathf.Clamp(ringSegmentCount, 3, 32);
        if (!_isTurnActive)
        {
            return showFullRingWhenInactive ? safeSegmentCount : 0;
        }

        if (!useTurnTimerCountdown ||
            turnSystem == null ||
            !turnSystem.UseTurnTimer)
        {
            return safeSegmentCount;
        }

        float duration = Mathf.Max(0.1f, turnSystem.TurnDurationSeconds);
        float remaining = Mathf.Clamp(turnSystem.TurnRemainingSeconds, 0f, duration);
        if (remaining <= 0f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.CeilToInt((remaining / duration) * safeSegmentCount), 0, safeSegmentCount);
    }

    private void ReleaseGeneratedMeshes()
    {
        DestroyGeneratedMesh(_discMesh);
        DestroyGeneratedMesh(_ringMesh);
        _discMesh = null;
        _ringMesh = null;
        _visibleRingSegmentCount = -1;
    }

    private static void DestroyGeneratedMesh(Mesh mesh)
    {
        if (mesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(mesh);
        }
    }

    private void ApplyVisualState()
    {
        if (_discRenderer == null || _ringRenderer == null)
        {
            return;
        }

        _discRenderer.enabled = alwaysShowDisc;
        _ringRenderer.enabled = _isTurnActive || inactiveRingAlpha > 0f;

        float stateEmission = _isTurnActive ? activeEmission : inactiveStateEmission;
        ApplyRendererColor(_discRenderer, indicatorColor, discAlpha, stateEmission);
        ApplyRendererColor(
            _ringRenderer,
            indicatorColor,
            _isTurnActive ? activeRingAlpha : inactiveRingAlpha,
            stateEmission);
    }

    private void ApplyRendererColor(Renderer targetRenderer, Color color, float alpha, float emissionMultiplier)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Color visibleColor = color;
        visibleColor.a = alpha;

        _propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(BaseColorId, visibleColor);
        _propertyBlock.SetColor(ColorId, visibleColor);
        _propertyBlock.SetColor(EmissionColorId, color * emissionMultiplier);
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    private static Mesh BuildDiscMesh(float radius, int resolution)
    {
        int safeResolution = Mathf.Max(8, resolution);
        Vector3[] vertices = new Vector3[safeResolution + 1];
        int[] triangles = new int[safeResolution * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < safeResolution; i++)
        {
            float angle = Mathf.PI * 2f * i / safeResolution;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < safeResolution; i++)
        {
            int next = i == safeResolution - 1 ? 1 : i + 2;
            int baseIndex = i * 3;
            triangles[baseIndex] = 0;
            triangles[baseIndex + 1] = next;
            triangles[baseIndex + 2] = i + 1;
        }

        Mesh mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildSegmentedRingMesh(
        float innerRadius,
        float outerRadius,
        int segmentCount,
        int visibleSegmentCount,
        float segmentFill,
        int circleResolution,
        float startAngleDegrees,
        bool clockwise)
    {
        int safeSegmentCount = Mathf.Max(3, segmentCount);
        int safeVisibleSegmentCount = Mathf.Clamp(visibleSegmentCount, 0, safeSegmentCount);
        int stepsPerSegment = Mathf.Max(2, circleResolution / safeSegmentCount);
        float fullSegmentAngle = Mathf.PI * 2f / safeSegmentCount;
        float visibleAngle = fullSegmentAngle * Mathf.Clamp(segmentFill, 0.2f, 0.95f);
        float startAngle = startAngleDegrees * Mathf.Deg2Rad;
        float direction = clockwise ? -1f : 1f;

        int verticesPerSegment = (stepsPerSegment + 1) * 2;
        int trianglesPerSegment = stepsPerSegment * 6;
        Vector3[] vertices = new Vector3[verticesPerSegment * safeVisibleSegmentCount];
        int[] triangles = new int[trianglesPerSegment * safeVisibleSegmentCount];

        int vertexIndex = 0;
        int triangleIndex = 0;
        int depletedSegmentCount = safeSegmentCount - safeVisibleSegmentCount;

        for (int visibleSegment = 0; visibleSegment < safeVisibleSegmentCount; visibleSegment++)
        {
            int segment = depletedSegmentCount + visibleSegment;
            float segmentCenter = startAngle + direction * fullSegmentAngle * segment;
            float arcStart = segmentCenter - visibleAngle * 0.5f;

            for (int step = 0; step <= stepsPerSegment; step++)
            {
                float t = step / (float)stepsPerSegment;
                float angle = arcStart + visibleAngle * t;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[vertexIndex] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[vertexIndex + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                if (step < stepsPerSegment)
                {
                    int a = vertexIndex;
                    int b = vertexIndex + 1;
                    int c = vertexIndex + 2;
                    int d = vertexIndex + 3;

                    triangles[triangleIndex] = a;
                    triangles[triangleIndex + 1] = d;
                    triangles[triangleIndex + 2] = b;
                    triangles[triangleIndex + 3] = a;
                    triangles[triangleIndex + 4] = c;
                    triangles[triangleIndex + 5] = d;
                    triangleIndex += 6;
                }

                vertexIndex += 2;
            }
        }

        Mesh mesh = new();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
