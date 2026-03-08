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
    public float extendedGhostDistance = 4.0f; // Khoảng cách mở rộng để vẫn hiện ghost khi ra ngoài grid
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

    // Public property để truy cập blockData
    public BlockData BlockData => blockData;
    
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

    void OnMouseDown()
    {
        isDragging = true;
        isSelected = true;
        startInput = GetInputWorld();
        startBlock = transform.position;
    }

    void Update()
    {
        // Handle scale animation
        if (isSelected)
        {
            Vector3 targetScale = originalScale * selectedScale;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            
            // Smooth Y offset khi hover
            float targetYOffset = hoverYOffset;
            currentYOffset = Mathf.Lerp(currentYOffset, targetYOffset, Time.deltaTime * yOffsetSpeed);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, originalScale, Time.deltaTime * scaleSpeed);
            
            // Smooth Y offset trở về 0 khi không hover
            currentYOffset = Mathf.Lerp(currentYOffset, 0f, Time.deltaTime * yOffsetSpeed);
        }

        if (!isDragging) return;

        Vector3 mouseWorld = GetInputWorld();
        Vector3 freeMove =
            startBlock + (mouseWorld - startInput) * dragMultiplier;

        // Thêm Y offset vào vị trí block
        freeMove.y += currentYOffset;
        
        // Smooth movement đến target position
        targetPosition = freeMove;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);

        // Dùng block center để tính anchor cho ghost
        Vector3 blockCenterWorld = transform.position;
        Vector2Int anchor =
            GridHelper.WorldToGrid(grid, blockCenterWorld);

        // Clamp anchor trong grid bounds
        anchor.x = Mathf.Clamp(anchor.x, 0, grid.GridSize - 1);
        anchor.y = Mathf.Clamp(anchor.y, 0, grid.GridSize - 1);

        // Kiểm tra khoảng cách đến grid để hiển thị ghost
        Vector3 anchorWorldPos = grid.GetAnchorWorldPosition(anchor.x, anchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, anchorWorldPos);
        
        
        // Kiểm tra xem block có quá xa grid không (quá 1 ô)
        bool tooFarFromGrid = distanceToGrid > (grid.CellWorldSize * maxGhostDistance);
        
        // Nếu quá xa, xóa ghost và không cho phép đặt
        if (tooFarFromGrid)
        {
            if (ghostVisible)
            {
                grid.ClearGhost();
                ghostVisible = false;
                ghostAnchor = new Vector2Int(-999, -999);
            }
            return;
        }
        
        // Kiểm tra xem có phải ô rìa không và tính drop zone radius tương ứng
        float currentDropZoneRadius = dropZoneRadius;
        if (IsEdgeCell(anchor.x, anchor.y))
        {
            currentDropZoneRadius *= edgeDropZoneMultiplier;
        }
        
        // Mở rộng phạm vi detect - dùng drop zone radius
        bool nearGrid = distanceToGrid < (grid.CellWorldSize * currentDropZoneRadius);

        // Update ghost chỉ khi đang ở gần grid
        if (nearGrid)
        {
            bool canPlaceBlock = grid.CanPlace(blockData, anchor.x, anchor.y);
            
            if (canPlaceBlock)
            {
                grid.ShowGhost(blockData, anchor.x, anchor.y, blockColor);
                ghostVisible = true;
                ghostAnchor = anchor; // Ghost luôn là trạng thái hiện tại
            }
            else
            {
                // Tìm kiếm khe hở trong phạm vi 1 ô
                Vector2Int nearbyAnchor;
                bool foundNearbyPlacement = TryFindNearbyPlacement(anchor, out nearbyAnchor);
                
                if (foundNearbyPlacement)
                {
                    grid.ShowGhost(blockData, nearbyAnchor.x, nearbyAnchor.y, blockColor);
                    ghostVisible = true;
                    ghostAnchor = nearbyAnchor;
                }
                else
                {
                    grid.ClearGhost();
                    ghostVisible = false;
                    ghostAnchor = new Vector2Int(-999, -999);
                }
            }
        }
        else
        {
            // Clear ghost khi ra ngoài vùng gần grid
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
        isDragging = false;
        isSelected = false;

        Vector3 blockCenterWorld = transform.position;
        
        // Kiểm tra khoảng cách đến grid trước khi làm gì khác
        Vector2Int currentAnchor = GridHelper.WorldToGrid(grid, blockCenterWorld);
        currentAnchor.x = Mathf.Clamp(currentAnchor.x, 0, grid.GridSize - 1);
        currentAnchor.y = Mathf.Clamp(currentAnchor.y, 0, grid.GridSize - 1);
        
        Vector3 anchorWorldPos = grid.GetAnchorWorldPosition(currentAnchor.x, currentAnchor.y);
        float distanceToGrid = Vector3.Distance(blockCenterWorld, anchorWorldPos);
        
        // Nếu quá xa grid (quá 1 ô), trả về spawn area
        if (distanceToGrid > (grid.CellWorldSize * maxGhostDistance))
        {
            ReturnToSpawnPosition();
            return;
        }

        // Nếu ghost đang hiện, coi như block chắc chắn sẽ đặt vào đúng ghost đó
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
        }
        else
        {
            // Ghost không hiện -> thử tìm kiếm khe hở gần nhất trước khi dùng logic cũ
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
            
            // Nếu không tìm thấy khe hở, dùng logic cũ
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

            
            // Trả về vị trí ban đầu
            ReturnToSpawnPosition();
        }
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

    private bool TryFindBestPlacementAnchor(Vector3 blockCenterWorld, out Vector2Int bestAnchor)
    {
        bestAnchor = new Vector2Int(-999, -999);

        if (grid == null || blockData == null)
            return false;

        // Anchor gần nhất theo rounding
        Vector2Int approxAnchor = GridHelper.WorldToGrid(grid, blockCenterWorld);
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

        float maxSnapWorldDistance = grid.CellWorldSize * currentDropZoneRadius * snapReleaseDistanceMultiplier;
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
        if (debugGizmosOnlyWhileDragging && !isDragging) return;

        if (grid == null)
        {
            grid = FindObjectOfType<GridView>();
        }

        if (grid == null || blockData == null) return;

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

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(approxWorld, grid.CellWorldSize * currentDropZoneRadius);
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.18f);
        Gizmos.DrawWireSphere(approxWorld, grid.CellWorldSize * extendedGhostDistance);
        
        // Hiển thị vùng giới hạn tối đa cho ghost (1 ô)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(approxWorld, grid.CellWorldSize * maxGhostDistance);

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
                    Gizmos.DrawWireCube(w, Vector3.one * (grid.CellWorldSize * 0.9f));
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
                            Gizmos.DrawCube(w, Vector3.one * (grid.CellWorldSize * 0.85f));
                        }
            }
        }
    }

    Vector3 GetInputWorld()
    {
        Vector3 p = Input.mousePosition;
        p.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(p);
    }
}
