using UnityEngine;

public class OmokFallingStone : MonoBehaviour
{
    [Header("Landing")]
    [SerializeField, Min(0f)] private float minLifetimeBeforeSnap = 0.05f;
    [SerializeField, Min(0f)] private float minBoardContactDurationBeforeSnap = 0.18f;
    [SerializeField, Min(0f)] private float landingHeightTolerance = 0.02f;
    [SerializeField, Min(0f)] private float fallbackSnapDelay = 5f;

    private OmokStoneDropper _owner;
    private OmokGrid _grid;
    private Rigidbody _cachedRigidbody;
    private Collider _cachedCollider;
    private bool _isInitialized;
    private bool _isSnapped;
    private bool _isBlockedByBlocker;
    private bool _isFailed;
    private bool _isRemoved;
    private float _spawnTime;
    private OmokStoneSnapTiming _snapTiming;
    private bool _hasReservedTarget;
    private bool _hasBoardContact;
    private float _boardContactTime;
    private Vector2Int _targetCoordinate;
    private Vector3 _targetWorldPosition;
    private Quaternion _targetWorldRotation = Quaternion.identity;
    private float _targetSnapOffsetAlongGridUp;
    private bool _hasTargetSnapOffset;
    private float _gravityScale = 1f;
    private bool _guideStraightToTarget;
    private bool _hasBlockerAnchor;
    private Vector3 _straightGuideStartPosition;
    private Vector3 _previousBlockerProbePosition;
    private Vector3 _blockerAnchorLocalPosition;
    private Quaternion _blockerAnchorLocalRotation;

    public Vector2Int Coordinate { get; private set; }
    public bool IsSnapped => _isSnapped;
    public bool IsBlockedByBlocker => _isBlockedByBlocker;
    public bool IsRemoved => _isRemoved;
    public Transform BlockerTarget { get; private set; }
    public OmokStoneColor StoneColor { get; private set; }
    public OmokStoneSnapTiming SnapTiming => _snapTiming;
    public bool HasReservedTarget => _hasReservedTarget;
    public Vector2Int TargetCoordinate => _targetCoordinate;
    public Vector3 TargetWorldPosition => _targetWorldPosition;

    private void Awake()
    {
        _cachedRigidbody = GetComponent<Rigidbody>();
        _cachedCollider = GetComponent<Collider>();
    }

    public void Initialize(
        OmokStoneDropper dropper,
        OmokGrid omokGrid,
        Rigidbody rigidbody,
        OmokStoneColor stoneColor,
        OmokStoneSnapTiming timing,
        bool reservedTarget,
        Vector2Int reservedCoordinate,
        Vector3 snappedWorldPosition,
        Quaternion snappedWorldRotation,
        float fallGravityScale,
        bool guideStraight = false)
    {
        _owner = dropper;
        _grid = omokGrid;
        _cachedRigidbody = rigidbody != null ? rigidbody : GetComponent<Rigidbody>();
        _cachedCollider = GetComponent<Collider>();
        _isInitialized = true;
        _isSnapped = false;
        _isBlockedByBlocker = false;
        _isFailed = false;
        _isRemoved = false;
        BlockerTarget = null;
        _hasBoardContact = false;
        _boardContactTime = 0f;
        _hasBlockerAnchor = false;
        _hasTargetSnapOffset = false;
        _spawnTime = Time.time;
        StoneColor = stoneColor;
        _snapTiming = timing;
        _hasReservedTarget = reservedTarget;
        _targetCoordinate = reservedCoordinate;
        _targetWorldPosition = snappedWorldPosition;
        _targetWorldRotation = snappedWorldRotation;
        if (_grid != null)
        {
            _targetSnapOffsetAlongGridUp = CalculateSnapOffsetAlongNormal(_grid.transform.up);
            _hasTargetSnapOffset = true;
        }

        _gravityScale = Mathf.Max(0f, fallGravityScale);
        _guideStraightToTarget = guideStraight;
        _straightGuideStartPosition = transform.position;
        _previousBlockerProbePosition = GetBlockerProbePosition();
    }

    private void Update()
    {
        if (!_isInitialized || _isSnapped || _isFailed || _isBlockedByBlocker)
        {
            return;
        }

        if (TryStickToBlockerAlongMovement())
        {
            return;
        }

        if (HasReachedLandingHeight())
        {
            TryResolveLanding();
            return;
        }

        if (_hasBoardContact &&
            fallbackSnapDelay > 0f &&
            Time.time - _boardContactTime >= minBoardContactDurationBeforeSnap &&
            Time.time - _spawnTime >= fallbackSnapDelay)
        {
            TryResolveLanding();
        }
    }

