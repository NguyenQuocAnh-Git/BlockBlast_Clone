using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

/// <summary>
/// Performance monitoring for block placement and spawn operations
/// Helps identify bottlenecks and optimize frame time
/// </summary>
public class BlockPerformanceMonitor : MonoBehaviour
{
    [Header("Performance Monitoring")]
    public bool enablePerformanceLogging = true;
    public bool enableDetailedLogging = false;
    public float logThresholdMs = 16.67f; // Log if operation takes longer than 1 frame (60fps)
    
    [Header("Performance Metrics")]
    [SerializeField] private float lastBlockPlacementTime = 0f;
    [SerializeField] private float lastSpawnTime = 0f;
    [SerializeField] private float lastGameOverCheckTime = 0f;
    [SerializeField] private int totalBlockPlacements = 0;
    [SerializeField] private int totalSpawns = 0;
    [SerializeField] private int totalGameOverChecks = 0;
    
    // Performance tracking
    private Dictionary<string, List<float>> _performanceHistory = new Dictionary<string, List<float>>();
    private Stopwatch _stopwatch = new Stopwatch();
    
    // Singleton for easy access
    public static BlockPerformanceMonitor Instance { get; private set; }
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Initialize performance history
        _performanceHistory["BlockPlacement"] = new List<float>();
        _performanceHistory["BlockSpawn"] = new List<float>();
        _performanceHistory["GameOverCheck"] = new List<float>();
        _performanceHistory["GridAnalysis"] = new List<float>();
        _performanceHistory["ShapeGeneration"] = new List<float>();
    }
    
    /// <summary>
    /// Start timing an operation
    /// </summary>
    public void StartTiming(string operationName)
    {
        if (!enablePerformanceLogging) return;
        
        _stopwatch.Restart();
    }
    
    /// <summary>
    /// End timing an operation and log if it exceeds threshold
    /// </summary>
    public void EndTiming(string operationName)
    {
        if (!enablePerformanceLogging) return;
        
        _stopwatch.Stop();
        float elapsedMs = (float)_stopwatch.Elapsed.TotalMilliseconds;
        
        // Track performance
        if (!_performanceHistory.ContainsKey(operationName))
        {
            _performanceHistory[operationName] = new List<float>();
        }
        _performanceHistory[operationName].Add(elapsedMs);
        
        // Keep only last 100 measurements
        if (_performanceHistory[operationName].Count > 100)
        {
            _performanceHistory[operationName].RemoveAt(0);
        }
        
        // Update specific metrics
        switch (operationName)
        {
            case "BlockPlacement":
                lastBlockPlacementTime = elapsedMs;
                totalBlockPlacements++;
                break;
            case "BlockSpawn":
                lastSpawnTime = elapsedMs;
                totalSpawns++;
                break;
            case "GameOverCheck":
                lastGameOverCheckTime = elapsedMs;
                totalGameOverChecks++;
                break;
        }
        
        // Log if exceeds threshold
        if (elapsedMs > logThresholdMs)
        {
            UnityEngine.Debug.LogWarning($"[PERF] {operationName} took {elapsedMs:F2}ms (threshold: {logThresholdMs:F2}ms)");
            
            if (enableDetailedLogging)
            {
                LogDetailedPerformance(operationName, elapsedMs);
            }
        }
    }
    
    /// <summary>
    /// Log detailed performance information
    /// </summary>
    private void LogDetailedPerformance(string operationName, float elapsedMs)
    {
        var history = _performanceHistory[operationName];
        if (history.Count < 2) return;
        
        float avg = history.Count > 0 ? history.Average() : 0f;
        float max = history.Count > 0 ? history.Max() : 0f;
        float min = history.Count > 0 ? history.Min() : 0f;
        
        UnityEngine.Debug.Log($"[PERF DETAIL] {operationName}: {elapsedMs:F2}ms (Avg: {avg:F2}ms, Min: {min:F2}ms, Max: {max:F2}ms, Samples: {history.Count})");
    }
    
    /// <summary>
    /// Get performance statistics for an operation
    /// </summary>
    public BlockPerformanceStats GetStats(string operationName)
    {
        if (!_performanceHistory.ContainsKey(operationName) || _performanceHistory[operationName].Count == 0)
        {
            return new BlockPerformanceStats();
        }
        
        var history = _performanceHistory[operationName];
        return new BlockPerformanceStats
        {
            Average = history.Average(),
            Min = history.Min(),
            Max = history.Max(),
            Count = history.Count,
            Last = history.LastOrDefault()
        };
    }
    
    /// <summary>
    /// Get overall performance summary
    /// </summary>
    public string GetPerformanceSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== BLOCK PERFORMANCE SUMMARY ===");
        sb.AppendLine($"Block Placements: {totalBlockPlacements} (Last: {lastBlockPlacementTime:F2}ms)");
        sb.AppendLine($"Block Spawns: {totalSpawns} (Last: {lastSpawnTime:F2}ms)");
        sb.AppendLine($"Game Over Checks: {totalGameOverChecks} (Last: {lastGameOverCheckTime:F2}ms)");
        
        foreach (var kvp in _performanceHistory)
        {
            if (kvp.Value.Count > 0)
            {
                var stats = GetStats(kvp.Key);
                sb.AppendLine($"{kvp.Key}: Avg {stats.Average:F2}ms, Max {stats.Max:F2}ms ({stats.Count} samples)");
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Clear all performance history
    /// </summary>
    public void ClearHistory()
    {
        foreach (var list in _performanceHistory.Values)
        {
            list.Clear();
        }
        
        totalBlockPlacements = 0;
        totalSpawns = 0;
        totalGameOverChecks = 0;
        lastBlockPlacementTime = 0f;
        lastSpawnTime = 0f;
        lastGameOverCheckTime = 0f;
    }
    
    /// <summary>
    /// Check if performance is within acceptable range
    /// </summary>
    public bool IsPerformanceHealthy()
    {
        float avgPlacement = GetStats("BlockPlacement").Average;
        float avgSpawn = GetStats("BlockSpawn").Average;
        float avgGameOverCheck = GetStats("GameOverCheck").Average;
        
        return avgPlacement < logThresholdMs && 
               avgSpawn < logThresholdMs && 
               avgGameOverCheck < logThresholdMs;
    }
    
    void OnGUI()
    {
        if (!enablePerformanceLogging) return;
        
        // Simple on-screen performance display
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");
        GUILayout.Label("Block Performance Monitor", GUI.skin.box);
        
        GUILayout.Label($"Last Placement: {lastBlockPlacementTime:F1}ms");
        GUILayout.Label($"Last Spawn: {lastSpawnTime:F1}ms");
        GUILayout.Label($"Last Game Over Check: {lastGameOverCheckTime:F1}ms");
        
        if (GUILayout.Button("Log Summary"))
        {
            UnityEngine.Debug.Log(GetPerformanceSummary());
        }
        
        if (GUILayout.Button("Clear History"))
        {
            ClearHistory();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

/// <summary>
/// Performance statistics structure for Block Performance Monitor
/// </summary>
[System.Serializable]
public struct BlockPerformanceStats
{
    public float Average;
    public float Min;
    public float Max;
    public int Count;
    public float Last;
}
