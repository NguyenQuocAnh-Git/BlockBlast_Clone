using System.Collections.Generic;
using UnityEngine;

public class BlockSpawnController : MonoBehaviour
{
    [Header("Sprite")]
    public Sprite cellSprite;

    [Header("Block Colors")]
    public Color[] blockColors = new Color[5]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.magenta
    };

    [Header("Scale")]
    public float cellScaleRatio = 0.5f;

    [Header("Layout")]
    [Tooltip("Chiều rộng khu vực spawn (world units)")]
    public float spawnAreaWorldWidth = 12f;
    [Tooltip("Offset dọc so với transform.position")]
    public float spawnAreaYOffset = 0f;
    [Tooltip("Khoảng cách cộng thêm giữa các block (world units)")]
    public float extraSpacing = 0f;

    private float cellWorldSize;
    private readonly List<GameObject> currentBlocks = new List<GameObject>();

    void Start()
    {
        CalculateCellWorldSize();

        // Delay spawn để map kịp generate trước
        Invoke(nameof(Spawn3Blocks), 0.2f);
    }

    




    // ==============================

    // Spawn 3 block m?i

    // ==============================

    public void SpawnNewBlocks()
    {
        currentBlocks.Clear();
        Spawn3Blocks();

        ComboManager.Instance?.OnNewBlocksSpawned();
    }



    // Method d? block b�o c�o khi b? destroy

    public void OnBlockDestroyed(GameObject block)
    {
        currentBlocks.Remove(block);

        if (currentBlocks.Count == 0)
        {
            SpawnNewBlocks();
        }
    }

    

    // Clear t?t c? blocks trong spawn area

    public void ClearAllBlocks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            Destroy(child.gameObject);
        }

        currentBlocks.Clear();
    }

    // Check whether any currently spawned block can be placed into the provided grid.
    // Simple, early-exit check for game-over logic.
    public bool AnyBlockPlaceable(GridView grid)
    {
        if (grid == null) return false;
        if (currentBlocks == null || currentBlocks.Count == 0) return false;

        int size = grid.GridSize;
        foreach (var go in currentBlocks)
        {
            if (go == null) continue;
            var drag = go.GetComponent<DragBlockController>();
            if (drag == null) continue;
            var block = drag.BlockData;
            if (block == null) continue;

            // Try anchors; early exit on first valid placement
            for (int ax = 0; ax < size; ax++)
            {
                for (int ay = 0; ay < size; ay++)
                {
                    if (grid.CanPlace(block, ax, ay)) return true;
                }
            }
        }

        return false;
    }



    // ==============================

    // 1?? T�nh k�ch thu?c cell (world)

    // ==============================

    void CalculateCellWorldSize()
    {
        GameObject temp = new GameObject("TempCell");
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = cellSprite;

        cellWorldSize = sr.bounds.size.x * cellScaleRatio;
        Destroy(temp);
    }



    // ==============================

    // 2?? Spawn 3 block preview

    // ==============================

    void Spawn3Blocks()
    {
        // Căn theo vị trí spawn area: block giữa nằm ở tâm, hai block còn lại ở hai bên
        Vector3 areaCenter = transform.position;
        float slotSpacing = (spawnAreaWorldWidth / 3f) + extraSpacing;
        float centerY = areaCenter.y + spawnAreaYOffset;

        List<List<Vector2Int>> shapesToSpawn = new List<List<Vector2Int>>();
        for (int i = 0; i < 3; i++)
        {
            shapesToSpawn.Add(TetrisShapes.GetRandomShapeWithRotation());
        }

        List<int> colorIndices = new List<int>();
        for (int i = 0; i < shapesToSpawn.Count; i++)
        {
            colorIndices.Add(Random.Range(0, blockColors.Length));
        }

        for (int i = 0; i < 3; i++)
        {
            float slotCenterX = areaCenter.x + (i - 1) * slotSpacing;

            GameObject block = new GameObject($"BlockPreview_{i}");
            block.transform.SetParent(transform);
            // Place the block transform at the visual slot center for now.
            block.transform.position = new Vector3(slotCenterX, centerY, 0);

            block.AddComponent<DragBlockController>();

            // Spawn cells using the new origin-based layout so that the
            // block's transform becomes the shape-origin (min x,min y) while
            // keeping the visual centroid at the original slot center.
            SpawnBlockCellsNew(block.transform, shapesToSpawn[i], colorIndices[i]);

            currentBlocks.Add(block);
        }

        // Ngay sau khi spawn, kiểm tra xem có block nào đặt được không
        GameManager.Instance?.CheckGameOver();
    }

    // ==============================

    // 3?? Build block theo trường tâm

    // ==============================

    void SpawnBlockCells(Transform parent, List<Vector2Int> shape, int colorIndex)
    {
        DragBlockController dragController = parent.GetComponent<DragBlockController>();
        if (dragController != null)
        {
            dragController.SetShape(shape);
            dragController.SetBlockColor(blockColors[colorIndex % blockColors.Length]);
        }

        Vector2 centroid = Vector2.zero;
        foreach (var c in shape) centroid += c;
        centroid /= shape.Count;

        foreach (var offset in shape)
        {
            GameObject cell = new GameObject("Cell");
            cell.transform.SetParent(parent);

            SpriteRenderer sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;
            sr.color = blockColors[colorIndex % blockColors.Length];

            sr.sortingOrder = 5;
            Vector2 localPos =

                (offset - centroid) * cellWorldSize;



            cell.transform.localPosition =

                new Vector3(localPos.x, localPos.y, 0);



            cell.transform.localScale =

                Vector3.one * cellScaleRatio;

        }

    }

    // New spawn method: parent.transform represents the shape-origin (min x,min y).
    void SpawnBlockCellsNew(Transform parent, List<Vector2Int> shape, int colorIndex)
    {
        DragBlockController dragController = parent.GetComponent<DragBlockController>();
        if (dragController != null)
        {
            dragController.SetShape(shape);
            dragController.SetBlockColor(blockColors[colorIndex % blockColors.Length]);
        }

        // Compute minimum offset (shape-origin)
        Vector2 minOffset = new Vector2(float.MaxValue, float.MaxValue);
        foreach (var c in shape)
        {
            if (c.x < minOffset.x) minOffset.x = c.x;
            if (c.y < minOffset.y) minOffset.y = c.y;
        }

        List<Vector2> localOffsets = new List<Vector2>(shape.Count);
        Vector2 centroidLocal = Vector2.zero;
        foreach (var off in shape)
        {
            Vector2 lo = (off - minOffset);
            localOffsets.Add(lo);
            centroidLocal += lo;
        }
        centroidLocal /= shape.Count;

        // Keep visual centroid at parent's original position but make parent represent origin
        Vector3 slotCenter = parent.transform.position;
        Vector3 originWorldPos = slotCenter - (Vector3)(centroidLocal * cellWorldSize);
        parent.transform.position = originWorldPos;

        // Create child cells relative to shape-origin
        foreach (var lo in localOffsets)
        {
            GameObject cell = new GameObject("Cell");
            cell.transform.SetParent(parent);

            SpriteRenderer sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;
            sr.color = blockColors[colorIndex % blockColors.Length];
            sr.sortingOrder = 5;

            Vector2 localPos = lo * cellWorldSize;
            cell.transform.localPosition = new Vector3(localPos.x, localPos.y, 0);
            cell.transform.localScale = Vector3.one * cellScaleRatio;
        }
    }

}

