using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simplified UnifiedGridAnalyzer
/// - Single-pass grid scan
/// - Returns a lightweight UnifiedGridAnalysis populated with basic info:
///   totalOccupiedCells, totalNearFullLines, near-full rows/cols, and a small set of placeable shapes.
/// This keeps logic easy to understand and fast.
/// </summary>
public class UnifiedGridAnalyzer
{
    private GridView grid;
    private int maxEmptyCellsForLine;

    public UnifiedGridAnalyzer(GridView gridView, int maxEmptyCells = 3, int minOccupancyForPerfectClear = 6)
    {
        grid = gridView;
        maxEmptyCellsForLine = Mathf.Max(1, maxEmptyCells);
    }

    // Perform a single fast analysis of the grid.
    public UnifiedGridAnalysis PerformUnifiedAnalysis()
    {
        var result = new UnifiedGridAnalysis();
        result.gridAnalysis = new GridAnalysis();
        if (grid == null) return result;

        int gridSize = grid.GridSize;
        int occupiedCount = 0;

        // Collect near-full rows/cols in one pass.
        for (int i = 0; i < gridSize; i++)
        {
            List<Vector2Int> emptyRow = new List<Vector2Int>();
            List<Vector2Int> emptyCol = new List<Vector2Int>();

            for (int j = 0; j < gridSize; j++)
            {
                if (!grid.IsCellOccupied(j, i)) emptyRow.Add(new Vector2Int(j, i));
                else occupiedCount++;

                if (!grid.IsCellOccupied(i, j)) emptyCol.Add(new Vector2Int(i, j));
            }

            if (emptyRow.Count > 0 && emptyRow.Count <= maxEmptyCellsForLine)
                result.gridAnalysis.nearFullRows.Add(new NearFullLine(i, emptyRow));

            if (emptyCol.Count > 0 && emptyCol.Count <= maxEmptyCellsForLine)
                result.gridAnalysis.nearFullCols.Add(new NearFullLine(i, emptyCol));
        }

        // occupiedCount counted rows * cols; fix by recounting properly
        // (simpler and reliable) do a small separate count
        occupiedCount = 0;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                if (grid.IsCellOccupied(x, y)) occupiedCount++;

        result.totalOccupiedCells = occupiedCount;
        result.totalNearFullLines = result.gridAnalysis.nearFullRows.Count + result.gridAnalysis.nearFullCols.Count;

        // Build critical gaps (unique)
        var gaps = new HashSet<Vector2Int>();
        foreach (var r in result.gridAnalysis.nearFullRows) foreach (var p in r.emptyCells) gaps.Add(p);
        foreach (var c in result.gridAnalysis.nearFullCols) foreach (var p in c.emptyCells) gaps.Add(p);
        result.criticalGaps = new List<Vector2Int>(gaps);

        // Quick placeable shapes: take up to 20 variations and test simple placement
        result.allPlaceableShapes = GetQuickPlaceableShapes(20);

        // Simple categories
        result.largeShapes = FilterBySize(result.allPlaceableShapes, BlockSize.Large);
        result.comboShapes = FilterBySize(result.allPlaceableShapes, BlockSize.Medium);
        result.perfectClearShapes = new List<List<Vector2Int>>(); // keep empty in simplified analyzer
        result.mergingShapes = new List<List<Vector2Int>>();

        // Opportunity flags (very conservative)
        result.hasNormalComboOpportunity = result.comboShapes.Count > 0 && result.totalNearFullLines > 0;
        result.hasMegaComboOpportunity = result.totalNearFullLines >= 2;
        result.hasPerfectClearOpportunity = false;
        result.hasMergingOpportunity = result.mergingShapes.Count > 0;

        return result;
    }

    private List<List<Vector2Int>> GetQuickPlaceableShapes(int maxShapes)
    {
        var placeable = new List<List<Vector2Int>>();
        if (grid == null) return placeable;

        int gridSize = grid.GridSize;
        int added = 0;

        // Iterate through precomputed variations and add those that can be placed somewhere.
        foreach (var shape in ShapeDatabase.AllShapeVariations)
        {
            if (added >= maxShapes) break;
            if (shape == null) continue;
            // Compute bounding box to limit anchor search (optimized for fixed small grid)
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in shape)
            {
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            if (width > gridSize || height > gridSize) continue;

            if (CanPlaceAnywhere(shape, gridSize, width, height))
            {
                placeable.Add(shape);
                added++;
            }
        }
        // Fallback: ensure at least 3 shapes
        while (placeable.Count < 3)
        {
            // Deterministic fallback: take next shape variation that can fit
            foreach (var v in ShapeDatabase.AllShapeVariations)
            {
                if (v == null) continue;
                if (placeable.Contains(v)) continue;
                // check bounding box quickly
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var p in v)
                {
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (width <= gridSize && height <= gridSize)
                {
                    placeable.Add(v);
                    break;
                }
            }
        }

        return placeable;
    }

    // Optimized CanPlaceAnywhere that uses bounding box dimensions to limit anchor search.
    private bool CanPlaceAnywhere(List<Vector2Int> shape, int gridSize, int width, int height)
    {
        int maxX = gridSize - width;
        int maxY = gridSize - height;
        for (int ax = 0; ax <= maxX; ax++)
        {
            for (int ay = 0; ay <= maxY; ay++)
            {
                if (CanPlaceAt(shape, ax, ay, gridSize)) return true;
            }
        }
        return false;
    }

    private bool CanPlaceAt(List<Vector2Int> shape, int startX, int startY, int gridSize)
    {
        foreach (var o in shape)
        {
            int x = startX + o.x;
            int y = startY + o.y;
            if (x < 0 || x >= gridSize || y < 0 || y >= gridSize) return false;
            if (grid.IsCellOccupied(x, y)) return false;
        }
        return true;
    }

    private List<List<Vector2Int>> FilterBySize(List<List<Vector2Int>> shapes, BlockSize size)
    {
        var res = new List<List<Vector2Int>>();
        foreach (var s in shapes)
        {
            if (ShapeDatabase.GetClassification(s) == size) res.Add(s);
        }
        return res;
    }

    // Minimal public helpers kept for compatibility
    public void ClearAllCaches() { /* no cache in simplified analyzer */ }
    public string GetCacheStats() => "SimplifiedAnalyzer: no caches";
}
