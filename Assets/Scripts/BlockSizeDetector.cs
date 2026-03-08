using UnityEngine;
using System.Collections.Generic;

public static class BlockSizeDetector
{
    // Kiểm tra xem có phải block lớn không (3x3, 2x3, hoặc 1x5)
    public static bool IsLargeBlock(List<Vector2Int> shape)
    {
        if (shape.Count >= 6) // 2x3 hoặc 3x2 trở lên
            return true;
            
        // Normalize shape trước khi kiểm tra kích thước
        List<Vector2Int> normalizedShape = TetrisShapes.NormalizeShape(shape);
        
        // Kiểm tra kích thước thực tế của shape
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        
        foreach (var pos in normalizedShape)
        {
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }
        
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        
        // Block lớn nếu có kích thước 3x3, 2x3/3x2, hoặc 1x5/5x1
        return (width >= 3 && height >= 3) || (width >= 2 && height >= 3) || 
               (width == 5 && height == 1) || (width == 1 && height == 5);
    }
}
