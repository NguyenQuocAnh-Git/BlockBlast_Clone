using UnityEngine;
using System.Collections.Generic;

public static class TetrisShapes
{
    // Các shape Tetris chuẩn (tọa độ local)
    public static readonly List<List<Vector2Int>> Shapes = new List<List<Vector2Int>>
    {
        // I-piece (ngang 4 ô)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2, Vector2Int.right * 3 },
        
        // O-piece (vuông 2x2)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up + Vector2Int.right },
        
        // T-piece
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.left, Vector2Int.right, Vector2Int.up },
        
        // S-piece (Z)
        new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
        
        // Z-piece (S ngược)
        new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
        
        // J-piece
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.left, Vector2Int.up, Vector2Int.up * 2 },
        
        // L-piece
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up * 2 },
        
        // L góc vuông 2x2 (hình chữ L nhỏ)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
        
        // Thanh dài 2 ô (I-piece 2 ô)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right },

        new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2)
        },
        
        // Khối 3x3
        new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
        },
        
        // L-piece 3x3 (góc L lớn)
        new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(0, 2)
        },
        
        // Khối 1x3 (thanh ngang 3 ô)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 },
        
        // Khối 1x5 (thanh ngang 5 ô)
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2, Vector2Int.right * 3, Vector2Int.right * 4 }
    };

    // Lấy shape ngẫu nhiên
    public static List<Vector2Int> GetRandomShape()
    {
        int index = Random.Range(0, Shapes.Count);
        return NormalizeShape(new List<Vector2Int>(Shapes[index]));
    }

    // Xoay shape 90 độ theo chiều kim đồng hồ
    public static List<Vector2Int> RotateShape(List<Vector2Int> shape)
    {
        List<Vector2Int> rotated = new List<Vector2Int>();
        
        foreach (var pos in shape)
        {
            // Xoay 90 độ: (x, y) -> (-y, x)
            Vector2Int newPos = new Vector2Int(-pos.y, pos.x);
            rotated.Add(newPos);
        }
        
        return NormalizeShape(rotated);
    }

    public static List<Vector2Int> FlipShapeHorizontal(List<Vector2Int> shape)
    {
        List<Vector2Int> flipped = new List<Vector2Int>();

        foreach (var pos in shape)
        {
            flipped.Add(new Vector2Int(-pos.x, pos.y));
        }

        return NormalizeShape(flipped);
    }

    public static List<Vector2Int> FlipShapeVertical(List<Vector2Int> shape)
    {
        List<Vector2Int> flipped = new List<Vector2Int>();

        foreach (var pos in shape)
        {
            flipped.Add(new Vector2Int(pos.x, -pos.y));
        }

        return NormalizeShape(flipped);
    }

    // Normalize shape để có tọa độ không âm
    public static List<Vector2Int> NormalizeShape(List<Vector2Int> shape)
    {
        if (shape.Count == 0) return shape;

        // Tìm min x và min y
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        
        foreach (var pos in shape)
        {
            minX = Mathf.Min(minX, pos.x);
            minY = Mathf.Min(minY, pos.y);
        }

        // Shift tất cả positions để không âm
        List<Vector2Int> normalized = new List<Vector2Int>();
        Vector2Int offset = new Vector2Int(-minX, -minY);
        
        foreach (var pos in shape)
        {
            Vector2Int newPos = pos + offset;
            normalized.Add(newPos);
        }
        
        // Debug log để kiểm tra shape sau khi normalize
        string shapeStr = string.Join(", ", normalized);
        
        
        return normalized;
    }

    // Lấy shape với rotation ngẫu nhiên (0-270 độ) - OPTIMIZED
    public static List<Vector2Int> GetRandomShapeWithRotation()
    {
        // Use pre-computed database for better performance
        return ShapeDatabase.GetRandomShapeVariation();
    }
}
