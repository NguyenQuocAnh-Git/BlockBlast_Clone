using UnityEngine;
using System.Collections.Generic;

public class DragBlockController : MonoBehaviour
{
    public float dragMultiplier = 1.7f;
    public float snapDistance = 3.0f; // Tăng thêm cho edge detection
    public float selectedScale = 2.7f; // Scale khi được chọn
    public float scaleSpeed = 15f; // Tốc độ scale animation
    public float dropZoneRadius = 1.2f; // Tăng radius cho feel tốt hơn
    public float edgeDropZoneMultiplier = 1.5f; // Multiplier cho các ô rìa
    public float extendedGhostDistance = 2.0f; // Khoảng cách mở rộng (số ô) để vẫn hiện ghost khi ra ngoài grid
    public float maxGhostDistance = 1.0f; // Khoảng cách tối đa để hiện ghost (tương đương 1 ô)
    
    [Header("Mobile Settings")]
    public float hoverYOffset = 2.0f; // Dịch chuyển lên theo trục Y khi hover để tránh che block trên mobile
    public float yOffsetSpeed = 10f; // Tốc độ dịch chuyển Y

    public int snapSearchRadius = 1;
    public float snapReleaseDistanceMultiplier = 1.0f;

    public bool debugGizmos = false;
    public bool debugGizmosOnlyWhileDragging = true;
    public bool debugGizmosShowGhostCells = true;
    public int debugSearchRadius = 2;
    public float debugPointRadius = 0.12f;

    private Camera cam;
    private GridView grid;
    private BlockSpawnController spawnController; // Reference để báo cáo khi destroy

    private bool isDragging;
    private bool isSelected; // Đang được chọn
    private Vector3 startInput;
    private Vector3 startBlock;
    private Vector3 originalScale; // Scale ban đầu
    private Vector3 targetPosition; // Vị trí mục tiêu cho smooth movement
    private float currentYOffset; // Y offset hiện tại

    private BlockData blockData;
    private Vector2Int ghostAnchor = new Vector2Int(-999, -999);
    private bool ghostVisible;
    private Color blockColor = Color.white; // Màu của block
    // Saved child transforms for restoring after hover ends
    private Dictionary<Transform, Vector3> savedLocalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> savedLocalScales = new Dictionary<Transform, Vector3>();
    private bool adjustedToGridScale = false;

    // Public property để truy cập blockData
    public BlockData BlockData => blockData;
    
    // Static selection state to enable ray-based selection without colliders
    public static DragBlockController CurrentSelected;
    public static Vector3 LastClickWorld = Vector3.zero;
    public float selectionRadius = 2.0f; // world units for nearest selection
    public bool useRayBasedSelection = true;
    
    // Method để set màu block từ BlockSpawnController
    public void SetBlockColor(Color color)
    {
        blockColor = color;
    }

    // Method để set shape từ BlockSpawnController
    public void SetShape(List<Vector2Int> shape)
    {
        blockData = new BlockData(shape);
    }

    // Kiểm tra xem có phải ô rìa không (để tăng drop zone)
    private bool IsEdgeCell(int x, int y)
    {
        int gridSize = grid.GridSize;
        
        // Ô rìa: x = 0, x = gridSize-1, y = 0, y = gridSize-1
        return x <= 0 || x >= gridSize - 1 || y <= 0 || y >= gridSize - 1;
    }

    void Awake()
    {
        cam = Camera.main;
        grid = FindObjectOfType<GridView>();
        spawnController = FindObjectOfType<BlockSpawnController>(); // Tìm spawn controller
        
        originalScale = transform.localScale;
    }
    
    // Start dragging this block programmatically (from ray/select input)
    public void BeginDragFromClick(Vector3 clickWorld)
    {
        if (CurrentSelected != null) return;
        CurrentSelected = this;
        LastClickWorld = clickWorld;

        isDragging = true;
        isSelected = true;
        startInput = clickWorld;
        startBlock = transform.position;

        // Immediately apply Y offset (no scale change). Save original child transforms
        currentYOffset = hoverYOffset;
        SaveChildTransforms();
        AdjustCellsToGridScale();
    }

