using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Performance Monitor - Theo dõi hiệu năng mà không dùng debug logs
/// Cung cấp thống kê performance cho việc tối ưu hóa
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    [Header("Performance Monitoring")]
    public bool enableMonitoring = true;
    public int sampleSize = 100; // Số samples cho thống kê
    public float reportInterval = 5f; // Giây giữa các báo cáo
    
    // Performance metrics
    private Queue<float> frameTimeSamples = new Queue<float>();
    private Queue<float> blockSpawnTimeSamples = new Queue<float>();
    private Queue<float> gridAnalysisTimeSamples = new Queue<float>();
    
    // Timing
    private float lastReportTime = 0f;
    private Stopwatch currentFrameTimer = new Stopwatch();
    private Stopwatch currentBlockSpawnTimer = new Stopwatch();
    private Stopwatch currentGridAnalysisTimer = new Stopwatch();
    
    // Statistics
    private PerformanceStats currentStats = new PerformanceStats();
    
    // Singleton pattern
    private static PerformanceMonitor _instance;
    public static PerformanceMonitor Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PerformanceMonitor>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("PerformanceMonitor");
                    _instance = go.AddComponent<PerformanceMonitor>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        lastReportTime = Time.time;
        currentFrameTimer.Start();
    }
    
    void Update()
    {
        if (!enableMonitoring) return;
        
        // Record frame time
        currentFrameTimer.Stop();
        float frameTime = (float)currentFrameTimer.Elapsed.TotalMilliseconds;
        AddFrameTimeSample(frameTime);
        currentFrameTimer.Restart();
        
        // Generate periodic reports
        if (Time.time - lastReportTime >= reportInterval)
        {
            UpdateStatistics();
            lastReportTime = Time.time;
        }
    }
    
    /// <summary>
    /// Bắt đầu đo thời gian cho block spawn
    /// </summary>
    public void StartBlockSpawnTimer()
    {
        if (!enableMonitoring) return;
        currentBlockSpawnTimer.Restart();
    }
    
    /// <summary>
    /// Kết thúc đo thời gian cho block spawn
    /// </summary>
    public void EndBlockSpawnTimer()
    {
        if (!enableMonitoring) return;
        currentBlockSpawnTimer.Stop();
        float spawnTime = (float)currentBlockSpawnTimer.Elapsed.TotalMilliseconds;
        AddBlockSpawnTimeSample(spawnTime);
    }
    
    /// <summary>
    /// Bắt đầu đo thời gian cho grid analysis
    /// </summary>
    public void StartGridAnalysisTimer()
    {
        if (!enableMonitoring) return;
        currentGridAnalysisTimer.Restart();
    }
    
    /// <summary>
    /// Kết thúc đo thời gian cho grid analysis
    /// </summary>
    public void EndGridAnalysisTimer()
    {
        if (!enableMonitoring) return;
        currentGridAnalysisTimer.Stop();
        float analysisTime = (float)currentGridAnalysisTimer.Elapsed.TotalMilliseconds;
        AddGridAnalysisTimeSample(analysisTime);
    }
    
    /// <summary>
    /// Thêm frame time sample
    /// </summary>
    private void AddFrameTimeSample(float frameTime)
    {
        frameTimeSamples.Enqueue(frameTime);
        if (frameTimeSamples.Count > sampleSize)
        {
            frameTimeSamples.Dequeue();
        }
    }
    
    /// <summary>
    /// Thêm block spawn time sample
    /// </summary>
    private void AddBlockSpawnTimeSample(float spawnTime)
    {
        blockSpawnTimeSamples.Enqueue(spawnTime);
        if (blockSpawnTimeSamples.Count > sampleSize)
        {
            blockSpawnTimeSamples.Dequeue();
        }
    }
    
    /// <summary>
    /// Thêm grid analysis time sample
    /// </summary>
    private void AddGridAnalysisTimeSample(float analysisTime)
    {
        gridAnalysisTimeSamples.Enqueue(analysisTime);
        if (gridAnalysisTimeSamples.Count > sampleSize)
        {
            gridAnalysisTimeSamples.Dequeue();
        }
    }
    
    /// <summary>
    /// Cập nhật thống kê performance
    /// </summary>
    private void UpdateStatistics()
    {
        currentStats = new PerformanceStats();
        
        if (frameTimeSamples.Count > 0)
        {
            currentStats.avgFrameTime = CalculateAverage(frameTimeSamples);
            currentStats.maxFrameTime = CalculateMax(frameTimeSamples);
            currentStats.minFrameTime = CalculateMin(frameTimeSamples);
            currentStats.fps = 1000f / currentStats.avgFrameTime;
        }
        
        if (blockSpawnTimeSamples.Count > 0)
        {
            currentStats.avgBlockSpawnTime = CalculateAverage(blockSpawnTimeSamples);
            currentStats.maxBlockSpawnTime = CalculateMax(blockSpawnTimeSamples);
            currentStats.minBlockSpawnTime = CalculateMin(blockSpawnTimeSamples);
        }
        
        if (gridAnalysisTimeSamples.Count > 0)
        {
            currentStats.avgGridAnalysisTime = CalculateAverage(gridAnalysisTimeSamples);
            currentStats.maxGridAnalysisTime = CalculateMax(gridAnalysisTimeSamples);
            currentStats.minGridAnalysisTime = CalculateMin(gridAnalysisTimeSamples);
        }
        
        currentStats.sampleCount = frameTimeSamples.Count;
        currentStats.lastUpdateTime = Time.time;
    }
    
    /// <summary>
    /// Tính giá trị trung bình
    /// </summary>
    private float CalculateAverage(Queue<float> samples)
    {
        if (samples.Count == 0) return 0f;
        
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample;
        }
        return sum / samples.Count;
    }
    
    /// <summary>
    /// Tính giá trị lớn nhất
    /// </summary>
    private float CalculateMax(Queue<float> samples)
    {
        if (samples.Count == 0) return 0f;
        
        float max = float.MinValue;
        foreach (float sample in samples)
        {
            if (sample > max) max = sample;
        }
        return max;
    }
    
    /// <summary>
    /// Tính giá trị nhỏ nhất
    /// </summary>
    private float CalculateMin(Queue<float> samples)
    {
        if (samples.Count == 0) return 0f;
        
        float min = float.MaxValue;
        foreach (float sample in samples)
        {
            if (sample < min) min = sample;
        }
        return min;
    }
    
    /// <summary>
    /// Lấy thống kê performance hiện tại
    /// </summary>
    public PerformanceStats GetCurrentStats()
    {
        return currentStats;
    }
    
    /// <summary>
    /// Lấy performance report dạng string
    /// </summary>
    public string GetPerformanceReport()
    {
        if (currentStats.sampleCount == 0)
        {
            return "No performance data available";
        }
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Performance Report ===");
        sb.AppendLine($"Samples: {currentStats.sampleCount}");
        sb.AppendLine($"FPS: {currentStats.fps:F1}");
        sb.AppendLine($"Frame Time: {currentStats.avgFrameTime:F2}ms (min: {currentStats.minFrameTime:F2}ms, max: {currentStats.maxFrameTime:F2}ms)");
        sb.AppendLine($"Block Spawn: {currentStats.avgBlockSpawnTime:F2}ms (min: {currentStats.minBlockSpawnTime:F2}ms, max: {currentStats.maxBlockSpawnTime:F2}ms)");
        sb.AppendLine($"Grid Analysis: {currentStats.avgGridAnalysisTime:F2}ms (min: {currentStats.minGridAnalysisTime:F2}ms, max: {currentStats.maxGridAnalysisTime:F2}ms)");
        
        // Performance warnings
        if (currentStats.avgFrameTime > 16.67f) // 60 FPS threshold
        {
            sb.AppendLine("⚠️ WARNING: Frame time exceeds 60 FPS target");
        }
        
        if (currentStats.avgBlockSpawnTime > 50f) // 50ms threshold
        {
            sb.AppendLine("⚠️ WARNING: Block spawn time is high");
        }
        
        if (currentStats.avgGridAnalysisTime > 20f) // 20ms threshold
        {
            sb.AppendLine("⚠️ WARNING: Grid analysis time is high");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Xóa tất cả samples
    /// </summary>
    public void ClearAllSamples()
    {
        frameTimeSamples.Clear();
        blockSpawnTimeSamples.Clear();
        gridAnalysisTimeSamples.Clear();
        currentStats = new PerformanceStats();
    }
    
    /// <summary>
    /// Kiểm tra xem system có đang under heavy load không
    /// </summary>
    public bool IsUnderHeavyLoad()
    {
        return currentStats.avgFrameTime > 20f || // Below 50 FPS
               currentStats.avgBlockSpawnTime > 100f || // Block spawn > 100ms
               currentStats.avgGridAnalysisTime > 50f; // Grid analysis > 50ms
    }
    
    /// <summary>
    /// Lấy performance score (0-100)
    /// </summary>
    public float GetPerformanceScore()
    {
        if (currentStats.sampleCount == 0) return 100f;
        
        float frameScore = Mathf.Clamp01(1f - (currentStats.avgFrameTime / 33.33f)) * 100f; // 30 FPS = 0 score
        float spawnScore = Mathf.Clamp01(1f - (currentStats.avgBlockSpawnTime / 200f)) * 100f; // 200ms = 0 score
        float analysisScore = Mathf.Clamp01(1f - (currentStats.avgGridAnalysisTime / 100f)) * 100f; // 100ms = 0 score
        
        return (frameScore + spawnScore + analysisScore) / 3f;
    }
    
    void OnGUI()
    {
        if (!enableMonitoring) return;
        
        // Hiển thị performance info trên screen (chỉ trong development build)
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.skin.label.fontSize = 12;
        GUI.skin.label.normal.textColor = Color.white;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"FPS: {currentStats.fps:F1}");
        GUILayout.Label($"Frame: {currentStats.avgFrameTime:F1}ms");
        GUILayout.Label($"Spawn: {currentStats.avgBlockSpawnTime:F1}ms");
        GUILayout.Label($"Analysis: {currentStats.avgGridAnalysisTime:F1}ms");
        GUILayout.Label($"Score: {GetPerformanceScore():F0}%");
        
        if (IsUnderHeavyLoad())
        {
            GUI.color = Color.red;
            GUILayout.Label("⚠️ HEAVY LOAD");
            GUI.color = Color.white;
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        #endif
    }
}

/// <summary>
/// Performance statistics data structure
/// </summary>
[System.Serializable]
public class PerformanceStats
{
    public float avgFrameTime;
    public float maxFrameTime;
    public float minFrameTime;
    public float fps;
    
    public float avgBlockSpawnTime;
    public float maxBlockSpawnTime;
    public float minBlockSpawnTime;
    
    public float avgGridAnalysisTime;
    public float maxGridAnalysisTime;
    public float minGridAnalysisTime;
    
    public int sampleCount;
    public float lastUpdateTime;
}
