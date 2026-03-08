using System.Collections.Generic;
using UnityEngine;

public class BlockData
{
    public int[,] mask = new int[5, 5];

    public BlockData(List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            // Clamp coordinates vào bounds 5x5
            int clampedX = Mathf.Clamp(c.x, 0, 4);
            int clampedY = Mathf.Clamp(c.y, 0, 4);
            
            if (clampedX != c.x || clampedY != c.y)
            {
                
            }
            
            mask[clampedX, clampedY] = 1;
        }
    }
}
