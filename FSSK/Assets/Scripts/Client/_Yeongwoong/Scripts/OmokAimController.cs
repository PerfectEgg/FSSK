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
    [SerializeField, Min(0f)] private float windAimInputDeadZoneCells = 0.02f;
    [SerializeField] private bool useRelativeMouseForWindAim = true;
    [SerializeField, Min(0.01f)] private float windAimRelativeMousePixelScale = 20f;
    [SerializeField] private bool clampWindAimToScreen = true;
    [SerializeField, Range(0f, 0.49f)] private float windAimScreenPadding = 0.03f;
    [SerializeField, Range(0.01f, 0.5f)] private float windAimScreenSoftZone = 0.12f;
    [SerializeField, Range(0f, 1f)] private float windAimScreenEdgeMinSpeed = 0.08f;

    private bool _hasWindAimPosition;
    private Vector3 _windAimPosition;
    private Vector3 _previousPointerDragPosition;
    private Vector3 _previousPointerScreenPosition;
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
        _previousPointerScreenPosition = default;
        _shouldReanchorPointerOnNextAim = false;
    }

    public void BeginAimSession()
    {
        BeginAimSession(default, false);
    }

    public void BeginAimSession(Vector3 initialAimPosition)
    {
        BeginAimSession(initialAimPosition, true);
    }

    private void BeginAimSession(Vector3 initialAimPosition, bool useInitialAimPosition)
    {
        if (useWindAim && keepAimPositionAfterDrop && _hasWindAimPosition)
        {
            _shouldReanchorPointerOnNextAim = true;
        }
        else
        {
            ResetAimState();
        }

        if (useWindAim && useInitialAimPosition)
        {
            _windAimPosition = initialAimPosition;
            _hasWindAimPosition = true;
            _shouldReanchorPointerOnNextAim = true;
        }

        bool lockRelativeCursor = ShouldUseLockedRelativeWindAim();
        if ((!hideSystemCursorWhileAiming && !unlockSystemCursorWhileAiming && !lockRelativeCursor) || _hasCursorOverride)
        {
            return;
        }

        _previousCursorVisible = Cursor.visible;
        _previousCursorLockState = Cursor.lockState;
        _hasCursorOverride = true;

        if (lockRelativeCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (unlockSystemCursorWhileAiming)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!lockRelativeCursor && hideSystemCursorWhileAiming)
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

    public void SetCurrentAimPosition(Vector3 aimWorldPosition)
    {
        if (!useWindAim || !_hasWindAimPosition)
        {
            return;
        }

        _windAimPosition = aimWorldPosition;
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

        Vector3 pointerScreenPosition = GetClampedPointerScreenPosition();

        if (!_hasWindAimPosition)
        {
            _windAimPosition = pointerDragPosition;
            _previousPointerDragPosition = pointerDragPosition;
            _previousPointerScreenPosition = pointerScreenPosition;
            _hasWindAimPosition = true;
            return _windAimPosition;
        }

        if (_shouldReanchorPointerOnNextAim)
        {
            _previousPointerDragPosition = pointerDragPosition;
            _previousPointerScreenPosition = pointerScreenPosition;
            _shouldReanchorPointerOnNextAim = false;
            return _windAimPosition;
        }

        bool useRelativeMouse = ShouldUseLockedRelativeWindAim();
        Vector3 pointerDelta = useRelativeMouse
            ? GetRelativeMouseAimDelta(_windAimPosition)
            : TryGetScreenSpaceAimDelta(pointerScreenPosition, _previousPointerScreenPosition, _windAimPosition, out Vector3 screenSpaceDelta)
                ? screenSpaceDelta
                : pointerDragPosition - _previousPointerDragPosition;
        float deadZoneDistance = windAimInputDeadZoneCells * cellSize;
        float deadZoneSqrDistance = deadZoneDistance * deadZoneDistance;

        Vector3 aimDelta = Vector3.zero;
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

            aimDelta += pointerDelta * Mathf.Max(0f, sensitivity);
        }

        aimDelta += windDirection * (windAimDriftCellsPerSecond * cellSize * Time.deltaTime);
        _windAimPosition = useRelativeMouse
            ? _windAimPosition + aimDelta
            : ClampWindAimToScreen(ApplyWindAimScreenEdgeDamping(_windAimPosition, aimDelta));
        _previousPointerDragPosition = pointerDragPosition;
        _previousPointerScreenPosition = pointerScreenPosition;
        return _windAimPosition;
    }

    private bool ShouldUseLockedRelativeWindAim()
    {
        return useWindAim && useRelativeMouseForWindAim;
    }

    private Vector3 GetRelativeMouseAimDelta(Vector3 aimPosition)
    {
        Vector3 screenDelta = new Vector3(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y"),
            0f) * Mathf.Max(0.01f, windAimRelativeMousePixelScale);

        return TryConvertScreenDeltaToWorldDelta(screenDelta, aimPosition, out Vector3 worldDelta)
            ? worldDelta
            : Vector3.zero;
    }

    private Vector3 GetClampedPointerScreenPosition()
    {
        Vector3 pointerPosition = Input.mousePosition;
        pointerPosition.x = Mathf.Clamp(pointerPosition.x, 0f, Mathf.Max(0f, Screen.width - 1f));
        pointerPosition.y = Mathf.Clamp(pointerPosition.y, 0f, Mathf.Max(0f, Screen.height - 1f));
        return pointerPosition;
    }

    private bool TryGetScreenSpaceAimDelta(Vector3 pointerScreenPosition, Vector3 previousPointerScreenPosition, Vector3 aimPosition, out Vector3 worldDelta)
    {
        worldDelta = default;

        Vector3 screenDelta = pointerScreenPosition - previousPointerScreenPosition;
        if (screenDelta.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        return TryConvertScreenDeltaToWorldDelta(screenDelta, aimPosition, out worldDelta);
    }

    private bool TryConvertScreenDeltaToWorldDelta(Vector3 screenDelta, Vector3 aimPosition, out Vector3 worldDelta)
    {
        worldDelta = default;

        if (screenDelta.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector3 aimScreenPosition = cameraToUse.WorldToScreenPoint(aimPosition);
        if (aimScreenPosition.z <= 0f)
        {
            return false;
        }

        Vector3 planeNormal = grid != null ? grid.transform.up : Vector3.up;
        Plane aimPlane = new Plane(planeNormal, aimPosition);
        Ray currentAimRay = cameraToUse.ScreenPointToRay(aimScreenPosition);
        Ray nextAimRay = cameraToUse.ScreenPointToRay(aimScreenPosition + screenDelta);
        if (!aimPlane.Raycast(currentAimRay, out float currentDistance) ||
            !aimPlane.Raycast(nextAimRay, out float nextDistance))
        {
            return false;
        }

        worldDelta = nextAimRay.GetPoint(nextDistance) - currentAimRay.GetPoint(currentDistance);
        return true;
    }

    private Vector3 ApplyWindAimScreenEdgeDamping(Vector3 aimPosition, Vector3 worldDelta)
    {
        if (!clampWindAimToScreen || worldDelta.sqrMagnitude <= 0.000001f)
        {
            return aimPosition + worldDelta;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return aimPosition + worldDelta;
        }

        Vector3 currentViewportPosition = cameraToUse.WorldToViewportPoint(aimPosition);
        Vector3 proposedViewportPosition = cameraToUse.WorldToViewportPoint(aimPosition + worldDelta);
        if (currentViewportPosition.z <= 0f || proposedViewportPosition.z <= 0f)
        {
            return aimPosition + worldDelta;
        }

        float padding = Mathf.Clamp(windAimScreenPadding, 0f, 0.49f);
        float softZone = Mathf.Clamp(windAimScreenSoftZone, 0.01f, 0.5f);
        float minSpeed = Mathf.Clamp01(windAimScreenEdgeMinSpeed);
        float min = padding;
        float max = 1f - padding;
        Vector3 adjustedViewportPosition = currentViewportPosition;
        adjustedViewportPosition.x += GetDampedViewportDelta(currentViewportPosition.x, proposedViewportPosition.x - currentViewportPosition.x, min, max, softZone, minSpeed);
        adjustedViewportPosition.y += GetDampedViewportDelta(currentViewportPosition.y, proposedViewportPosition.y - currentViewportPosition.y, min, max, softZone, minSpeed);
        adjustedViewportPosition.x = Mathf.Clamp(adjustedViewportPosition.x, min, max);
        adjustedViewportPosition.y = Mathf.Clamp(adjustedViewportPosition.y, min, max);

        Vector3 planeNormal = grid != null ? grid.transform.up : Vector3.up;
        Plane aimPlane = new Plane(planeNormal, aimPosition);
        Ray adjustedRay = cameraToUse.ViewportPointToRay(adjustedViewportPosition);
        return aimPlane.Raycast(adjustedRay, out float hitDistance)
            ? adjustedRay.GetPoint(hitDistance)
            : aimPosition + worldDelta;
    }

    private float GetDampedViewportDelta(float current, float delta, float min, float max, float softZone, float minSpeed)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return 0f;
        }

        float distanceToEdge = delta < 0f ? current - min : max - current;
        if (distanceToEdge >= softZone)
        {
            return delta;
        }

        float edgeRatio = Mathf.Clamp01(distanceToEdge / softZone);
        float speed = Mathf.SmoothStep(minSpeed, 1f, edgeRatio);
        return delta * speed;
    }

    private Vector3 ClampWindAimToScreen(Vector3 aimPosition)
    {
        if (!clampWindAimToScreen)
        {
            return aimPosition;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return aimPosition;
        }

        Vector3 viewportPosition = cameraToUse.WorldToViewportPoint(aimPosition);
        if (viewportPosition.z <= 0f)
        {
            return aimPosition;
        }

        float padding = Mathf.Clamp(windAimScreenPadding, 0f, 0.49f);
        float clampedX = Mathf.Clamp(viewportPosition.x, padding, 1f - padding);
        float clampedY = Mathf.Clamp(viewportPosition.y, padding, 1f - padding);
        if (Mathf.Approximately(viewportPosition.x, clampedX) &&
            Mathf.Approximately(viewportPosition.y, clampedY))
        {
            return aimPosition;
        }

        Vector3 up = grid != null ? grid.transform.up : Vector3.up;
        Plane aimPlane = new(up, aimPosition);
        Ray clampedRay = cameraToUse.ViewportPointToRay(new Vector3(clampedX, clampedY, viewportPosition.z));
        return aimPlane.Raycast(clampedRay, out float hitDistance)
            ? clampedRay.GetPoint(hitDistance)
            : aimPosition;
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
        Vector3 planePoint = GetGridPlaneBasePosition() + (planeNormal * hoverHeight);
        Plane dragPlane = new Plane(planeNormal, planePoint);
        Ray mouseRay = cameraToUse.ScreenPointToRay(Input.mousePosition);

        if (!dragPlane.Raycast(mouseRay, out float hitDistance))
        {
            return false;
        }

        dragPosition = mouseRay.GetPoint(hitDistance);
        return true;
    }

    private Vector3 GetGridPlaneBasePosition()
    {
        if (grid != null && grid.IsReady)
        {
            return grid.GetWorldPosition(0, 0);
        }

        return grid != null ? grid.transform.position : transform.position;
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
