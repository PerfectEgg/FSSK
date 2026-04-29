using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public enum OmokStoneSnapTiming
{
    OnPlacement,
    OnLanding
}

public enum OmokBlockerAttachmentMode
{
    SurfaceContact,
    ObjectCenterXZ
}

public enum OmokStoneColor
{
    None,
    Gold,
    Silver
}

[Serializable]
public class OmokStoneLauncher
{
    [SerializeField] private Transform source;
    [SerializeField] private GameObject stonePrefab;

    public Transform Source => source;
    public GameObject StonePrefab => stonePrefab;
}

public readonly struct OmokStonePlacementRequest
{
    public OmokStonePlacementRequest(
        OmokStoneColor stoneColor,
        Vector2Int targetCoordinate,
        Vector3 releasePosition,
        bool lockToTargetCoordinate = false)
    {
        StoneColor = stoneColor;
        TargetCoordinate = targetCoordinate;
        ReleasePosition = releasePosition;
        LockToTargetCoordinate = lockToTargetCoordinate;
    }

    public OmokStoneColor StoneColor { get; }
    public Vector2Int TargetCoordinate { get; }
    public Vector3 ReleasePosition { get; }
    public bool LockToTargetCoordinate { get; }
}

public readonly struct OmokBlockedStoneResult
{
    public OmokBlockedStoneResult(OmokStoneColor stoneColor, Transform blockerTarget, int consecutiveSameColorStackCount)
    {
        StoneColor = stoneColor;
        BlockerTarget = blockerTarget;
        ConsecutiveSameColorStackCount = consecutiveSameColorStackCount;
    }

    public OmokStoneColor StoneColor { get; }
    public Transform BlockerTarget { get; }
    public int ConsecutiveSameColorStackCount { get; }
}

[RequireComponent(typeof(OmokAimController))]
public class OmokStoneDropper : MonoBehaviour
{
    private const string BOARD_LAYER_NAME = "Board";
    private const string BLOCKER_LAYER_NAME = "Blocker";
    private const float DRAG_HOVER_HEIGHT_MAX_CELL_MULTIPLIER = 6f;
    private const float PREVIEW_LINE_WIDTH_CELL_MULTIPLIER = 0.06f;
    private const float PREVIEW_RAY_LINE_WIDTH_MULTIPLIER = 0.22f;
    private const float PREVIEW_RAY_TARGET_GAP_CELL_MULTIPLIER = 0.08f;
    private const float PREVIEW_LINE_INSET_CELL_MULTIPLIER = 0.06f;
    private const float PREVIEW_LINE_HEIGHT_MIN_CELL_MULTIPLIER = 0.03f;
    private const float PREVIEW_TARGET_HEIGHT_CELL_MULTIPLIER = 0.06f;
    private const float SETTLE_OFFSET_CELL_MULTIPLIER = 0.002f;
    private const float BLOCKER_STACK_GAP_CELL_MULTIPLIER = 0.002f;
    private const float INITIAL_FALL_SPEED_CELL_MULTIPLIER = 0.08f;
    private const float FALL_GRAVITY_SCALE_MAX = 0.35f;
    private const int CURSOR_WARP_MAX_ATTEMPTS = 4;
    private static readonly int _baseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int _colorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int _surfacePropertyId = Shader.PropertyToID("_Surface");
    private static readonly int _blendPropertyId = Shader.PropertyToID("_Blend");
    private static readonly int _srcBlendPropertyId = Shader.PropertyToID("_SrcBlend");
    private static readonly int _dstBlendPropertyId = Shader.PropertyToID("_DstBlend");
    private static readonly int _zWritePropertyId = Shader.PropertyToID("_ZWrite");

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativeCursorPoint point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
#endif

    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private OmokAimController aimController;
    [SerializeField] private OmokMatchManager matchManager;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform stoneRoot;

    [Header("Launchers")]
    [FormerlySerializedAs("blackLauncher")]
    [SerializeField] private OmokStoneLauncher goldLauncher = new();
    [FormerlySerializedAs("whiteLauncher")]
    [SerializeField] private OmokStoneLauncher silverLauncher = new();

    [Header("Manual Input")]
    [SerializeField] private bool useBuiltInMouseInput = true;
    [FormerlySerializedAs("allowBlackManualPlacement")]
    [SerializeField] private bool allowGoldManualPlacement = true;
    [FormerlySerializedAs("allowWhiteManualPlacement")]
    [SerializeField] private bool allowSilverManualPlacement = true;
    [SerializeField] private bool restrictManualDragToCurrentTurn = true;
    [SerializeField] private OmokStoneColor localManualStoneColor = OmokStoneColor.None;

    [Header("Drag")]
    [SerializeField, Min(0f)] private float dragHoverHeight = 3f;
    [SerializeField] private bool keepDraggedStoneOnScreen = true;
    [SerializeField, Range(0f, 0.2f)] private float draggedStoneScreenPadding = 0.015f;

    [Header("Cursor")]
    [SerializeField] private bool warpCursorToDropPositionOnRelease = true;

    [Header("Placement Offset")]
    [SerializeField] private bool usePlacementTargetOffset;
    [SerializeField] private Vector2Int placementTargetOffset;

    [Header("Spawn")]
    [SerializeField] private OmokStoneSnapTiming snapTiming = OmokStoneSnapTiming.OnLanding;
    [SerializeField, Min(1f)] private float raycastDistance = 500f;

    [Header("Landing")]
    [SerializeField, Min(0f)] private float settleOffset = 0.01f;
    [SerializeField] private bool alignPlacedStoneToBoard = true;

    [Header("Blocker")]
    [SerializeField] private OmokBlockerAttachmentMode blockerAttachmentMode = OmokBlockerAttachmentMode.SurfaceContact;
    [SerializeField, Min(0f)] private float blockerCenterStackGap = 0.01f;
    [SerializeField, Range(0.1f, 1.5f)] private float blockerProbeRadiusMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float blockerProbeExtraRadius;

    [Header("Drop Feel")]
    [SerializeField, Min(0f)] private float initialFallSpeed = 5f;
    [SerializeField, Min(0f)] private float fallGravityScale = 15f;

    [Header("Preview")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Color validPreviewColor = new(0.25f, 1f, 0.45f, 0.95f);
    [SerializeField] private Color blockedPreviewColor = new(1f, 0.3f, 0.3f, 0.95f);
    [SerializeField, Min(0.005f)] private float previewLineWidth = 1f;
    [SerializeField, Min(0f)] private float previewTargetHeightOffset = 0.1f;
    [SerializeField, Range(0.15f, 0.85f)] private float previewStoneAlpha = 0.45f;
    [SerializeField, Range(0.85f, 1f)] private float previewStoneScaleMultiplier = 0.97f;

    private readonly HashSet<Vector2Int> _occupiedCoordinates = new();
    private readonly HashSet<Vector2Int> _reservedCoordinates = new();
    private readonly Dictionary<Vector2Int, OmokFallingStone> _stonesByCoordinate = new();
    private readonly Dictionary<Transform, float> _blockerCenterStackTopY = new();
    private readonly Dictionary<Transform, List<OmokStoneColor>> _blockerStoneStacks = new();
    private readonly List<Material> _draggedStonePreviewMaterials = new();
    private LineRenderer _previewRenderer;
    private LineRenderer _previewRayRenderer;
    private Material _previewMaterial;

    private OmokStoneLauncher _activeLauncher;
    private bool _isDraggingLauncher;
    private bool _hasDragBoardTarget;
    private Vector2Int _currentDragCoordinate;
    private Vector3 _currentDragReleasePosition;
    private bool _hasCurrentDragDropSpawnPosition;
    private Vector3 _currentDragDropSpawnPosition;
    private GameObject _draggedStoneObject;
    private int _boardLayer = -1;
    private int _blockerLayer = -1;
    private int _launcherLayerMask;
    private OmokMatchManager _subscribedMatchManager;
    private bool _hasPendingCursorWarp;
    private Vector3 _pendingCursorWarpWorldPosition;
    private int _pendingCursorWarpFrame;
    private int _pendingCursorWarpAttempts;

    public OmokStoneSnapTiming SnapTiming => snapTiming;
    public bool UsePlacementTargetOffset => usePlacementTargetOffset;
    public Vector2Int PlacementTargetOffset => placementTargetOffset;
    public bool UseWindAim => aimController != null && aimController.UseWindAim;
    public Vector2 WindAimDirection => aimController != null ? aimController.WindAimDirection : Vector2.zero;
    public bool HideSystemCursorWhileAiming => aimController != null && aimController.HideSystemCursorWhileAiming;
    public bool UnlockSystemCursorWhileAiming => aimController != null && aimController.UnlockSystemCursorWhileAiming;
    public bool KeepAimPositionAfterDrop => aimController != null && aimController.KeepAimPositionAfterDrop;
    public bool UseBuiltInMouseInput => useBuiltInMouseInput;
    public bool RestrictManualDragToCurrentTurn => restrictManualDragToCurrentTurn;
    public OmokStoneColor LocalManualStoneColor => localManualStoneColor;
    public bool IsDragging => _isDraggingLauncher;
    public event Action<OmokStonePlacementRequest> OnPlacementRequested;
    public event Action<Vector2Int, OmokStoneColor> OnStonePlaced;
    public event Action<OmokBlockedStoneResult> OnStoneBlocked;

    private void Reset()
    {
        grid = GetComponent<OmokGrid>();
        aimController = GetComponent<OmokAimController>();
        matchManager = GetComponent<OmokMatchManager>();
    }

    private void Awake()
    {
        ResolveNamedLayers();

        if (grid == null)
        {
            grid = GetComponent<OmokGrid>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        EnsureMatchManager();
        EnsureAimController(true);
        CacheExistingStoneCoordinates();
    }

    private void OnEnable()
    {
        EnsureMatchManager();
        SubscribeToMatchManager();
        CancelDragIfActiveLauncherNoLongerAllowed();
    }

    private void OnValidate()
    {
        ResolveNamedLayers();
        EnsureAimController(false);
        blockerCenterStackGap = Mathf.Max(0f, blockerCenterStackGap);
        blockerProbeRadiusMultiplier = Mathf.Clamp(blockerProbeRadiusMultiplier, 0.1f, 1.5f);
        blockerProbeExtraRadius = Mathf.Max(0f, blockerProbeExtraRadius);
    }

    private bool EnsureAimController(bool createIfMissing)
    {
        if (aimController == null)
        {
            aimController = GetComponent<OmokAimController>();
        }

        if (aimController == null && createIfMissing)
        {
            aimController = gameObject.AddComponent<OmokAimController>();
        }

        if (aimController == null)
        {
            return false;
        }

        aimController.SetReferences(grid, targetCamera);
        return true;
    }

    private bool EnsureMatchManager()
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

        if (isActiveAndEnabled)
        {
            SubscribeToMatchManager();
        }

        return matchManager != null;
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
        CancelDragIfActiveLauncherNoLongerAllowed();
    }

    private void HandleMatchEnded(OmokStoneColor resultWinner)
    {
        CancelDrag();
    }

    private void Update()
    {
        ApplyPendingCursorWarp();

        if (!useBuiltInMouseInput)
        {
            return;
        }

        if (_isDraggingLauncher)
        {
            UpdateDrag();

            if (Input.GetMouseButtonUp(0))
            {
                TryReleaseDrag();
            }

            return;
        }

        HidePreview();

        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDrag();
        }
    }