    void Update()
    {
        // Global input handling (works even without 2D colliders)
        if (useRayBasedSelection && Input.GetMouseButtonDown(0) && CurrentSelected == null)
        {
            Vector3 clickWorld = GetInputWorld();
            LastClickWorld = clickWorld;
            TrySelectNearest(clickWorld);
        }

        // If mouse released and this is the current selected, perform release
        if (useRayBasedSelection && Input.GetMouseButtonUp(0) && CurrentSelected == this)
        {
            Release();
            CurrentSelected = null;
        }

        // Handle hover Y offset only; scale is managed per-cell to match grid
        if (isSelected)
        {
            currentYOffset = hoverYOffset;
        }
        else
        {
            currentYOffset = Mathf.Lerp(currentYOffset, 0f, Time.deltaTime * yOffsetSpeed);
        }

        if (!isDragging) return;

        Vector3 mouseWorld = GetInputWorld();
        Vector3 freeMove = startBlock + (mouseWorld - startInput) * dragMultiplier;
        freeMove.y += currentYOffset;

        // Follow 1:1 with player's input while dragging (no smoothing)
        targetPosition = freeMove;
        transform.position = targetPosition;

        // Dùng block center để tính anchor cho ghost
        Vector3 blockCenterWorld = transform.position;
        Vector2Int anchor =
            GridHelper.WorldToGrid(grid, blockCenterWorld);

        // Clamp anchor trong grid bounds
        anchor.x = Mathf.Clamp(anchor.x, 0, grid.GridSize - 1);
        anchor.y = Mathf.Clamp(anchor.y, 0, grid.GridSize - 1);

        // Smarter ghost placement logic:
        // - Use a prioritized search for a desirable anchor near the block center
        // - Allow ghosts to appear within an extended distance, and remove noisy stepwise toggles
        Vector3 anchorWorldPos = grid.GetAnchorWorldPosition(anchor.x, anchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, anchorWorldPos);

        // If too far from grid entirely, clear ghost
        if (distanceToGrid > (GetDisplayedCellWorldSize() * extendedGhostDistance))
        {
            if (ghostVisible)
            {
                grid.ClearGhost();
                ghostVisible = false;
                ghostAnchor = new Vector2Int(-999, -999);
            }
            return;
        }

        // Determine the best anchor for the current block center
        Vector2Int desiredAnchor;
        bool hasDesired = GetDesiredGhostAnchor(blockCenterWorld, out desiredAnchor);

        if (hasDesired)
        {
            // Only update ghost when anchor changes to avoid flicker
            if (!ghostVisible || desiredAnchor != ghostAnchor)
            {
                grid.ShowGhost(blockData, desiredAnchor.x, desiredAnchor.y, blockColor);
                ghostVisible = true;
                ghostAnchor = desiredAnchor;
            }
        }
        else
        {
            if (ghostVisible)
            {
                grid.ClearGhost();
                ghostVisible = false;
                ghostAnchor = new Vector2Int(-999, -999);
            }
        }
    }

    void OnMouseUp()
    {
        // Unity's OnMouseUp is not reliable without colliders. Keep for safety but
        // prefer calling Release() from the input handling path.
        Release();
    }

    // Centralized release logic (moved out so it can be invoked without colliders)
    public void Release()
    {
        isDragging = false;
        isSelected = false;

        // Restore child transforms if they were adjusted to grid scale
        if (adjustedToGridScale)
        {
            RestoreChildTransforms();
            adjustedToGridScale = false;
        }

        Vector3 blockCenterWorld = transform.position;

        // Kiểm tra khoảng cách đến grid trước khi làm gì khác
        Vector2Int currentAnchor = GridHelper.WorldToGrid(grid, blockCenterWorld);
        currentAnchor.x = Mathf.Clamp(currentAnchor.x, 0, grid.GridSize - 1);
        currentAnchor.y = Mathf.Clamp(currentAnchor.y, 0, grid.GridSize - 1);

        Vector3 anchorWorldPos = grid.GetAnchorWorldPosition(currentAnchor.x, currentAnchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, anchorWorldPos);

        // Nếu ghost đang hiện và vị trí ghost hợp lệ => snap ngay lập tức (tin tưởng ghost)
        if (ghostVisible && ghostAnchor.x != -999 && grid.CanPlace(blockData, ghostAnchor.x, ghostAnchor.y))
        {
            grid.ClearGhost();

            // Snap block đến vị trí ghost
            transform.position = grid.GetAnchorWorldPosition(ghostAnchor.x, ghostAnchor.y);
            grid.ApplyBlock(blockData, ghostAnchor.x, ghostAnchor.y, blockColor);

            // Báo cáo cho spawn controller trước khi destroy
            if (spawnController != null)
            {
                spawnController.OnBlockDestroyed(gameObject);
            }

            // Báo cáo cho GameManager để kiểm tra game over
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBlockPlaced();
            }

            Destroy(gameObject);
            return;
        }

