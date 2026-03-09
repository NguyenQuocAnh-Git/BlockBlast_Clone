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

    public void CheckGameOver()
    {
        if (isGameOver || gridView == null || spawnController == null) return;

        // Nếu không còn block nào trong khu spawn, tạo mới rồi kiểm tra các block mới
        if (spawnController.transform.childCount == 0)
        {
            spawnController.SpawnNewBlocks();
        }

        // Use BlockSpawnController helper for a concise, efficient check
        if (spawnController.AnyBlockPlaceable(gridView))
        {
            return; // At least one spawnable block can be placed
        }

        TriggerGameOver();
    }

    private bool CanPlaceBlockAnywhere(BlockData block)
    {
        if (gridView == null || block == null) return false;

        int size = gridView.GridSize;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (gridView.CanPlace(block, x, y))
                {
                    return true;
                }
            }
        }

        return false;
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
        CheckGameOver();
    }
}