    internal bool TryFinalizeStone(OmokFallingStone stone)
    {
        if (stone == null || grid == null || !grid.IsReady)
        {
            return false;
        }

        if (stone.SnapTiming == OmokStoneSnapTiming.OnPlacement || stone.HasReservedTarget)
        {
            Vector2Int reservedCoordinate = stone.TargetCoordinate;

            if (!_reservedCoordinates.Remove(reservedCoordinate))
            {
                if (_occupiedCoordinates.Contains(reservedCoordinate))
                {
                    Destroy(stone.gameObject);
                }

                return false;
            }

            if (TryGetBlockerAtCoordinate(reservedCoordinate, GetFallingStoneBlockerProbeRadius(stone), out Collider reservedBlocker))
            {
                return TryStickStoneToBlocker(stone, reservedBlocker);
            }

            if (_occupiedCoordinates.Contains(reservedCoordinate))
            {
                Destroy(stone.gameObject);
                return false;
            }

            _occupiedCoordinates.Add(reservedCoordinate);
            stone.SnapTo(reservedCoordinate, stone.TargetWorldPosition);
            _stonesByCoordinate[reservedCoordinate] = stone;
            OnStonePlaced?.Invoke(reservedCoordinate, stone.StoneColor);
            return true;
        }

        if (!grid.TryGetCoordinate(stone.transform.position, out Vector2Int landedCoordinate))
        {
            Destroy(stone.gameObject);
            return false;
        }

        if (TryGetBlockerAtCoordinate(landedCoordinate, GetFallingStoneBlockerProbeRadius(stone), out Collider landedBlocker))
        {
            return TryStickStoneToBlocker(stone, landedBlocker);
        }

        if (IsCoordinateBlocked(landedCoordinate))
        {
            Destroy(stone.gameObject);
            return false;
        }

        _occupiedCoordinates.Add(landedCoordinate);

        Vector3 snappedPosition = grid.GetWorldPosition(landedCoordinate) +
                                  (grid.transform.up * (stone.GetSnapOffsetAlongNormal(grid.transform.up) + GetEffectiveSettleOffset()));

        stone.SnapTo(landedCoordinate, snappedPosition);
        _stonesByCoordinate[landedCoordinate] = stone;
        OnStonePlaced?.Invoke(landedCoordinate, stone.StoneColor);
        return true;
    }

    internal void ReleaseReservation(Vector2Int coordinate)
    {
        _reservedCoordinates.Remove(coordinate);
    }

    public void SetManualPlacementState(bool allowGold, bool allowSilver)
    {
        allowGoldManualPlacement = allowGold;
        allowSilverManualPlacement = allowSilver;
        CancelDragIfActiveLauncherNoLongerAllowed();
    }

    public void SetLocalManualStoneColor(OmokStoneColor stoneColor)
    {
        localManualStoneColor = stoneColor;
        CancelDragIfActiveLauncherNoLongerAllowed();
    }

    public void ClearLocalManualStoneColor()
    {
        SetLocalManualStoneColor(OmokStoneColor.None);
    }

    public void SetRestrictManualDragToCurrentTurn(bool shouldRestrict)
    {
        restrictManualDragToCurrentTurn = shouldRestrict;
        CancelDragIfActiveLauncherNoLongerAllowed();
    }

    public bool CanBeginManualDrag(OmokStoneColor stoneColor)
    {
        if (!useBuiltInMouseInput ||
            stoneColor == OmokStoneColor.None ||
            !IsManualStoneColorEnabled(stoneColor))
        {
            return false;
        }

        if (localManualStoneColor != OmokStoneColor.None && localManualStoneColor != stoneColor)
        {
            return false;
        }

        return !restrictManualDragToCurrentTurn ||
               !EnsureMatchManager() ||
               matchManager.CanTakeTurn(stoneColor);
    }

    public void SetBuiltInMouseInputEnabled(bool isEnabled)
    {
        useBuiltInMouseInput = isEnabled;

        if (!useBuiltInMouseInput)
        {
            CancelDrag();
        }
    }

    public bool TryRequestPlacement(OmokStoneColor stoneColor, Vector2Int targetCoordinate)
    {
        if (!TryBuildPlacementRequest(stoneColor, targetCoordinate, out OmokStonePlacementRequest request))
        {
            return false;
        }

        if (OnPlacementRequested == null)
        {
            return false;
        }

        OnPlacementRequested.Invoke(request);
        return true;
    }

    public bool TryRemoveStoneAt(Vector2Int coordinate, OmokStoneColor expectedColor = OmokStoneColor.None)
    {
        if (!IsInsideBoard(coordinate))
        {
            return false;
        }

        OmokFallingStone stone = GetStoneAtCoordinate(coordinate);
        if (stone != null && !MatchesExpectedColor(stone, expectedColor))
        {
            return false;
        }

        bool hadOccupiedCoordinate = _occupiedCoordinates.Remove(coordinate);
        _reservedCoordinates.Remove(coordinate);
        _stonesByCoordinate.Remove(coordinate);

        if (stone == null)
        {
            return hadOccupiedCoordinate;
        }

        Destroy(stone.gameObject);
        return true;
    }

    public bool TryGetStoneTransformAt(Vector2Int coordinate, out Transform stoneTransform)
    {
        return TryGetStoneTransformAt(coordinate, OmokStoneColor.None, out stoneTransform);
    }

    public bool TryGetStoneTransformAt(Vector2Int coordinate, OmokStoneColor expectedColor, out Transform stoneTransform)
    {
        stoneTransform = null;

        if (!IsInsideBoard(coordinate))
        {
            return false;
        }

        OmokFallingStone stone = GetStoneAtCoordinate(coordinate);
        if (stone == null || !MatchesExpectedColor(stone, expectedColor))
        {
            return false;
        }

        stoneTransform = stone.transform;
        return true;
    }

    public void SetPlacementTargetOffsetEnabled(bool isEnabled)
    {
        usePlacementTargetOffset = isEnabled;
    }

    public void SetPlacementTargetOffset(Vector2Int offset)
    {
        placementTargetOffset = offset;
        usePlacementTargetOffset = offset != Vector2Int.zero;
    }

    public void SetPlacementTargetOffset(int offsetX, int offsetY)
    {
        SetPlacementTargetOffset(new Vector2Int(offsetX, offsetY));
    }

    public void ClearPlacementTargetOffset()
    {
        placementTargetOffset = Vector2Int.zero;
        usePlacementTargetOffset = false;
    }

    public void TogglePlacementTargetOffset()
    {
        usePlacementTargetOffset = !usePlacementTargetOffset;
    }

    public void EnablePlacementOffsetUpOne()
    {
        SetPlacementTargetOffset(0, 1);
    }

    public void EnablePlacementOffsetUpTwo()
    {
        SetPlacementTargetOffset(0, 2);
    }

    public void EnablePlacementOffsetDownOne()
    {
        SetPlacementTargetOffset(0, -1);
    }

    public void EnablePlacementOffsetDownTwo()
    {
        SetPlacementTargetOffset(0, -2);
    }

    public void EnablePlacementOffsetLeftOne()
    {
        SetPlacementTargetOffset(-1, 0);
    }

    public void EnablePlacementOffsetLeftTwo()
    {
        SetPlacementTargetOffset(-2, 0);
    }

    public void EnablePlacementOffsetRightOne()
    {
        SetPlacementTargetOffset(1, 0);
    }

    public void EnablePlacementOffsetRightTwo()
    {
        SetPlacementTargetOffset(2, 0);
    }

    public void SetWindAimEnabled(bool isEnabled)
    {
        if (EnsureAimController(true))
        {
            aimController.SetWindAimEnabled(isEnabled);
        }
    }

    public void SetWindAimDirection(Vector2 direction)
    {
        if (EnsureAimController(true))
        {
            aimController.SetWindAimDirection(direction);
        }
    }

    public void SetWindAimDirection(float directionX, float directionY)
    {
        SetWindAimDirection(new Vector2(directionX, directionY));
    }

    public void ConfigureWindAim(Vector2 direction, float driftCellsPerSecond)
    {
        if (EnsureAimController(true))
        {
            aimController.ConfigureWindAim(direction, driftCellsPerSecond);
        }
    }

    public void ConfigureWindAim(float directionX, float directionY, float driftCellsPerSecond)
    {
        ConfigureWindAim(new Vector2(directionX, directionY), driftCellsPerSecond);
    }

    public void ClearWindAim()
    {
        if (EnsureAimController(true))
        {
            aimController.ClearWindAim();
        }
    }

    public void SetHideSystemCursorWhileAiming(bool shouldHide)
    {
        if (EnsureAimController(true))
        {
            aimController.SetHideSystemCursorWhileAiming(shouldHide);
        }
    }

    public void SetUnlockSystemCursorWhileAiming(bool shouldUnlock)
    {
        if (EnsureAimController(true))
        {
            aimController.SetUnlockSystemCursorWhileAiming(shouldUnlock);
        }
    }

    public void EnableWindAimUp()
    {
        SetWindAimDirection(0f, 1f);
        SetWindAimEnabled(true);
    }

    public void EnableWindAimDown()
    {
        SetWindAimDirection(0f, -1f);
        SetWindAimEnabled(true);
    }

    public void EnableWindAimLeft()
    {
        SetWindAimDirection(-1f, 0f);
        SetWindAimEnabled(true);
    }

    public void EnableWindAimRight()
    {
        SetWindAimDirection(1f, 0f);
        SetWindAimEnabled(true);
    }

    public bool TryBeginDrag()
    {
        if (_isDraggingLauncher)
        {
            return false;
        }

        if (!TryGetLauncherAtMouse(out OmokStoneLauncher launcher))
        {
            return false;
        }

        if (!CanBeginManualDragFromLauncher(launcher))
        {
            return false;
        }

        if (!TryCreateDraggedStone(launcher))
        {
            return false;
        }

        _activeLauncher = launcher;
        _isDraggingLauncher = true;
        _hasDragBoardTarget = false;
        if (EnsureAimController(true))
        {
            aimController.BeginAimSession(GetInitialDragAimPosition(launcher));
        }

        UpdateDragState();
        return true;
    }

    private Vector3 GetInitialDragAimPosition(OmokStoneLauncher launcher)
    {
        Vector3 sourcePosition = transform.position;
        if (_draggedStoneObject != null)
        {
            sourcePosition = _draggedStoneObject.transform.position;
        }
        else if (launcher != null && launcher.Source != null)
        {
            sourcePosition = launcher.Source.position;
        }

        if (aimController == null || !aimController.UseWindAim || grid == null || !grid.IsReady)
        {
            return sourcePosition;
        }

        Vector3 up = GetGridUp();
        Vector3 hoverPlanePoint = GetGridPlaneBasePosition() + (up * GetEffectiveDragHoverHeight());
        return MovePointAlongAxis(sourcePosition, up, Vector3.Dot(hoverPlanePoint, up));
    }

    public void UpdateDrag()
    {
        if (!_isDraggingLauncher)
        {
            return;
        }

        UpdateDragState();
    }

