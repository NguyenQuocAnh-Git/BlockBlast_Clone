using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    // tuned for 8x8 grid
    public int minEmptyCellsForLargeBlockSelector = 6;

    // References
    private SmartBlockSpawner smartSpawner;
    private GridView grid;
    private ComboManager comboManager;

    // Analyzer
    private UnifiedGridAnalyzer unifiedAnalyzer;

    // No background analysis here — analysis is performed on-demand when spawn is requested.

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
        // For 8x8 grid, consider near-full lines when <=2 empty cells
        unifiedAnalyzer = new UnifiedGridAnalyzer(grid, 2, 6);
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

            // Prefer smart spawner's lookahead selection when available and enabled.
            if (useSmartSpawning && smartSpawner != null && smartSpawner.enableSmartSpawning)
            {
                var smart = smartSpawner.GetSmartBlockList(3);
                if (smart != null && smart.Count > 0)
                {
                    CacheGeneratedShapes(smart);
                    return smart;
                }
            }

            // Deterministic selection enhanced: prefer shapes that can fill critical gaps
            if (analysis.allPlaceableShapes != null && analysis.allPlaceableShapes.Count > 0)
            {
                var prioritized = new List<List<Vector2Int>>();
                var remainingPlaceable = new List<List<Vector2Int>>(analysis.allPlaceableShapes);

                // If there are critical gaps (cells in near-full lines), find shapes that can cover them.
                if (analysis.criticalGaps != null && analysis.criticalGaps.Count > 0)
                {
                    foreach (var shape in analysis.allPlaceableShapes)
                    {
                        if (prioritized.Count >= 3) break;
                        var bd = new BlockData(shape);
                        bool covers = false;
                        foreach (var gap in analysis.criticalGaps)
                        {
                            foreach (var cell in shape)
                            {
                                int ax = gap.x - cell.x;
                                int ay = gap.y - cell.y;
                                if (ax < 0 || ay < 0 || ax >= gridSize || ay >= gridSize) continue;
                                if (grid != null && grid.CanPlace(bd, ax, ay))
                                {
                                    covers = true;
                                    break;
                                }
                            }
                            if (covers) break;
                        }
                        if (covers)
                        {
                            prioritized.Add(shape);
                            remainingPlaceable.Remove(shape);
                        }
                    }
                }

                // Fill remaining slots deterministically preferring larger shapes
                var orderedPlaceable = remainingPlaceable
                    .OrderByDescending(s => ShapeDatabase.GetSize(s))
                    .ThenByDescending(s => s.Count)
                    .ToList();

                foreach (var s in orderedPlaceable)
                {
                    if (prioritized.Count >= 3) break;
                    prioritized.Add(s);
                }

                shapes.AddRange(prioritized.Take(3));
            }
            else
            {
                // As a last resort, pick first available variations from the precomputed database
                // that fit the current emptyCells count (deterministic, no randomness).
                int need = 3;
                foreach (var v in ShapeDatabase.AllShapeVariations)
                {
                    if (v == null) continue;
                    if (v.Count > emptyCells) continue;
                    shapes.Add(TetrisShapes.NormalizeShape(v));
                    if (shapes.Count >= 3) break;
                }
            }

            CacheGeneratedShapes(shapes);
            return shapes;
        }
        finally
        {
            _isGeneratingShapes = false;
        }
    }

    // No continuous coroutine; shapes generated on-demand when spawn is requested.

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
