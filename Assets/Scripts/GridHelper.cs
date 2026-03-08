using UnityEngine;

public static class GridHelper
{
    public static Vector2Int WorldToGrid(GridView grid, Vector3 worldPos)
    {
        Vector3 local =
            grid.transform.InverseTransformPoint(worldPos);

        // Dùng công thức tương tự GetAnchorWorldPosition để đảm bảo consistency
        float offset = -(grid.GridSize - 1) * 0.5f * grid.CellWorldSize;
        
        // Tính grid coordinates từ world position - dùng Floor để nhất quán với GetAnchorWorldPosition
        int x = Mathf.FloorToInt((local.x - offset) / grid.CellWorldSize - 0.4f);
        int y = Mathf.FloorToInt((local.y - offset) / grid.CellWorldSize);

        return new Vector2Int(x, y);
    }
}
