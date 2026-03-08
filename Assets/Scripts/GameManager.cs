using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game State")]
    public static GameManager Instance;
    public bool isGameOver = false;

    [Header("Score")]
    public TMP_Text currentScoreText;
    public TMP_Text highScoreText;
    public int pointsPerCell = 1;
    public int pointsPerLine = 10;
    public int comboBonusPerExtraLine = 5;

    private int currentScore;
    private int highScore;
    private const string HighScoreKey = "HIGH_SCORE";

    [Header("Haptics")]
    public bool enableVibration = true;
    public int vibrationBaseMs = 18;
    public int vibrationExtraMsPerCombo = 14;
    public int vibrationRepeatsMax = 3;
    public float vibrationRepeatInterval = 0.04f;

    private Coroutine vibrationRoutine;

    [Header("UI References")]
    public GameObject gameOverPanel;
    public Button restartButton;
    public TMP_Text gameOverText;
    
    [Header("Responsive UI")]
    public UIManager uiManager;
    public ResponsiveCamera responsiveCamera;

    [Header("Game Components")]
    private GridView gridView;
    private BlockSpawnController spawnController;
    private MapGenerator mapGenerator;
    
    [Header("Performance Optimization")]
    private Dictionary<string, List<Vector2Int>> _blockPlacementCache = new Dictionary<string, List<Vector2Int>>();
    private string _lastGridHash = "";
    private bool _isCheckingGameOver = false;
    private Coroutine _deferredGameOverCheck;
    private Coroutine _deferredSpawnRoutine;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Giới hạn FPS ở mức 60
        Application.targetFrameRate = 60;
        
        // Create ComboManager if it doesn't exist
        if (ComboManager.Instance == null)
        {
            GameObject comboManagerObj = new GameObject("ComboManager");
            comboManagerObj.AddComponent<ComboManager>();
        }
        
        // Get references
        gridView = FindObjectOfType<GridView>();
        spawnController = FindObjectOfType<BlockSpawnController>();
        mapGenerator = FindObjectOfType<MapGenerator>();
        
        // Get responsive components
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();
            
        if (responsiveCamera == null)
            responsiveCamera = FindObjectOfType<ResponsiveCamera>();
        
        // Setup UI
        SetupUI();
        
        // Hide game over panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        LoadScores();
        ResetCurrentScore();
        
        // Generate random map khi game bắt đầu
        GenerateInitialMap();
    }
    
    // Generate initial random map
    private void GenerateInitialMap()
    {
        if (mapGenerator != null)
        {
            // Delay một chút để đảm bảo GridView đã khởi tạo hoàn toàn
            Invoke("DelayedGenerateMap", 0.1f);
        }
        else
        {
        }
    }
    
    // Delayed map generation
    private void DelayedGenerateMap()
    {
        if (mapGenerator != null)
        {
            mapGenerator.GenerateRandomMap();
        }
    }

    void SetupUI()
    {
        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }

        // Setup game over text
        if (gameOverText != null)
        {
            gameOverText.text = "Game Over!";
        }

        UpdateScoreUI();
    }

    public void OnLinesCleared(int combo)
    {
        if (!enableVibration) return;
        if (combo <= 0) return;

        if (vibrationRoutine != null)
        {
            StopCoroutine(vibrationRoutine);
            vibrationRoutine = null;
        }

        vibrationRoutine = StartCoroutine(VibrateCoroutine(combo));
    }

    private IEnumerator VibrateCoroutine(int combo)
    {
        int repeats = Mathf.Clamp(combo, 1, Mathf.Max(1, vibrationRepeatsMax));
        int durationMs = Mathf.Max(1, vibrationBaseMs + (Mathf.Max(0, combo - 1) * vibrationExtraMsPerCombo));

        for (int i = 0; i < repeats; i++)
        {
            Vibrate(durationMs, combo);
            if (i < repeats - 1)
            {
                yield return new WaitForSeconds(vibrationRepeatInterval);
            }
        }

        vibrationRoutine = null;
    }

    private void Vibrate(int durationMs, int combo)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                using (AndroidJavaObject vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) { Handheld.Vibrate(); return; }

                    int sdkInt;
                    using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        sdkInt = version.GetStatic<int>("SDK_INT");
                    }

                    if (sdkInt >= 26)
                    {
                        int amplitude = Mathf.Clamp(80 + combo * 40, 1, 255);
                        using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)durationMs, amplitude))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", (long)durationMs);
                    }
                }
            }
        }
        catch
        {
            Handheld.Vibrate();
        }
#else
        Handheld.Vibrate();
