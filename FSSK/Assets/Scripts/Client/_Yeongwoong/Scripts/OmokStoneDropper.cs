using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    Black,
    White
}

[Serializable]
public class OmokStoneLauncher
{
    public Transform source;
    public GameObject stonePrefab;
}

public readonly struct OmokStonePlacementRequest
{
    public OmokStonePlacementRequest(OmokStoneColor stoneColor, Vector2Int targetCoordinate, Vector3 releasePosition)
    {
        StoneColor = stoneColor;
        TargetCoordinate = targetCoordinate;
        ReleasePosition = releasePosition;
    }

    public OmokStoneColor StoneColor { get; }
    public Vector2Int TargetCoordinate { get; }
    public Vector3 ReleasePosition { get; }
}

public class OmokStoneDropper : MonoBehaviour
{
    private const string BoardLayerName = "Board";
    private const string BlockerLayerName = "Blocker";
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int SurfacePropertyId = Shader.PropertyToID("_Surface");
    private static readonly int BlendPropertyId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendPropertyId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendPropertyId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWritePropertyId = Shader.PropertyToID("_ZWrite");

    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform stoneRoot;

    [Header("Launchers")]
    [SerializeField] private OmokStoneLauncher blackLauncher = new();
    [SerializeField] private OmokStoneLauncher whiteLauncher = new();

    [Header("Manual Input")]
    [SerializeField] private bool allowBlackManualPlacement = true;
    [SerializeField] private bool allowWhiteManualPlacement = true;

    [Header("Drag")]
    [SerializeField, Min(0f)] private float dragHoverHeight = 1.25f;

    [Header("Spawn")]
    [SerializeField] private OmokStoneSnapTiming snapTiming = OmokStoneSnapTiming.OnPlacement;
    [SerializeField, Min(1f)] private float raycastDistance = 500f;

    [Header("Landing")]
    [SerializeField, Min(0f)] private float settleOffset = 0.01f;

    [Header("Blocker")]
    [SerializeField] private OmokBlockerAttachmentMode blockerAttachmentMode = OmokBlockerAttachmentMode.SurfaceContact;
    [SerializeField, Min(0f)] private float blockerCenterStackGap = 0.01f;

    [Header("Drop Feel")]
    [SerializeField, Min(0f)] private float initialFallSpeed = 2.5f;
    [SerializeField, Min(0f)] private float fallGravityScale = 1f;

