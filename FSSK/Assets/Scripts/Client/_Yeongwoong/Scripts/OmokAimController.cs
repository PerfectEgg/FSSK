using UnityEngine;

public readonly struct OmokAimState
{
    public OmokAimState(Vector3 pointerWorldPosition, Vector3 aimWorldPosition, bool hasCoordinate, Vector2Int coordinate)
    {
        PointerWorldPosition = pointerWorldPosition;
        AimWorldPosition = aimWorldPosition;
        HasCoordinate = hasCoordinate;
        Coordinate = coordinate;
    }

    public Vector3 PointerWorldPosition { get; }
    public Vector3 AimWorldPosition { get; }
    public bool HasCoordinate { get; }
    public Vector2Int Coordinate { get; }
}

public class OmokAimController : MonoBehaviour
{
    private const string BOARD_LAYER_NAME = "Board";

    [Header("References")]
    [SerializeField] private OmokGrid grid;
    [SerializeField] private Camera targetCamera;

    [Header("Cursor")]
    [SerializeField] private bool hideSystemCursorWhileAiming;
    [SerializeField] private bool unlockSystemCursorWhileAiming = true;

    [Header("Preview Aim")]
    [SerializeField] private bool showOutOfBoardAimPreview = true;
    [SerializeField] private bool keepAimPositionAfterDrop = true;

    [Header("Wind Aim")]
    [SerializeField] private bool useWindAim;
    [SerializeField] private Vector2 windAimDirection = Vector2.right;
    [SerializeField, Min(0f)] private float windAimDriftCellsPerSecond = 0.35f;
    [SerializeField, Min(0f)] private float windAimBaseSensitivity = 1f;
    [SerializeField, Min(0f)] private float windAimSameDirectionSensitivity = 1.35f;
    [SerializeField, Min(0f)] private float windAimOppositeDirectionSensitivity = 0.6f;
    [SerializeField, Min(0f)] private float windAimMaxDistanceFromMouseCells = 3f;
    [SerializeField, Min(0f)] private float windAimInputDeadZoneCells = 0.02f;

    private bool _hasWindAimPosition;
    private Vector3 _windAimPosition;
    private Vector3 _previousPointerDragPosition;
    private bool _shouldReanchorPointerOnNextAim;
    private bool _hasCursorOverride;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLockState;
    private int _boardLayer = -1;
    private int _boardLayerMask;

    public bool UseWindAim => useWindAim;
    public Vector2 WindAimDirection => windAimDirection;
    public bool HideSystemCursorWhileAiming => hideSystemCursorWhileAiming;
    public bool UnlockSystemCursorWhileAiming => unlockSystemCursorWhileAiming;
    public bool KeepAimPositionAfterDrop => keepAimPositionAfterDrop;

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

