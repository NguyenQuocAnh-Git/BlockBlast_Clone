using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DragBlockController : MonoBehaviour
{
    // Constants
    private const int InvalidAnchor = -999;

    [Header("Drag & Drop")]
    public float dragMultiplier = 1.7f;
    public float dropZoneRadius = 1.2f;
    public float edgeDropZoneMultiplier = 1.5f;
    public float maxGhostDistance = 1.0f;
    public int snapSearchRadius = 1;
    public float snapReleaseDistanceMultiplier = 1.0f;

    [Header("Mobile")]
    public float hoverYOffset = 2.0f;
    public float yOffsetSpeed = 10f;

    [Header("Selection")]
    public float selectionRadius = 2.0f;
    public bool useRayBasedSelection = true;

    [Header("Debug")]
    public bool debugGizmos = false;
    public bool debugGizmosOnlyWhileDragging = true;
    public int debugSearchRadius = 2;
    public float debugPointRadius = 0.12f;

    // Cached references
    private Camera cam;
    private GridView grid;
    private BlockSpawnController spawnController;

    // Transient state
    private bool isDragging;
    private bool isSelected;
    private Vector3 startInput;
    private Vector3 startBlock;
    private float currentYOffset;

    private BlockData blockData;
    private Color blockColor = Color.white;

    // Save child transforms when we adjust to grid scale
    private readonly Dictionary<Transform, Vector3> savedLocalPositions = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> savedLocalScales = new Dictionary<Transform, Vector3>();
    private bool adjustedToGridScale;

    // Public and static helpers
    public BlockData BlockData => blockData;
    public static DragBlockController CurrentSelected;
    public static Vector3 LastClickWorld = Vector3.zero;

    public void SetBlockColor(Color color) => blockColor = color;
    public void SetShape(List<Vector2Int> shape) => blockData = new BlockData(shape);

    void Awake()
    {
        cam = Camera.main;
        grid = FindObjectOfType<GridView>();
        spawnController = FindObjectOfType<BlockSpawnController>();
    }

    // Called externally to start drag (from selection)
    public void BeginDragFromClick(Vector3 clickWorld)
    {
        if (CurrentSelected != null) return;
        CurrentSelected = this;
        LastClickWorld = clickWorld;

        isDragging = true;
        isSelected = true;
        startInput = clickWorld;
        startBlock = transform.position;

        currentYOffset = hoverYOffset;
        SaveChildTransforms();
        AdjustCellsToGridScale();
    }

    void Update()
    {
        if (useRayBasedSelection && Input.GetMouseButtonDown(0) && CurrentSelected == null)
        {
            Vector3 clickWorld = GetInputWorld();
            LastClickWorld = clickWorld;
            TrySelectNearest(clickWorld);
        }

        if (useRayBasedSelection && Input.GetMouseButtonUp(0) && CurrentSelected == this)
        {
            Release();
            CurrentSelected = null;
        }

        currentYOffset = isSelected ? hoverYOffset : Mathf.Lerp(currentYOffset, 0f, Time.deltaTime * yOffsetSpeed);

        if (!isDragging) return;

        // Move with input
        Vector3 mouseWorld = GetInputWorld();
        Vector3 freeMove = startBlock + (mouseWorld - startInput) * dragMultiplier;
        freeMove.y += currentYOffset;
        transform.position = freeMove;

    }

    void OnMouseUp() => Release(); // fallback for collider setups

    // Release logic: try placing or return to spawn
    public void Release()
    {
        isDragging = false;
        isSelected = false;

        if (adjustedToGridScale)
        {
            RestoreChildTransforms();
            adjustedToGridScale = false;
        }

        if (grid == null || blockData == null)
        {
            ReturnToSpawnPosition();
            return;
        }

        // Prefer snap based on where the player released (pointer position).
        Vector3 pointerWorld = GetInputWorld();

        // Determine which cell of the block the pointer is over (mask indices 0..4).
        float disp = GetDisplayedCellWorldSize();
        Vector2 centroid = GetShapeCentroidLocal();
        Vector2 delta = (Vector2)(pointerWorld - transform.position);
        Vector2 maskFloat = new Vector2(delta.x / disp + centroid.x, delta.y / disp + centroid.y);
        Vector2Int maskIndex = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(maskFloat.x), 0, 4),
            Mathf.Clamp(Mathf.RoundToInt(maskFloat.y), 0, 4)
        );

        // Find the grid cell under the pointer, then compute the anchor so that
        // block.mask[maskIndex] would land on that cell.
        Vector2Int pointerGrid = GridHelper.WorldToGrid(grid, pointerWorld);
        pointerGrid = ClampToGrid(pointerGrid);
        Vector2Int desiredAnchor = new Vector2Int(pointerGrid.x - maskIndex.x, pointerGrid.y - maskIndex.y);

        // If pointer is far from grid entirely, cancel.
        float distToGrid = Vector3.Distance(pointerWorld, grid.GetAnchorWorldPosition(pointerGrid.x, pointerGrid.y));
        if (distToGrid > disp * maxGhostDistance)
        {
            ReturnToSpawnPosition();
            GameManager.Instance?.CheckGameOver();
            return;
        }

        // Try desired anchor first, then nearby anchors, then fallback to best by proximity.
        if (TryPlaceAt(desiredAnchor)) return;
        if (TryFindNearbyPlacement(desiredAnchor, pointerWorld, out Vector2Int nearby) && TryPlaceAt(nearby)) return;
        if (TryFindBestPlacementAnchor(pointerWorld, out Vector2Int best) && TryPlaceAt(best)) return;

        // Fallback
        ReturnToSpawnPosition();
        GameManager.Instance?.CheckGameOver();
    }

    private void ReturnToSpawnPosition()
    {
        StartCoroutine(SmoothReturnToSpawn());
    }

    private IEnumerator SmoothReturnToSpawn()
    {
        Vector3 from = transform.position;
        Vector3 to = startBlock;
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - (1f - t) * (1f - t); // ease-out
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        transform.position = to;
    }

    Vector3 GetInputWorld()
    {
        Vector3 p = Input.mousePosition;
        p.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(p);
    }

    // Select nearest block within selectionRadius
    private void TrySelectNearest(Vector3 clickWorld)
    {
        var all = FindObjectsOfType<DragBlockController>();
        float best = float.MaxValue;
        DragBlockController chosen = null;
        foreach (var c in all)
        {
            if (c == null) continue;
            float d = Vector3.Distance(clickWorld, c.transform.position);
            if (d < best && d <= c.selectionRadius)
            {
                best = d;
                chosen = c;
            }
        }
        chosen?.BeginDragFromClick(clickWorld);
    }

    // Scale/position child cells to match grid cell size
    private void AdjustCellsToGridScale()
    {
        if (grid == null) return;
        float targetWorldCellSize = GetDisplayedCellWorldSize();

        var cells = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            if (t.GetComponent<SpriteRenderer>() != null) cells.Add(t);
        }
        if (cells.Count == 0) return;

        // Find smallest non-zero spacing between children (local)
        float minSpacing = float.MaxValue;
        for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                float d = Vector3.Distance(cells[i].localPosition, cells[j].localPosition);
                if (d > 0.0001f && d < minSpacing) minSpacing = d;
            }
        float currentSpacing = (minSpacing == float.MaxValue) ? 1f : minSpacing;
        float posFactor = currentSpacing > 0f ? (targetWorldCellSize / currentSpacing) : 1f;

        foreach (var t in cells)
        {
            t.localPosition *= posFactor;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            float baseSize = sr.sprite != null ? sr.sprite.bounds.size.x : sr.bounds.size.x;
            if (baseSize <= 0f) continue;
            t.localScale = Vector3.one * (targetWorldCellSize / baseSize);
            adjustedToGridScale = true;
        }
    }

    private void SaveChildTransforms()
    {
        savedLocalPositions.Clear();
        savedLocalScales.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform t = transform.GetChild(i);
            savedLocalPositions[t] = t.localPosition;
            savedLocalScales[t] = t.localScale;
        }
    }

    private void RestoreChildTransforms()
    {
        foreach (var kv in savedLocalPositions) if (kv.Key != null) kv.Key.localPosition = kv.Value;
        foreach (var kv in savedLocalScales) if (kv.Key != null) kv.Key.localScale = kv.Value;
        savedLocalPositions.Clear();
        savedLocalScales.Clear();
    }

    private float GetDisplayedCellWorldSize()
    {
        if (grid == null) return 1f;
        return grid.CellWorldSize * Mathf.Abs(grid.transform.localScale.x);
    }

    // Compute centroid of the shape in mask-local coordinates (0..4)
    private Vector2 GetShapeCentroidLocal()
    {
        if (blockData == null) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (blockData.mask[i, j] == 1)
                {
                    sum += new Vector2(i, j);
                    count++;
                }
        return count > 0 ? (sum / count) : Vector2.zero;
    }

    // Returns the world position of the block's shape-origin (mask-space (0,0))
    private Vector3 GetShapeOriginWorld()
    {
        Vector2 centroid = GetShapeCentroidLocal();
        float disp = GetDisplayedCellWorldSize();
        // The transform.position currently represents the visual centroid (spawn code centers by centroid).
        // To get the origin, subtract centroid in world units.
        return transform.position - (Vector3)(centroid * disp);
    }

    // Map world position to nearest grid anchor using rounding
    private Vector2Int NearestAnchorByRounding(Vector3 worldPos)
    {
        if (grid == null) return new Vector2Int(InvalidAnchor, InvalidAnchor);
        Vector3 local = grid.transform.InverseTransformPoint(worldPos);
        float offset = -(grid.GridSize - 1) * 0.5f * grid.CellWorldSize;
        float fx = (local.x - offset) / grid.CellWorldSize;
        float fy = (local.y - offset) / grid.CellWorldSize;
        return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(fx), 0, grid.GridSize - 1),
                               Mathf.Clamp(Mathf.RoundToInt(fy), 0, grid.GridSize - 1));
    }

    private Vector2Int ClampToGrid(Vector2Int a)
    {
        if (grid == null) return a;
        a.x = Mathf.Clamp(a.x, 0, grid.GridSize - 1);
        a.y = Mathf.Clamp(a.y, 0, grid.GridSize - 1);
        return a;
    }

    private bool IsEdgeCell(int x, int y)
    {
        if (grid == null) return false;
        int s = grid.GridSize;
        return x <= 0 || x >= s - 1 || y <= 0 || y >= s - 1;
    }

    private bool TryPlaceAt(Vector2Int anchor)
    {
        if (grid == null || blockData == null) return false;
        if (!grid.CanPlace(blockData, anchor.x, anchor.y)) return false;
        // Align the visual transform so that the block's shape-origin (mask (0,0))
        // is placed at the grid anchor. transform.position represents the visual
        // centroid, so we offset by centroid * displayedCellSize.
        Vector2 centroid = GetShapeCentroidLocal();
        float disp = GetDisplayedCellWorldSize();
        Vector3 anchorWorld = grid.GetAnchorWorldPosition(anchor.x, anchor.y);
        transform.position = anchorWorld + (Vector3)(centroid * disp);
        grid.ApplyBlock(blockData, anchor.x, anchor.y, blockColor);
        spawnController?.OnBlockDestroyed(gameObject);
        GameManager.Instance?.OnBlockPlaced();
        Destroy(gameObject);
        return true;
    }

    // Try finding a valid spot within 1 cell radius
    private bool TryFindNearbyPlacement(Vector2Int centerAnchor, Vector3 referenceWorld, out Vector2Int bestAnchor)
    {
        bestAnchor = new Vector2Int(InvalidAnchor, InvalidAnchor);
        if (grid == null || blockData == null) return false;

        float bestDist = float.MaxValue;
        bool found = false;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = centerAnchor.x + dx, y = centerAnchor.y + dy;
                if (x < 0 || x >= grid.GridSize || y < 0 || y >= grid.GridSize) continue;
                if (!grid.CanPlace(blockData, x, y)) continue;
                float d = Vector3.Distance(referenceWorld, grid.GetAnchorWorldPosition(x, y));
                if (d < bestDist) { bestDist = d; bestAnchor = new Vector2Int(x, y); found = true; }
            }
        return found;
    }

    private bool TryFindBestPlacementAnchor(Vector3 world, out Vector2Int bestAnchor)
    {
        bestAnchor = new Vector2Int(InvalidAnchor, InvalidAnchor);
        if (grid == null || blockData == null) return false;

        Vector2Int approx = NearestAnchorByRounding(world);
        Vector3 approxWorld = grid.GetAnchorWorldPosition(approx.x, approx.y);
        float distToApprox = Vector3.Distance(world, approxWorld);

        float zone = dropZoneRadius * (IsEdgeCell(approx.x, approx.y) ? edgeDropZoneMultiplier : 1f);
        float maxSnap = GetDisplayedCellWorldSize() * zone * snapReleaseDistanceMultiplier;
        if (distToApprox > maxSnap) return false;

        int radius = Mathf.Max(0, snapSearchRadius);
        float bestDist = float.MaxValue;
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = approx.x + dx, y = approx.y + dy;
                if (x < 0 || x >= grid.GridSize || y < 0 || y >= grid.GridSize) continue;
                if (!grid.CanPlace(blockData, x, y)) continue;
                Vector3 w = grid.GetAnchorWorldPosition(x, y);
                float d = Vector3.Distance(world, w);
                if (d > maxSnap) continue;
                if (d < bestDist) { bestDist = d; bestAnchor = new Vector2Int(x, y); }
            }
        return bestAnchor.x != InvalidAnchor;
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !debugGizmos) return;
        if (grid == null) grid = FindObjectOfType<GridView>();
        if (grid == null || blockData == null) return;

        bool showDragGizmos = !debugGizmosOnlyWhileDragging || isDragging;
        if (!showDragGizmos) return;

        Vector3 center = GetShapeOriginWorld();
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(center, debugPointRadius);

        Vector2Int approx = GridHelper.WorldToGrid(grid, center);
        approx = ClampToGrid(approx);
        Vector3 approxWorld = grid.GetAnchorWorldPosition(approx.x, approx.y);
        bool canApprox = grid.CanPlace(blockData, approx.x, approx.y);
        Gizmos.color = canApprox ? new Color(1f, 0.65f, 0.1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawWireSphere(approxWorld, debugPointRadius);
        Gizmos.DrawLine(center, approxWorld);

        float disp = GetDisplayedCellWorldSize();
        float zone = dropZoneRadius * (IsEdgeCell(approx.x, approx.y) ? edgeDropZoneMultiplier : 1f);
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(approxWorld, disp * zone);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(approxWorld, disp * maxGhostDistance);

        if (TryFindBestPlacementAnchor(center, out Vector2Int best))
        {
            Vector3 bw = grid.GetAnchorWorldPosition(best.x, best.y);
            Gizmos.color = new Color(0.15f, 1f, 1f, 1f);
            Gizmos.DrawWireSphere(bw, debugPointRadius * 1.3f);
            Gizmos.DrawLine(approxWorld, bw);

            int r = Mathf.Max(0, debugSearchRadius);
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    int x = approx.x + dx, y = approx.y + dy;
                    if (x < 0 || x >= grid.GridSize || y < 0 || y >= grid.GridSize) continue;
                    bool can = grid.CanPlace(blockData, x, y);
                    Vector3 w = grid.GetAnchorWorldPosition(x, y);
                    Gizmos.color = can ? new Color(0.2f, 1f, 0.2f, 0.22f) : new Color(1f, 0.2f, 0.2f, 0.18f);
                    Gizmos.DrawWireCube(w, Vector3.one * (disp * 0.9f));
                }

        }

        // Additional selection debug
        if (LastClickWorld != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(LastClickWorld, debugPointRadius * 1.3f);
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.12f);
            Gizmos.DrawWireSphere(LastClickWorld, selectionRadius);
            Camera camLocal = cam != null ? cam : Camera.main;
            if (camLocal != null) { Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f); Gizmos.DrawLine(camLocal.transform.position, LastClickWorld); }
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, selectionRadius);
        if (CurrentSelected != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.0f, 0.9f);
            Gizmos.DrawLine(LastClickWorld, CurrentSelected.transform.position);
            Gizmos.DrawWireSphere(CurrentSelected.transform.position, debugPointRadius * 1.5f);
        }
    }
}
