using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawn Priority enum for different block spawning priorities
/// </summary>
public enum SpawnPriority
{
    PerfectClear,
    BlockMerging,
    SmartMegaCombo,
    NormalCombo,
    LargeBlocks,
    SmartSpawning,
    Random
}

/// <summary>
/// Class để lưu kết quả phân tích thống nhất của grid
/// Chứa tất cả thông tin cần thiết cho việc ra quyết định spawn
/// </summary>
public class UnifiedGridAnalysis
{
    public GridAnalysis gridAnalysis;
    public List<List<Vector2Int>> allPlaceableShapes;
    public List<List<Vector2Int>> largeShapes;
    public List<List<Vector2Int>> comboShapes;
    public List<List<Vector2Int>> smartMegaShapes;
    public List<List<Vector2Int>> perfectClearShapes;
    public List<List<Vector2Int>> mergingShapes;
    public List<Vector2Int> criticalGaps;
    public bool hasMegaComboOpportunity;
    public bool hasNormalComboOpportunity;
    public bool hasPerfectClearOpportunity;
    public bool hasMergingOpportunity;
    public int totalNearFullLines;
    public int totalOccupiedCells;
    
    public UnifiedGridAnalysis()
    {
        allPlaceableShapes = new List<List<Vector2Int>>();
        largeShapes = new List<List<Vector2Int>>();
        comboShapes = new List<List<Vector2Int>>();
        smartMegaShapes = new List<List<Vector2Int>>();
        perfectClearShapes = new List<List<Vector2Int>>();
        mergingShapes = new List<List<Vector2Int>>();
        criticalGaps = new List<Vector2Int>();
        hasMegaComboOpportunity = false;
        hasNormalComboOpportunity = false;
        hasPerfectClearOpportunity = false;
        hasMergingOpportunity = false;
        totalNearFullLines = 0;
        totalOccupiedCells = 0;
    }
}

/// <summary>
/// Class để lưu kết quả phân tích grid cơ bản
/// </summary>
public class GridAnalysis
{
    public List<NearFullLine> nearFullRows = new List<NearFullLine>();
    public List<NearFullLine> nearFullCols = new List<NearFullLine>();
}

/// <summary>
/// Class để lưu thông tin hàng/cột gần đầy
/// </summary>
public class NearFullLine
{
    public int index; // Chỉ số hàng hoặc cột
    public List<Vector2Int> emptyCells = new List<Vector2Int>();
    
    public NearFullLine(int idx, List<Vector2Int> empties)
    {
        index = idx;
        emptyCells = empties;
    }
}

/// <summary>
/// Class để lưu thông tin shape và coverage
/// </summary>
public class ShapeCoverage
{
    public List<Vector2Int> shape;
    public int coverage;
    
    public ShapeCoverage(List<Vector2Int> shp, int cov)
    {
        shape = shp;
        coverage = cov;
    }
}

/// <summary>
/// Class để lưu thông tin cơ hội perfect clear
/// </summary>
public class PerfectClearOpportunity
{
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    public List<List<Vector2Int>> perfectShapes = new List<List<Vector2Int>>();
    public bool isValid = false;
    public int totalCellsToClear = 0;
}

/// <summary>
/// Class để lưu thông tin cơ hội block merging
/// </summary>
public class MergingOpportunity
{
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();
    public List<List<Vector2Int>> mergingShapes = new List<List<Vector2Int>>();
    public bool isValid = false;
    public int mergeScore = 0;
}

/// <summary>
/// Enum cho loại block size
/// </summary>
public enum BlockSize
{
    Tiny,   // 1 ô
    Small,  // 2-3 ô
    Medium, // 4-5 ô
    Large   // 6+ ô
}

/// <summary>
/// Enum cho loại spawn strategy
/// </summary>
public enum SpawnStrategy
{
    PerfectClear,
    BlockMerging,
    SmartMegaCombo,
    NormalCombo,
    LargeBlocks,
    SmartSpawning,
    Random
}
