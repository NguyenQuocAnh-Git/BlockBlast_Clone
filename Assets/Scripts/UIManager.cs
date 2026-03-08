using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Canvas References")]
    public Canvas gameCanvas;
    public Canvas uiCanvas;
    
    [Header("UI Anchoring")]
    public bool useSafeArea = true;
    public float uiPadding = 20f;
    
    [Header("Score UI")]
    public RectTransform currentScorePanel;
    public RectTransform highScorePanel;
    public RectTransform gameOverPanel;
    
    [Header("Background")]
    public RectTransform backgroundPanel;
    public bool scaleBackgroundWithScreen = true;
    
    private Rect safeArea;
    private Vector2 minAnchor;
    private Vector2 maxAnchor;
    
    void Start()
    {
        SetupSafeArea();
        SetupUIAnchoring();
        SetupBackgroundScaling();
    }
    
    void SetupSafeArea()
    {
        if (!useSafeArea) return;
        
        safeArea = Screen.safeArea;
        
        // Cập nhật anchors cho các UI panels
        if (currentScorePanel != null)
        {
            UpdatePanelAnchors(currentScorePanel, TextAnchor.UpperLeft);
        }
        
        if (highScorePanel != null)
        {
            UpdatePanelAnchors(highScorePanel, TextAnchor.UpperRight);
        }
        
        if (gameOverPanel != null)
        {
            UpdatePanelAnchors(gameOverPanel, TextAnchor.MiddleCenter);
        }
    }
    
    void UpdatePanelAnchors(RectTransform panel, TextAnchor anchor)
    {
        if (panel == null) return;
        
        // Lưu lại vị trí và kích thước hiện tại
        Vector2 originalPosition = panel.anchoredPosition;
        Vector2 originalSize = panel.sizeDelta;
        
        // Cập nhật anchors dựa trên safe area
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                panel.anchorMin = new Vector2(0, 1);
                panel.anchorMax = new Vector2(0, 1);
                panel.pivot = new Vector2(0, 1);
                panel.anchoredPosition = new Vector2(safeArea.xMin + uiPadding, safeArea.yMax - uiPadding);
                break;
                
            case TextAnchor.UpperRight:
                panel.anchorMin = new Vector2(1, 1);
                panel.anchorMax = new Vector2(1, 1);
                panel.pivot = new Vector2(1, 1);
                panel.anchoredPosition = new Vector2(safeArea.xMax - uiPadding, safeArea.yMax - uiPadding);
                break;
                
            case TextAnchor.MiddleCenter:
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                break;
        }
    }
    
    void SetupUIAnchoring()
    {
        // Đảm bảo UI không bị che bởi grid
        if (uiCanvas != null)
        {
            uiCanvas.sortingOrder = 10; // Luôn trên grid
        }
        
        if (gameCanvas != null)
        {
            gameCanvas.sortingOrder = 5; // Dưới UI nhưng trên game objects
        }
    }
    
    void SetupBackgroundScaling()
    {
        if (backgroundPanel == null || !scaleBackgroundWithScreen) return;
        
        // Scale background để phủ toàn màn hình
        backgroundPanel.anchorMin = Vector2.zero;
        backgroundPanel.anchorMax = Vector2.one;
        backgroundPanel.offsetMin = Vector2.zero;
        backgroundPanel.offsetMax = Vector2.zero;
        backgroundPanel.pivot = Vector2.one * 0.5f;
        backgroundPanel.anchoredPosition = Vector2.zero;
    }
    
    // Method để cập nhật khi screen orientation thay đổi
    void OnRectTransformDimensionsChange()
    {
        if (useSafeArea)
        {
            SetupSafeArea();
        }
    }
    
    // Method để đảm bảo UI luôn visible trên mọi màn hình
    public void EnsureUIVisibility()
    {
        // Kiểm tra và điều chỉnh nếu UI bị ngoài màn hình
        if (currentScorePanel != null)
        {
            RectTransform scoreRect = currentScorePanel;
            if (scoreRect.anchoredPosition.x < safeArea.xMin)
            {
                scoreRect.anchoredPosition = new Vector2(safeArea.xMin + uiPadding, scoreRect.anchoredPosition.y);
            }
        }
        
        if (highScorePanel != null)
        {
            RectTransform highScoreRect = highScorePanel;
            if (highScoreRect.anchoredPosition.x > safeArea.xMax)
            {
                highScoreRect.anchoredPosition = new Vector2(safeArea.xMax - uiPadding, highScoreRect.anchoredPosition.y);
            }
        }
    }
}
