using UnityEngine;

public class OmokFallingStone : MonoBehaviour
{
    [Header("Landing")]
    [SerializeField, Min(0f)] private float minLifetimeBeforeSnap = 0.05f;
    [SerializeField, Min(0f)] private float fallbackSnapDelay = 5f;

    private OmokStoneDropper owner;
    private OmokGrid grid;
    private Rigidbody cachedRigidbody;
    private Collider cachedCollider;
    private bool isInitialized;
    private bool isSnapped;
    private bool isBlockedByBlocker;
    private bool isFailed;
    private float spawnTime;
    private OmokStoneSnapTiming snapTiming;
    private bool hasReservedTarget;
    private Vector2Int targetCoordinate;
    private Vector3 targetWorldPosition;
    private float gravityScale = 1f;

    public Vector2Int Coordinate { get; private set; }
    public bool IsSnapped => isSnapped;
    public bool IsBlockedByBlocker => isBlockedByBlocker;
    public Transform BlockerTarget { get; private set; }
    public OmokStoneColor StoneColor { get; private set; }
    public OmokStoneSnapTiming SnapTiming => snapTiming;
    public Vector2Int TargetCoordinate => targetCoordinate;
    public Vector3 TargetWorldPosition => targetWorldPosition;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
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
        float fallGravityScale)
    {
        owner = dropper;
        grid = omokGrid;
        cachedRigidbody = rigidbody != null ? rigidbody : GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        isInitialized = true;
        isSnapped = false;
        isBlockedByBlocker = false;
        isFailed = false;
        BlockerTarget = null;
        spawnTime = Time.time;
        StoneColor = stoneColor;
        snapTiming = timing;
        hasReservedTarget = reservedTarget;
        targetCoordinate = reservedCoordinate;
        targetWorldPosition = snappedWorldPosition;
        gravityScale = Mathf.Max(0f, fallGravityScale);
    }

    private void Update()
    {
        if (!isInitialized || isSnapped || isFailed || isBlockedByBlocker)
        {
            return;
        }

        if (cachedRigidbody != null && cachedRigidbody.IsSleeping())
        {
            TryResolveLanding();
            return;
        }

        if (fallbackSnapDelay > 0f && Time.time - spawnTime >= fallbackSnapDelay)
        {
            TryResolveLanding();
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized || isSnapped || isFailed || isBlockedByBlocker || cachedRigidbody == null || cachedRigidbody.isKinematic)
        {
            return;
        }

        if (Mathf.Approximately(gravityScale, 1f))
        {
            return;
        }

        cachedRigidbody.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
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
        if (cachedCollider == null)
        {
            return 0f;
        }

        Bounds bounds = cachedCollider.bounds;
        Vector3 absoluteNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        return Vector3.Dot(bounds.extents, absoluteNormal);
    }

    public void SnapTo(Vector2Int coordinate, Vector3 worldPosition)
    {
        targetCoordinate = coordinate;
        targetWorldPosition = worldPosition;
        Coordinate = coordinate;
        isSnapped = true;
        transform.position = worldPosition;

        if (cachedRigidbody == null)
        {
            return;
        }

        cachedRigidbody.linearVelocity = Vector3.zero;
        cachedRigidbody.angularVelocity = Vector3.zero;
        cachedRigidbody.useGravity = false;
        cachedRigidbody.isKinematic = true;
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
        if (isBlockedByBlocker)
        {
            return;
        }

        isBlockedByBlocker = true;
        hasReservedTarget = false;
        BlockerTarget = blockerTarget;

        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.isKinematic = true;
        }

        if (applyPosition)
        {
            transform.position = worldPosition;
        }

        if (blockerTarget != null)
        {
            transform.SetParent(blockerTarget, true);
        }

        if (blockerLayer >= 0)
        {
            SetLayerRecursively(transform, blockerLayer);
        }
    }

    private void TryResolveLanding()
    {
        if (!isInitialized || isSnapped || isFailed || isBlockedByBlocker || owner == null || grid == null)
        {
            return;
        }

        if (Time.time - spawnTime < minLifetimeBeforeSnap)
        {
            return;
        }

        owner.TryFinalizeStone(this);
    }

    private void OnDestroy()
    {
        if (!isSnapped && hasReservedTarget && owner != null)
        {
            owner.ReleaseReservation(targetCoordinate);
        }
    }

    private void HandleContact(Collider collider)
    {
        if (!isInitialized || isSnapped || isFailed || isBlockedByBlocker)
        {
            return;
        }

        if (owner != null && owner.IsBlockerHit(collider))
        {
            owner.TryStickStoneToBlocker(this, collider);
            return;
        }

        TryResolveLanding();
    }

    private void FailPlacement()
    {
        if (isFailed)
        {
            return;
        }

        isFailed = true;
        Destroy(gameObject);
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
}