    [Header("Preview")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Color validPreviewColor = new(0.25f, 1f, 0.45f, 0.95f);
    [SerializeField] private Color blockedPreviewColor = new(1f, 0.3f, 0.3f, 0.95f);
    [SerializeField, Min(0.005f)] private float previewLineWidth = 0.08f;
    [SerializeField, Min(0f)] private float previewTargetHeightOffset = 0.05f;
    [SerializeField, Range(0.15f, 0.85f)] private float previewStoneAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float blockedPreviewTintStrength = 0.65f;
    [SerializeField, Range(0.85f, 1f)] private float previewStoneScaleMultiplier = 0.97f;

    private readonly HashSet<Vector2Int> occupiedCoordinates = new();
    private readonly HashSet<Vector2Int> reservedCoordinates = new();
    private readonly Dictionary<Transform, float> blockerCenterStackTopY = new();
    private readonly List<Material> draggedStonePreviewMaterials = new();
    private LineRenderer previewRenderer;
    private LineRenderer previewRayRenderer;
    private Material previewMaterial;

    private OmokStoneLauncher activeLauncher;
    private bool isDraggingLauncher;
    private bool hasDragBoardTarget;
    private Vector2Int currentDragCoordinate;
    private GameObject draggedStoneObject;
    private int boardLayer = -1;
    private int blockerLayer = -1;
    private int boardLayerMask;
    private int launcherLayerMask;

    public OmokStoneSnapTiming SnapTiming => snapTiming;
    public event Action<OmokStonePlacementRequest> PlacementRequested;
    public event Action<Vector2Int, OmokStoneColor> StonePlaced;
    public event Action<OmokStoneColor> StoneBlocked;

    private void Reset()
    {
        grid = GetComponent<OmokGrid>();
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

        CacheExistingStoneCoordinates();
    }

    private void OnValidate()
    {
        ResolveNamedLayers();
    }

    private void Update()
    {
        if (isDraggingLauncher)
        {
            UpdateDragState();

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

        if (stone.SnapTiming == OmokStoneSnapTiming.OnPlacement)
        {
            Vector2Int reservedCoordinate = stone.TargetCoordinate;

            if (!reservedCoordinates.Remove(reservedCoordinate))
            {
                if (occupiedCoordinates.Contains(reservedCoordinate))
                {
                    Destroy(stone.gameObject);
                }

                return false;
            }

            if (occupiedCoordinates.Contains(reservedCoordinate))
            {
                Destroy(stone.gameObject);
                return false;
            }

            occupiedCoordinates.Add(reservedCoordinate);
            stone.SnapTo(reservedCoordinate, stone.TargetWorldPosition);
            StonePlaced?.Invoke(reservedCoordinate, stone.StoneColor);
            return true;
        }

        if (!grid.TryGetCoordinate(stone.transform.position, out Vector2Int landedCoordinate))
        {
            Destroy(stone.gameObject);
            return false;
        }

        if (IsCoordinateBlocked(landedCoordinate))
        {
            Destroy(stone.gameObject);
            return false;
        }

        occupiedCoordinates.Add(landedCoordinate);

        Vector3 snappedPosition = grid.GetWorldPosition(landedCoordinate) +
                                  (grid.transform.up * (stone.GetSnapOffsetAlongNormal(grid.transform.up) + settleOffset));

        stone.SnapTo(landedCoordinate, snappedPosition);
        StonePlaced?.Invoke(landedCoordinate, stone.StoneColor);
        return true;
    }

    internal void ReleaseReservation(Vector2Int coordinate)
    {
        reservedCoordinates.Remove(coordinate);
    }

    public void SetManualPlacementState(bool allowBlack, bool allowWhite)
    {
        allowBlackManualPlacement = allowBlack;
        allowWhiteManualPlacement = allowWhite;
    }

    public bool TryRequestPlacement(OmokStoneColor stoneColor, Vector2Int targetCoordinate)
    {
        if (!TryBuildPlacementRequest(stoneColor, targetCoordinate, out OmokStonePlacementRequest request))
        {
            return false;
        }

        if (PlacementRequested == null)
        {
            return false;
        }

        PlacementRequested.Invoke(request);
        return true;
    }

    public bool TryPlaceStone(OmokStoneColor stoneColor, Vector2Int targetCoordinate)
    {
        return TryRequestPlacement(stoneColor, targetCoordinate);
    }

    private void TryBeginDrag()
    {
        if (!TryGetLauncherAtMouse(out OmokStoneLauncher launcher))
        {
            return;
        }

        if (!TryCreateDraggedStone(launcher))
        {
            return;
        }

        activeLauncher = launcher;
        isDraggingLauncher = true;
        hasDragBoardTarget = false;
        UpdateDragState();
    }

    private void UpdateDragState()
    {
        hasDragBoardTarget = false;

        if (!TryGetBoardHit(out RaycastHit boardHit))
        {
            UpdateDraggedStoneOffBoard();
            HidePreview();
            return;
        }

        if (!TryGetCoordinate(boardHit.point, out Vector2Int previewCoordinate))
        {
            UpdateDraggedStoneOnBoard(boardHit.point);
            HidePreview();
            return;
        }

        currentDragCoordinate = previewCoordinate;
        hasDragBoardTarget = true;

        if (snapTiming == OmokStoneSnapTiming.OnPlacement)
        {
            UpdateDraggedStoneOnBoard(previewCoordinate);
        }
        else
        {
            UpdateDraggedStoneOnBoard(boardHit.point);
        }

        bool isBlocked = IsCoordinateBlocked(previewCoordinate);
        UpdateDraggedStonePreviewVisual(isBlocked);
        DrawPreview(previewCoordinate, isBlocked);
    }

    private void TryReleaseDrag()
    {
        OmokStoneLauncher releasedLauncher = activeLauncher;
        bool canPlace = hasDragBoardTarget;
        Vector2Int targetCoordinate = currentDragCoordinate;
        GameObject releasedPreviewObject = draggedStoneObject;
        Vector3 releasePosition = releasedPreviewObject != null ? releasedPreviewObject.transform.position : default;
        bool isBlocked = canPlace && releasedPreviewObject != null && IsCoordinateBlocked(targetCoordinate);

        ClearDragState();
        DestroyDraggedStonePreview(releasedPreviewObject);

        if (!canPlace || releasedPreviewObject == null || isBlocked)
        {
            return;
        }

        if (!TryBuildPlacementRequest(releasedLauncher, releasePosition, targetCoordinate, out OmokStonePlacementRequest request))
        {
            return;
        }

        PlacementRequested?.Invoke(request);
    }

    private void ClearDragState()
    {
        activeLauncher = null;
        isDraggingLauncher = false;
        hasDragBoardTarget = false;
        draggedStoneObject = null;
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

        Vector3 spawnPosition = snapTiming == OmokStoneSnapTiming.OnPlacement
            ? grid.GetWorldPosition(request.TargetCoordinate) + (grid.transform.up * dragHoverHeight)
            : request.ReleasePosition;
        GameObject stoneObject = Instantiate(launcher.stonePrefab, spawnPosition, launcher.stonePrefab.transform.rotation, stoneRoot);

        if (snapTiming == OmokStoneSnapTiming.OnPlacement)
        {
            stoneObject.transform.position = spawnPosition;
        }

        ActivateStoneColliders(stoneObject);

        Rigidbody rigidbody = stoneObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = stoneObject.AddComponent<Rigidbody>();
        }

        ConfigureRigidbody(rigidbody);
        ApplyReleaseMotion(stoneObject.transform, rigidbody);

        OmokFallingStone fallingStone = stoneObject.GetComponent<OmokFallingStone>();
        if (fallingStone == null)
        {
            fallingStone = stoneObject.AddComponent<OmokFallingStone>();
        }

        bool reserveTarget = snapTiming == OmokStoneSnapTiming.OnPlacement;
        if (reserveTarget)
        {
            reservedCoordinates.Add(request.TargetCoordinate);
        }

        Vector3 targetWorldPosition = grid.GetWorldPosition(request.TargetCoordinate);
        Vector3 snappedPosition = targetWorldPosition +
                                  (grid.transform.up * (fallingStone.GetSnapOffsetAlongNormal(grid.transform.up) + settleOffset));

        fallingStone.Initialize(this,
                                grid,
                                rigidbody,
                                request.StoneColor,
                                snapTiming,
                                reserveTarget,
                                request.TargetCoordinate,
                                snappedPosition,
                                fallGravityScale);
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

        rigidbody.linearVelocity = -up * initialFallSpeed;
    }

    private bool TryGetLauncherAtMouse(out OmokStoneLauncher launcher)
    {
        launcher = null;

        if (launcherLayerMask == 0)
        {
            launcherLayerMask = BuildLauncherLayerMask();
            if (launcherLayerMask == 0)
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
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, launcherLayerMask, QueryTriggerInteraction.Ignore);
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

        if (allowBlackManualPlacement && IsLauncherHit(collider, blackLauncher))
        {
            launcher = blackLauncher;
            return true;
        }

        if (allowWhiteManualPlacement && IsLauncherHit(collider, whiteLauncher))
        {
            launcher = whiteLauncher;
            return true;
        }

        return false;
    }

    private bool IsLauncherHit(Collider collider, OmokStoneLauncher launcher)
    {
        if (collider == null || !IsLauncherConfigured(launcher))
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == launcher.source || hitTransform.IsChildOf(launcher.source);
    }

    private bool IsLauncherConfigured(OmokStoneLauncher launcher)
    {
        return launcher != null && launcher.source != null && launcher.stonePrefab != null;
    }

    private OmokStoneColor GetLauncherStoneColor(OmokStoneLauncher launcher)
    {
        if (launcher == blackLauncher)
        {
            return OmokStoneColor.Black;
        }

        if (launcher == whiteLauncher)
        {
            return OmokStoneColor.White;
        }

        return OmokStoneColor.None;
    }

    private bool TryGetLauncherForColor(OmokStoneColor stoneColor, out OmokStoneLauncher launcher)
    {
        launcher = stoneColor switch
        {
            OmokStoneColor.Black => blackLauncher,
            OmokStoneColor.White => whiteLauncher,
            _ => null
        };

        return IsLauncherConfigured(launcher);
    }

    private bool TryGetBoardHit(out RaycastHit boardHit)
    {
        boardHit = default;

        if (boardLayerMask == 0)
        {
            return false;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out boardHit, raycastDistance, boardLayerMask, QueryTriggerInteraction.Ignore);
    }

    private bool IsBoardHit(Collider collider)
    {
        if (collider == null || grid == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return IsLayerInHierarchy(hitTransform, boardLayer) ||
               hitTransform == grid.transform ||
               hitTransform.IsChildOf(grid.transform);
    }

    internal bool IsBlockerHit(Collider collider)
    {
        return collider != null && IsLayerInHierarchy(collider.transform, blockerLayer);
    }

    internal bool TryStickStoneToBlocker(OmokFallingStone stone, Collider blockerCollider)
    {
        if (stone == null || blockerCollider == null)
        {
            return false;
        }

        Transform blockerTarget = GetBlockerAttachmentTarget(blockerCollider);
        ReleaseReservation(stone.TargetCoordinate);

        if (blockerAttachmentMode == OmokBlockerAttachmentMode.ObjectCenterXZ)
        {
            Vector3 centerPosition = blockerTarget != null ? blockerTarget.position : blockerCollider.bounds.center;
            Vector3 stickPosition = stone.transform.position;
            stickPosition.x = centerPosition.x;
            stickPosition.z = centerPosition.z;

            Transform stackKey = blockerTarget != null ? blockerTarget : blockerCollider.transform;
            float halfHeight = stone.GetSnapOffsetAlongNormal(Vector3.up);
            if (stackKey != null && blockerCenterStackTopY.TryGetValue(stackKey, out float stackTopY))
            {
                stickPosition.y = Mathf.Max(stickPosition.y, stackTopY + halfHeight + blockerCenterStackGap);
            }

            if (stackKey != null)
            {
                blockerCenterStackTopY[stackKey] = stickPosition.y + halfHeight;
            }

            stone.StickToBlocker(blockerTarget, blockerLayer, stickPosition);
        }
        else
        {
            stone.StickToBlocker(blockerTarget, blockerLayer);
        }

        StoneBlocked?.Invoke(stone.StoneColor);
        return true;
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

    private bool IsCoordinateBlocked(Vector2Int coordinate)
    {
        return occupiedCoordinates.Contains(coordinate) || reservedCoordinates.Contains(coordinate);
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

        request = new OmokStonePlacementRequest(stoneColor, targetCoordinate, releasePosition);
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

        Vector3 releasePosition = grid.GetWorldPosition(targetCoordinate) + (grid.transform.up * dragHoverHeight);
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

    private void DrawPreview(Vector2Int coordinate, bool isBlocked)
    {
        if (!showPreview)
        {
            HidePreview();
            return;
        }

        EnsurePreviewRenderer();
        if (previewRenderer == null)
        {
            return;
        }

        previewRenderer.widthMultiplier = previewLineWidth;

        Color color = isBlocked ? blockedPreviewColor : validPreviewColor;
        previewRenderer.startColor = color;
        previewRenderer.endColor = color;

        Vector3[] corners = GetPreviewCorners(coordinate);
        previewRenderer.positionCount = corners.Length;
        previewRenderer.SetPositions(corners);
        previewRenderer.enabled = true;

        DrawPreviewRay(coordinate, color);
    }

    private void DrawPreviewRay(Vector2Int coordinate, Color color)
    {
        if (previewRayRenderer == null || draggedStoneObject == null || grid == null || !grid.IsReady)
        {
            return;
        }

        Vector3 targetPosition = grid.GetWorldPosition(coordinate) + (grid.transform.up * previewTargetHeightOffset);

        previewRayRenderer.widthMultiplier = previewLineWidth;
        previewRayRenderer.startColor = color;
        previewRayRenderer.endColor = color;
        previewRayRenderer.positionCount = 2;
        previewRayRenderer.SetPosition(0, draggedStoneObject.transform.position);
        previewRayRenderer.SetPosition(1, targetPosition);
        previewRayRenderer.enabled = true;
    }

    private bool TryCreateDraggedStone(OmokStoneLauncher launcher)
    {
        if (!IsLauncherConfigured(launcher))
        {
            return false;
        }

        draggedStoneObject = Instantiate(launcher.stonePrefab, launcher.source.position, launcher.stonePrefab.transform.rotation, stoneRoot);
        Collider[] dragColliders = draggedStoneObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in dragColliders)
        {
            collider.enabled = false;
        }

        Rigidbody existingRigidbody = draggedStoneObject.GetComponent<Rigidbody>();
        if (existingRigidbody != null)
        {
            Destroy(existingRigidbody);
        }

        OmokFallingStone existingFallingStone = draggedStoneObject.GetComponent<OmokFallingStone>();
        if (existingFallingStone != null)
        {
            Destroy(existingFallingStone);
        }

        ApplyDraggedStonePreviewAppearance(launcher, draggedStoneObject);
        return true;
    }

    private void UpdateDraggedStoneOnBoard(Vector3 boardHitPoint)
    {
        if (draggedStoneObject == null || grid == null)
        {
            return;
        }

        draggedStoneObject.transform.position = boardHitPoint + (grid.transform.up * dragHoverHeight);
    }

    private void UpdateDraggedStoneOnBoard(Vector2Int coordinate)
    {
        if (draggedStoneObject == null || grid == null || !grid.IsReady)
        {
            return;
        }

        draggedStoneObject.transform.position = grid.GetWorldPosition(coordinate) + (grid.transform.up * dragHoverHeight);
    }

    private void UpdateDraggedStoneOffBoard()
    {
        if (draggedStoneObject == null)
        {
            return;
        }

        if (!TryGetDragPlanePoint(out Vector3 dragPosition))
        {
            return;
        }

        draggedStoneObject.transform.position = dragPosition;
    }

    private bool TryGetDragPlanePoint(out Vector3 dragPosition)
    {
        dragPosition = default;

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector3 planeNormal = grid != null ? grid.transform.up : Vector3.up;
        Vector3 planePoint = (grid != null ? grid.transform.position : transform.position) + (planeNormal * dragHoverHeight);
        Plane dragPlane = new Plane(planeNormal, planePoint);
        Ray mouseRay = cameraToUse.ScreenPointToRay(Input.mousePosition);

        if (!dragPlane.Raycast(mouseRay, out float hitDistance))
        {
            return false;
        }

        dragPosition = mouseRay.GetPoint(hitDistance);
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
        Vector3 offset = grid.transform.up * previewTargetHeightOffset;

        Vector3 bottomLeft = center - horizontalHalfStep - verticalHalfStep + offset;
        Vector3 topLeft = center - horizontalHalfStep + verticalHalfStep + offset;
        Vector3 topRight = center + horizontalHalfStep + verticalHalfStep + offset;
        Vector3 bottomRight = center + horizontalHalfStep - verticalHalfStep + offset;

        return new[]
        {
            bottomLeft,
            topLeft,
            topRight,
            bottomRight,
            bottomLeft
        };
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
        if (previewRenderer != null && previewRayRenderer != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (previewMaterial == null && shader != null)
        {
            previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (previewRenderer == null)
        {
            previewRenderer = CreatePreviewLineRenderer("StonePlacementPreviewOutline");
            previewRenderer.loop = false;
            previewRenderer.numCornerVertices = 2;
        }

        if (previewRayRenderer == null)
        {
            previewRayRenderer = CreatePreviewLineRenderer("StonePlacementPreviewRay");
            previewRayRenderer.loop = false;
            previewRayRenderer.numCornerVertices = 0;
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
        renderer.widthMultiplier = previewLineWidth;
        renderer.enabled = false;

        if (previewMaterial != null)
        {
            renderer.material = previewMaterial;
        }

        return renderer;
    }

    private void HidePreview()
    {
        if (previewRenderer != null)
        {
            previewRenderer.enabled = false;
        }

        if (previewRayRenderer != null)
        {
            previewRayRenderer.enabled = false;
        }
    }

    private void CacheExistingStoneCoordinates()
    {
        occupiedCoordinates.Clear();
        reservedCoordinates.Clear();

        if (grid == null || !grid.IsReady)
        {
            return;
        }

        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider collider in colliders)
        {
            if (collider == null || IsBoardHit(collider) || IsBlockerHit(collider) || collider.isTrigger)
            {
                continue;
            }

            if (IsLauncherHit(collider, blackLauncher) || IsLauncherHit(collider, whiteLauncher))
            {
                continue;
            }

            if (!grid.TryGetCoordinate(collider.bounds.center, out Vector2Int coordinate))
            {
                continue;
            }

            occupiedCoordinates.Add(coordinate);
        }
    }

    private void OnDisable()
    {
        DestroyDraggedStonePreview(draggedStoneObject);

        ClearDragState();
    }

    private void OnDestroy()
    {
        if (previewRenderer != null)
        {
            Destroy(previewRenderer.gameObject);
        }

        if (previewRayRenderer != null)
        {
            Destroy(previewRayRenderer.gameObject);
        }

        if (previewMaterial != null)
        {
            Destroy(previewMaterial);
        }

        DestroyDraggedStonePreview(draggedStoneObject);
    }

    private void ResolveNamedLayers()
    {
        boardLayer = LayerMask.NameToLayer(BoardLayerName);
        blockerLayer = LayerMask.NameToLayer(BlockerLayerName);
        boardLayerMask = boardLayer >= 0 ? 1 << boardLayer : 0;
        launcherLayerMask = BuildLauncherLayerMask();
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

    private int BuildLauncherLayerMask()
    {
        int mask = 0;
        AddLauncherLayers(blackLauncher, ref mask);
        AddLauncherLayers(whiteLauncher, ref mask);
        return mask;
    }

    private static void AddLauncherLayers(OmokStoneLauncher launcher, ref int mask)
    {
        if (launcher == null || launcher.source == null)
        {
            return;
        }

        Transform[] hierarchy = launcher.source.GetComponentsInChildren<Transform>(true);
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

        draggedStonePreviewMaterials.Clear();
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
                ConfigurePreviewMaterial(previewMaterialInstance, GetDraggedStonePreviewColor(launcher, false));
                previewMaterials[i] = previewMaterialInstance;
                draggedStonePreviewMaterials.Add(previewMaterialInstance);
            }

            renderer.sharedMaterials = previewMaterials;
        }
    }

    private void UpdateDraggedStonePreviewVisual(bool isBlocked)
    {
        if (draggedStoneObject == null || activeLauncher == null)
        {
            return;
        }

        Color previewColor = GetDraggedStonePreviewColor(activeLauncher, isBlocked);
        foreach (Material material in draggedStonePreviewMaterials)
        {
            if (material == null)
            {
                continue;
            }

            ApplyPreviewMaterialColor(material, previewColor);
        }
    }

    private Color GetDraggedStonePreviewColor(OmokStoneLauncher launcher, bool isBlocked)
    {
        OmokStoneColor stoneColor = GetLauncherStoneColor(launcher);
        Color baseColor = stoneColor == OmokStoneColor.Black
            ? new Color(0.18f, 0.18f, 0.18f, previewStoneAlpha)
            : new Color(1f, 1f, 1f, previewStoneAlpha);

        if (!isBlocked)
        {
            return baseColor;
        }

        Color tinted = Color.Lerp(baseColor, blockedPreviewColor, blockedPreviewTintStrength);
        tinted.a = Mathf.Max(baseColor.a, previewStoneAlpha);
        return tinted;
    }

    private void ConfigurePreviewMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(SurfacePropertyId))
        {
            material.SetFloat(SurfacePropertyId, 1f);
        }

        if (material.HasProperty(BlendPropertyId))
        {
            material.SetFloat(BlendPropertyId, 0f);
        }

        if (material.HasProperty(SrcBlendPropertyId))
        {
            material.SetFloat(SrcBlendPropertyId, (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty(DstBlendPropertyId))
        {
            material.SetFloat(DstBlendPropertyId, (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ZWritePropertyId))
        {
            material.SetFloat(ZWritePropertyId, 0f);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        ApplyPreviewMaterialColor(material, color);
    }

    private static void ApplyPreviewMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorPropertyId))
        {
            material.SetColor(BaseColorPropertyId, color);
        }

        if (material.HasProperty(ColorPropertyId))
        {
            material.SetColor(ColorPropertyId, color);
        }
    }

    private void DestroyDraggedStonePreview(GameObject previewObject)
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        foreach (Material material in draggedStonePreviewMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        draggedStonePreviewMaterials.Clear();
    }

    private static Transform GetBlockerAttachmentTarget(Collider blockerCollider)
    {
        if (blockerCollider == null)
        {
            return null;
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

}
