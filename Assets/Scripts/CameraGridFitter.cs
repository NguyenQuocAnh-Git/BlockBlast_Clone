using UnityEngine;

public class CameraGridFitter : MonoBehaviour
{
    [Header("Grid Settings")]
    public GridView gridView;
    public float paddingPercentage = 0.1f; // 10% padding around grid
    
    [Header("Camera Settings")]
    public Camera targetCamera;
    public bool maintainAspectRatio = true;
    public float targetAspectRatio = 9f / 16f; // Default portrait mode
    
    [Header("Debug")]
    public bool debugMode = false;
    
    private float gridWidth;
    private float gridHeight;
    private Vector2 gridBounds;
    
    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
            
        if (targetCamera == null)
            targetCamera = Camera.main;
            
        if (gridView == null)
            gridView = FindObjectOfType<GridView>();
    }
    
    void Start()
    {
        // Delay một chút để đảm bảo grid đã được khởi tạo
        Invoke("FitCameraToGrid", 0.1f);
    }
    
    void Update()
    {
        // Kiểm tra thay đổi screen resolution hoặc aspect ratio
        if (Screen.width != 0 && Screen.height != 0)
        {
            float currentAspectRatio = (float)Screen.width / Screen.height;
            if (Mathf.Abs(currentAspectRatio - targetAspectRatio) > 0.01f)
            {
                FitCameraToGrid();
            }
        }
    }
    
    /// <summary>
    /// Điều chỉnh camera để hiển thị toàn bộ grid với padding
    /// </summary>
    public void FitCameraToGrid()
    {
        if (gridView == null || targetCamera == null)
        {
            
            return;
        }
        
        // Lấy kích thước thực tế của grid trong world space
        CalculateGridBounds();
        
        if (gridBounds.x <= 0 || gridBounds.y <= 0)
        {
            
            return;
        }
        
        // Tính toán orthographic size phù hợp
        float requiredOrthographicSize = CalculateRequiredOrthographicSize();
        
        // Áp dụng cho camera
        targetCamera.orthographicSize = requiredOrthographicSize;
        
        // Cập nhật aspect ratio nếu cần
        if (maintainAspectRatio)
        {
            UpdateAspectRatio();
        }
        
        if (debugMode)
        {
            float paddedWidth = gridBounds.x * (1f + paddingPercentage);
            float currentAspectRatio = maintainAspectRatio ? targetAspectRatio : (float)Screen.width / Screen.height;
            float visibleWidth = requiredOrthographicSize * 2f * currentAspectRatio;
            float visibleHeight = requiredOrthographicSize * 2f;
            
            
            
            
            
            
            
            
            
            
        }
    }
    
    /// <summary>
    /// Tính toán kích thước thực tế của grid
    /// </summary>
    private void CalculateGridBounds()
    {
        if (gridView == null) return;
        
        // Lấy cell world size từ grid
        float cellSize = gridView.CellWorldSize;
        int gridSize = gridView.GridSize;
        
        // Tính toán kích thước grid trong world space
        gridWidth = gridSize * cellSize;
        gridHeight = gridSize * cellSize;
        
        // Lấy vào account scale của grid transform
        Vector3 gridScale = gridView.transform.localScale;
        gridWidth *= gridScale.x;
        gridHeight *= gridScale.y;
        
        gridBounds = new Vector2(gridWidth, gridHeight);
        
        if (debugMode)
        {
            
        }
    }
    
    /// <summary>
    /// Tính toán orthographic size cần thiết để fit chiều rộng camera với chiều rộng padded grid
    /// </summary>
    private float CalculateRequiredOrthographicSize()
    {
        // Thêm padding vào kích thước grid
        float paddedWidth = gridBounds.x * (1f + paddingPercentage);
        
        // Lấy aspect ratio hiện tại của camera (hoặc target aspect ratio nếu maintain)
        float cameraAspectRatio = maintainAspectRatio ? targetAspectRatio : (float)Screen.width / Screen.height;
        
        // Tính orthographic size dựa trên chiều rộng - đây là key change!
        // Thay vì fit cả width và height, chỉ fit width và để height tự động
        float orthographicSize = paddedWidth / (2f * cameraAspectRatio);
        
        return orthographicSize;
    }
    
    /// <summary>
    /// Cập nhật aspect ratio của camera - chỉ fit chiều rộng
    /// </summary>
    private void UpdateAspectRatio()
    {
        if (!maintainAspectRatio) return;
        
        // Chỉ fit chiều rộng với target aspect ratio
        float paddedWidth = gridBounds.x * (1f + paddingPercentage);
        float orthographicSize = paddedWidth / (2f * targetAspectRatio);
        
        targetCamera.orthographicSize = orthographicSize;
        
        if (debugMode)
        {
            
        }
    }
    
    /// <summary>
    /// Method public để gọi từ Inspector hoặc script khác
    /// </summary>
    [ContextMenu("Fit Camera to Grid")]
    public void FitCameraToGridNow()
    {
        FitCameraToGrid();
    }
    
    /// <summary>
    /// Set padding percentage
    /// </summary>
    public void SetPadding(float percentage)
    {
        paddingPercentage = Mathf.Clamp01(percentage);
        FitCameraToGrid();
    }
    
    /// <summary>
    /// Set target aspect ratio
    /// </summary>
    public void SetTargetAspectRatio(float aspectRatio)
    {
        targetAspectRatio = aspectRatio;
        FitCameraToGrid();
    }
    
    void OnValidate()
    {
        // Validate values trong Inspector
        paddingPercentage = Mathf.Clamp01(paddingPercentage);
        targetAspectRatio = Mathf.Max(0.1f, targetAspectRatio);
    }
    
    void OnDrawGizmosSelected()
    {
        if (!debugMode || gridView == null) return;
        
        // Vẽ grid bounds trong scene view
        Gizmos.color = Color.yellow;
        Vector3 center = gridView.transform.position;
        Vector3 size = new Vector3(gridBounds.x, gridBounds.y, 0.1f);
        Gizmos.DrawWireCube(center, size);
        
        // Vẽ padded bounds (chỉ quan tâm chiều rộng)
        Gizmos.color = Color.cyan;
        float paddedWidth = gridBounds.x * (1f + paddingPercentage);
        Vector3 paddedSize = new Vector3(paddedWidth, gridBounds.y, 0.1f);
        Gizmos.DrawWireCube(center, paddedSize);
        
        // Vẽ camera view bounds
        if (targetCamera != null)
        {
            Gizmos.color = Color.green;
            float camHeight = targetCamera.orthographicSize * 2f;
            float camWidth = camHeight * targetCamera.aspect;
            Vector3 camSize = new Vector3(camWidth, camHeight, 0.1f);
            Gizmos.DrawWireCube(targetCamera.transform.position, camSize);
            
            // Vẽ vertical lines để so sánh chiều rộng
            Gizmos.color = Color.red;
            // Camera edges
            Vector3 camLeft = targetCamera.transform.position + new Vector3(-camWidth/2, 0, 0);
            Vector3 camRight = targetCamera.transform.position + new Vector3(camWidth/2, 0, 0);
            // Padded grid edges
            Vector3 paddedLeft = center + new Vector3(-paddedWidth/2, 0, 0);
            Vector3 paddedRight = center + new Vector3(paddedWidth/2, 0, 0);
            
            // Vẽ vertical lines
            Gizmos.DrawLine(camLeft + Vector3.up * 2, camLeft + Vector3.down * 2);
            Gizmos.DrawLine(camRight + Vector3.up * 2, camRight + Vector3.down * 2);
            Gizmos.DrawLine(paddedLeft + Vector3.up * 1.5f, paddedLeft + Vector3.down * 1.5f);
            Gizmos.DrawLine(paddedRight + Vector3.up * 1.5f, paddedRight + Vector3.down * 1.5f);
            
            // Vẽ points tại edges
            Gizmos.DrawSphere(camLeft, 0.05f);
            Gizmos.DrawSphere(camRight, 0.05f);
            Gizmos.DrawSphere(paddedLeft, 0.03f);
            Gizmos.DrawSphere(paddedRight, 0.03f);
            
            // Vẽ horizontal line để show width difference
            if (Mathf.Abs(camWidth - paddedWidth) > 0.1f)
            {
                Gizmos.color = Color.magenta;
                Vector3 startLine = new Vector3(Mathf.Min(camLeft.x, paddedLeft.x), center.y - 2, 0);
                Vector3 endLine = new Vector3(Mathf.Max(camRight.x, paddedRight.x), center.y - 2, 0);
                Gizmos.DrawLine(startLine, endLine);
            }
        }
    }
}
