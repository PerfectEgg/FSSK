using UnityEngine;

public class OmokGrid : MonoBehaviour
{
    [Header("Board Setup")]
    [SerializeField, Min(2)] private int boardSize = 19;
    [SerializeField] private Transform gridMin;
    [SerializeField] private Transform gridMax;

    [Header("Snap")]
    [SerializeField, Range(0f, 1f)] private float outsideToleranceInCells = 0.45f;

    [Header("Debug")]
    [SerializeField] private bool drawGridGizmos = true;
    [SerializeField] private bool drawGridLines = true;
    [SerializeField, Min(0.01f)] private float gizmoRadius = 0.06f;
    [SerializeField, Min(0f)] private float gizmoHeightOffset = 0.02f;

    public int BoardSize => boardSize;
    public bool IsReady => gridMin != null && gridMax != null && boardSize > 1;

    public Vector2 CellSize
    {
        get
        {
            if (!IsReady)
            {
                return Vector2.zero;
            }

            Vector3 step = GetCellStepLocal();
            return new Vector2(Mathf.Abs(step.x), Mathf.Abs(step.z));
        }
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        if (!IsReady)
        {
            return transform.position;
        }

        x = Mathf.Clamp(x, 0, boardSize - 1);
        y = Mathf.Clamp(y, 0, boardSize - 1);

        Vector3 minLocal = transform.InverseTransformPoint(gridMin.position);
        Vector3 maxLocal = transform.InverseTransformPoint(gridMax.position);
        Vector3 step = GetCellStepLocal();

        Vector3 localPosition = new Vector3(
            minLocal.x + (step.x * x),
            Mathf.Lerp(minLocal.y, maxLocal.y, 0.5f),
            minLocal.z + (step.z * y));

        return transform.TransformPoint(localPosition);
    }

    public Vector3 GetWorldPosition(Vector2Int coordinate)
    {
        return GetWorldPosition(coordinate.x, coordinate.y);
    }

    public bool TryGetCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
    {
        coordinate = default;

        if (!IsReady)
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector3 minLocal = transform.InverseTransformPoint(gridMin.position);
        Vector3 step = GetCellStepLocal();

        if (Mathf.Approximately(step.x, 0f) || Mathf.Approximately(step.z, 0f))
        {
            return false;
        }

        float x = (localPosition.x - minLocal.x) / step.x;
        float y = (localPosition.z - minLocal.z) / step.z;

        if (!IsWithinSnapRange(x) || !IsWithinSnapRange(y))
        {
            return false;
        }

        coordinate = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(x), 0, boardSize - 1),
            Mathf.Clamp(Mathf.RoundToInt(y), 0, boardSize - 1));

        return true;
    }

    public bool TryGetSnappedWorldPosition(Vector3 worldPosition, out Vector3 snappedWorldPosition, out Vector2Int coordinate)
    {
        if (TryGetCoordinate(worldPosition, out coordinate))
        {
            snappedWorldPosition = GetWorldPosition(coordinate);
            return true;
        }

        snappedWorldPosition = default;
        return false;
    }

    private Vector3 GetCellStepLocal()
    {
        Vector3 minLocal = transform.InverseTransformPoint(gridMin.position);
        Vector3 maxLocal = transform.InverseTransformPoint(gridMax.position);
        float denominator = boardSize - 1;

        return new Vector3(
            (maxLocal.x - minLocal.x) / denominator,
            0f,
            (maxLocal.z - minLocal.z) / denominator);
    }

    private bool IsWithinSnapRange(float coordinateOnAxis)
    {
        return coordinateOnAxis >= -outsideToleranceInCells &&
               coordinateOnAxis <= (boardSize - 1) + outsideToleranceInCells;
    }

    private void OnValidate()
    {
        boardSize = Mathf.Max(2, boardSize);
        outsideToleranceInCells = Mathf.Clamp01(outsideToleranceInCells);
        gizmoRadius = Mathf.Max(0.01f, gizmoRadius);
        gizmoHeightOffset = Mathf.Max(0f, gizmoHeightOffset);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos || !IsReady)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        if (drawGridLines)
        {
            for (int i = 0; i < boardSize; i++)
            {
                Gizmos.DrawLine(GetGizmoWorldPosition(0, i), GetGizmoWorldPosition(boardSize - 1, i));
                Gizmos.DrawLine(GetGizmoWorldPosition(i, 0), GetGizmoWorldPosition(i, boardSize - 1));
            }
        }

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                Vector3 gizmoPosition = GetGizmoWorldPosition(x, y);
                Gizmos.DrawSphere(gizmoPosition, gizmoRadius);
                Gizmos.DrawWireSphere(gizmoPosition, gizmoRadius);
            }
        }
    }

    private Vector3 GetGizmoWorldPosition(int x, int y)
    {
        return GetWorldPosition(x, y) + (transform.up * gizmoHeightOffset);
    }
}