        // Nếu quá xa grid (quá maxGhostDistance), trả về spawn area
        if (distanceToGrid > (GetDisplayedCellWorldSize() * maxGhostDistance))
        {
            ReturnToSpawnPosition();
            GameManager.Instance?.CheckGameOver();
            return;
        }

        // Ghost không hiện hoặc không hợp lệ -> thử tìm kiếm khe hở gần nhất trước khi dùng logic cũ
        Vector2Int searchAnchor = GridHelper.WorldToGrid(grid, blockCenterWorld);
        searchAnchor.x = Mathf.Clamp(searchAnchor.x, 0, grid.GridSize - 1);
        searchAnchor.y = Mathf.Clamp(searchAnchor.y, 0, grid.GridSize - 1);

        Vector2Int nearbyPlacementAnchor;
        bool foundNearby = TryFindNearbyPlacement(searchAnchor, out nearbyPlacementAnchor);

        if (foundNearby)
        {
            grid.ShowGhost(blockData, nearbyPlacementAnchor.x, nearbyPlacementAnchor.y, blockColor);
            grid.ClearGhost();

            transform.position = grid.GetAnchorWorldPosition(nearbyPlacementAnchor.x, nearbyPlacementAnchor.y);
            grid.ApplyBlock(blockData, nearbyPlacementAnchor.x, nearbyPlacementAnchor.y, blockColor);

            if (spawnController != null)
            {
                spawnController.OnBlockDestroyed(gameObject);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBlockPlaced();
            }

            Destroy(gameObject);
            return;
        }

        // Nếu không tìm thấy khe hở, dùng logic cũ để tìm vị trí tốt nhất
        Vector2Int bestAnchor;
        bool hasBestAnchor = TryFindBestPlacementAnchor(blockCenterWorld, out bestAnchor);

        if (hasBestAnchor)
        {
            grid.ShowGhost(blockData, bestAnchor.x, bestAnchor.y, blockColor);
            grid.ClearGhost();

            transform.position = grid.GetAnchorWorldPosition(bestAnchor.x, bestAnchor.y);
            grid.ApplyBlock(blockData, bestAnchor.x, bestAnchor.y, blockColor);

            if (spawnController != null)
            {
                spawnController.OnBlockDestroyed(gameObject);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnBlockPlaced();
            }

            Destroy(gameObject);
            return;
        }