    private void UpdateDragState()
    {
        _hasDragBoardTarget = false;
        ClearCurrentDragDropSpawnPosition();

        if (!EnsureAimController(true) ||
            !aimController.TryGetBoardAim(GetEffectiveDragHoverHeight(), raycastDistance, out OmokAimState aimState))
        {
            UpdateDraggedStoneOffBoard();
            HidePreview();
            return;
        }

        Vector3 dragAimPosition = ClampDraggedStoneToScreen(aimState.AimWorldPosition);
        UpdateDraggedStoneAtPosition(dragAimPosition);
        if (aimController != null)
        {
            aimController.SetCurrentAimPosition(dragAimPosition);
        }

        if (!aimState.HasCoordinate)
        {
            HidePreview();
            return;
        }

        Vector2Int rawCoordinate = TryGetCoordinate(dragAimPosition, out Vector2Int clampedCoordinate)
            ? clampedCoordinate
            : aimState.Coordinate;
        Vector2Int targetCoordinate = GetPlacementTargetCoordinate(rawCoordinate);
        _currentDragCoordinate = targetCoordinate;
        _currentDragReleasePosition = dragAimPosition;
        if (!IsInsideBoard(targetCoordinate))
        {
            UpdateDraggedStonePreviewVisual();
            HidePreview();
            return;
        }

        _hasDragBoardTarget = true;

        Vector3 draggedStoneCenter = GetDraggedStonePreviewProbeCenter(_currentDragReleasePosition);
        PlacementPreviewState previewState = BuildPlacementPreviewState(targetCoordinate, _currentDragReleasePosition, draggedStoneCenter);
        UpdateCurrentDragDropSpawnPosition(previewState, draggedStoneCenter);

        UpdateDraggedStonePreviewVisual();
        DrawPreview(targetCoordinate, previewState);
    }

    private void UpdateCurrentDragDropSpawnPosition(PlacementPreviewState previewState, Vector3 draggedStoneCenter)
    {
        if (!previewState.IsBlockerStackTarget || !previewState.HasStoneWorldPosition)
        {
            ClearCurrentDragDropSpawnPosition();
            return;
        }

        Vector3 centerToTransformOffset = _currentDragReleasePosition - draggedStoneCenter;
        Vector3 dropSpawnPosition = previewState.StoneWorldPosition + centerToTransformOffset;
        Vector3 up = GetGridUp();
        float spawnHeight = Vector3.Dot(dropSpawnPosition, up) + GetMinimumVisibleFallDistance();
        _currentDragDropSpawnPosition = MovePointAlongAxis(dropSpawnPosition, up, spawnHeight);
        _hasCurrentDragDropSpawnPosition = true;
    }

    private void ClearCurrentDragDropSpawnPosition()
    {
        _hasCurrentDragDropSpawnPosition = false;
        _currentDragDropSpawnPosition = default;
    }

    private Vector3 GetCurrentDragReleasePosition()
    {
        return _hasCurrentDragDropSpawnPosition ? _currentDragDropSpawnPosition : _currentDragReleasePosition;
    }

    public bool TryReleaseDrag()
    {
        if (!_isDraggingLauncher)
        {
            return false;
        }

        OmokStoneLauncher releasedLauncher = _activeLauncher;
        bool canPlace = _hasDragBoardTarget;
        Vector2Int targetCoordinate = _currentDragCoordinate;
        GameObject releasedPreviewObject = _draggedStoneObject;
        Vector3 releasePosition = GetCurrentDragReleasePosition();
        bool isCoordinateBlocked = canPlace &&
                                   releasedPreviewObject != null &&
                                   (!IsInsideBoard(targetCoordinate) || IsCoordinateBlocked(targetCoordinate));
        OmokStonePlacementRequest request = default;
        bool hasPlacementRequest = false;
        if (canPlace && releasedPreviewObject != null && !isCoordinateBlocked)
        {
            hasPlacementRequest = TryBuildPlacementRequest(releasedLauncher,
                                                           releasePosition,
                                                           targetCoordinate,
                                                           out request);
        }

        if (hasPlacementRequest && aimController != null)
        {
            aimController.StoreAimPosition(GetDropAimWorldPosition(request.TargetCoordinate));
        }

        Vector3 cursorWarpPosition = hasPlacementRequest ? request.ReleasePosition : default;
        ClearDragState();
        if (hasPlacementRequest)
        {
            QueueCursorWarpToDropPosition(cursorWarpPosition);
        }

        DestroyDraggedStonePreview(releasedPreviewObject);

        if (!hasPlacementRequest)
        {
            return false;
        }

        OnPlacementRequested?.Invoke(request);
        return true;
    }

    public void CancelDrag()
    {
        if (!_isDraggingLauncher && _draggedStoneObject == null)
        {
            return;
        }

        if (aimController != null)
        {
            aimController.ResetAimState();
        }

        GameObject draggedPreviewObject = _draggedStoneObject;
        ClearDragState();
        DestroyDraggedStonePreview(draggedPreviewObject);
    }

    private void ClearDragState()
    {
        _activeLauncher = null;
        _isDraggingLauncher = false;
        _hasDragBoardTarget = false;
        _currentDragReleasePosition = default;
        ClearCurrentDragDropSpawnPosition();
        if (aimController != null)
        {
            aimController.EndAimSession();
        }

        _draggedStoneObject = null;
        HidePreview();
    }

    public bool TryExecutePlacement(OmokStonePlacementRequest request)
    {
        if (!TryGetLauncherForColor(request.StoneColor, out OmokStoneLauncher launcher) ||
            !IsLauncherConfigured(launcher) ||
            grid == null ||
            !grid.IsReady ||
            !IsInsideBoard(request.TargetCoordinate))
        {
            return false;
        }

        if (IsCoordinateBlocked(request.TargetCoordinate))
        {
            return false;
        }

        bool guideStraightToTarget = request.LockToTargetCoordinate;
        bool spawnAtTarget = snapTiming == OmokStoneSnapTiming.OnPlacement && !guideStraightToTarget;
        Vector3 spawnPosition = spawnAtTarget
            ? grid.GetWorldPosition(request.TargetCoordinate) + (grid.transform.up * GetEffectiveDragHoverHeight())
            : request.ReleasePosition;
        GameObject stoneObject = Instantiate(launcher.StonePrefab, spawnPosition, launcher.StonePrefab.transform.rotation, stoneRoot);
        Quaternion snappedRotation = GetStableStoneRotation(stoneObject.transform);
        stoneObject.transform.rotation = snappedRotation;

        if (spawnAtTarget)
        {
            stoneObject.transform.position = spawnPosition;
        }

        ActivateStoneColliders(stoneObject);

        Rigidbody rigidbody = stoneObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = stoneObject.AddComponent<Rigidbody>();
        }

        OmokFallingStone fallingStone = stoneObject.GetComponent<OmokFallingStone>();
        if (fallingStone == null)
        {
            fallingStone = stoneObject.AddComponent<OmokFallingStone>();
        }

        bool reserveTarget = spawnAtTarget || guideStraightToTarget;
        if (reserveTarget)
        {
            _reservedCoordinates.Add(request.TargetCoordinate);
        }

        Vector3 targetWorldPosition = grid.GetWorldPosition(request.TargetCoordinate);
        Vector3 snappedPosition = targetWorldPosition +
                                  (grid.transform.up * (fallingStone.GetSnapOffsetAlongNormal(grid.transform.up) + GetEffectiveSettleOffset()));
        if (!spawnAtTarget)
        {
            spawnPosition = EnsureDropStartsAboveTarget(spawnPosition, snappedPosition);
            stoneObject.transform.position = spawnPosition;
            rigidbody.position = spawnPosition;
        }

        fallingStone.Initialize(this,
                                grid,
                                rigidbody,
                                request.StoneColor,
                                snapTiming,
                                reserveTarget,
                                request.TargetCoordinate,
                                snappedPosition,
                                snappedRotation,
                                GetEffectiveFallGravityScale(),
                                guideStraightToTarget);

