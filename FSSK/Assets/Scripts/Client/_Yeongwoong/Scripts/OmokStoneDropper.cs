using System;
using System.Collections.Generic;
using UnityEngine;

public enum OmokStoneSnapTiming
{
    OnPlacement,
    OnLanding
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

public class OmokStoneDropper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform stoneRoot;

    [Header("Launchers")]
    [SerializeField] private OmokStoneLauncher blackLauncher = new();
    [SerializeField] private OmokStoneLauncher whiteLauncher = new();

    [Header("Drag")]
    [SerializeField, Min(0f)] private float dragHoverHeight = 1.25f;

    [Header("Spawn")]
    [SerializeField] private OmokStoneSnapTiming snapTiming = OmokStoneSnapTiming.OnPlacement;
    [SerializeField, Min(1f)] private float raycastDistance = 500f;
    [SerializeField] private LayerMask raycastLayers = ~0;

    [Header("Landing")]
    [SerializeField, Min(0f)] private float settleOffset = 0.01f;

    [Header("Drop Feel")]
    [SerializeField, Min(0f)] private float initialFallSpeed = 2.5f;
    [SerializeField, Min(0f)] private float fallGravityScale = 1f;

    [Header("Preview")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private Color validPreviewColor = new(0.25f, 1f, 0.45f, 0.95f);
    [SerializeField] private Color blockedPreviewColor = new(1f, 0.3f, 0.3f, 0.95f);
    [SerializeField, Min(0.005f)] private float previewLineWidth = 0.08f;
    [SerializeField, Min(0f)] private float previewTargetHeightOffset = 0.05f;

    private readonly HashSet<Vector2Int> occupiedCoordinates = new();
    private readonly HashSet<Vector2Int> reservedCoordinates = new();
    private LineRenderer previewRenderer;
    private LineRenderer previewRayRenderer;
    private Material previewMaterial;

    private OmokStoneLauncher activeLauncher;
    private bool isDraggingLauncher;
    private bool hasDragBoardTarget;
    private RaycastHit currentDragBoardHit;
    private Vector2Int currentDragCoordinate;
    private GameObject draggedStoneObject;

    public OmokStoneSnapTiming SnapTiming => snapTiming;
    public event Action<Vector2Int, OmokStoneColor> StonePlaced;

    private void Reset()
    {
        grid = GetComponent<OmokGrid>();
    }

    private void Awake()
    {
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

        currentDragBoardHit = boardHit;
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

        DrawPreview(previewCoordinate, IsCoordinateBlocked(previewCoordinate));
    }

    private void TryReleaseDrag()
    {
        OmokStoneLauncher releasedLauncher = activeLauncher;
        bool canPlace = hasDragBoardTarget;
        RaycastHit boardHit = currentDragBoardHit;
        Vector2Int targetCoordinate = currentDragCoordinate;
        GameObject releasedStoneObject = draggedStoneObject;

        ClearDragState();

        if (!canPlace || releasedStoneObject == null)
        {
            if (releasedStoneObject != null)
            {
                Destroy(releasedStoneObject);
            }

            return;
        }

        TryReleaseStone(releasedLauncher, releasedStoneObject, boardHit, targetCoordinate);
    }

    private void ClearDragState()
    {
        activeLauncher = null;
        isDraggingLauncher = false;
        hasDragBoardTarget = false;
        draggedStoneObject = null;
        HidePreview();
    }

    private void TryReleaseStone(OmokStoneLauncher launcher, GameObject stoneObject, RaycastHit boardHit, Vector2Int targetCoordinate)
    {
        if (!IsLauncherConfigured(launcher) || stoneObject == null || grid == null || !grid.IsReady)
        {
            return;
        }

        OmokStoneColor stoneColor = GetLauncherStoneColor(launcher);
        if (stoneColor == OmokStoneColor.None)
        {
            Destroy(stoneObject);
            return;
        }

        if (snapTiming == OmokStoneSnapTiming.OnPlacement && IsCoordinateBlocked(targetCoordinate))
        {
            Destroy(stoneObject);
            return;
        }

        if (snapTiming == OmokStoneSnapTiming.OnPlacement)
        {
            stoneObject.transform.position = grid.GetWorldPosition(targetCoordinate) + (grid.transform.up * dragHoverHeight);
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
            reservedCoordinates.Add(targetCoordinate);
        }

        Vector3 targetWorldPosition = grid.GetWorldPosition(targetCoordinate);
        Vector3 snappedPosition = targetWorldPosition +
                                  (grid.transform.up * (fallingStone.GetSnapOffsetAlongNormal(grid.transform.up) + settleOffset));

        fallingStone.Initialize(this, grid, rigidbody, stoneColor, snapTiming, reserveTarget, targetCoordinate, snappedPosition, fallGravityScale);
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

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, raycastLayers, QueryTriggerInteraction.Ignore);
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

        if (IsLauncherHit(collider, blackLauncher))
        {
            launcher = blackLauncher;
            return true;
        }

        if (IsLauncherHit(collider, whiteLauncher))
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

    private bool TryGetBoardHit(out RaycastHit boardHit)
    {
        boardHit = default;

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, raycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsBoardHit(hit.collider))
            {
                boardHit = hit;
                return true;
            }
        }

        return false;
    }

    private bool IsBoardHit(Collider collider)
    {
        if (collider == null || grid == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == grid.transform || hitTransform.IsChildOf(grid.transform);
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
            if (collider == null || IsBoardHit(collider) || collider.isTrigger)
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
        if (draggedStoneObject != null)
        {
            Destroy(draggedStoneObject);
        }

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
    }
}
