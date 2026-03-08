using UnityEngine;

public class GridData
{
    public const int Size = 8;
    public int[,] data = new int[Size, Size];

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Size && y >= 0 && y < Size;
    }

    public bool CanPlace(BlockData block, int anchorX, int anchorY)
    {
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (block.mask[i, j] == 0) continue;

                int gx = anchorX + i;
                int gy = anchorY + j;

                if (!IsInside(gx, gy)) return false;
                if (data[gx, gy] == 1) return false;
            }
        }
        return true;
    }

    public void Apply(BlockData block, int anchorX, int anchorY)
    {
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (block.mask[i, j] == 1)
                {
                    data[anchorX + i, anchorY + j] = 1;
                }
            }
        }
    }
}