    private void LateUpdate()
    {
        if (!_isBlockedByBlocker || !_hasBlockerAnchor || BlockerTarget == null)
        {
            return;
        }

        transform.localPosition = _blockerAnchorLocalPosition;
        transform.localRotation = _blockerAnchorLocalRotation;
    }

    private void FixedUpdate()
    {
        if (!_isInitialized || _isSnapped || _isFailed || _isBlockedByBlocker || _cachedRigidbody == null || _cachedRigidbody.isKinematic)
        {
            return;
        }

        if (_guideStraightToTarget)
        {
            GuideStraightMotionToTarget();
        }

        if (Mathf.Approximately(_gravityScale, 1f))
        {
            return;
        }

        _cachedRigidbody.AddForce(Physics.gravity * (_gravityScale - 1f), ForceMode.Acceleration);
    }

    private void GuideStraightMotionToTarget()
    {
        if (_cachedRigidbody == null || _grid == null)
        {
            _guideStraightToTarget = false;
            return;
        }

        Vector3 up = _grid.transform.up;
        Vector3 velocity = _cachedRigidbody.linearVelocity;

        float startHeight = Vector3.Dot(_straightGuideStartPosition, up);
        float targetHeight = Vector3.Dot(_targetWorldPosition, up);
        float currentHeight = Vector3.Dot(transform.position, up);
        float totalFallDistance = startHeight - targetHeight;
        if (totalFallDistance <= 0.001f)
        {
            _guideStraightToTarget = false;
            return;
        }

        float fallProgress = Mathf.Clamp01((startHeight - currentHeight) / totalFallDistance);
        Vector3 targetLinePosition = Vector3.Lerp(_straightGuideStartPosition, _targetWorldPosition, fallProgress);
        Vector3 verticalOffset = Vector3.Project(transform.position - targetLinePosition, up);
        _cachedRigidbody.MovePosition(targetLinePosition + verticalOffset);
        _cachedRigidbody.linearVelocity = Vector3.Project(velocity, up);

        if (fallProgress >= 1f)
        {
            _guideStraightToTarget = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    public float GetSnapOffsetAlongNormal(Vector3 normal)
    {
        if (CanUseTargetSnapOffset(normal))
        {
            return _targetSnapOffsetAlongGridUp;
        }

        return CalculateSnapOffsetAlongNormal(normal);
    }

    private float CalculateSnapOffsetAlongNormal(Vector3 normal)
    {
        if (_cachedCollider == null)
        {
            return 0f;
        }

        Bounds bounds = _cachedCollider.bounds;
        Vector3 absoluteNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        return Vector3.Dot(bounds.extents, absoluteNormal);
    }

    private bool HasReachedLandingHeight()
    {
        if (_grid == null)
        {
            return true;
        }

        Vector3 up = _grid.transform.up;
        if (up.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        up.Normalize();
        Vector3 currentPosition = _cachedRigidbody != null ? _cachedRigidbody.position : transform.position;
        float currentHeight = Vector3.Dot(currentPosition, up);
        float targetHeight = Vector3.Dot(_targetWorldPosition, up);
        return currentHeight <= targetHeight + GetLandingHeightTolerance(up);
    }

    private float GetLandingHeightTolerance(Vector3 up)
    {
        float snapOffset = _hasTargetSnapOffset ? _targetSnapOffsetAlongGridUp : CalculateSnapOffsetAlongNormal(up);
        return Mathf.Max(landingHeightTolerance, snapOffset * 0.03f);
    }

    public float GetMaxExtentOnAxes(Vector3 firstAxis, Vector3 secondAxis)
    {
        if (_cachedCollider == null)
        {
            return 0f;
        }

        Bounds bounds = _cachedCollider.bounds;
        return Mathf.Max(
            ProjectBoundsExtent(bounds.extents, firstAxis),
            ProjectBoundsExtent(bounds.extents, secondAxis));
    }

    public void SnapTo(Vector2Int coordinate, Vector3 worldPosition)
    {
        _targetCoordinate = coordinate;
        _targetWorldPosition = worldPosition;
        Coordinate = coordinate;
        _isSnapped = true;
        transform.SetPositionAndRotation(worldPosition, _targetWorldRotation);

        if (_cachedRigidbody == null)
        {
            return;
        }

        _cachedRigidbody.linearVelocity = Vector3.zero;
        _cachedRigidbody.angularVelocity = Vector3.zero;
        _cachedRigidbody.useGravity = false;
        _cachedRigidbody.isKinematic = true;
        _cachedRigidbody.position = worldPosition;
        _cachedRigidbody.rotation = _targetWorldRotation;
    }

    public void MarkRemovedFromBoard()
    {
        _isRemoved = true;
        _isSnapped = false;
        _isBlockedByBlocker = false;
        _hasReservedTarget = false;

        if (_cachedRigidbody != null)
        {
            _cachedRigidbody.linearVelocity = Vector3.zero;
            _cachedRigidbody.angularVelocity = Vector3.zero;
            _cachedRigidbody.useGravity = false;
            _cachedRigidbody.isKinematic = true;
        }

        gameObject.SetActive(false);
    }

    private bool CanUseTargetSnapOffset(Vector3 normal)
    {
        if (!_isInitialized || !_hasTargetSnapOffset || _grid == null)
        {
            return false;
        }

        Vector3 normalizedNormal = normal.normalized;
        Vector3 gridUp = _grid.transform.up.normalized;
        return normalizedNormal.sqrMagnitude > 0.0001f &&
               gridUp.sqrMagnitude > 0.0001f &&
               Vector3.Dot(normalizedNormal, gridUp) > 0.999f;
    }

    public void StickToBlocker(Transform blockerTarget, int blockerLayer)
    {
        StickToBlocker(blockerTarget, blockerLayer, false, default);
    }

    public void StickToBlocker(Transform blockerTarget, int blockerLayer, Vector3 worldPosition)
    {
        StickToBlocker(blockerTarget, blockerLayer, true, worldPosition);
    }

    private void StickToBlocker(Transform blockerTarget, int blockerLayer, bool applyPosition, Vector3 worldPosition)
    {
        if (_isBlockedByBlocker)
        {
            return;
        }

        _isBlockedByBlocker = true;
        _hasReservedTarget = false;
        _guideStraightToTarget = false;
        BlockerTarget = blockerTarget;

        if (_cachedRigidbody != null)
        {
            _cachedRigidbody.linearVelocity = Vector3.zero;
            _cachedRigidbody.angularVelocity = Vector3.zero;
            _cachedRigidbody.useGravity = false;
            _cachedRigidbody.isKinematic = true;
        }

        if (applyPosition)
        {
            transform.position = worldPosition;
        }

        if (blockerTarget != null)
        {
            transform.SetParent(blockerTarget, true);
            _blockerAnchorLocalPosition = transform.localPosition;
            _blockerAnchorLocalRotation = transform.localRotation;
            _hasBlockerAnchor = true;
        }

        if (blockerLayer >= 0)
        {
            SetLayerRecursively(transform, blockerLayer);
        }
    }

    private void TryResolveLanding()
    {
        if (!_isInitialized || _isSnapped || _isFailed || _isBlockedByBlocker || _owner == null || _grid == null)
        {
            return;
        }

        if (Time.time - _spawnTime < minLifetimeBeforeSnap)
        {
            return;
        }

        _owner.TryFinalizeStone(this);
    }

    private bool TryStickToBlockerAlongMovement()
    {
        if (_owner == null)
        {
            _previousBlockerProbePosition = GetBlockerProbePosition();
            return false;
        }

        Vector3 currentPosition = GetBlockerProbePosition();
        if (!_owner.TryGetBlockerAlongStonePath(this, _previousBlockerProbePosition, currentPosition, out Collider blockerCollider))
        {
            _previousBlockerProbePosition = currentPosition;
            return false;
        }

        _owner.TryStickStoneToBlocker(this, blockerCollider);
        return true;
    }

    private Vector3 GetBlockerProbePosition()
    {
        return _cachedCollider != null
            ? _cachedCollider.bounds.center
            : transform.position;
    }

    private void OnDestroy()
    {
        if (!_isSnapped && _hasReservedTarget && _owner != null)
        {
            _owner.ReleaseReservation(_targetCoordinate);
        }
    }

    private void HandleContact(Collider collider)
    {
        if (!_isInitialized || _isSnapped || _isFailed || _isBlockedByBlocker)
        {
            return;
        }

        if (_owner != null && _owner.IsBlockerHit(collider))
        {
            _owner.TryStickStoneToBlocker(this, collider);
            return;
        }

        if (_owner != null && _owner.IsBoardHit(collider))
        {
            if (!_hasBoardContact)
            {
                _boardContactTime = Time.time;
            }

            _hasBoardContact = true;
            IgnoreUnrelatedCollision(collider);
            return;
        }

        IgnoreUnrelatedCollision(collider);
    }

    private void FailPlacement()
    {
        if (_isFailed)
        {
            return;
        }

        _isFailed = true;
        Destroy(gameObject);
    }

    private void IgnoreUnrelatedCollision(Collider collider)
    {
        if (_cachedCollider == null || collider == null || collider == _cachedCollider)
        {
            return;
        }

        Physics.IgnoreCollision(_cachedCollider, collider, true);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static float ProjectBoundsExtent(Vector3 boundsExtents, Vector3 axis)
    {
        axis.Normalize();
        return Mathf.Abs(axis.x) * boundsExtents.x +
               Mathf.Abs(axis.y) * boundsExtents.y +
               Mathf.Abs(axis.z) * boundsExtents.z;
    }
}