        ResolveNamedLayers();
    }

    private void OnValidate()
    {
        ResolveNamedLayers();
    }

    public void SetReferences(OmokGrid omokGrid, Camera cameraToUse)
    {
        if (grid == null)
        {
            grid = omokGrid;
        }

        if (targetCamera == null)
        {
            targetCamera = cameraToUse;
        }

        ResolveNamedLayers();
    }

    public bool TryGetBoardAim(float hoverHeight, float raycastDistance, out OmokAimState aimState)
    {
        aimState = default;

        Vector3 pointerPosition;
        if (TryGetBoardHit(raycastDistance, out RaycastHit boardHit))
        {
            pointerPosition = GetPointerDragPosition(boardHit.point, hoverHeight);
        }
        else if (!TryGetDragPlanePoint(hoverHeight, out pointerPosition))
        {
            return false;
        }

        Vector3 aimPosition = GetAimDragPosition(pointerPosition);
        bool hasCoordinate = TryGetAimCoordinate(aimPosition, out Vector2Int coordinate);
        aimState = new OmokAimState(pointerPosition, aimPosition, hasCoordinate, coordinate);
        return true;
    }

    public bool TryGetFreeAim(float hoverHeight, out Vector3 aimPosition)
    {
        aimPosition = default;

        if (!TryGetDragPlanePoint(hoverHeight, out Vector3 pointerPosition))
        {
            return false;
        }

        aimPosition = GetAimDragPosition(pointerPosition);
        return true;
    }

    public void ResetAimState()
    {
        _hasWindAimPosition = false;
        _windAimPosition = default;
        _previousPointerDragPosition = default;
        _shouldReanchorPointerOnNextAim = false;
    }

    public void BeginAimSession()
    {
        if (useWindAim && keepAimPositionAfterDrop && _hasWindAimPosition)
        {
            _shouldReanchorPointerOnNextAim = true;
        }
        else
        {
            ResetAimState();
        }

        if ((!hideSystemCursorWhileAiming && !unlockSystemCursorWhileAiming) || _hasCursorOverride)
        {
            return;
        }

        _previousCursorVisible = Cursor.visible;
        _previousCursorLockState = Cursor.lockState;
        _hasCursorOverride = true;

        if (unlockSystemCursorWhileAiming)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (hideSystemCursorWhileAiming)
        {
            Cursor.visible = false;
        }
    }

    public void EndAimSession()
    {
        if (!keepAimPositionAfterDrop)
        {
            ResetAimState();
        }

        if (!_hasCursorOverride)
        {
            return;
        }

        Cursor.lockState = _previousCursorLockState;
        Cursor.visible = _previousCursorVisible;
        _hasCursorOverride = false;
    }

    public void StoreAimPosition(Vector3 aimWorldPosition)
    {
        if (!useWindAim || !keepAimPositionAfterDrop)
        {
            return;
        }

        _windAimPosition = aimWorldPosition;
        _hasWindAimPosition = true;
        _shouldReanchorPointerOnNextAim = true;
    }

    public void SetHideSystemCursorWhileAiming(bool shouldHide)
    {
        hideSystemCursorWhileAiming = shouldHide;

        if (!shouldHide)
        {
            EndAimSession();
        }
    }

    public void SetUnlockSystemCursorWhileAiming(bool shouldUnlock)
    {
        unlockSystemCursorWhileAiming = shouldUnlock;

        if (!shouldUnlock)
        {
            EndAimSession();
        }
    }

    public void SetWindAimEnabled(bool isEnabled)
    {
        useWindAim = isEnabled;
        ResetAimState();
    }

    public void SetWindAimDirection(Vector2 direction)
    {
        windAimDirection = direction;
        ResetAimState();
    }

    public void SetWindAimDirection(float directionX, float directionY)
    {
        SetWindAimDirection(new Vector2(directionX, directionY));
    }

    public void ConfigureWindAim(Vector2 direction, float driftCellsPerSecond)
    {
        windAimDirection = direction;
        windAimDriftCellsPerSecond = Mathf.Max(0f, driftCellsPerSecond);
        useWindAim = direction.sqrMagnitude > 0.0001f;
        ResetAimState();
    }

    public void ConfigureWindAim(float directionX, float directionY, float driftCellsPerSecond)
    {
        ConfigureWindAim(new Vector2(directionX, directionY), driftCellsPerSecond);
    }

    public void ClearWindAim()
    {
        useWindAim = false;
        ResetAimState();
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

    private Vector3 GetAimDragPosition(Vector3 pointerDragPosition)
    {
        if (!useWindAim)
        {
            ResetAimState();
            return pointerDragPosition;
        }

        if (!TryGetWindAimWorldDirection(out Vector3 windDirection, out float cellSize))
        {
            ResetAimState();
            return pointerDragPosition;
        }

        if (!_hasWindAimPosition)
        {
            _windAimPosition = pointerDragPosition;
            _previousPointerDragPosition = pointerDragPosition;
            _hasWindAimPosition = true;
            return _windAimPosition;
        }

        if (_shouldReanchorPointerOnNextAim)
        {
            _previousPointerDragPosition = pointerDragPosition;
            _shouldReanchorPointerOnNextAim = false;
            return _windAimPosition;
        }

        Vector3 pointerDelta = pointerDragPosition - _previousPointerDragPosition;
        float deadZoneDistance = windAimInputDeadZoneCells * cellSize;
        float deadZoneSqrDistance = deadZoneDistance * deadZoneDistance;

        if (pointerDelta.sqrMagnitude > deadZoneSqrDistance)
        {
            float windDot = Vector3.Dot(pointerDelta.normalized, windDirection);
            float sensitivity = windAimBaseSensitivity;

            if (windDot > 0f)
            {
                sensitivity = Mathf.Lerp(windAimBaseSensitivity, windAimSameDirectionSensitivity, windDot);
            }
            else if (windDot < 0f)
            {
                sensitivity = Mathf.Lerp(windAimBaseSensitivity, windAimOppositeDirectionSensitivity, -windDot);
            }

            _windAimPosition += pointerDelta * Mathf.Max(0f, sensitivity);
        }

        _windAimPosition += windDirection * (windAimDriftCellsPerSecond * cellSize * Time.deltaTime);
        _windAimPosition = ClampWindAimDistance(pointerDragPosition, _windAimPosition, cellSize);
        _previousPointerDragPosition = pointerDragPosition;
        return _windAimPosition;
    }

    private Vector3 ClampWindAimDistance(Vector3 pointerDragPosition, Vector3 aimPosition, float cellSize)
    {
        if (windAimMaxDistanceFromMouseCells <= 0f)
        {
            return aimPosition;
        }

        float maxDistance = windAimMaxDistanceFromMouseCells * cellSize;
        Vector3 offsetFromPointer = aimPosition - pointerDragPosition;
        if (offsetFromPointer.sqrMagnitude <= maxDistance * maxDistance)
        {
            return aimPosition;
        }

        return pointerDragPosition + (offsetFromPointer.normalized * maxDistance);
    }

    private bool TryGetWindAimWorldDirection(out Vector3 worldDirection, out float cellSize)
    {
        worldDirection = default;
        cellSize = 1f;

        if (windAimDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        GetGridStepVectors(out Vector3 xStep, out Vector3 yStep);
        cellSize = Mathf.Max(0.001f, (xStep.magnitude + yStep.magnitude) * 0.5f);

        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        Vector3 direction = (xStep * windAimDirection.x) + (yStep * windAimDirection.y);
        direction = Vector3.ProjectOnPlane(direction, up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        worldDirection = direction.normalized;
        return true;
    }

    private bool TryGetBoardHit(float raycastDistance, out RaycastHit boardHit)
    {
        boardHit = default;

        if (_boardLayerMask == 0)
        {
            return false;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out boardHit, raycastDistance, _boardLayerMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetPointerDragPosition(Vector3 fallbackBoardHitPoint, float hoverHeight)
    {
        if (TryGetDragPlanePoint(hoverHeight, out Vector3 dragPosition))
        {
            return dragPosition;
        }

        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        return fallbackBoardHitPoint + (up * hoverHeight);
    }

    private bool TryGetDragPlanePoint(float hoverHeight, out Vector3 dragPosition)
    {
        dragPosition = default;

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector3 planeNormal = grid != null ? grid.transform.up : Vector3.up;
        Vector3 planePoint = (grid != null ? grid.transform.position : transform.position) + (planeNormal * hoverHeight);
        Plane dragPlane = new Plane(planeNormal, planePoint);
        Ray mouseRay = cameraToUse.ScreenPointToRay(Input.mousePosition);

        if (!dragPlane.Raycast(mouseRay, out float hitDistance))
        {
            return false;
        }

        dragPosition = mouseRay.GetPoint(hitDistance);
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

    private bool TryGetAimCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
    {
        if (TryGetCoordinate(worldPosition, out coordinate))
        {
            return true;
        }

        return showOutOfBoardAimPreview && TryGetUnclampedCoordinate(worldPosition, out coordinate);
    }

    private bool TryGetUnclampedCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
    {
        coordinate = default;

        if (grid == null || !grid.IsReady)
        {
            return false;
        }

        Vector3 origin = grid.GetWorldPosition(0, 0);
        GetGridStepVectors(out Vector3 xStep, out Vector3 yStep);
        if (xStep.sqrMagnitude <= 0.0001f || yStep.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 offset = worldPosition - origin;
        float x = Vector3.Dot(offset, xStep) / xStep.sqrMagnitude;
        float y = Vector3.Dot(offset, yStep) / yStep.sqrMagnitude;
        coordinate = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
        return true;
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

    private void ResolveNamedLayers()
    {
        _boardLayer = LayerMask.NameToLayer(BOARD_LAYER_NAME);
        _boardLayerMask = _boardLayer >= 0 ? 1 << _boardLayer : 0;
    }
}
