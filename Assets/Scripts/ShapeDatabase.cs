using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pre-computed shape database to optimize performance
/// Eliminates redundant calculations during runtime
/// </summary>
public static class ShapeDatabase
{
    // Pre-computed rotations for each shape index
    public static readonly Dictionary<int, List<List<Vector2Int>>> AllRotations;
    
    // Pre-computed bounds for each shape variation
    public static readonly Dictionary<List<Vector2Int>, Bounds> ShapeBounds;
    
    // Pre-computed shape sizes for quick filtering
    public static readonly Dictionary<List<Vector2Int>, int> ShapeSizes;
    
    // Pre-computed shape classifications (Small, Medium, Large)
    public static readonly Dictionary<List<Vector2Int>, BlockSize> ShapeClassifications;
    
    // All possible shape variations (rotations + flips) for quick access
    public static readonly List<List<Vector2Int>> AllShapeVariations;
    
    // Variations grouped by size for balanced sampling
    public static readonly List<List<Vector2Int>> TinyVariations;
    public static readonly List<List<Vector2Int>> SmallVariations;
    public static readonly List<List<Vector2Int>> MediumVariations;
    public static readonly List<List<Vector2Int>> LargeVariations;
    
    static ShapeDatabase()
    {
        AllRotations = new Dictionary<int, List<List<Vector2Int>>>();
        ShapeBounds = new Dictionary<List<Vector2Int>, Bounds>();
        ShapeSizes = new Dictionary<List<Vector2Int>, int>();
        ShapeClassifications = new Dictionary<List<Vector2Int>, BlockSize>();
        AllShapeVariations = new List<List<Vector2Int>>();
        TinyVariations = new List<List<Vector2Int>>();
        SmallVariations = new List<List<Vector2Int>>();
        MediumVariations = new List<List<Vector2Int>>();
        LargeVariations = new List<List<Vector2Int>>();
        
        PrecomputeAllShapeData();
    }
    
    /// <summary>
    /// Pre-compute all shape data once at startup
    /// </summary>
    private static void PrecomputeAllShapeData()
    {
        for (int shapeIndex = 0; shapeIndex < TetrisShapes.Shapes.Count; shapeIndex++)
        {
            var baseShape = TetrisShapes.Shapes[shapeIndex];
            var rotations = new List<List<Vector2Int>>();
            
            // Generate all 4 rotations
            for (int rotation = 0; rotation < 4; rotation++)
            {
                List<Vector2Int> rotatedShape;
                if (rotation == 0)
                {
                    rotatedShape = new List<Vector2Int>(baseShape);
                }
                else
                {
                    rotatedShape = TetrisShapes.RotateShape(rotations[rotation - 1]);
                }
                
                // Normalize and store
                var normalizedShape = TetrisShapes.NormalizeShape(rotatedShape);
                rotations.Add(normalizedShape);
                
                // Store bounds and size for this rotation
                if (!ShapeBounds.ContainsKey(normalizedShape))
                {
                    ShapeBounds[normalizedShape] = CalculateBounds(normalizedShape);
                    ShapeSizes[normalizedShape] = normalizedShape.Count;
                    ShapeClassifications[normalizedShape] = ClassifyShape(normalizedShape);
                    AllShapeVariations.Add(normalizedShape);
                    AddToSizePool(normalizedShape);
                }
                
                // Also generate flipped variations
                var horizontalFlip = TetrisShapes.FlipShapeHorizontal(normalizedShape);
                var verticalFlip = TetrisShapes.FlipShapeVertical(normalizedShape);
                
                if (!ShapeBounds.ContainsKey(horizontalFlip))
                {
                    ShapeBounds[horizontalFlip] = CalculateBounds(horizontalFlip);
                    ShapeSizes[horizontalFlip] = horizontalFlip.Count;
                    ShapeClassifications[horizontalFlip] = ClassifyShape(horizontalFlip);
                    AllShapeVariations.Add(horizontalFlip);
                    AddToSizePool(horizontalFlip);
                }
                
                if (!ShapeBounds.ContainsKey(verticalFlip))
                {
                    ShapeBounds[verticalFlip] = CalculateBounds(verticalFlip);
                    ShapeSizes[verticalFlip] = verticalFlip.Count;
                    ShapeClassifications[verticalFlip] = ClassifyShape(verticalFlip);
                    AllShapeVariations.Add(verticalFlip);
                    AddToSizePool(verticalFlip);
                }
            }
            
            AllRotations[shapeIndex] = rotations;
        }
    }
    
