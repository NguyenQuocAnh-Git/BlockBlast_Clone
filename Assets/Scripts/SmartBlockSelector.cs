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
    public bool prioritizeLargeBlocks = false;
    // tuned for 8x8 grid
    public int minEmptyCellsForLargeBlockSelector = 6;

    // References
    private SmartBlockSpawner smartSpawner;
    private GridView grid;
    private ComboManager comboManager;

    // Analyzer
    private UnifiedGridAnalyzer unifiedAnalyzer;
    [Header("Diversity & Scoring")]
    [Tooltip("Weight for clearing critical gaps (higher => prefer clear)")]
    public float clearPriority = 1.2f;
    [Tooltip("Weight for preferring larger shapes (tunable)")]
    public float sizePriority = 0.4f;
    [Tooltip("Randomness factor to introduce variety")]
    public float randomness = 0.25f;
    [Tooltip("Recent shapes history length to avoid repeats")]
    public int recentHistorySize = 4;
    private Queue<string> _recentShapeKeys = new Queue<string>();
    private Queue<string> _recentTriplets = new Queue<string>();
    [Header("Triplet History")]
    [Tooltip("How many recent triplets to remember for diversity")]
    public int recentTripletHistorySize = 4;
    [Tooltip("If the last N triplets are identical, avoid returning the same triplet (N = threshold)")]
    public int tripletRepeatThreshold = 2;

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

                // If there are critical gaps, try to find a combination of up to 3 placeable shapes
                // whose placements (without modifying the grid) would cover all gaps -> full clear.
                if (analysis.criticalGaps != null && analysis.criticalGaps.Count > 0)
                {
                    if (TryFindClearCombo(analysis, out var combo))
                    {
                        var final = SelectFinalShapes(combo, analysis);
                        CacheGeneratedShapes(final);
                        return final;
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

                // Fill remaining slots: respect prioritizeLargeBlocks flag.
                List<List<Vector2Int>> orderedPlaceable;
                if (prioritizeLargeBlocks)
                {
                    orderedPlaceable = remainingPlaceable
                        .OrderByDescending(s => ShapeDatabase.GetSize(s))
                        .ThenByDescending(s => s.Count)
                        .ToList();
                }
                else
                {
                    // Allow small shapes normally by shuffling remaining candidates
                    orderedPlaceable = remainingPlaceable.OrderBy(s => UnityEngine.Random.value).ToList();
                }

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

            // Final selection: balance clear-priority and diversity before caching
            var finalShapes = SelectFinalShapes(shapes, analysis);
            CacheGeneratedShapes(finalShapes);
            return finalShapes;
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

    // Try to find up to 3 placeable shapes (from analysis.allPlaceableShapes) and anchors
    // such that their occupied cells (when placed) cover all critical gaps reported by analysis.
    private bool TryFindClearCombo(UnifiedGridAnalysis analysis, out List<List<Vector2Int>> comboShapes)
    {
        comboShapes = null;
        if (analysis == null || analysis.criticalGaps == null || analysis.criticalGaps.Count == 0) return false;
        if (grid == null) return false;

        int gridSize = grid.GridSize;
        var gapsSet = new HashSet<Vector2Int>(analysis.criticalGaps);

        // Candidate placements that cover at least one gap
        var placements = new List<Placement>();

        foreach (var shape in analysis.allPlaceableShapes)
        {
            if (shape == null) continue;
            var bd = new BlockData(shape);
            for (int ax = 0; ax < gridSize; ax++)
            {
                for (int ay = 0; ay < gridSize; ay++)
                {
                    if (!grid.CanPlace(bd, ax, ay)) continue;
                    var occ = new HashSet<Vector2Int>();
                    var covers = new HashSet<Vector2Int>();
                    foreach (var c in shape)
                    {
                        var p = new Vector2Int(ax + c.x, ay + c.y);
                        occ.Add(p);
                        if (gapsSet.Contains(p)) covers.Add(p);
                    }
                    if (covers.Count > 0)
                    {
                        placements.Add(new Placement { shape = shape, anchor = new Vector2Int(ax, ay), occupied = occ, covered = covers });
                    }
                }
            }
        }

        if (placements.Count == 0) return false;

        // Try single placement
        foreach (var p in placements)
        {
            if (p.covered.SetEquals(gapsSet))
            {
                comboShapes = new List<List<Vector2Int>> { p.shape };
                return true;
            }
        }

        // Try pairs
        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                var p1 = placements[i];
                var p2 = placements[j];
                if (p1.occupied.Overlaps(p2.occupied)) continue;
                var union = new HashSet<Vector2Int>(p1.covered);
                union.UnionWith(p2.covered);
                if (union.SetEquals(gapsSet))
                {
                    comboShapes = new List<List<Vector2Int>> { p1.shape, p2.shape };
                    return true;
                }
            }
        }

        // Try triplets (bounded)
        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                var p1 = placements[i];
                var p2 = placements[j];
                if (p1.occupied.Overlaps(p2.occupied)) continue;
                for (int k = j + 1; k < placements.Count; k++)
                {
                    var p3 = placements[k];
                    if (p1.occupied.Overlaps(p3.occupied)) continue;
                    if (p2.occupied.Overlaps(p3.occupied)) continue;
                    var union = new HashSet<Vector2Int>(p1.covered);
                    union.UnionWith(p2.covered);
                    union.UnionWith(p3.covered);
                    if (union.SetEquals(gapsSet))
                    {
                        comboShapes = new List<List<Vector2Int>> { p1.shape, p2.shape, p3.shape };
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // Internal helper type for placement candidates
    private class Placement
    {
        public List<Vector2Int> shape;
        public Vector2Int anchor;
        public HashSet<Vector2Int> occupied;
        public HashSet<Vector2Int> covered;
    }

    // Select final up to 3 shapes from candidates balancing clear potential and diversity.
    private List<List<Vector2Int>> SelectFinalShapes(List<List<Vector2Int>> candidates, UnifiedGridAnalysis analysis)
    {
        var result = new List<List<Vector2Int>>();
        if (candidates == null || candidates.Count == 0) return result;
        if (grid == null) return candidates.Take(3).ToList();

        // Prepare gap set
        var gaps = (analysis != null && analysis.criticalGaps != null) ? new HashSet<Vector2Int>(analysis.criticalGaps) : new HashSet<Vector2Int>();
        int gapCount = Mathf.Max(1, gaps.Count);

        // Compute candidate scores
        float maxSize = candidates.Max(s => ShapeDatabase.GetSize(s));
        var scored = new List<(List<Vector2Int> shape, float score, string key)>();

        foreach (var shape in candidates)
        {
            if (shape == null) continue;
            string key = ShapeKey(shape);

            // Coverage: maximum number of gaps this shape can cover at any valid anchor
            int bestCover = 0;
            var bd = new BlockData(shape);
            int gridSize = grid.GridSize;
            for (int ax = 0; ax < gridSize; ax++)
            {
                for (int ay = 0; ay < gridSize; ay++)
                {
                    if (!grid.CanPlace(bd, ax, ay)) continue;
                    int cover = 0;
                    foreach (var c in shape)
                    {
                        var p = new Vector2Int(ax + c.x, ay + c.y);
                        if (gaps.Contains(p)) cover++;
                    }
                    if (cover > bestCover) bestCover = cover;
                }
            }

            float coverageScore = (float)bestCover / gapCount; // 0..1
            float sizeScore = ShapeDatabase.GetSize(shape) / Mathf.Max(1f, maxSize); // 0..1
            float rand = UnityEngine.Random.value * randomness;

            float total = clearPriority * coverageScore + sizePriority * sizeScore + rand;

            // Penalize recently used shapes to improve diversity
            if (_recentShapeKeys.Contains(key)) total *= 0.6f;

            scored.Add((shape, total, key));
        }

        // Pick top shapes while avoiding duplicates, up to 3
        var ordered = scored.OrderByDescending(x => x.score).ToList();
        foreach (var item in ordered)
        {
            if (result.Count >= 3) break;
            // avoid adding same shape multiple times
            if (result.Any(r => ShapeKey(r) == item.key)) continue;
            result.Add(item.shape);
            _recentShapeKeys.Enqueue(item.key);
            if (_recentShapeKeys.Count > recentHistorySize) _recentShapeKeys.Dequeue();
        }

        // Fill with random shapes if not enough
        while (result.Count < 3)
        {
            var r = ShapeDatabase.GetRandomShapeVariation();
            if (r == null) break;
            string k = ShapeKey(r);
            if (result.Any(rr => ShapeKey(rr) == k)) { continue; }
            result.Add(r);
            _recentShapeKeys.Enqueue(k);
            if (_recentShapeKeys.Count > recentHistorySize) _recentShapeKeys.Dequeue();
        }
        // Ensure we are not producing the same triplet 3 times in a row (threshold control).
        string tripletKey = string.Join("|", result.Select(r => ShapeKey(r)));
        if (IsTripletRepeated(tripletKey))
        {
            // Try to modify one element to avoid repeating the triplet
            var availCandidates = candidates ?? new List<List<Vector2Int>>();
            var shuffledCandidates = availCandidates.OrderBy(x => UnityEngine.Random.value).ToList();
            bool replaced = false;
            for (int i = 0; i < result.Count && !replaced; i++)
            {
                foreach (var cand in shuffledCandidates)
                {
                    if (cand == null) continue;
                    string ck = ShapeKey(cand);
                    if (result.Any(r => ShapeKey(r) == ck)) continue;
                    var temp = new List<List<Vector2Int>>(result);
                    temp[i] = cand;
                    string newKey = string.Join("|", temp.Select(r => ShapeKey(r)));
                    if (!IsTripletRepeated(newKey))
                    {
                        result = temp;
                        replaced = true;
                        break;
                    }
                }
            }

            // Last resort: shuffle order to make key different
            if (!replaced)
            {
                var shuffled = result.OrderBy(x => UnityEngine.Random.value).ToList();
                string newKey = string.Join("|", shuffled.Select(r => ShapeKey(r)));
                if (!IsTripletRepeated(newKey))
                {
                    result = shuffled;
                }
            }
        }

        // Record triplet key into history
        string finalKey = string.Join("|", result.Select(r => ShapeKey(r)));
        _recentTriplets.Enqueue(finalKey);
        if (_recentTriplets.Count > recentTripletHistorySize) _recentTriplets.Dequeue();

        return result;
    }

    private string ShapeKey(List<Vector2Int> shape)
    {
        var norm = TetrisShapes.NormalizeShape(shape);
        return string.Join(";", norm.Select(p => p.x + "," + p.y));
    }

    private bool IsTripletRepeated(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (_recentTriplets == null || _recentTriplets.Count == 0) return false;
        if (tripletRepeatThreshold <= 0) return false;

        // Check last N entries in queue; if all equal to key, consider repeated.
        var arr = _recentTriplets.ToArray();
        int n = Mathf.Min(tripletRepeatThreshold, arr.Length);
        if (n <= 0) return false;
        for (int i = 1; i <= n; i++)
        {
            if (arr[arr.Length - i] != key) return false;
        }
        return true;
    }
}
