using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridView : MonoBehaviour
{
    [Header("Grid Size")]
    public int gridSize = 8;

    public int GridSize => gridSize;

    [Header("Sprites")]
    public Sprite gridSprite;
    public Sprite cellSprite;
    public Sprite emptySprite;

    [Header("Block Colors")]
    public Color[] blockColors = new Color[5]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.magenta
    };
    
    [Header("Highlight Colors")]
    public Color[] highlightColors = new Color[] { 
        Color.red, Color.yellow, Color.green, Color.cyan, 
        Color.blue, Color.magenta, Color.white 
    };

    [Header("Screen Ratios")]
    [Range(0f, 0.3f)] public float topFreeRatio = 0.15f;
    [Range(0f, 0.5f)] public float bottomReservedRatio = 0.30f;
    [Range(0.6f, 1f)] public float gridWidthRatio = 0.9f;

    private int[,] gridData;
    private SpriteRenderer[,] renderers;
    private float cellWorldSize;
    private bool[,] highlightState; // Tracking highlight state
    private Color[,] cellColors; // Lưu màu của từng cell khi được đặt
    
    // Public access to grid data for ComboManager
    public int[,] GridData => gridData;

    [Header("Feedback")]
    public bool enableGridShake = true;
    public float shakeBaseDuration = 0.10f;
    public float shakeBaseMagnitude = 0.05f;
    public float shakeDurationPerCombo = 0.04f;
    public float shakeMagnitudePerCombo = 0.03f;
    
    [Header("Particle Effects")]
    public ParticleEffectManager particleEffectManager;

    private Vector3 basePosition;
    private Coroutine shakeRoutine;

    public float CellWorldSize => cellWorldSize;

    // Kiểm tra xem cell có bị chiếm không
    public bool IsCellOccupied(int x, int y)
    {
        if (x < 0 || x >= gridSize || y < 0 || y >= gridSize)
            return false;
            
        return gridData[x, y] == 1;
    }

    void Start()
    {
        gridData = new int[gridSize, gridSize];
        renderers = new SpriteRenderer[gridSize, gridSize];
        highlightState = new bool[gridSize, gridSize];
        cellColors = new Color[gridSize, gridSize];

        cellWorldSize = gridSprite.bounds.size.x;

        BuildGrid();
        FitToScreen();
        
        // Initialize particle effect manager if not assigned
        if (particleEffectManager == null)
        {
            particleEffectManager = FindObjectOfType<ParticleEffectManager>();
            if (particleEffectManager == null)
            {
                GameObject particleManagerObj = new GameObject("ParticleEffectManager");
                particleEffectManager = particleManagerObj.AddComponent<ParticleEffectManager>();
            }
            else
            {
            }
        }
        else
        {
        }
        
    }

    void BuildGrid()
    {
        float start = -(gridSize - 1) * 0.5f * cellWorldSize;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                cell.transform.SetParent(transform);
                cell.transform.localPosition =
                    new Vector3(start + x * cellWorldSize,
                                start + y * cellWorldSize,
                                0);

                var sr = cell.AddComponent<SpriteRenderer>();
                sr.sprite = gridSprite;
                sr.color = Color.white;
                sr.sortingOrder = 0;

                renderers[x, y] = sr;
            }
        }
    }

    public void FitToScreen()
    {
        Camera cam = Camera.main;
        float screenH = cam.orthographicSize * 2f;
        float screenW = screenH * cam.aspect;

        float gridWorldSize = gridSize * cellWorldSize;
        float scaleW = (screenW * gridWidthRatio) / gridWorldSize;
        float usableH = screenH * (1f - topFreeRatio - bottomReservedRatio);
        float scaleH = usableH / gridWorldSize;

        float scale = Mathf.Min(scaleW, scaleH);
        transform.localScale = Vector3.one * scale;

        float bottomY =
            -screenH / 2f + screenH * bottomReservedRatio;

        float centerY = bottomY + usableH / 2f;
        transform.position = new Vector3(0, centerY, 0);

        basePosition = transform.position;
    }

    // ===== LOGIC =====

    public bool CanPlace(BlockData block, int ax, int ay)
    {
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (block.mask[i, j] == 1)
                {
                    int gx = ax + i;
                    int gy = ay + j;

                    if (gx < 0 || gx >= gridSize ||
                        gy < 0 || gy >= gridSize)
                        return false;

                    if (gridData[gx, gy] == 1)
                        return false;
                }
        return true;
    }

    public void ClearGhost()
    {
        // Clear highlights trước
        ClearAllHighlights();
        
        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                if (gridData[x, y] == 0)
                {
                    renderers[x, y].sprite = gridSprite;
                    renderers[x, y].color = Color.white;
                }
    }

    // Overload cho trường hợp không có màu block (để tương thích với code cũ)
    public void ShowGhost(BlockData block, int ax, int ay)
    {
        ShowGhost(block, ax, ay, Color.white);
    }

    public void ShowGhost(BlockData block, int ax, int ay, Color blockColor)
    {
        ClearGhost();

        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (block.mask[i, j] == 1)
                {
                    int gx = ax + i;
                    int gy = ay + j;

                    renderers[gx, gy].sprite = cellSprite;
                    // Dùng màu của block với alpha 0.4f
                    Color ghostColor = new Color(blockColor.r, blockColor.g, blockColor.b, 0.4f);
                    renderers[gx, gy].color = ghostColor;
                }
        
        List<int> rowsToClear = GetRowsToClear(block, ax, ay);
        List<int> colsToClear = GetColsToClear(block, ax, ay);
        
        if (rowsToClear.Count > 0 || colsToClear.Count > 0)
        {
            HighlightLinesToClear(rowsToClear, colsToClear, blockColor, block, ax, ay);
        }
    }

    // Overload cho trường hợp không có màu block (để tương thích với code cũ)
    public void ApplyBlock(BlockData block, int ax, int ay)
    {
        ApplyBlock(block, ax, ay, Color.white);
    }
    
    public void ApplyBlock(BlockData block, int ax, int ay, Color blockColor)
    {
        int placedCells = 0;
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (block.mask[i, j] == 1)
                {
                    int gx = ax + i;
                    int gy = ay + j;

                    gridData[gx, gy] = 1;
                    renderers[gx, gy].sprite = cellSprite;
                    renderers[gx, gy].color = blockColor;
                    cellColors[gx, gy] = blockColor; // Lưu màu của cell
                    placedCells++;
                }
        
        // Notify ComboManager of block placement
        if (ComboManager.Instance != null)
        {
            ComboManager.Instance.OnBlockPlaced(block, new Vector2Int(ax, ay), blockColor);
        }
        
        // Kiểm tra và clear hàng/cột đầy sau khi đặt block
        CheckAndClearLines(out int clearedRows, out int clearedCols);

        int combo = clearedRows + clearedCols;
        if (combo > 0)
        {
            TriggerGridShake(combo);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLinesCleared(combo);
            }
            
            // Notify ComboManager for 2-round bonus system
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnLinesCleared(clearedRows, clearedCols);
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScoreForPlacement(placedCells, clearedRows, clearedCols);
        }
    }

    private void TriggerGridShake(int combo)
    {
        if (!enableGridShake) return;
        if (combo <= 0) return;

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        shakeRoutine = StartCoroutine(ShakeCoroutine(combo));
    }

    private IEnumerator ShakeCoroutine(int combo)
    {
        float duration = Mathf.Max(0f, shakeBaseDuration + (Mathf.Max(0, combo - 1) * shakeDurationPerCombo));
        float magnitude = Mathf.Max(0f, shakeBaseMagnitude + (Mathf.Max(0, combo - 1) * shakeMagnitudePerCombo));

        float t = 0f;
        while (t < duration)
        {
            Vector2 offset2 = Random.insideUnitCircle * magnitude;
            transform.position = basePosition + new Vector3(offset2.x, offset2.y, 0f);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = basePosition;
        shakeRoutine = null;
    }

    // Kiểm tra hàng/cột sẽ được clear khi đặt block vào vị trí ghost
    public List<int> GetRowsToClear(BlockData block, int ax, int ay)
    {
        List<int> rowsToClear = new List<int>();
        
        // Tạo grid tạm thời với block được thêm vào
        int[,] tempGrid = (int[,])gridData.Clone();
        
        // Apply block vào temp grid
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (block.mask[i, j] == 1)
                {
                    int gx = ax + i;
                    int gy = ay + j;
                    if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize)
                        tempGrid[gx, gy] = 1;
                }
        
        // Kiểm tra các hàng trong temp grid
        for (int y = 0; y < gridSize; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < gridSize; x++)
            {
                if (tempGrid[x, y] == 0)
                {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull)
                rowsToClear.Add(y);
        }
        
        return rowsToClear;
    }
    
    public List<int> GetColsToClear(BlockData block, int ax, int ay)
    {
        List<int> colsToClear = new List<int>();
        
        // Tạo grid tạm thời với block được thêm vào
        int[,] tempGrid = (int[,])gridData.Clone();
        
        // Apply block vào temp grid
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (block.mask[i, j] == 1)
                {
                    int gx = ax + i;
                    int gy = ay + j;
                    if (gx >= 0 && gx < gridSize && gy >= 0 && gy < gridSize)
                        tempGrid[gx, gy] = 1;
                }
        
        // Kiểm tra các cột trong temp grid
        for (int x = 0; x < gridSize; x++)
        {
            bool colFull = true;
            for (int y = 0; y < gridSize; y++)
            {
                if (tempGrid[x, y] == 0)
                {
                    colFull = false;
                    break;
                }
            }
            if (colFull)
                colsToClear.Add(x);
        }
        
        return colsToClear;
    }

    // Highlight hàng/cột sẽ được clear
    public void HighlightLinesToClear(List<int> rows, List<int> cols, Color blockColor, BlockData ghostBlock = null, int ghostAx = -999, int ghostAy = -999)
    {
        // Clear highlight cũ trước
        ClearAllHighlights();
        
        // Highlight các hàng
        foreach (int y in rows)
        {
            for (int x = 0; x < gridSize; x++)
            {
                // Highlight filled cells hoặc ghost cells
                if (gridData[x, y] == 1 || IsGhostCell(x, y, ghostBlock, ghostAx, ghostAy))
                {
                    // Dùng màu của block đang kéo cho highlight
                    Color highlightColor = new Color(blockColor.r, blockColor.g, blockColor.b, 0.8f);
                    renderers[x, y].color = highlightColor;
                    highlightState[x, y] = true;
                }
            }
        }
        
        // Highlight các cột
        foreach (int x in cols)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Highlight filled cells hoặc ghost cells
                if (gridData[x, y] == 1 || IsGhostCell(x, y, ghostBlock, ghostAx, ghostAy))
                {
                    // Dùng màu của block đang kéo cho highlight
                    Color highlightColor = new Color(blockColor.r, blockColor.g, blockColor.b, 0.8f);
                    renderers[x, y].color = highlightColor;
                    highlightState[x, y] = true;
                }
            }
        }
    }
    
    // Overload cho trường hợp không có màu block (để tương thích với code cũ)
    public void HighlightLinesToClear(List<int> rows, List<int> cols, BlockData ghostBlock = null, int ghostAx = -999, int ghostAy = -999)
    {
        HighlightLinesToClear(rows, cols, Color.white, ghostBlock, ghostAx, ghostAy);
    }
    
    // Kiểm tra xem cell có phải là ghost cell không
    private bool IsGhostCell(int x, int y, BlockData ghostBlock, int ghostAx, int ghostAy)
    {
        if (ghostBlock == null || ghostAx == -999 || ghostAy == -999)
            return false;
            
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                if (ghostBlock.mask[i, j] == 1)
                {
                    int gx = ghostAx + i;
                    int gy = ghostAy + j;
                    if (gx == x && gy == y)
                        return true;
                }
        return false;
    }
    
    // Clear tất cả highlights
    public void ClearAllHighlights()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (highlightState[x, y])
                {
                    // Reset về màu gốc của cell khi được đặt
                    if (gridData[x, y] == 1)
                    {
                        renderers[x, y].color = cellColors[x, y];
                    }
                    highlightState[x, y] = false;
                }
            }
        }
    }

    // Kiểm tra và clear hàng/cột đầy
    void CheckAndClearLines(out int clearedRows, out int clearedCols)
    {
        clearedRows = 0;
        clearedCols = 0;

        List<int> rowsToClear = new List<int>();
        List<int> colsToClear = new List<int>();
        
        // Kiểm tra các hàng
        for (int y = 0; y < gridSize; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < gridSize; x++)
            {
                if (gridData[x, y] == 0)
                {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull)
            {
                rowsToClear.Add(y);
            }
        }
        
        // Kiểm tra các cột
        for (int x = 0; x < gridSize; x++)
        {
            bool colFull = true;
            for (int y = 0; y < gridSize; y++)
            {
                if (gridData[x, y] == 0)
                {
                    colFull = false;
                    break;
                }
            }
            if (colFull)
            {
                colsToClear.Add(x);
            }
        }
        
        // Clear các hàng và cột đầy
        if (rowsToClear.Count > 0 || colsToClear.Count > 0)
        {
            clearedRows = rowsToClear.Count;
            clearedCols = colsToClear.Count;
            ClearLines(rowsToClear, colsToClear);
        }
    }
    
    // Clear các hàng và cột đã chọn
    void ClearLines(List<int> rows, List<int> cols)
    {
        List<Vector2Int> clearedPositions = new List<Vector2Int>();
        List<Color> clearedColors = new List<Color>();
        
        
        // Clear các hàng
        foreach (int y in rows)
        {
            for (int x = 0; x < gridSize; x++)
            {
                // Store position and color before clearing
                if (gridData[x, y] == 1)
                {
                    clearedPositions.Add(new Vector2Int(x, y));
                    clearedColors.Add(cellColors[x, y]);
                    
                    
                    // Play particle effect for this cell
                    if (particleEffectManager != null)
                    {
                        particleEffectManager.PlayCellDestroyEffect(x, y, cellColors[x, y]);
                    }
                    else
                    {
                    }
                }
                
                gridData[x, y] = 0;
                // Dùng gridSprite cho empty state (grid background)
                renderers[x, y].sprite = gridSprite;
                renderers[x, y].color = Color.white;
            }
        }
        
        // Clear các cột
        foreach (int x in cols)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Store position and color before clearing (avoid duplicates)
                if (gridData[x, y] == 1)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (!clearedPositions.Contains(pos))
                    {
                        clearedPositions.Add(pos);
                        clearedColors.Add(cellColors[x, y]);
                        
                        
                        // Play particle effect for this cell
                        if (particleEffectManager != null)
                        {
                            particleEffectManager.PlayCellDestroyEffect(x, y, cellColors[x, y]);
                        }
                    }
                }
                
                gridData[x, y] = 0;
                // Dùng gridSprite cho empty state (grid background)
                renderers[x, y].sprite = gridSprite;
                renderers[x, y].color = Color.white;
            }
        }
        
        // Play enhanced line clear effect for multiple cells
        if (clearedPositions.Count > 1 && particleEffectManager != null)
        {
            particleEffectManager.PlayLineClearEffect(clearedPositions, clearedColors);
        }
        
    }

    public Vector3 GetAnchorWorldPosition(int ax, int ay)
    {
        float offset =
            -(gridSize - 1) * 0.5f * cellWorldSize;

        Vector3 localPos =
            new Vector3(offset + ax * cellWorldSize,
                        offset + ay * cellWorldSize,
                        0);

        return transform.TransformPoint(localPos);
    }
    
    // Clear toàn bộ grid về trạng thái trống
    public void ClearGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                gridData[x, y] = 0;
                renderers[x, y].sprite = gridSprite;
                renderers[x, y].color = Color.white;
                cellColors[x, y] = Color.white; // Reset màu về trắng
                highlightState[x, y] = false;
            }
        }
        
        ClearGhost();
    }
    
    // Get cell renderer tại vị trí cụ thể
    public SpriteRenderer GetCellRenderer(int x, int y)
    {
        if (x >= 0 && x < gridSize && y >= 0 && y < gridSize)
        {
            return renderers[x, y];
        }
        return null;
    }
}