        // Nếu vẫn không được, trả về vị trí ban đầu
        ReturnToSpawnPosition();
        GameManager.Instance?.CheckGameOver();
    }
    
    // Method mới để trả về vị trí spawn với animation mượt
    private void ReturnToSpawnPosition()
    {
        grid.ClearGhost();
        ghostVisible = false;
        ghostAnchor = new Vector2Int(-999, -999);
        
        // Coroutine để smooth movement về spawn position
        StartCoroutine(SmoothReturnToSpawn());
    }
    
    private System.Collections.IEnumerator SmoothReturnToSpawn()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startBlock;
        float duration = 0.3f; // Thời gian animation
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Dùng ease-out curve cho movement mượt
            t = 1f - (1f - t) * (1f - t);
            
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        transform.position = targetPos;
    }

    private bool TryFindNearbyPlacement(Vector2Int centerAnchor, out Vector2Int bestAnchor)
    {
        bestAnchor = new Vector2Int(-999, -999);
        
        if (grid == null || blockData == null)
            return false;
            
        float bestDistance = float.MaxValue;
        bool foundPlacement = false;
        
        // Tìm kiếm trong phạm vi 1 ô xung quanh centerAnchor
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                // Bỏ qua vị trí trung tâm (đã kiểm tra trước đó)
                if (dx == 0 && dy == 0)
                    continue;
                    
                int testX = centerAnchor.x + dx;
                int testY = centerAnchor.y + dy;
                
                // Kiểm tra bounds
                if (testX < 0 || testX >= grid.GridSize || testY < 0 || testY >= grid.GridSize)
                    continue;
                
                // Kiểm tra có thể đặt block không
                if (grid.CanPlace(blockData, testX, testY))
                {
                    // Tính khoảng cách đến vị trí hiện tại của block
                    Vector3 testWorldPos = grid.GetAnchorWorldPosition(testX, testY);
                    Vector3 currentBlockPos = transform.position;
                    float distance = Vector3.Distance(currentBlockPos, testWorldPos);
                    
                    // Chọn vị trí gần nhất
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAnchor = new Vector2Int(testX, testY);
                        foundPlacement = true;
                    }
                }
            }
        }
        
        return foundPlacement;
    }

    // Determine a smart desired ghost anchor given the block center world position.
    private bool GetDesiredGhostAnchor(Vector3 blockCenterWorld, out Vector2Int desiredAnchor)
    {
        desiredAnchor = new Vector2Int(-999, -999);
        if (grid == null || blockData == null) return false;

        // Use rounding-based nearest anchor for more intuitive placement
        Vector2Int approxAnchor = NearestAnchorByRounding(blockCenterWorld);
        approxAnchor.x = Mathf.Clamp(approxAnchor.x, 0, grid.GridSize - 1);
        approxAnchor.y = Mathf.Clamp(approxAnchor.y, 0, grid.GridSize - 1);

        Vector3 approxWorld = grid.GetAnchorWorldPosition(approxAnchor.x, approxAnchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, approxWorld);

        // If outside extended ghost distance, don't show
        if (distanceToGrid > GetDisplayedCellWorldSize() * extendedGhostDistance) return false;

        // Prefer exact approx position if placeable
        if (grid.CanPlace(blockData, approxAnchor.x, approxAnchor.y))
        {
            desiredAnchor = approxAnchor;
            return true;
        }

        // Next try nearby placements (1 cell radius)
        Vector2Int nearby;
        if (TryFindNearbyPlacement(approxAnchor, out nearby))
        {
            desiredAnchor = nearby;
            return true;
        }

        // Finally try best placement anchor within snap rules
        Vector2Int best;
        if (TryFindBestPlacementAnchor(blockCenterWorld, out best))
        {
            desiredAnchor = best;
            return true;
        }

        return false;
    }

    private bool TryFindBestPlacementAnchor(Vector3 blockCenterWorld, out Vector2Int bestAnchor)
    {
        bestAnchor = new Vector2Int(-999, -999);

        if (grid == null || blockData == null)
            return false;

        // Anchor gần nhất theo rounding (more intuitive than floor bias)
        Vector2Int approxAnchor = NearestAnchorByRounding(blockCenterWorld);
        approxAnchor.x = Mathf.Clamp(approxAnchor.x, 0, grid.GridSize - 1);
        approxAnchor.y = Mathf.Clamp(approxAnchor.y, 0, grid.GridSize - 1);

        // Chỉ attempt snap khi đang gần grid (giống logic ghost)
        Vector3 approxWorld = grid.GetAnchorWorldPosition(approxAnchor.x, approxAnchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, approxWorld);

        float currentDropZoneRadius = dropZoneRadius;
        if (IsEdgeCell(approxAnchor.x, approxAnchor.y))
        {
            currentDropZoneRadius *= edgeDropZoneMultiplier;
        }

        float maxSnapWorldDistance = GetDisplayedCellWorldSize() * currentDropZoneRadius * snapReleaseDistanceMultiplier;
        if (distanceToGrid > maxSnapWorldDistance)
            return false;

        // Search quanh approx anchor để bắt các trường hợp lệch nhẹ
        int searchRadius = Mathf.Max(0, snapSearchRadius);
        float bestDist = float.MaxValue;

        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int ax = approxAnchor.x + dx;
                int ay = approxAnchor.y + dy;

                if (ax < 0 || ax >= grid.GridSize || ay < 0 || ay >= grid.GridSize)
                    continue;

                if (!grid.CanPlace(blockData, ax, ay))
                    continue;

                Vector3 aw = grid.GetAnchorWorldPosition(ax, ay);
                float d = Vector3.Distance(blockCenterWorld, aw);
                if (d > maxSnapWorldDistance)
                    continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestAnchor = new Vector2Int(ax, ay);
                }
            }
        }

        return bestAnchor.x != -999;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!debugGizmos) return;

        if (grid == null)
        {
            grid = FindObjectOfType<GridView>();
        }

        // Show the original drag/placement gizmos only when appropriate
        bool showDragGizmos = !debugGizmosOnlyWhileDragging || isDragging;
        if (showDragGizmos && grid != null && blockData != null)
        {
            Vector3 center = transform.position;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, debugPointRadius);

            Vector2Int approxAnchor = GridHelper.WorldToGrid(grid, center);
            approxAnchor.x = Mathf.Clamp(approxAnchor.x, 0, grid.GridSize - 1);
            approxAnchor.y = Mathf.Clamp(approxAnchor.y, 0, grid.GridSize - 1);

            Vector3 approxWorld = grid.GetAnchorWorldPosition(approxAnchor.x, approxAnchor.y);
            bool canApprox = grid.CanPlace(blockData, approxAnchor.x, approxAnchor.y);

            Gizmos.color = canApprox ? new Color(1f, 0.65f, 0.1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
            Gizmos.DrawWireSphere(approxWorld, debugPointRadius);
            Gizmos.DrawLine(center, approxWorld);

            float currentDropZoneRadius = dropZoneRadius;
            if (IsEdgeCell(approxAnchor.x, approxAnchor.y))
            {
                currentDropZoneRadius *= edgeDropZoneMultiplier;
            }

            float dispCell = GetDisplayedCellWorldSize();
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(approxWorld, dispCell * currentDropZoneRadius);
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.18f);
            Gizmos.DrawWireSphere(approxWorld, dispCell * extendedGhostDistance);

            // Hiển thị vùng giới hạn tối đa cho ghost (1 ô)
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(approxWorld, dispCell * maxGhostDistance);

            if (ghostVisible && ghostAnchor.x != -999)
            {
                Vector3 ghostWorld = grid.GetAnchorWorldPosition(ghostAnchor.x, ghostAnchor.y);
                bool canGhost = grid.CanPlace(blockData, ghostAnchor.x, ghostAnchor.y);
                Gizmos.color = canGhost ? new Color(0.25f, 1f, 0.25f, 1f) : new Color(0.6f, 0.6f, 0.6f, 1f);
                Gizmos.DrawWireSphere(ghostWorld, debugPointRadius * 1.1f);
            }

            Vector2Int bestAnchor;
            bool hasBestAnchor = TryFindBestPlacementAnchor(center, out bestAnchor);
            if (hasBestAnchor)
            {
                Vector3 bestWorld = grid.GetAnchorWorldPosition(bestAnchor.x, bestAnchor.y);
                Gizmos.color = new Color(0.15f, 1f, 1f, 1f);
                Gizmos.DrawWireSphere(bestWorld, debugPointRadius * 1.3f);
                Gizmos.DrawLine(approxWorld, bestWorld);

                int r = Mathf.Max(0, debugSearchRadius);
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int ax = approxAnchor.x + dx;
                        int ay = approxAnchor.y + dy;
                        if (ax < 0 || ax >= grid.GridSize || ay < 0 || ay >= grid.GridSize)
                            continue;

                        bool can = grid.CanPlace(blockData, ax, ay);
                        Vector3 w = grid.GetAnchorWorldPosition(ax, ay);
                        Gizmos.color = can ? new Color(0.2f, 1f, 0.2f, 0.22f) : new Color(1f, 0.2f, 0.2f, 0.18f);
                        Gizmos.DrawWireCube(w, Vector3.one * (dispCell * 0.9f));
                    }
                }

                if (debugGizmosShowGhostCells)
                {
                    Gizmos.color = new Color(1f, 1f, 1f, 0.18f);
                    for (int i = 0; i < 5; i++)
                        for (int j = 0; j < 5; j++)
                            if (blockData.mask[i, j] == 1)
                            {
                                int gx = bestAnchor.x + i;
                                int gy = bestAnchor.y + j;
                                if (gx < 0 || gx >= grid.GridSize || gy < 0 || gy >= grid.GridSize)
                                    continue;
                                Vector3 w = grid.GetAnchorWorldPosition(gx, gy);
                                Gizmos.DrawCube(w, Vector3.one * (dispCell * 0.85f));
                            }
                }
            }
        }

        // Draw raycast / click info for debug: last click and line to current selected
        if (debugGizmos)
        {
            Camera camLocal = cam != null ? cam : Camera.main;

            // Draw last click point and its radius (visualize effective selection area)
            if (LastClickWorld != Vector3.zero)
            {
                Gizmos.color = new Color(1f, 1f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(LastClickWorld, debugPointRadius * 1.3f);

                // Draw a larger circle to represent selection radius (use this instance's selectionRadius)
                Gizmos.color = new Color(1f, 1f, 0.2f, 0.12f);
                Gizmos.DrawWireSphere(LastClickWorld, selectionRadius);

                if (camLocal != null)
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
                    Gizmos.DrawLine(camLocal.transform.position, LastClickWorld);
                }
            }

            // Draw selection radius around this block to show its hit area
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

    Vector3 GetInputWorld()
    {
        Vector3 p = Input.mousePosition;
        p.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(p);
    }

    // Try select nearest block to click point (no physics colliders required)
    private void TrySelectNearest(Vector3 clickWorld)
    {
        var all = FindObjectsOfType<DragBlockController>();
        float bestDist = float.MaxValue;
        DragBlockController best = null;
        foreach (var c in all)
        {
            // Skip if this instance is being destroyed or has no block data
            if (c == null) continue;
            float d = Vector3.Distance(clickWorld, c.transform.position);
            if (d < bestDist && d <= c.selectionRadius)
            {
                bestDist = d;
                best = c;
            }
        }

        if (best != null)
        {
            best.BeginDragFromClick(clickWorld);
        }
    }

    // Adjust child cell transforms so their displayed size matches grid cell size
    private void AdjustCellsToGridScale()
    {
        if (grid == null) grid = FindObjectOfType<GridView>();
        if (grid == null) return;

        // target world size of a grid cell (sprite size * grid scale)
        float targetWorldCellSize = grid.CellWorldSize * grid.transform.localScale.x;

        // collect child sprite renderers (cells)
        List<Transform> cells = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) cells.Add(t);
        }

        if (cells.Count == 0) return;

        // compute current smallest non-zero spacing between cells (local space)
        float minSpacing = float.MaxValue;
        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = i + 1; j < cells.Count; j++)
            {
                float dist = Vector3.Distance(cells[i].localPosition, cells[j].localPosition);
                if (dist > 0.0001f && dist < minSpacing) minSpacing = dist;
            }
        }

        // If couldn't determine spacing (single cell), fall back to existing cellWorldSize if available
        float currentSpacing = (minSpacing == float.MaxValue) ? (cells[0].localPosition == Vector3.zero ? 1f : 1f) : minSpacing;

        // Compute positional scale factor to map local positions to grid spacing
        float posFactor = currentSpacing > 0f ? (targetWorldCellSize / currentSpacing) : 1f;

        foreach (var t in cells)
        {
            // scale position
            t.localPosition = t.localPosition * posFactor;

            // scale sprite so its displayed world size equals grid cell size
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            float baseSpriteSize = sr.sprite != null ? sr.sprite.bounds.size.x : sr.bounds.size.x;
            if (baseSpriteSize <= 0f) continue;
            float desiredLocalScale = targetWorldCellSize / baseSpriteSize;
            t.localScale = Vector3.one * desiredLocalScale;
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
        foreach (var kv in savedLocalPositions)
        {
            if (kv.Key != null) kv.Key.localPosition = kv.Value;
        }
        foreach (var kv in savedLocalScales)
        {
            if (kv.Key != null) kv.Key.localScale = kv.Value;
        }
        savedLocalPositions.Clear();
        savedLocalScales.Clear();
    }

    // Return cell displayed world size (sprite size * grid scale)
    private float GetDisplayedCellWorldSize()
    {
        if (grid == null) grid = FindObjectOfType<GridView>();
        if (grid == null) return 1f;
        return grid.CellWorldSize * Mathf.Abs(grid.transform.localScale.x);
    }

    // Compute nearest anchor using rounding (more natural mapping from world position)
    private Vector2Int NearestAnchorByRounding(Vector3 worldPos)
    {
        if (grid == null) grid = FindObjectOfType<GridView>();
        if (grid == null) return new Vector2Int(-999, -999);

        Vector3 local = grid.transform.InverseTransformPoint(worldPos);
        float offset = -(grid.GridSize - 1) * 0.5f * grid.CellWorldSize;

        float fx = (local.x - offset) / grid.CellWorldSize;
        float fy = (local.y - offset) / grid.CellWorldSize;

        int ax = Mathf.RoundToInt(fx);
        int ay = Mathf.RoundToInt(fy);

        ax = Mathf.Clamp(ax, 0, grid.GridSize - 1);
        ay = Mathf.Clamp(ay, 0, grid.GridSize - 1);
        return new Vector2Int(ax, ay);
    }
}
