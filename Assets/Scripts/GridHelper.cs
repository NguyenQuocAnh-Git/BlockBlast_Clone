using UnityEngine;

public static class GridHelper
{
    public static Vector2Int WorldToGrid(GridView grid, Vector3 worldPos)
    {
        // Chuyển world -> local (đã tính cả scale/rotation của grid)
        Vector3 local = grid.transform.InverseTransformPoint(worldPos);

        // Offset tâm lưới nằm ở 0, mỗi ô có tâm tại offset + index * cellWorldSize
        float offset = -(grid.GridSize - 1) * 0.5f * grid.CellWorldSize;

        // Dùng round-to-nearest để chọn ô có tâm gần nhất (tránh sai lệch floor)
        float fx = (local.x - offset) / grid.CellWorldSize;
        float fy = (local.y - offset) / grid.CellWorldSize;

        int x = Mathf.RoundToInt(fx);
        int y = Mathf.RoundToInt(fy);

        return new Vector2Int(x, y);
    }
}
