using UnityEngine;

public class ResponsiveCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public bool maintainAspectRatio = true;
    public float targetAspectRatio = 9f / 16f; // Portrait mode cho mobile
    
    [Header("Grid Scaling")]
    public GridView gridView;
    public BlockSpawnController spawnController;
    
    [Header("Background")]
    public Transform backgroundTransform;
    public bool scaleBackgroundToFit = true;
    
    private float currentAspectRatio;
    private Vector3 originalBackgroundScale;
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        if (backgroundTransform != null)
            originalBackgroundScale = backgroundTransform.localScale;
            
        UpdateCameraSettings();
    }
    
    void Update()
    {
        // Kiểm tra thay đổi aspect ratio
        float newAspectRatio = (float)Screen.width / Screen.height;
        if (Mathf.Abs(newAspectRatio - currentAspectRatio) > 0.01f)
        {
            currentAspectRatio = newAspectRatio;
            UpdateCameraSettings();
        }
    }
    
    void UpdateCameraSettings()
    {
        if (mainCamera == null) return;
        
        currentAspectRatio = (float)Screen.width / Screen.height;
        
        if (maintainAspectRatio)
        {
            // Tính toán orthographic size để maintain aspect ratio
            float targetHeight = mainCamera.orthographicSize * 2f;
            float targetWidth = targetHeight * targetAspectRatio;
            float currentWidth = targetHeight * currentAspectRatio;
            
            if (currentWidth > targetWidth)
            {
                // Màn hình rộng hơn target - điều chỉnh height
                mainCamera.orthographicSize = (targetWidth / currentAspectRatio) / 2f;
            }
            else
            {
                // Màn hình hẹp hơn target - giữ nguyên
                mainCamera.orthographicSize = targetHeight / 2f;
            }
        }
        
        // Update grid scaling
        UpdateGridScaling();
        
        // Update background scaling
        UpdateBackgroundScaling();
    }
    
    void UpdateGridScaling()
    {
        if (gridView == null) return;
        
        // Force grid to re-fit to screen
        gridView.FitToScreen();
    }
    
    void UpdateBackgroundScaling()
    {
        if (backgroundTransform == null || !scaleBackgroundToFit) return;
        
        // Scale background để phủ toàn màn hình
        float screenHeight = mainCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * currentAspectRatio;
        
        // Tính scale factor để background phủ màn hình
        float scaleX = screenWidth / 10f; // Giả sử background gốc width = 10
        float scaleY = screenHeight / 10f; // Giả sử background gốc height = 10
        
        float maxScale = Mathf.Max(scaleX, scaleY);
        backgroundTransform.localScale = originalBackgroundScale * maxScale;
        
        // Đặt background ở vị trí trung tâm
        backgroundTransform.position = Vector3.zero;
    }
    
    // Method để lấy screen dimensions cho các object khác
    public Vector2 GetScreenDimensions()
    {
        if (mainCamera == null) return Vector2.zero;
        
        float height = mainCamera.orthographicSize * 2f;
        float width = height * currentAspectRatio;
        
        return new Vector2(width, height);
    }
    
    // Method để convert world position sang screen position
    public Vector2 WorldToScreenPoint(Vector3 worldPosition)
    {
        if (mainCamera == null) return Vector2.zero;
        return mainCamera.WorldToScreenPoint(worldPosition);
    }
    
    // Method để convert screen position sang world position
    public Vector2 ScreenToWorldPoint(Vector2 screenPosition)
    {
        if (mainCamera == null) return Vector2.zero;
        return mainCamera.ScreenToWorldPoint(screenPosition);
    }
}
