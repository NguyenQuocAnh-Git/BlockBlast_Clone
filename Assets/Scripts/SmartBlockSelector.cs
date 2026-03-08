using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simplified SmartBlockSelector
/// - Uses the simplified UnifiedGridAnalyzer
/// - Keeps selection rules minimal and readable
/// </summary>
public class SmartBlockSelector : MonoBehaviour
{
    [Header("Simplified Settings")]
    public bool useSmartSpawning = true;
    public bool prioritizeLargeBlocks = true;
    public int minEmptyCellsForLargeBlockSelector = 10;

    // References
    private SmartBlockSpawner smartSpawner;
    private GridView grid;
    private ComboManager comboManager;

    // Analyzer
    private UnifiedGridAnalyzer unifiedAnalyzer;

    // Cache
    private List<List<Vector2Int>> _cachedShapes = new List<List<Vector2Int>>();
    private bool _isGeneratingShapes = false;
    private int _lastGeneratedFrame = -1;
    private int _shapeGenerationCooldown = 3;

    void Start()
    {
        grid = FindObjectOfType<GridView>();
        if (useSmartSpawning) smartSpawner = FindObjectOfType<SmartBlockSpawner>();
        comboManager = ComboManager.Instance;
        InitializeModularComponents();
    }

    private void InitializeModularComponents()
    {
        unifiedAnalyzer = new UnifiedGridAnalyzer(grid, 3, 6);
    }

    public List<List<Vector2Int>> GetShapesToSpawn()
    {
        if (_cachedShapes.Count == 3 && !_isGeneratingShapes &&
            Time.frameCount - _lastGeneratedFrame < _shapeGenerationCooldown)
        {
            return new List<List<Vector2Int>>(_cachedShapes);
        }

        if (_isGeneratingShapes) return GetFallbackShapes();
        return GenerateShapesImmediate();
    }

    private List<List<Vector2Int>> GenerateShapesImmediate()
    {
        _isGeneratingShapes = true;
        _lastGeneratedFrame = Time.frameCount;

        try
        {
            var analysis = unifiedAnalyzer.PerformUnifiedAnalysis();
            int gridSize = grid != null ? grid.GridSize : 8;
            int totalCells = gridSize * gridSize;
            int emptyCells = Mathf.Max(0, totalCells - analysis.totalOccupiedCells);

            var shapes = new List<List<Vector2Int>>();

            if (prioritizeLargeBlocks && analysis.largeShapes.Count > 0 && emptyCells >= minEmptyCellsForLargeBlockSelector)
            {
                for (int i = 0; i < 3; i++)
                    shapes.Add(analysis.largeShapes[i % analysis.largeShapes.Count]);
            }
            else if (analysis.totalNearFullLines >= 2 && analysis.allPlaceableShapes.Count > 0)
            {
                shapes.AddRange(analysis.allPlaceableShapes.GetRange(0, Mathf.Min(3, analysis.allPlaceableShapes.Count)));
            }
            else
            {
                for (int i = 0; i < 3; i++) shapes.Add(ShapeDatabase.GetRandomShapeVariation());
            }

            CacheGeneratedShapes(shapes);
            return shapes;
        }
        finally
        {
            _isGeneratingShapes = false;
        }
    }

    private void CacheGeneratedShapes(List<List<Vector2Int>> shapes)
    {
        _cachedShapes = new List<List<Vector2Int>>(shapes);
    }

    private List<List<Vector2Int>> GetFallbackShapes()
    {
        if (_cachedShapes.Count > 0) return new List<List<Vector2Int>>(_cachedShapes);
        var fallback = new List<List<Vector2Int>>();
        for (int i = 0; i < 3; i++) fallback.Add(ShapeDatabase.GetRandomShapeVariation());
        return fallback;
    }

    public void ClearCachedShapes()
    {
        _cachedShapes.Clear();
        _lastGeneratedFrame = -1;
    }

    public void ClearCache()
    {
        _cachedShapes.Clear();
        unifiedAnalyzer?.ClearAllCaches();
    }

    public string GetAnalysisStats()
    {
        var a = unifiedAnalyzer?.PerformUnifiedAnalysis();
        if (a == null) return "No analysis available";
        return $"Occupied: {a.totalOccupiedCells}, NearFull: {a.totalNearFullLines}, HasCombo: {a.hasNormalComboOpportunity}";
    }
}
