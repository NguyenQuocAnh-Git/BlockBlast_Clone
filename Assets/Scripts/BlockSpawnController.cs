using UnityEngine;

using System.Collections.Generic;



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

    [Range(0f, 0.3f)]

    public float raiseRatio = 0.12f;



    private float cellWorldSize;

    private List<GameObject> currentBlocks = new List<GameObject>(); // Tracking c�c block hi?n t?i

    

    [Header("Smart Spawning")]
    public SmartBlockSelector smartBlockSelector;



    void Start()
    {
        CalculateCellWorldSize();
        
        // Auto-add SmartBlockSelector if not assigned
        if (smartBlockSelector == null)
        {
            smartBlockSelector = GetComponent<SmartBlockSelector>();
            if (smartBlockSelector == null)
            {
                smartBlockSelector = gameObject.AddComponent<SmartBlockSelector>();
            }
        }
        
        // DELAY SPAWN d? map du?c generate tru?c (map generate sau 0.1s)
        Invoke("Spawn3Blocks", 0.2f);
        
        // Notify ComboManager of new spawn round
        if (ComboManager.Instance != null)
        {
            ComboManager.Instance?.OnNewBlocksSpawned();
        
        // End performance monitoring
        BlockPerformanceMonitor.Instance?.EndTiming("BlockSpawn");
        }
    }

    




    // ==============================

    // Spawn 3 block m?i

    // ==============================

    public void SpawnNewBlocks()

    {

        // Clear old blocks list

        currentBlocks.Clear();
        
        Spawn3Blocks();
        
        // Notify ComboManager of new spawn round
        if (ComboManager.Instance != null)
        {
            ComboManager.Instance?.OnNewBlocksSpawned();
        
        // End performance monitoring
        BlockPerformanceMonitor.Instance?.EndTiming("BlockSpawn");
        }

    }



    // Method d? block b�o c�o khi b? destroy

    public void OnBlockDestroyed(GameObject block)

    {

        currentBlocks.Remove(block);

        

        // Ki?m tra n?u h?t block th� spawn m?i

        if (currentBlocks.Count == 0)

        {

            SpawnNewBlocks();

        }

    }

    

    // Clear t?t c? blocks trong spawn area

    public void ClearAllBlocks()

    {

        // Destroy t?t c? child objects

        for (int i = transform.childCount - 1; i >= 0; i--)

        {

            Transform child = transform.GetChild(i);

            Destroy(child.gameObject);

        }

        

        // Clear current blocks list

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



        // ----- bottom area -----

        float bottomHeight = screenHeight * bottomReservedRatio;



        float centerY =

            -screenHeight / 2f

            + bottomHeight * (0.5f + verticalPaddingRatio + raiseRatio);



        // ----- slot spacing (t�m m�n h�nh) -----

        float usableWidth =

            screenWidth * (1f - horizontalPaddingRatio * 2f);



        float slotSpacing = usableWidth / 3f;



        // Get smart blocks or random blocks
        List<List<Vector2Int>> shapesToSpawn;
        if (smartBlockSelector != null)
        {
            shapesToSpawn = smartBlockSelector.GetShapesToSpawn();
        }
        else
        {
            // Fallback to random blocks if SmartBlockSelector is not available
            shapesToSpawn = new List<List<Vector2Int>>();
            for (int i = 0; i < 3; i++)
            {
                shapesToSpawn.Add(TetrisShapes.GetRandomShapeWithRotation());
            }
            
        }

        // FIX: �?m b?o lu�n c� d? 3 shapes
        while (shapesToSpawn.Count < 3)
        {
            shapesToSpawn.Add(TetrisShapes.GetRandomShapeWithRotation());
            
        }

        // DEBUG: Log s? lu?ng shapes th?c t?
        

        

        // T?o danh s�ch index m�u ng?u nhi�n
        List<int> colorIndices = new List<int>();
        for (int i = 0; i < shapesToSpawn.Count; i++)
        {
            colorIndices.Add(Random.Range(0, blockColors.Length));
        }

        // FIX: S? d?ng s? lu?ng shapes th?c t? thay v� c? d?nh 3
        int blocksToSpawn = Mathf.Min(3, shapesToSpawn.Count);
        
        for (int i = 0; i < blocksToSpawn; i++)
        {

            float slotCenterX = (i - 1) * slotSpacing;



            GameObject block = new GameObject($"BlockPreview_{i}");

            block.transform.SetParent(transform);

            block.transform.position = new Vector3(slotCenterX, centerY, 0);



            // ?? g?n script drag NGAY KHI SPAWN

            block.AddComponent<DragBlockController>();



            // spawn cell con theo shape v?i m�u ng?u nhi�n

            SpawnBlockCells(block.transform, shapesToSpawn[i], colorIndices[i]);



            // ?? fit collider bao to�n b? block
            BlockColliderBuilder.FitColliderToChildren(block);



            // Th�m v�o tracking list

            currentBlocks.Add(block);

        }

    }

    




    // ==============================

    // 3?? Build block theo tr?ng t�m

    // ==============================

    void SpawnBlockCells(Transform parent, List<Vector2Int> shape, int colorIndex)

    {

        // Truy?n shape cho DragBlockController

        DragBlockController dragController = parent.GetComponent<DragBlockController>();

        if (dragController != null)

        {

            dragController.SetShape(shape);

            // Truy?n m�u cho DragBlockController d? d�ng cho highlight

            dragController.SetBlockColor(blockColors[colorIndex % blockColors.Length]);

        }



        // ----- t�nh centroid -----

        Vector2 centroid = Vector2.zero;

        foreach (var c in shape)

            centroid += c;

        centroid /= shape.Count;



        // ----- spawn cell -----

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

