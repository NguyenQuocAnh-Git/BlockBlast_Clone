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
    public float bottomReservedRatio = 0.3f;
    public float horizontalPaddingRatio = 0.08f;
    public float verticalPaddingRatio = 0.15f;

    [Header("Fine Tune")]
    [Range(0f, 0.3f)] public float raiseRatio = 0.12f;

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
        Camera cam = Camera.main;
        float screenHeight = cam.orthographicSize * 2f;
        float screenWidth = screenHeight * cam.aspect;

        float bottomHeight = screenHeight * bottomReservedRatio;
        float centerY = -screenHeight / 2f + bottomHeight * (0.5f + verticalPaddingRatio + raiseRatio);

        float usableWidth = screenWidth * (1f - horizontalPaddingRatio * 2f);
        float slotSpacing = usableWidth / 3f;

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
            float slotCenterX = (i - 1) * slotSpacing;

            GameObject block = new GameObject($"BlockPreview_{i}");
            block.transform.SetParent(transform);
            block.transform.position = new Vector3(slotCenterX, centerY, 0);

            block.AddComponent<DragBlockController>();

            SpawnBlockCells(block.transform, shapesToSpawn[i], colorIndices[i]);

            BlockColliderBuilder.FitColliderToChildren(block);

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




}