        ConfigureRigidbody(rigidbody);
        ApplyReleaseMotion(stoneObject.transform, rigidbody);
        return true;
    }

    private void ConfigureRigidbody(Rigidbody rigidbody)
    {
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0.05f;
    }

    private void ApplyReleaseMotion(Transform stoneTransform, Rigidbody rigidbody)
    {
        if (stoneTransform == null || rigidbody == null)
        {
            return;
        }

        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        rigidbody.linearVelocity = -up * GetEffectiveInitialFallSpeed();
    }

    private Vector3 EnsureDropStartsAboveTarget(Vector3 spawnPosition, Vector3 targetPosition)
    {
        Vector3 up = GetGridUp();
        float currentHeight = Vector3.Dot(spawnPosition, up);
        float minimumHeight = Vector3.Dot(targetPosition, up) + GetMinimumVisibleFallDistance();
        return currentHeight >= minimumHeight
            ? spawnPosition
            : MovePointAlongAxis(spawnPosition, up, minimumHeight);
    }

    private Quaternion GetStableStoneRotation(Transform stoneTransform)
    {
        if (!alignPlacedStoneToBoard || stoneTransform == null || grid == null)
        {
            return stoneTransform != null ? stoneTransform.rotation : Quaternion.identity;
        }

        Vector3 stoneUp = stoneTransform.up;
        Vector3 boardUp = grid.transform.up;
        if (stoneUp.sqrMagnitude <= 0.0001f || boardUp.sqrMagnitude <= 0.0001f)
        {
            return stoneTransform.rotation;
        }

        return Quaternion.FromToRotation(stoneUp, boardUp) * stoneTransform.rotation;
    }

    private bool TryGetLauncherAtMouse(out OmokStoneLauncher launcher)
    {
        launcher = null;

        if (_launcherLayerMask == 0)
        {
            _launcherLayerMask = BuildLauncherLayerMask();
            if (_launcherLayerMask == 0)
            {
                return false;
            }
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, _launcherLayerMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (TryMatchLauncher(hit.collider, out launcher))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryMatchLauncher(Collider collider, out OmokStoneLauncher launcher)
    {
        launcher = null;

        if (allowGoldManualPlacement && IsLauncherHit(collider, goldLauncher))
        {
            launcher = goldLauncher;
            return true;
        }

        if (allowSilverManualPlacement && IsLauncherHit(collider, silverLauncher))
        {
            launcher = silverLauncher;
            return true;
        }

        return false;
    }

    private bool CanBeginManualDragFromLauncher(OmokStoneLauncher launcher)
    {
        return CanBeginManualDrag(GetLauncherStoneColor(launcher));
    }

    private bool IsManualStoneColorEnabled(OmokStoneColor stoneColor)
    {
        return stoneColor switch
        {
            OmokStoneColor.Gold => allowGoldManualPlacement,
            OmokStoneColor.Silver => allowSilverManualPlacement,
            _ => false
        };
    }

    private void CancelDragIfActiveLauncherNoLongerAllowed()
    {
        if (!_isDraggingLauncher)
        {
            return;
        }

        if (!CanBeginManualDragFromLauncher(_activeLauncher))
        {
            CancelDrag();
        }
    }

    private bool IsLauncherHit(Collider collider, OmokStoneLauncher launcher)
    {
        if (collider == null || !IsLauncherConfigured(launcher))
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == launcher.Source || hitTransform.IsChildOf(launcher.Source);
    }

    private bool IsLauncherConfigured(OmokStoneLauncher launcher)
    {
        return launcher != null && launcher.Source != null && launcher.StonePrefab != null;
    }

    private OmokStoneColor GetLauncherStoneColor(OmokStoneLauncher launcher)
    {
        if (launcher == goldLauncher)
        {
            return OmokStoneColor.Gold;
        }

        if (launcher == silverLauncher)
        {
            return OmokStoneColor.Silver;
        }

        return OmokStoneColor.None;
    }

    private bool TryGetLauncherForColor(OmokStoneColor stoneColor, out OmokStoneLauncher launcher)
    {
        launcher = stoneColor switch
        {
            OmokStoneColor.Gold => goldLauncher,
            OmokStoneColor.Silver => silverLauncher,
            _ => null
        };

        return IsLauncherConfigured(launcher);
    }

    internal bool IsBoardHit(Collider collider)
    {
        if (collider == null || grid == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return IsLayerInHierarchy(hitTransform, _boardLayer) ||
               hitTransform == grid.transform ||
               hitTransform.IsChildOf(grid.transform);
    }

    internal bool IsBlockerHit(Collider collider)
    {
        return TryResolveBlockerSettings(collider, out _);
    }

    internal bool TryStickStoneToBlocker(OmokFallingStone stone, Collider blockerCollider)
    {
        if (stone == null || !TryResolveBlockerSettings(blockerCollider, out ResolvedBlockerSettings blockerSettings))
        {
            return false;
        }

        Transform blockerTarget = GetBlockerAttachmentTarget(blockerCollider);
        Transform stackKey = GetBlockerStackKey(blockerTarget, blockerCollider);
        ReleaseReservation(stone.TargetCoordinate);

        if (!blockerSettings.KeepBlockedStone)
        {
            Destroy(stone.gameObject);
        }
        else if (blockerSettings.AttachmentMode == OmokBlockerAttachmentMode.ObjectCenterXZ)
        {
            Vector3 stickPosition = GetBlockerStackStonePosition(blockerCollider, stone, stackKey);

            if (stackKey != null)
            {
                Vector3 up = GetGridUp();
                float halfHeight = stone.GetSnapOffsetAlongNormal(up);
                _blockerCenterStackTopY[stackKey] = Vector3.Dot(stickPosition, up) + halfHeight;
            }

            stone.StickToBlocker(blockerTarget, _blockerLayer, stickPosition);
        }
        else
        {
            Vector3 stickPosition = GetBlockerSurfaceStonePosition(blockerCollider, stone, stone.transform.position);
            stone.StickToBlocker(blockerTarget, _blockerLayer, stickPosition);
        }

        int consecutiveSameColorStackCount = blockerSettings.CountForBlockerStackWin
            ? RegisterBlockedStone(stackKey, stone.StoneColor)
            : 0;

        if (blockerSettings.ConsumeTurnWhenBlocked)
        {
            OnStoneBlocked?.Invoke(new OmokBlockedStoneResult(stone.StoneColor, stackKey, consecutiveSameColorStackCount));
        }

        return true;
    }

    internal bool TryGetBlockerAlongStonePath(
        OmokFallingStone stone,
        Vector3 startPosition,
        Vector3 endPosition,
        out Collider blockerCollider)
    {
        blockerCollider = null;

        if (stone == null)
        {
            return false;
        }

        float probeRadius = GetFallingStoneBlockerProbeRadius(stone);
        if (probeRadius <= 0f)
        {
            return false;
        }

        Physics.SyncTransforms();
        Collider[] startOverlaps = Physics.OverlapSphere(startPosition, probeRadius, ~0, QueryTriggerInteraction.Collide);
        if (TryGetNearestBlockerCollider(startOverlaps, startPosition, out blockerCollider))
        {
            return true;
        }

        Collider[] endOverlaps = Physics.OverlapSphere(endPosition, probeRadius, ~0, QueryTriggerInteraction.Collide);
        if (TryGetNearestBlockerCollider(endOverlaps, endPosition, out blockerCollider))
        {
            return true;
        }

        Vector3 movement = endPosition - startPosition;
        float distance = movement.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        Collider[] capsuleOverlaps = Physics.OverlapCapsule(startPosition, endPosition, probeRadius, ~0, QueryTriggerInteraction.Collide);
        if (TryGetNearestBlockerCollider(capsuleOverlaps, startPosition, out blockerCollider))
        {
            return true;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            startPosition,
            probeRadius,
            movement / distance,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        if (!TryGetNearestBlockerHit(hits, out RaycastHit hit))
        {
            return false;
        }

        blockerCollider = hit.collider;
        return blockerCollider != null;
    }

    private bool TryGetCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
    {
        coordinate = default;

        if (grid == null || !grid.IsReady)
        {
            return false;
        }

        return grid.TryGetCoordinate(worldPosition, out coordinate);
    }

    private Vector2Int GetPlacementTargetCoordinate(Vector2Int aimCoordinate)
    {
        return usePlacementTargetOffset ? aimCoordinate + placementTargetOffset : aimCoordinate;
    }

    private Vector3 GetDropAimWorldPosition(Vector2Int coordinate)
    {
        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        return GetGridWorldPositionUnclamped(coordinate) + (up * GetEffectiveDragHoverHeight());
    }

    private bool IsCoordinateBlocked(Vector2Int coordinate)
    {
        return _occupiedCoordinates.Contains(coordinate) || _reservedCoordinates.Contains(coordinate);
    }

    private OmokFallingStone GetStoneAtCoordinate(Vector2Int coordinate)
    {
        if (_stonesByCoordinate.TryGetValue(coordinate, out OmokFallingStone trackedStone))
        {
            if (trackedStone != null)
            {
                return trackedStone;
            }

            _stonesByCoordinate.Remove(coordinate);
        }

        OmokFallingStone foundStone = FindPlacedStoneAtCoordinate(coordinate);
        if (foundStone != null)
        {
            _stonesByCoordinate[coordinate] = foundStone;
        }

        return foundStone;
    }

    private OmokFallingStone FindPlacedStoneAtCoordinate(Vector2Int coordinate)
    {
        OmokFallingStone[] stones = FindObjectsByType<OmokFallingStone>(FindObjectsSortMode.None);
        foreach (OmokFallingStone stone in stones)
        {
            if (stone == null || stone.IsBlockedByBlocker)
            {
                continue;
            }

            if (stone.IsSnapped && stone.Coordinate == coordinate)
            {
                return stone;
            }
        }

        return null;
    }

    private static bool MatchesExpectedColor(OmokFallingStone stone, OmokStoneColor expectedColor)
    {
        return expectedColor == OmokStoneColor.None ||
               stone == null ||
               stone.StoneColor == OmokStoneColor.None ||
               stone.StoneColor == expectedColor;
    }

    private PlacementPreviewState BuildPlacementPreviewState(Vector2Int coordinate, Vector3 releasePosition, Vector3 stoneProbeCenter)
    {
        Vector3 boardTargetPosition = GetPreviewWorldPosition(coordinate);
        if (!IsInsideBoard(coordinate))
        {
            return new PlacementPreviewState(true, true, boardTargetPosition, stoneProbeCenter, boardTargetPosition);
        }

        bool isCoordinateBlocked = IsCoordinateBlocked(coordinate);
        Vector3 blockerProbeOrigin = GetBlockerPreviewProbeOrigin(coordinate, releasePosition, stoneProbeCenter);
        if (!isCoordinateBlocked &&
            TryGetBlockerPreviewStonePosition(coordinate, blockerProbeOrigin, out Vector3 blockerPreviewPosition))
        {
            return new PlacementPreviewState(false, true, blockerPreviewPosition, stoneProbeCenter, blockerPreviewPosition, true);
        }

        return new PlacementPreviewState(isCoordinateBlocked, false, default, stoneProbeCenter, boardTargetPosition);
    }

    private Vector3 GetBlockerPreviewProbeOrigin(Vector2Int targetCoordinate, Vector3 releasePosition, Vector3 stoneProbeCenter)
    {
        if (!usePlacementTargetOffset ||
            !TryGetCoordinate(releasePosition, out Vector2Int releaseCoordinate) ||
            releaseCoordinate == targetCoordinate)
        {
            return stoneProbeCenter;
        }

        Vector3 targetBoardPosition = GetGridWorldPositionUnclamped(targetCoordinate);
        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        float heightFromBoard = Mathf.Max(GetEffectiveDragHoverHeight(), Mathf.Abs(Vector3.Dot(releasePosition - targetBoardPosition, up)));
        return targetBoardPosition + (up * heightFromBoard) + (stoneProbeCenter - releasePosition);
    }

    private Vector3 GetPreviewWorldPosition(Vector2Int coordinate)
    {
        return GetGridWorldPositionUnclamped(coordinate) + (grid.transform.up * GetEffectivePreviewTargetHeightOffset());
    }

    private Vector3 GetGridWorldPositionUnclamped(Vector2Int coordinate)
    {
        if (grid == null || !grid.IsReady)
        {
            return transform.position;
        }

        Vector3 origin = grid.GetWorldPosition(0, 0);
        GetGridStepVectors(out Vector3 xStep, out Vector3 yStep);
        return origin + (xStep * coordinate.x) + (yStep * coordinate.y);
    }

    private Vector3 GetGridPlaneBasePosition()
    {
        if (grid != null && grid.IsReady)
        {
            return grid.GetWorldPosition(0, 0);
        }

        return grid != null ? grid.transform.position : transform.position;
    }

    private void GetGridStepVectors(out Vector3 xStep, out Vector3 yStep)
    {
        if (grid == null || !grid.IsReady)
        {
            xStep = transform.right;
            yStep = transform.forward;
            return;
        }

        Vector3 origin = grid.GetWorldPosition(0, 0);
        xStep = grid.BoardSize > 1 ? grid.GetWorldPosition(1, 0) - origin : grid.transform.right;
        yStep = grid.BoardSize > 1 ? grid.GetWorldPosition(0, 1) - origin : grid.transform.forward;
    }

    private float GetEffectiveDragHoverHeight()
    {
        float requestedCellHeight = Mathf.Max(0f, dragHoverHeight);
        float cellSize = GetGridCellSize();
        if (cellSize <= 0f)
        {
            return requestedCellHeight;
        }

        return Mathf.Min(requestedCellHeight * cellSize, cellSize * DRAG_HOVER_HEIGHT_MAX_CELL_MULTIPLIER);
    }

    private float GetMinimumVisibleFallDistance()
    {
        float cellSize = GetGridCellSize();
        float minimumByCell = cellSize > 0f ? cellSize * 0.75f : 0.05f;
        return Mathf.Max(0.05f, minimumByCell, GetEffectiveDragHoverHeight());
    }

    private float GetEffectivePreviewLineWidth()
    {
        float baseWidth = Mathf.Max(0.005f, previewLineWidth);
        float cellSize = GetGridCellSize();
        return cellSize > 0f
            ? Mathf.Max(0.005f, cellSize * PREVIEW_LINE_WIDTH_CELL_MULTIPLIER * baseWidth)
            : baseWidth;
    }

    private float GetEffectivePreviewRayLineWidth()
    {
        return Mathf.Max(0.002f, GetEffectivePreviewLineWidth() * PREVIEW_RAY_LINE_WIDTH_MULTIPLIER);
    }

    private float GetEffectivePreviewTargetHeightOffset()
    {
        float offset = GetCellClampedDistance(previewTargetHeightOffset, PREVIEW_TARGET_HEIGHT_CELL_MULTIPLIER);
        float cellSize = GetGridCellSize();
        return cellSize > 0f
            ? Mathf.Max(offset, cellSize * PREVIEW_LINE_HEIGHT_MIN_CELL_MULTIPLIER)
            : offset;
    }

    private float GetEffectiveSettleOffset()
    {
        return GetCellClampedDistance(settleOffset, SETTLE_OFFSET_CELL_MULTIPLIER);
    }

    private float GetEffectiveBlockerCenterStackGap()
    {
        return GetCellClampedDistance(blockerCenterStackGap, BLOCKER_STACK_GAP_CELL_MULTIPLIER);
    }

    private float GetEffectiveInitialFallSpeed()
    {
        return GetCellClampedDistance(initialFallSpeed, INITIAL_FALL_SPEED_CELL_MULTIPLIER);
    }

    private float GetEffectiveFallGravityScale()
    {
        return Mathf.Min(Mathf.Max(0f, fallGravityScale), FALL_GRAVITY_SCALE_MAX);
    }

    private float GetCellClampedDistance(float serializedValue, float cellMultiplier)
    {
        serializedValue = Mathf.Max(0f, serializedValue);
        float cellSize = GetGridCellSize();
        return cellSize > 0f ? Mathf.Min(serializedValue, cellSize * cellMultiplier) : serializedValue;
    }

    private float GetGridCellSize()
    {
        if (grid == null || !grid.IsReady)
        {
            return 0f;
        }

        GetGridStepVectors(out Vector3 xStep, out Vector3 yStep);
        return Mathf.Max(0.001f, (xStep.magnitude + yStep.magnitude) * 0.5f);
    }

    private Vector3 GetGridUp()
    {
        return grid != null ? grid.transform.up : Vector3.up;
    }

    private Vector3 GetBlockerStackStonePosition(Collider blockerCollider, OmokFallingStone stone, Transform stackKey)
    {
        Physics.SyncTransforms();
        Vector3 up = GetGridUp();
        Vector3 centerPosition = GetBlockerStackCenter(blockerCollider);
        float halfHeight = stone != null ? stone.GetSnapOffsetAlongNormal(up) : 0f;
        float gap = GetEffectiveBlockerCenterStackGap();
        float targetHeight = GetBoundsMaxAlongAxis(blockerCollider.bounds, up) + halfHeight + gap;

        if (stackKey != null && _blockerCenterStackTopY.TryGetValue(stackKey, out float stackTopHeight))
        {
            targetHeight = Mathf.Max(targetHeight, stackTopHeight + halfHeight + gap);
        }

        return MovePointAlongAxis(centerPosition, up, targetHeight);
    }

    private Vector3 GetBlockerSurfaceStonePosition(Collider blockerCollider, OmokFallingStone stone, Vector3 fallbackPosition)
    {
        if (blockerCollider == null)
        {
            return fallbackPosition;
        }

        Physics.SyncTransforms();
        Vector3 up = GetGridUp();
        float halfHeight = stone != null ? stone.GetSnapOffsetAlongNormal(up) : 0f;
        float gap = GetEffectiveBlockerCenterStackGap();
        float targetHeight = GetBoundsMaxAlongAxis(blockerCollider.bounds, up) + halfHeight + gap;
        return MovePointAlongAxis(fallbackPosition, up, targetHeight);
    }

    private bool TryGetBlockerAtCoordinate(Vector2Int coordinate, float probeRadius, out Collider blockerCollider)
    {
        blockerCollider = null;

        if (grid == null || !grid.IsReady || !IsInsideBoard(coordinate))
        {
            return false;
        }

        Vector3 up = grid.transform.up;
        Vector3 boardPosition = grid.GetWorldPosition(coordinate);
        float fallbackRadius = Mathf.Max(0.02f, GetGridCellSize() * 0.35f);
        probeRadius = Mathf.Max(probeRadius, fallbackRadius);
        float probeHeight = Mathf.Max(
            raycastDistance,
            GetEffectiveDragHoverHeight() * 2f,
            GetGridCellSize() * 4f,
            probeRadius * 4f);
        Vector3 start = boardPosition + (up * (probeHeight * 0.5f));
        Vector3 end = boardPosition - (up * (probeRadius + GetEffectivePreviewTargetHeightOffset()));
        Vector3 direction = -up;
        float distance = Vector3.Distance(start, end);

        Physics.SyncTransforms();
        RaycastHit[] sphereHits = Physics.SphereCastAll(
            start,
            probeRadius,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        if (TryGetNearestBlockerHit(sphereHits, out RaycastHit sphereHit))
        {
            blockerCollider = sphereHit.collider;
            return blockerCollider != null;
        }

        Collider[] capsuleOverlaps = Physics.OverlapCapsule(
            start,
            end,
            probeRadius,
            ~0,
            QueryTriggerInteraction.Collide);

        return TryGetNearestBlockerCollider(capsuleOverlaps, boardPosition, out blockerCollider);
    }

    private Vector3 GetBlockerPreviewStackStonePosition(Collider blockerCollider, Vector3 fallbackPosition)
    {
        if (blockerCollider == null)
        {
            return fallbackPosition;
        }

        Vector3 up = GetGridUp();
        Physics.SyncTransforms();
        Transform stackKey = GetBlockerAttachmentTarget(blockerCollider);
        stackKey = stackKey != null ? stackKey : blockerCollider.transform;
        Vector3 centerPosition = GetBlockerStackCenter(blockerCollider);
        float halfHeight = GetDraggedStonePreviewExtent(up);
        float gap = GetEffectiveBlockerCenterStackGap();
        float targetHeight = GetBoundsMaxAlongAxis(blockerCollider.bounds, up) + halfHeight + gap;

        if (stackKey != null && _blockerCenterStackTopY.TryGetValue(stackKey, out float stackTopHeight))
        {
            targetHeight = Mathf.Max(targetHeight, stackTopHeight + halfHeight + gap);
        }

        return MovePointAlongAxis(centerPosition, up, targetHeight);
    }

    private Vector3 GetBlockerStackCenter(Collider blockerCollider)
    {
        if (TryGetExplicitAttachmentTarget(blockerCollider, out Transform explicitTarget))
        {
            return explicitTarget.position;
        }

        OmokBlockerTarget blockerTarget = blockerCollider != null
            ? blockerCollider.GetComponentInParent<OmokBlockerTarget>()
            : null;
        if (blockerTarget != null)
        {
            return blockerTarget.transform.position;
        }

        OmokFallingStone fallingStone = blockerCollider != null
            ? blockerCollider.GetComponentInParent<OmokFallingStone>()
            : null;
        if (fallingStone != null && fallingStone.BlockerTarget != null)
        {
            return fallingStone.BlockerTarget.position;
        }

        return blockerCollider != null ? blockerCollider.bounds.center : transform.position;
    }

    private static Vector3 MovePointAlongAxis(Vector3 point, Vector3 axis, float targetAxisPosition)
    {
        axis.Normalize();
        float currentAxisPosition = Vector3.Dot(point, axis);
        return point + (axis * (targetAxisPosition - currentAxisPosition));
    }

    private bool TryGetBlockerPreviewStonePosition(
        Vector2Int coordinate,
        Vector3 origin,
        out Vector3 blockerPreviewPosition)
    {
        blockerPreviewPosition = default;

        if (grid == null || !grid.IsReady)
        {
            return false;
        }

        Vector3 targetPosition = grid.GetWorldPosition(coordinate);
        Vector3 up = grid.transform.up;
        Vector3 direction = -up;
        float probeRadius = GetDraggedStonePreviewProbeRadius();
        float verticalDistance = Mathf.Abs(Vector3.Dot(origin - targetPosition, up));
        float probeDistance = Mathf.Max(
            verticalDistance + probeRadius + GetEffectivePreviewTargetHeightOffset(),
            GetEffectiveDragHoverHeight() + probeRadius);

        if (TryGetBlockerAtCoordinate(coordinate, probeRadius, out Collider coordinateBlocker))
        {
            Vector3 impactStoneCenter = BuildBlockerPreviewImpactStoneCenter(coordinateBlocker, origin, up);
            blockerPreviewPosition = BuildBlockerPreviewStonePosition(coordinateBlocker, impactStoneCenter);
            return true;
        }

        if (TryGetDraggedStoneBoundsBlocker(out Collider boundsBlocker))
        {
            Vector3 impactStoneCenter = BuildBlockerPreviewImpactStoneCenter(boundsBlocker, origin, up);
            blockerPreviewPosition = BuildBlockerPreviewStonePosition(boundsBlocker, impactStoneCenter);
            return true;
        }

        if (probeRadius > 0f)
        {
            Collider[] overlappingColliders = Physics.OverlapSphere(origin, probeRadius, ~0, QueryTriggerInteraction.Collide);
            if (TryGetNearestBlockerCollider(overlappingColliders, origin, out Collider overlappingBlocker))
            {
                blockerPreviewPosition = BuildBlockerPreviewStonePosition(overlappingBlocker, origin);
                return true;
            }

            RaycastHit[] sphereHits = Physics.SphereCastAll(origin, probeRadius, direction, probeDistance, ~0, QueryTriggerInteraction.Collide);
            if (TryGetNearestBlockerHit(sphereHits, out RaycastHit sphereHit))
            {
                Vector3 impactStoneCenter = origin + (direction * sphereHit.distance);
                blockerPreviewPosition = BuildBlockerPreviewStonePosition(sphereHit.collider, impactStoneCenter);
                return true;
            }

            Vector3 probeEnd = origin + (direction * probeDistance);
            Collider[] capsuleOverlaps = Physics.OverlapCapsule(origin, probeEnd, probeRadius, ~0, QueryTriggerInteraction.Collide);
            if (TryGetNearestBlockerCollider(capsuleOverlaps, origin, out Collider capsuleBlocker))
            {
                Vector3 impactStoneCenter = BuildBlockerPreviewImpactStoneCenter(capsuleBlocker, origin, up);
                blockerPreviewPosition = BuildBlockerPreviewStonePosition(capsuleBlocker, impactStoneCenter);
                return true;
            }

            return false;
        }

        RaycastHit[] rayHits = Physics.RaycastAll(origin, direction, probeDistance, ~0, QueryTriggerInteraction.Collide);
        if (!TryGetNearestBlockerHit(rayHits, out RaycastHit rayHit))
        {
            return false;
        }

        blockerPreviewPosition = BuildBlockerPreviewStonePosition(rayHit.collider, rayHit.point);
        return true;
    }

    private Vector3 BuildBlockerPreviewImpactStoneCenter(Collider blockerCollider, Vector3 origin, Vector3 up)
    {
        if (blockerCollider == null)
        {
            return origin;
        }

        float originHeight = Vector3.Dot(origin, up);
        float blockerTopHeight = GetBoundsMaxAlongAxis(blockerCollider.bounds, up);
        float stoneHalfHeight = GetDraggedStonePreviewExtent(up);
        float impactHeight = Mathf.Min(originHeight, blockerTopHeight + stoneHalfHeight);
        return origin + (up * (impactHeight - originHeight));
    }

    private Vector3 BuildBlockerPreviewStonePosition(Collider blockerCollider, Vector3 impactStoneCenter)
    {
        if (!TryResolveBlockerSettings(blockerCollider, out ResolvedBlockerSettings blockerSettings) ||
            !blockerSettings.KeepBlockedStone)
        {
            return impactStoneCenter;
        }

        return blockerSettings.AttachmentMode == OmokBlockerAttachmentMode.ObjectCenterXZ
            ? GetBlockerPreviewStackStonePosition(blockerCollider, impactStoneCenter)
            : GetBlockerPreviewSurfaceStonePosition(blockerCollider, impactStoneCenter);
    }

    private Vector3 GetBlockerPreviewSurfaceStonePosition(Collider blockerCollider, Vector3 fallbackPosition)
    {
        if (blockerCollider == null)
        {
            return fallbackPosition;
        }

        Vector3 up = GetGridUp();
        float halfHeight = GetDraggedStonePreviewExtent(up);
        float gap = GetEffectiveBlockerCenterStackGap();
        float targetHeight = GetBoundsMaxAlongAxis(blockerCollider.bounds, up) + halfHeight + gap;
        return MovePointAlongAxis(fallbackPosition, up, targetHeight);
    }

    private float GetDraggedStonePreviewProbeRadius()
    {
        if (!TryGetDraggedStonePreviewBounds(out Bounds bounds))
        {
            return 0f;
        }

        Vector3 right = grid != null ? grid.transform.right : Vector3.right;
        Vector3 forward = grid != null ? grid.transform.forward : Vector3.forward;
        float rightExtent = ProjectBoundsExtent(bounds.extents, right);
        float forwardExtent = ProjectBoundsExtent(bounds.extents, forward);
        return GetBlockerProbeRadius(Mathf.Max(rightExtent, forwardExtent));
    }

    private Vector3 GetDraggedStonePreviewProbeCenter(Vector3 fallbackPosition)
    {
        return TryGetDraggedStonePreviewBounds(out Bounds bounds)
            ? bounds.center
            : fallbackPosition;
    }

    private bool TryGetDraggedStoneBoundsBlocker(out Collider blockerCollider)
    {
        blockerCollider = null;

        if (!TryGetDraggedStonePreviewBounds(out Bounds bounds))
        {
            return false;
        }

        float contactPadding = Mathf.Max(0.02f, GetEffectivePreviewTargetHeightOffset(), blockerProbeExtraRadius);
        bounds.Expand(contactPadding * 2f);

        Physics.SyncTransforms();
        Collider[] overlappingColliders = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        return TryGetNearestBlockerCollider(overlappingColliders, bounds.center, out blockerCollider);
    }

    private float GetFallingStoneBlockerProbeRadius(OmokFallingStone stone)
    {
        if (stone == null || grid == null)
        {
            return 0f;
        }

        float baseRadius = stone.GetMaxExtentOnAxes(grid.transform.right, grid.transform.forward);
        return GetBlockerProbeRadius(baseRadius);
    }

    private float GetBlockerProbeRadius(float baseRadius)
    {
        return Mathf.Max(0f, (baseRadius * blockerProbeRadiusMultiplier) + blockerProbeExtraRadius);
    }

    private float GetDraggedStonePreviewExtent(Vector3 axis)
    {
        return TryGetDraggedStonePreviewBounds(out Bounds bounds)
            ? ProjectBoundsExtent(bounds.extents, axis)
            : 0f;
    }

    private bool TryGetDraggedStonePreviewBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (_draggedStoneObject == null)
        {
            return false;
        }

        Renderer[] renderers = _draggedStoneObject.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private bool TryBuildPlacementRequest(
        OmokStoneLauncher launcher,
        Vector3 releasePosition,
        Vector2Int targetCoordinate,
        out OmokStonePlacementRequest request)
    {
        request = default;

        if (!IsLauncherConfigured(launcher) ||
            grid == null ||
            !grid.IsReady ||
            !IsInsideBoard(targetCoordinate) ||
            IsCoordinateBlocked(targetCoordinate))
        {
            return false;
        }

        OmokStoneColor stoneColor = GetLauncherStoneColor(launcher);
        if (stoneColor == OmokStoneColor.None)
        {
            return false;
        }

        request = new OmokStonePlacementRequest(stoneColor, targetCoordinate, releasePosition, usePlacementTargetOffset);
        return true;
    }

    private bool TryBuildPlacementRequest(OmokStoneColor stoneColor, Vector2Int targetCoordinate, out OmokStonePlacementRequest request)
    {
        request = default;

        if (stoneColor == OmokStoneColor.None ||
            grid == null ||
            !grid.IsReady ||
            !IsInsideBoard(targetCoordinate) ||
            IsCoordinateBlocked(targetCoordinate))
        {
            return false;
        }

        Vector3 releasePosition = grid.GetWorldPosition(targetCoordinate) + (grid.transform.up * GetEffectiveDragHoverHeight());
        request = new OmokStonePlacementRequest(stoneColor, targetCoordinate, releasePosition);
        return true;
    }

    private bool IsInsideBoard(Vector2Int coordinate)
    {
        return grid != null &&
               coordinate.x >= 0 &&
               coordinate.x < grid.BoardSize &&
               coordinate.y >= 0 &&
               coordinate.y < grid.BoardSize;
    }

    private void DrawPreview(Vector2Int coordinate, PlacementPreviewState previewState)
    {
        if (!showPreview)
        {
            HidePreview();
            return;
        }

        EnsurePreviewRenderer();
        if (_previewRenderer == null)
        {
            return;
        }

        _previewRenderer.widthMultiplier = GetEffectivePreviewLineWidth();

        Color color = previewState.IsBlocked ? blockedPreviewColor : validPreviewColor;
        _previewRenderer.startColor = color;
        _previewRenderer.endColor = color;

        Vector3 previewTargetPosition = GetPreviewTargetPosition(previewState);
        Vector3[] corners = previewState.IsBlockerStackTarget
            ? GetPreviewCorners(previewTargetPosition)
            : GetPreviewCorners(coordinate);
        _previewRenderer.positionCount = corners.Length;
        _previewRenderer.SetPositions(corners);
        _previewRenderer.enabled = true;

        DrawPreviewRay(previewState.RayStartPosition, previewTargetPosition, color);
    }

    private Vector3 GetPreviewTargetPosition(PlacementPreviewState previewState)
    {
        if (!previewState.IsBlockerStackTarget || !previewState.HasStoneWorldPosition)
        {
            return previewState.RayTargetPosition;
        }

        Vector3 up = GetGridUp();
        float stoneHalfHeight = GetDraggedStonePreviewExtent(up);
        float markerHeight = Mathf.Max(GetEffectivePreviewTargetHeightOffset(), GetEffectivePreviewLineWidth());
        return previewState.StoneWorldPosition - (up * Mathf.Max(0f, stoneHalfHeight - markerHeight));
    }

    private void DrawPreviewRay(Vector3 startPosition, Vector3 targetPosition, Color color)
    {
        if (_previewRayRenderer == null || grid == null || !grid.IsReady)
        {
            return;
        }

        _previewRayRenderer.widthMultiplier = GetEffectivePreviewRayLineWidth();
        _previewRayRenderer.startColor = color;
        _previewRayRenderer.endColor = color;
        _previewRayRenderer.positionCount = 2;
        Vector3 rayStartPosition = GetPreviewRayStartPosition(startPosition, targetPosition);
        _previewRayRenderer.SetPosition(0, rayStartPosition);
        _previewRayRenderer.SetPosition(1, GetPreviewRayEndPosition(rayStartPosition, targetPosition));
        _previewRayRenderer.enabled = true;
    }

    private Vector3 GetPreviewRayStartPosition(Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 ray = targetPosition - startPosition;
        float distance = ray.magnitude;
        if (distance <= 0.001f)
        {
            return startPosition;
        }

        float startGap = Mathf.Max(GetEffectivePreviewLineWidth() * 1.5f, GetDraggedStonePreviewProbeRadius() * 0.65f);
        return startPosition + (ray / distance * Mathf.Min(startGap, distance * 0.45f));
    }

    private Vector3 GetPreviewRayEndPosition(Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 ray = targetPosition - startPosition;
        float distance = ray.magnitude;
        if (distance <= 0.001f)
        {
            return targetPosition;
        }

        float cellSize = GetGridCellSize();
        float targetGap = Mathf.Max(GetEffectivePreviewLineWidth() * 1.5f, cellSize * PREVIEW_RAY_TARGET_GAP_CELL_MULTIPLIER);
        if (distance <= targetGap)
        {
            return targetPosition;
        }

        return targetPosition - (ray / distance * targetGap);
    }

    private bool TryCreateDraggedStone(OmokStoneLauncher launcher)
    {
        if (!IsLauncherConfigured(launcher))
        {
            return false;
        }

        _draggedStoneObject = Instantiate(launcher.StonePrefab, launcher.Source.position, launcher.StonePrefab.transform.rotation, stoneRoot);
        Collider[] dragColliders = _draggedStoneObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in dragColliders)
        {
            collider.enabled = false;
        }

        Rigidbody existingRigidbody = _draggedStoneObject.GetComponent<Rigidbody>();
        if (existingRigidbody != null)
        {
            Destroy(existingRigidbody);
        }

        OmokFallingStone existingFallingStone = _draggedStoneObject.GetComponent<OmokFallingStone>();
        if (existingFallingStone != null)
        {
            Destroy(existingFallingStone);
        }

        ApplyDraggedStonePreviewAppearance(launcher, _draggedStoneObject);
        return true;
    }

    private void UpdateDraggedStoneOffBoard()
    {
        if (_draggedStoneObject == null || !EnsureAimController(true))
        {
            return;
        }

        if (!aimController.TryGetFreeAim(GetEffectiveDragHoverHeight(), out Vector3 aimPosition))
        {
            return;
        }

        Vector3 dragAimPosition = ClampDraggedStoneToScreen(aimPosition);
        UpdateDraggedStoneAtPosition(dragAimPosition);
        aimController.SetCurrentAimPosition(dragAimPosition);
    }

    private void UpdateDraggedStoneAtPosition(Vector3 position)
    {
        if (_draggedStoneObject == null)
        {
            return;
        }

        _draggedStoneObject.transform.position = position;
    }

    private Vector3 ClampDraggedStoneToScreen(Vector3 aimPosition)
    {
        if (!keepDraggedStoneOnScreen || _draggedStoneObject == null)
        {
            return aimPosition;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return aimPosition;
        }

        UpdateDraggedStoneAtPosition(aimPosition);
        if (!TryGetDraggedStoneViewportRect(cameraToUse, out Rect viewportRect))
        {
            return aimPosition;
        }

        float padding = Mathf.Clamp(draggedStoneScreenPadding, 0f, 0.2f);
        float min = padding;
        float max = 1f - padding;
        Vector2 viewportDelta = Vector2.zero;

        if (viewportRect.xMin < min)
        {
            viewportDelta.x = min - viewportRect.xMin;
        }
        else if (viewportRect.xMax > max)
        {
            viewportDelta.x = max - viewportRect.xMax;
        }

        if (viewportRect.yMin < min)
        {
            viewportDelta.y = min - viewportRect.yMin;
        }
        else if (viewportRect.yMax > max)
        {
            viewportDelta.y = max - viewportRect.yMax;
        }

        if (viewportDelta.sqrMagnitude <= 0.000001f)
        {
            return aimPosition;
        }

        return TryConvertViewportDeltaToWorldDelta(cameraToUse, aimPosition, viewportDelta, out Vector3 worldDelta)
            ? aimPosition + worldDelta
            : aimPosition;
    }

    private void QueueCursorWarpToDropPosition(Vector3 worldPosition)
    {
        if (!warpCursorToDropPositionOnRelease)
        {
            return;
        }

        _pendingCursorWarpWorldPosition = worldPosition;
        _pendingCursorWarpFrame = Time.frameCount + 1;
        _pendingCursorWarpAttempts = 0;
        _hasPendingCursorWarp = true;
    }

    private void ApplyPendingCursorWarp()
    {
        if (!_hasPendingCursorWarp || Time.frameCount < _pendingCursorWarpFrame)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.None)
        {
            DelayOrCancelPendingCursorWarp();
            return;
        }

        if (WarpCursorToDropPosition(_pendingCursorWarpWorldPosition))
        {
            _hasPendingCursorWarp = false;
            return;
        }

        DelayOrCancelPendingCursorWarp();
    }

    private void DelayOrCancelPendingCursorWarp()
    {
        _pendingCursorWarpAttempts++;
        if (_pendingCursorWarpAttempts >= CURSOR_WARP_MAX_ATTEMPTS)
        {
            _hasPendingCursorWarp = false;
            return;
        }

        _pendingCursorWarpFrame = Time.frameCount + 1;
    }

    private bool WarpCursorToDropPosition(Vector3 worldPosition)
    {
        if (!warpCursorToDropPositionOnRelease)
        {
            return false;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            return false;
        }

        return TryWarpCursorPosition(new Vector2(
            Mathf.Clamp(screenPosition.x, 0f, Mathf.Max(0f, Screen.width - 1f)),
            Mathf.Clamp(screenPosition.y, 0f, Mathf.Max(0f, Screen.height - 1f))));
    }

    private static bool TryWarpCursorPosition(Vector2 targetScreenPosition)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!GetCursorPos(out NativeCursorPoint currentCursorPosition))
        {
            return false;
        }

        Vector3 currentScreenPosition = Input.mousePosition;
        int targetX = Mathf.RoundToInt(currentCursorPosition.X + targetScreenPosition.x - currentScreenPosition.x);
        int targetY = Mathf.RoundToInt(currentCursorPosition.Y - (targetScreenPosition.y - currentScreenPosition.y));
        return SetCursorPos(targetX, targetY);
#else
        return false;
#endif
    }

    private bool TryGetDraggedStoneViewportRect(Camera cameraToUse, out Rect viewportRect)
    {
        viewportRect = default;

        if (!TryGetDraggedStonePreviewBounds(out Bounds bounds))
        {
            return false;
        }

        bool hasPoint = false;
        Vector2 min = default;
        Vector2 max = default;

        Vector3 boundsMin = bounds.min;
        Vector3 boundsMax = bounds.max;
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMin.x, boundsMin.y, boundsMin.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMin.x, boundsMin.y, boundsMax.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMin.x, boundsMax.y, boundsMin.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMin.x, boundsMax.y, boundsMax.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMax.x, boundsMin.y, boundsMin.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMax.x, boundsMin.y, boundsMax.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMax.x, boundsMax.y, boundsMin.z), ref hasPoint, ref min, ref max);
        EncapsulateViewportCorner(cameraToUse, new Vector3(boundsMax.x, boundsMax.y, boundsMax.z), ref hasPoint, ref min, ref max);

        if (!hasPoint)
        {
            return false;
        }

        viewportRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static void EncapsulateViewportCorner(Camera cameraToUse, Vector3 worldPoint, ref bool hasPoint, ref Vector2 min, ref Vector2 max)
    {
        Vector3 viewportPoint = cameraToUse.WorldToViewportPoint(worldPoint);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        Vector2 point = new(viewportPoint.x, viewportPoint.y);
        if (!hasPoint)
        {
            min = point;
            max = point;
            hasPoint = true;
            return;
        }

        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    private bool TryConvertViewportDeltaToWorldDelta(Camera cameraToUse, Vector3 aimPosition, Vector2 viewportDelta, out Vector3 worldDelta)
    {
        worldDelta = default;

        Vector3 aimViewportPosition = cameraToUse.WorldToViewportPoint(aimPosition);
        if (aimViewportPosition.z <= 0f)
        {
            return false;
        }

        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        Plane aimPlane = new(up, aimPosition);
        Ray currentRay = cameraToUse.ViewportPointToRay(aimViewportPosition);
        Ray adjustedRay = cameraToUse.ViewportPointToRay(aimViewportPosition + new Vector3(viewportDelta.x, viewportDelta.y, 0f));
        if (!aimPlane.Raycast(currentRay, out float currentDistance) ||
            !aimPlane.Raycast(adjustedRay, out float adjustedDistance))
        {
            return false;
        }

        worldDelta = adjustedRay.GetPoint(adjustedDistance) - currentRay.GetPoint(currentDistance);
        return true;
    }

    private void ActivateStoneColliders(GameObject stoneObject)
    {
        if (stoneObject == null)
        {
            return;
        }

        Collider[] colliders = stoneObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = true;
        }
    }

    private Vector3[] GetPreviewCorners(Vector2Int coordinate)
    {
        Vector3 center = grid.GetWorldPosition(coordinate);
        Vector3 horizontalHalfStep = GetHorizontalHalfStep(coordinate);
        Vector3 verticalHalfStep = GetVerticalHalfStep(coordinate);
        float inset = Mathf.Max(GetEffectivePreviewLineWidth(), GetGridCellSize() * PREVIEW_LINE_INSET_CELL_MULTIPLIER);
        horizontalHalfStep = GetInsetHalfStep(horizontalHalfStep, inset);
        verticalHalfStep = GetInsetHalfStep(verticalHalfStep, inset);
        Vector3 offset = grid.transform.up * GetEffectivePreviewTargetHeightOffset();

        Vector3 bottomLeft = center - horizontalHalfStep - verticalHalfStep + offset;
        Vector3 topLeft = center - horizontalHalfStep + verticalHalfStep + offset;
        Vector3 topRight = center + horizontalHalfStep + verticalHalfStep + offset;
        Vector3 bottomRight = center + horizontalHalfStep - verticalHalfStep + offset;

        return new[]
        {
            bottomLeft,
            topLeft,
            topRight,
            bottomRight
        };
    }

    private Vector3[] GetPreviewCorners(Vector3 center)
    {
        Vector3 right = grid != null ? grid.transform.right : Vector3.right;
        Vector3 forward = grid != null ? grid.transform.forward : Vector3.forward;
        float markerHalfSize = Mathf.Max(GetDraggedStonePreviewProbeRadius(), GetEffectivePreviewLineWidth() * 2f, 0.1f);
        Vector3 horizontalHalfStep = right * markerHalfSize;
        Vector3 verticalHalfStep = forward * markerHalfSize;

        Vector3 bottomLeft = center - horizontalHalfStep - verticalHalfStep;
        Vector3 topLeft = center - horizontalHalfStep + verticalHalfStep;
        Vector3 topRight = center + horizontalHalfStep + verticalHalfStep;
        Vector3 bottomRight = center + horizontalHalfStep - verticalHalfStep;

        return new[]
        {
            bottomLeft,
            topLeft,
            topRight,
            bottomRight
        };
    }

    private static Vector3 GetInsetHalfStep(Vector3 halfStep, float inset)
    {
        float magnitude = halfStep.magnitude;
        if (magnitude <= 0.001f)
        {
            return halfStep;
        }

        return halfStep.normalized * Mathf.Max(0.001f, magnitude - inset);
    }

    private Vector3 GetHorizontalHalfStep(Vector2Int coordinate)
    {
        Vector3 center = grid.GetWorldPosition(coordinate);

        if (coordinate.x < grid.BoardSize - 1)
        {
            return (grid.GetWorldPosition(coordinate.x + 1, coordinate.y) - center) * 0.5f;
        }

        if (coordinate.x > 0)
        {
            return (center - grid.GetWorldPosition(coordinate.x - 1, coordinate.y)) * 0.5f;
        }

        return grid.transform.right * 0.5f;
    }

    private Vector3 GetVerticalHalfStep(Vector2Int coordinate)
    {
        Vector3 center = grid.GetWorldPosition(coordinate);

        if (coordinate.y < grid.BoardSize - 1)
        {
            return (grid.GetWorldPosition(coordinate.x, coordinate.y + 1) - center) * 0.5f;
        }

        if (coordinate.y > 0)
        {
            return (center - grid.GetWorldPosition(coordinate.x, coordinate.y - 1)) * 0.5f;
        }

        return grid.transform.forward * 0.5f;
    }

    private void EnsurePreviewRenderer()
    {
        if (_previewRenderer != null && _previewRayRenderer != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (_previewMaterial == null && shader != null)
        {
            _previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (_previewRenderer == null)
        {
            _previewRenderer = CreatePreviewLineRenderer("StonePlacementPreviewOutline");
            _previewRenderer.loop = true;
            _previewRenderer.numCornerVertices = 4;
        }

        if (_previewRayRenderer == null)
        {
            _previewRayRenderer = CreatePreviewLineRenderer("StonePlacementPreviewRay");
            _previewRayRenderer.loop = false;
            _previewRayRenderer.numCornerVertices = 0;
        }
    }

    private LineRenderer CreatePreviewLineRenderer(string objectName)
    {
        GameObject previewObject = new GameObject(objectName);
        previewObject.hideFlags = HideFlags.HideAndDontSave;
        previewObject.transform.SetParent(transform, false);

        LineRenderer renderer = previewObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCapVertices = 2;
        renderer.widthMultiplier = GetEffectivePreviewLineWidth();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;

        if (_previewMaterial != null)
        {
            renderer.material = _previewMaterial;
        }

        return renderer;
    }

    private void HidePreview()
    {
        if (_previewRenderer != null)
        {
            _previewRenderer.enabled = false;
        }

        if (_previewRayRenderer != null)
        {
            _previewRayRenderer.enabled = false;
        }
    }

    private void CacheExistingStoneCoordinates()
    {
        _occupiedCoordinates.Clear();
        _reservedCoordinates.Clear();
        _stonesByCoordinate.Clear();
        _blockerCenterStackTopY.Clear();
        _blockerStoneStacks.Clear();

        if (grid == null || !grid.IsReady)
        {
            return;
        }

        Transform cacheRoot = stoneRoot != null ? stoneRoot : transform;
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider collider in colliders)
        {
            if (collider == null || IsBoardHit(collider) || IsBlockerHit(collider) || collider.isTrigger)
            {
                continue;
            }

            if (cacheRoot != null &&
                collider.transform != cacheRoot &&
                !collider.transform.IsChildOf(cacheRoot))
            {
                continue;
            }

            if (IsLauncherHit(collider, goldLauncher) || IsLauncherHit(collider, silverLauncher))
            {
                continue;
            }

            if (!grid.TryGetCoordinate(collider.bounds.center, out Vector2Int coordinate))
            {
                continue;
            }

            _occupiedCoordinates.Add(coordinate);

            OmokFallingStone stone = collider.GetComponentInParent<OmokFallingStone>();
            if (stone != null && stone.IsSnapped)
            {
                _stonesByCoordinate[coordinate] = stone;
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromMatchManager();
        DestroyDraggedStonePreview(_draggedStoneObject);

        ClearDragState();
    }

    private void OnDestroy()
    {
        if (_previewRenderer != null)
        {
            Destroy(_previewRenderer.gameObject);
        }

        if (_previewRayRenderer != null)
        {
            Destroy(_previewRayRenderer.gameObject);
        }

        if (_previewMaterial != null)
        {
            Destroy(_previewMaterial);
        }

        DestroyDraggedStonePreview(_draggedStoneObject);
    }

    private void ResolveNamedLayers()
    {
        _boardLayer = LayerMask.NameToLayer(BOARD_LAYER_NAME);
        _blockerLayer = LayerMask.NameToLayer(BLOCKER_LAYER_NAME);
        _launcherLayerMask = BuildLauncherLayerMask();
    }

    private static bool IsLayerInHierarchy(Transform target, int layer)
    {
        if (target == null || layer < 0)
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.gameObject.layer == layer)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool TryResolveBlockerSettings(Collider blockerCollider, out ResolvedBlockerSettings settings)
    {
        settings = default;

        if (blockerCollider == null)
        {
            return false;
        }

        OmokBlockerTarget explicitTarget = blockerCollider.GetComponentInParent<OmokBlockerTarget>();
        if (explicitTarget != null)
        {
            if (!explicitTarget.BlocksStone)
            {
                return false;
            }

            settings = new ResolvedBlockerSettings(
                explicitTarget.KeepBlockedStone,
                explicitTarget.AttachmentMode,
                explicitTarget.ConsumeTurnWhenBlocked,
                explicitTarget.CountForBlockerStackWin);
            return true;
        }

        if (IsLayerInHierarchy(blockerCollider.transform, _blockerLayer))
        {
            settings = new ResolvedBlockerSettings(
                true,
                blockerAttachmentMode,
                true,
                true);
            return true;
        }

        return false;
    }

    private bool TryGetNearestBlockerCollider(Collider[] colliders, Vector3 origin, out Collider nearestBlocker)
    {
        nearestBlocker = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (!IsBlockerHit(collider))
            {
                continue;
            }

            float sqrDistance = (collider.ClosestPoint(origin) - origin).sqrMagnitude;
            if (nearestBlocker == null || sqrDistance < nearestSqrDistance)
            {
                nearestBlocker = collider;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearestBlocker != null;
    }

    private bool TryGetNearestBlockerHit(RaycastHit[] hits, out RaycastHit nearestHit)
    {
        nearestHit = default;
        bool foundHit = false;

        foreach (RaycastHit hit in hits)
        {
            if (!IsBlockerHit(hit.collider))
            {
                continue;
            }

            if (!foundHit || hit.distance < nearestHit.distance)
            {
                nearestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private int RegisterBlockedStone(Transform stackKey, OmokStoneColor stoneColor)
    {
        if (stackKey == null || stoneColor == OmokStoneColor.None)
        {
            return 0;
        }

        if (!_blockerStoneStacks.TryGetValue(stackKey, out List<OmokStoneColor> stack))
        {
            stack = new List<OmokStoneColor>();
            _blockerStoneStacks.Add(stackKey, stack);
        }

        stack.Add(stoneColor);

        int consecutiveCount = 0;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i] != stoneColor)
            {
                break;
            }

            consecutiveCount++;
        }

        return consecutiveCount;
    }

    private static Transform GetBlockerStackKey(Transform blockerTarget, Collider blockerCollider)
    {
        if (blockerTarget != null)
        {
            return blockerTarget;
        }

        return blockerCollider != null ? blockerCollider.transform : null;
    }

    private static float ProjectBoundsExtent(Vector3 boundsExtents, Vector3 axis)
    {
        axis.Normalize();
        return Mathf.Abs(axis.x) * boundsExtents.x +
               Mathf.Abs(axis.y) * boundsExtents.y +
               Mathf.Abs(axis.z) * boundsExtents.z;
    }

    private static float GetBoundsMaxAlongAxis(Bounds bounds, Vector3 axis)
    {
        axis.Normalize();
        return Vector3.Dot(bounds.center, axis) + ProjectBoundsExtent(bounds.extents, axis);
    }

    private int BuildLauncherLayerMask()
    {
        int mask = 0;
        AddLauncherLayers(goldLauncher, ref mask);
        AddLauncherLayers(silverLauncher, ref mask);
        return mask;
    }

    private static void AddLauncherLayers(OmokStoneLauncher launcher, ref int mask)
    {
        if (launcher == null || launcher.Source == null)
        {
            return;
        }

        Transform[] hierarchy = launcher.Source.GetComponentsInChildren<Transform>(true);
        foreach (Transform current in hierarchy)
        {
            mask |= 1 << current.gameObject.layer;
        }
    }

    private void ApplyDraggedStonePreviewAppearance(OmokStoneLauncher launcher, GameObject previewObject)
    {
        if (previewObject == null)
        {
            return;
        }

        _draggedStonePreviewMaterials.Clear();
        previewObject.transform.localScale *= previewStoneScaleMultiplier;

        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material[] sharedMaterials = renderer.sharedMaterials;
            Material[] previewMaterials = new Material[sharedMaterials.Length];

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material sourceMaterial = sharedMaterials[i];
                if (sourceMaterial == null)
                {
                    continue;
                }

                Material previewMaterialInstance = new Material(sourceMaterial);
                ConfigurePreviewMaterial(previewMaterialInstance, GetDraggedStonePreviewColor(launcher));
                previewMaterials[i] = previewMaterialInstance;
                _draggedStonePreviewMaterials.Add(previewMaterialInstance);
            }

            renderer.sharedMaterials = previewMaterials;
        }
    }

    private void UpdateDraggedStonePreviewVisual()
    {
        if (_draggedStoneObject == null || _activeLauncher == null)
        {
            return;
        }

        Color previewColor = GetDraggedStonePreviewColor(_activeLauncher);
        foreach (Material material in _draggedStonePreviewMaterials)
        {
            if (material == null)
            {
                continue;
            }

            ApplyPreviewMaterialColor(material, previewColor);
        }
    }

    private Color GetDraggedStonePreviewColor(OmokStoneLauncher launcher)
    {
        OmokStoneColor stoneColor = GetLauncherStoneColor(launcher);
        return stoneColor == OmokStoneColor.Gold
            ? new Color(1f, 0.72f, 0.18f, previewStoneAlpha)
            : new Color(0.82f, 0.86f, 0.9f, previewStoneAlpha);
    }

    private void ConfigurePreviewMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(_surfacePropertyId))
        {
            material.SetFloat(_surfacePropertyId, 1f);
        }

        if (material.HasProperty(_blendPropertyId))
        {
            material.SetFloat(_blendPropertyId, 0f);
        }

        if (material.HasProperty(_srcBlendPropertyId))
        {
            material.SetFloat(_srcBlendPropertyId, (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty(_dstBlendPropertyId))
        {
            material.SetFloat(_dstBlendPropertyId, (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(_zWritePropertyId))
        {
            material.SetFloat(_zWritePropertyId, 0f);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        ApplyPreviewMaterialColor(material, color);
    }

    private static void ApplyPreviewMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(_baseColorPropertyId))
        {
            material.SetColor(_baseColorPropertyId, color);
        }

        if (material.HasProperty(_colorPropertyId))
        {
            material.SetColor(_colorPropertyId, color);
        }
    }

    private void DestroyDraggedStonePreview(GameObject previewObject)
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        foreach (Material material in _draggedStonePreviewMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        _draggedStonePreviewMaterials.Clear();
    }

    private static Transform GetBlockerAttachmentTarget(Collider blockerCollider)
    {
        if (blockerCollider == null)
        {
            return null;
        }

        if (TryGetExplicitAttachmentTarget(blockerCollider, out Transform explicitTarget))
        {
            return explicitTarget;
        }

        OmokFallingStone fallingStone = blockerCollider.GetComponentInParent<OmokFallingStone>();
        if (fallingStone != null && fallingStone.BlockerTarget != null)
        {
            return fallingStone.BlockerTarget;
        }

        return blockerCollider.attachedRigidbody != null
            ? blockerCollider.attachedRigidbody.transform
            : blockerCollider.transform;
    }

    private static bool TryGetExplicitAttachmentTarget(Collider blockerCollider, out Transform attachmentTarget)
    {
        attachmentTarget = null;

        if (blockerCollider == null)
        {
            return false;
        }

        OmokBlockerTarget blockerTarget = blockerCollider.GetComponentInParent<OmokBlockerTarget>();
        if (blockerTarget == null || blockerTarget.AttachmentTarget == null)
        {
            return false;
        }

        attachmentTarget = blockerTarget.AttachmentTarget;
        return true;
    }

    private readonly struct ResolvedBlockerSettings
    {
        public ResolvedBlockerSettings(
            bool keepBlockedStone,
            OmokBlockerAttachmentMode attachmentMode,
            bool consumeTurnWhenBlocked,
            bool countForBlockerStackWin)
        {
            KeepBlockedStone = keepBlockedStone;
            AttachmentMode = attachmentMode;
            ConsumeTurnWhenBlocked = consumeTurnWhenBlocked;
            CountForBlockerStackWin = countForBlockerStackWin && keepBlockedStone;
        }

        public bool KeepBlockedStone { get; }
        public OmokBlockerAttachmentMode AttachmentMode { get; }
        public bool ConsumeTurnWhenBlocked { get; }
        public bool CountForBlockerStackWin { get; }
    }

    private readonly struct PlacementPreviewState
    {
        public PlacementPreviewState(
            bool isBlocked,
            bool hasStoneWorldPosition,
            Vector3 stoneWorldPosition,
            Vector3 rayStartPosition,
            Vector3 rayTargetPosition,
            bool isBlockerStackTarget = false)
        {
            IsBlocked = isBlocked;
            HasStoneWorldPosition = hasStoneWorldPosition;
            StoneWorldPosition = stoneWorldPosition;
            RayStartPosition = rayStartPosition;
            RayTargetPosition = rayTargetPosition;
            IsBlockerStackTarget = isBlockerStackTarget;
        }

        public bool IsBlocked { get; }
        public bool IsBlockerStackTarget { get; }
        public bool HasStoneWorldPosition { get; }
        public Vector3 StoneWorldPosition { get; }
        public Vector3 RayStartPosition { get; }
        public Vector3 RayTargetPosition { get; }
    }

}