    private static void AddToSizePool(List<Vector2Int> shape)
    {
        var cls = ClassifyShape(shape);
        switch (cls)
        {
            case BlockSize.Tiny:
                TinyVariations.Add(shape);
                break;
            case BlockSize.Small:
                SmallVariations.Add(shape);
                break;
            case BlockSize.Medium:
                MediumVariations.Add(shape);
                break;
            case BlockSize.Large:
                LargeVariations.Add(shape);
                break;
        }
    }

    /// <summary>
    /// Calculate bounds for a shape
    /// </summary>
    private static Bounds CalculateBounds(List<Vector2Int> shape)
    {
        if (shape.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);
        
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        
        foreach (var pos in shape)
        {
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }
        
        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0);
        Vector3 size = new Vector3(maxX - minX + 1, maxY - minY + 1, 1);
        
        return new Bounds(center, size);
    }
    
    /// <summary>
    /// Classify shape by size
    /// </summary>
    private static BlockSize ClassifyShape(List<Vector2Int> shape)
    {
        int cellCount = shape.Count;
        
        if (cellCount <= 2)
            return BlockSize.Tiny;
        else if (cellCount <= 3)
            return BlockSize.Small;
        else if (cellCount <= 5)
            return BlockSize.Medium;
        else
            return BlockSize.Large;
    }
    
    /// <summary>
    /// Get all rotations for a specific shape index
    /// </summary>
    public static List<List<Vector2Int>> GetRotations(int shapeIndex)
    {
        return AllRotations.ContainsKey(shapeIndex) ? AllRotations[shapeIndex] : new List<List<Vector2Int>>();
    }
    
    /// <summary>
    /// Get pre-computed bounds for a shape
    /// </summary>
    public static Bounds GetBounds(List<Vector2Int> shape)
    {
        return ShapeBounds.ContainsKey(shape) ? ShapeBounds[shape] : CalculateBounds(shape);
    }
    
    /// <summary>
    /// Get pre-computed size for a shape
    /// </summary>
    public static int GetSize(List<Vector2Int> shape)
    {
        return ShapeSizes.ContainsKey(shape) ? ShapeSizes[shape] : shape.Count;
    }
    
    /// <summary>
    /// Get pre-computed classification for a shape
    /// </summary>
    public static BlockSize GetClassification(List<Vector2Int> shape)
    {
        return ShapeClassifications.ContainsKey(shape) ? ShapeClassifications[shape] : ClassifyShape(shape);
    }
    
    /// <summary>
    /// Get random shape variation (optimized)
    /// </summary>
    public static List<Vector2Int> GetRandomShapeVariation()
    {
        // Weighted sampling to prefer Medium/Large shapes to avoid over-representation of tiny/small variants.
        // Weights (tunable): Large 0.45, Medium 0.35, Small 0.15, Tiny 0.05
        float r = Random.value;
        List<List<Vector2Int>> pool = null;

        if (r < 0.45f && LargeVariations.Count > 0)
            pool = LargeVariations;
        else if (r < 0.45f + 0.35f && MediumVariations.Count > 0)
            pool = MediumVariations;
        else if (r < 0.45f + 0.35f + 0.15f && SmallVariations.Count > 0)
            pool = SmallVariations;
        else if (TinyVariations.Count > 0)
            pool = TinyVariations;
        else if (AllShapeVariations.Count > 0)
            pool = AllShapeVariations;
        else
            return new List<Vector2Int>();

        int idx = Random.Range(0, pool.Count);
        return new List<Vector2Int>(pool[idx]);
    }
    
    /// <summary>
    /// Get all shapes of specific size (optimized)
    /// </summary>
    public static List<List<Vector2Int>> GetShapesBySize(BlockSize size)
    {
        var result = new List<List<Vector2Int>>();
        
        foreach (var kvp in ShapeClassifications)
        {
            if (kvp.Value == size)
            {
                result.Add(kvp.Key);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Check if shape is small (optimized)
    /// </summary>
    public static bool IsSmallShape(List<Vector2Int> shape)
    {
        BlockSize classification = GetClassification(shape);
        return classification == BlockSize.Tiny || classification == BlockSize.Small;
    }
    
    /// <summary>
    /// Check if shape is large (optimized)
    /// </summary>
    public static bool IsLargeShape(List<Vector2Int> shape)
    {
        return GetClassification(shape) == BlockSize.Large;
    }
    
    /// <summary>
    /// Get database statistics for debugging
    /// </summary>
    public static string GetDatabaseStats()
    {
        return $"Shapes: {AllRotations.Count}, Variations: {AllShapeVariations.Count}, " +
               $"Bounds Cached: {ShapeBounds.Count}, Classifications: {ShapeClassifications.Count}";
    }
}