#endif
    }

    private void LoadScores()
    {
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    private void SaveHighScore()
    {
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();
    }

    private void ResetCurrentScore()
    {
        currentScore = 0;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (currentScoreText != null)
        {
            currentScoreText.text = currentScore.ToString();
        }

        if (highScoreText != null)
        {
            highScoreText.text = highScore.ToString();
        }
    }

    public void AddScoreForPlacement(int placedCells, int clearedRows, int clearedCols)
    {
        int lines = clearedRows + clearedCols;
        int add = Mathf.Max(0, placedCells) * Mathf.Max(0, pointsPerCell) + lines * Mathf.Max(0, pointsPerLine);
        if (lines > 1)
        {
            add += (lines - 1) * Mathf.Max(0, comboBonusPerExtraLine);
        }

        if (add <= 0) return;

        currentScore += add;
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
        }

        UpdateScoreUI();
    }
    
    // Lấy điểm số hiện tại cho SmartBlockSpawner
    public int GetCurrentScore()
    {
        return currentScore;
    }

    // OPTIMIZED: Kiểm tra game over với caching và early exit
    private void CheckGameOverOptimized()
    {
        if (isGameOver) return;

        // Lấy tất cả blocks hiện tại trong spawn area
        if (spawnController != null)
        {
            bool canPlaceAnyBlock = false;
            
            // Update placement cache if grid changed
            UpdatePlacementCache();

            // Kiểm tra mỗi block trong spawn area với cached results
            foreach (Transform blockTransform in spawnController.transform)
            {
                DragBlockController dragController = blockTransform.GetComponent<DragBlockController>();
                if (dragController != null && dragController.BlockData != null)
                {
                    // Sử dụng cached placement check
                    if (CanPlaceBlockAnywhereOptimized(dragController.BlockData))
                    {
                        canPlaceAnyBlock = true;
                        break;
                    }
                }
            }

            // Nếu không thể đặt block nào -> Game Over
            if (!canPlaceAnyBlock)
            {
                TriggerGameOver();
            }
        }
    }
    
    // Legacy method for compatibility
    public void CheckGameOver()
    {
        CheckGameOverOptimized();
    }

    // OPTIMIZED: Kiểm tra xem có thể đặt block ở bất kỳ vị trí nào không với caching
    private bool CanPlaceBlockAnywhereOptimized(BlockData block)
    {
        if (gridView == null) return false;
        
        // Tạo hash cho block shape
        string blockHash = GetBlockHash(block);
        
        // Check cache first
        if (_blockPlacementCache.ContainsKey(blockHash))
        {
            var validPositions = _blockPlacementCache[blockHash];
            if (validPositions.Count > 0)
            {
                // Verify first cached position is still valid
                var firstPos = validPositions[0];
                if (gridView.CanPlace(block, firstPos.x, firstPos.y))
                {
                    return true;
                }
                else
                {
                    // Cache invalid, remove and recalculate
                    _blockPlacementCache.Remove(blockHash);
                }
            }
        }
        
        // If not in cache or cache invalid, calculate with early exit
        List<Vector2Int> calculatedPositions = new List<Vector2Int>();
        
        // Smart scanning: prioritize center and edges first
        var scanOrder = GetSmartScanOrder();
        
        foreach (var pos in scanOrder)
        {
            if (gridView.CanPlace(block, pos.x, pos.y))
            {
                calculatedPositions.Add(pos);
                // Early exit after finding first valid position
                if (calculatedPositions.Count >= 3) // Cache first 3 valid positions
                {
                    break;
                }
            }
        }
        
        // Cache results
        _blockPlacementCache[blockHash] = calculatedPositions;
        
        return calculatedPositions.Count > 0;
    }
    
    // Legacy method for compatibility
    private bool CanPlaceBlockAnywhere(BlockData block)
    {
        return CanPlaceBlockAnywhereOptimized(block);
    }
    
    /// <summary>
    /// Create hash for block shape to enable caching
    /// </summary>
    private string GetBlockHash(BlockData block)
    {
        if (block == null || block.mask == null) return "empty";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                sb.Append(block.mask[i, j]);
            }
        }
        return sb.ToString();
    }
    
    /// <summary>
    /// Smart scan order: prioritize center and edges for faster placement
    /// </summary>
    private List<Vector2Int> GetSmartScanOrder()
    {
        List<Vector2Int> order = new List<Vector2Int>();
        int gridSize = gridView.GridSize;
        int center = gridSize / 2;
        
        // Add center positions first
        for (int x = center - 1; x <= center + 1; x++)
        {
            for (int y = center - 1; y <= center + 1; y++)
            {
                if (x >= 0 && x < gridSize && y >= 0 && y < gridSize)
                {
                    order.Add(new Vector2Int(x, y));
                }
            }
        }
        
        // Add edges
        for (int i = 0; i < gridSize; i++)
        {
            order.Add(new Vector2Int(i, 0)); // Bottom
            order.Add(new Vector2Int(i, gridSize - 1)); // Top
            order.Add(new Vector2Int(0, i)); // Left
            order.Add(new Vector2Int(gridSize - 1, i)); // Right
        }
        
        // Add remaining positions
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!order.Contains(pos))
                {
                    order.Add(pos);
                }
            }
        }
        
        return order;
    }
    
    /// <summary>
    /// Update placement cache when grid changes
    /// </summary>
    private void UpdatePlacementCache()
    {
        if (gridView == null) return;
        
        string currentGridHash = CalculateGridHash();
        if (currentGridHash != _lastGridHash)
        {
            // Grid changed, clear cache
            _blockPlacementCache.Clear();
            _lastGridHash = currentGridHash;
        }
    }
    
    /// <summary>
    /// Calculate hash of current grid state
    /// </summary>
    private string CalculateGridHash()
    {
        if (gridView == null) return "";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int x = 0; x < gridView.GridSize; x++)
        {
            for (int y = 0; y < gridView.GridSize; y++)
            {
                sb.Append(gridView.IsCellOccupied(x, y) ? "1" : "0");
            }
        }
        return sb.ToString();
    }

    // Kích hoạt game over
    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // Show game over UI
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Disable dragging cho tất cả blocks
        DisableAllDragging();
    }

    // Disable dragging cho tất cả blocks
    private void DisableAllDragging()
    {
        DragBlockController[] allDragControllers = FindObjectsOfType<DragBlockController>();
        foreach (DragBlockController dragController in allDragControllers)
        {
            dragController.enabled = false;
        }
    }

    // Restart game
    public void RestartGame()
    {

        // Reset game state
        isGameOver = false;

        ResetCurrentScore();

        // Clear grid trước khi generate map mới
        if (gridView != null)
        {
            gridView.ClearGrid();
        }

        // Generate new random map (sẽ thêm obstacles vào empty grid)
        GenerateInitialMap();

        // Clear old blocks trong spawn area trước
        ClearSpawnAreaBlocks();

        // Spawn new blocks
        if (spawnController != null)
        {
            spawnController.SpawnNewBlocks();
        }

        // Enable dragging lại
        EnableAllDragging();

        // Hide game over UI
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
    
    // Clear tất cả blocks trong spawn area
    private void ClearSpawnAreaBlocks()
    {
        if (spawnController != null)
        {
            spawnController.ClearAllBlocks();
        }
    }

    // Enable dragging cho tất cả blocks
    private void EnableAllDragging()
    {
        DragBlockController[] allDragControllers = FindObjectsOfType<DragBlockController>();
        foreach (DragBlockController dragController in allDragControllers)
        {
            dragController.enabled = true;
        }
    }

    // Public method để các script khác có thể gọi - OPTIMIZED
    public void OnBlockPlaced()
    {
        // Start performance monitoring
        BlockPerformanceMonitor.Instance?.StartTiming("BlockPlacement");
        
        // Cancel previous deferred operations
        if (_deferredGameOverCheck != null)
        {
            StopCoroutine(_deferredGameOverCheck);
            _deferredGameOverCheck = null;
        }
        
        if (_deferredSpawnRoutine != null)
        {
            StopCoroutine(_deferredSpawnRoutine);
            _deferredSpawnRoutine = null;
        }
        
        // Start deferred game over check (longer delay to avoid conflict)
        _deferredGameOverCheck = StartCoroutine(DeferredGameOverCheck());
        
        // End performance monitoring
        BlockPerformanceMonitor.Instance?.EndTiming("BlockPlacement");
    }
    
    /// <summary>
    /// Deferred game over check with performance optimization
    /// </summary>
    private IEnumerator DeferredGameOverCheck()
    {
        // Start performance monitoring
        BlockPerformanceMonitor.Instance?.StartTiming("GameOverCheck");
        
        // Wait longer to ensure block placement is complete and avoid spawn conflicts
        yield return new WaitForSeconds(0.8f);
        
        if (isGameOver || _isCheckingGameOver) yield break;
        
        _isCheckingGameOver = true;
        
        // Check if we need to spawn new blocks first
        if (spawnController != null)
        {
            int remainingBlocks = spawnController.transform.childCount;
            
            // If no blocks remaining, spawn new blocks first
            if (remainingBlocks == 0)
            {
                // Start deferred spawn
                _deferredSpawnRoutine = StartCoroutine(DeferredSpawnNewBlocks());
                
                // Wait for spawn to complete before checking game over
                yield return new WaitForSeconds(0.3f);
            }
        }
        
        // Now check game over with optimized method
        CheckGameOverOptimized();
        
        _isCheckingGameOver = false;
        _deferredGameOverCheck = null;
        
        // End performance monitoring
        BlockPerformanceMonitor.Instance?.EndTiming("GameOverCheck");
    }
    
    /// <summary>
    /// Deferred spawn to avoid blocking main thread
    /// </summary>
    private IEnumerator DeferredSpawnNewBlocks()
    {
        // Start performance monitoring
        BlockPerformanceMonitor.Instance?.StartTiming("BlockSpawn");
        
        // Small delay to ensure smooth transition
        yield return new WaitForSeconds(0.1f);
        
        if (spawnController != null)
        {
            spawnController.SpawnNewBlocks();
        }
        
        _deferredSpawnRoutine = null;
        
        // End performance monitoring
        BlockPerformanceMonitor.Instance?.EndTiming("BlockSpawn");
    }
}
