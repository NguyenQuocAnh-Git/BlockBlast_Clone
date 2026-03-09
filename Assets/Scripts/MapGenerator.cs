using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Generation Settings")]
    public int minObstacles = 3;
    public int maxObstacles = 8;
    public float obstacleDensity = 0.06f; // 6% cells filled with obstacles (reduced from 10%)
    
    [Header("Scoring-Friendly Patterns")]
    public bool enableEasyMode = true; // Creates more scoring opportunities
    public bool createLinePatterns = true; // Create near-full rows/columns
    public bool createRectanglePatterns = true; // Create rectangle formation opportunities
    public bool createPerfectClearPatterns = true; // Create perfect clear opportunities
    public float scoringPatternWeight = 0.7f; // 70% chance for scoring-friendly patterns
    
    [Header("Obstacle Patterns")]
    public bool useRandomPatterns = true;
    public bool useSymmetricPatterns = false;
    public bool useClusterPatterns = false; // Disabled - creates difficult patterns
    
    private GridView gridView;
    private int gridSize;
    
    // Expose grid size for other systems (read-only)
    public int GridSize => gridSize;
    
    void Awake()
    {
        gridView = FindObjectOfType<GridView>();
        gridSize = 8; // Hardcoded grid size
    }
    
    // Generate random obstacles khi game bắt đầu
    public void GenerateRandomMap()
    {
        // Đảm bảo gridView reference được set
        if (gridView == null)
        {
            gridView = FindObjectOfType<GridView>();
        }
        
        if (gridView == null)
        {
            
            return;
        }
        
        // KHÔNG clear grid - grid đã được khởi tạo sẵn trong GridView.Start()
        // Chỉ generate obstacles
        
        // Generate obstacles with scoring-friendly patterns
        if (enableEasyMode && Random.value < scoringPatternWeight)
        {
            GenerateScoringFriendlyMap();
        }
        else
        {
            // Fallback to original patterns
            if (useRandomPatterns)
            {
                GenerateRandomObstacles();
            }
            
            if (useSymmetricPatterns)
            {
                GenerateSymmetricObstacles();
            }
        }
        
        
    }
    
    // Generate random obstacles đơn giản
    private void GenerateRandomObstacles()
    {
        int obstacleCount = Random.Range(minObstacles, maxObstacles + 1);
        
        for (int i = 0; i < obstacleCount; i++)
        {
            int x = Random.Range(0, gridSize);
            int y = Random.Range(0, gridSize);
            
            // Đặt obstacle
            PlaceObstacle(x, y);
        }
    }
    
    // Generate symmetric obstacles
    private void GenerateSymmetricObstacles()
    {
        int patternCount = Random.Range(1, 4);
        
        for (int i = 0; i < patternCount; i++)
        {
            // Generate pattern ở một phần của grid
            int x = Random.Range(0, gridSize / 2);
            int y = Random.Range(0, gridSize / 2);
            
            // Mirror pattern để tạo symmetry
            List<Vector2Int> pattern = GenerateSmallPattern(x, y);
            
            foreach (var pos in pattern)
            {
                // Original
                PlaceObstacle(pos.x, pos.y);
                // Mirror X
                PlaceObstacle(gridSize - 1 - pos.x, pos.y);
                // Mirror Y
                PlaceObstacle(pos.x, gridSize - 1 - pos.y);
                // Mirror both
                PlaceObstacle(gridSize - 1 - pos.x, gridSize - 1 - pos.y);
            }
        }
    }
    
        
    // Generate small pattern cho symmetric
    private List<Vector2Int> GenerateSmallPattern(int startX, int startY)
    {
        List<Vector2Int> pattern = new List<Vector2Int>();
        int patternSize = Random.Range(2, 4);
        
        for (int i = 0; i < patternSize; i++)
        {
            int x = startX + Random.Range(0, 2);
            int y = startY + Random.Range(0, 2);
            
            if (x < gridSize / 2 && y < gridSize / 2)
            {
                pattern.Add(new Vector2Int(x, y));
            }
        }
        
        return pattern;
    }
    
    // Place obstacle tại vị trí cụ thể
    private void PlaceObstacle(int x, int y)
    {
        if (x >= 0 && x < gridSize && y >= 0 && y < gridSize)
        {
            // Tạo obstacle data và apply vào grid
            BlockData obstacle = new BlockData(new List<Vector2Int> { Vector2Int.zero });
            gridView.ApplyBlock(obstacle, x, y, Color.white);
            
            // Obstacles có màu giống như filled cells bình thường (màu trắng)
            // Không cần thay đổi màu vì ApplyBlock đã set màu trắng
        }
    }
    
    // Kiểm tra có phải vùng trung tâm không
    private bool IsCenterArea(int x, int y)
    {
        int centerStart = gridSize / 2 - 1;
        int centerEnd = gridSize / 2 + 1;
        
        return (x >= centerStart && x <= centerEnd && 
                y >= centerStart && y <= centerEnd);
    }
    
    // Get danh sách empty cells
    public List<Vector2Int> GetEmptyCells()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();
        
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (gridView.CanPlace(new BlockData(new List<Vector2Int> { Vector2Int.zero }), x, y))
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }
        
        return emptyCells;
    }
    
    // Generate scoring-friendly map with strategic patterns
    private void GenerateScoringFriendlyMap()
    {
        List<Vector2Int> occupiedPositions = new List<Vector2Int>();
        
        // Create patterns based on enabled settings
        if (createLinePatterns)
        {
            CreateLineCompletionPatterns(occupiedPositions);
        }
        
        if (createRectanglePatterns)
        {
            CreateRectanglePatterns(occupiedPositions);
        }
        
        if (createPerfectClearPatterns)
        {
            CreatePerfectClearPatterns(occupiedPositions);
        }
        
        // Fill remaining obstacles randomly if needed
        FillRemainingObstacles(occupiedPositions);
    }
    
    // Create near-full rows/columns for easy line clears
    private void CreateLineCompletionPatterns(List<Vector2Int> occupiedPositions)
    {
        int patternsToCreate = Random.Range(1, 3);
        
        for (int i = 0; i < patternsToCreate; i++)
        {
            bool createRow = Random.value > 0.5f;
            int lineIndex = Random.Range(0, gridSize);
            
            if (createRow)
            {
                // Create near-full row (6-7 cells)
                int cellsToFill = Random.Range(6, 8);
                List<int> availableCols = new List<int>();
                for (int x = 0; x < gridSize; x++)
                {
                    availableCols.Add(x);
                }
                
                // Shuffle and pick cells
                for (int j = 0; j < cellsToFill && availableCols.Count > 0; j++)
                {
                    int randomIndex = Random.Range(0, availableCols.Count);
                    int col = availableCols[randomIndex];
                    availableCols.RemoveAt(randomIndex);
                    
                    Vector2Int pos = new Vector2Int(col, lineIndex);
                    if (!occupiedPositions.Contains(pos))
                    {
                        PlaceObstacle(pos.x, pos.y);
                        occupiedPositions.Add(pos);
                    }
                }
            }
            else
            {
                // Create near-full column (6-7 cells)
                int cellsToFill = Random.Range(6, 8);
                List<int> availableRows = new List<int>();
                for (int y = 0; y < gridSize; y++)
                {
                    availableRows.Add(y);
                }
                
                // Shuffle and pick cells
                for (int j = 0; j < cellsToFill && availableRows.Count > 0; j++)
                {
                    int randomIndex = Random.Range(0, availableRows.Count);
                    int row = availableRows[randomIndex];
                    availableRows.RemoveAt(randomIndex);
                    
                    Vector2Int pos = new Vector2Int(lineIndex, row);
                    if (!occupiedPositions.Contains(pos))
                    {
                        PlaceObstacle(pos.x, pos.y);
                        occupiedPositions.Add(pos);
                    }
                }
            }
        }
    }
    
    // Create rectangle formation opportunities
    private void CreateRectanglePatterns(List<Vector2Int> occupiedPositions)
    {
        int rectanglesToCreate = Random.Range(1, 2);
        
        for (int i = 0; i < rectanglesToCreate; i++)
        {
            // Common rectangle sizes: 2x2, 2x3, 3x2, 3x3
            int width = Random.Range(2, 4);
            int height = Random.Range(2, 4);
            
            int startX = Random.Range(0, gridSize - width + 1);
            int startY = Random.Range(0, gridSize - height + 1);
            
            // Fill 60-80% of rectangle to create opportunity
            int cellsToFill = Mathf.RoundToInt((width * height) * Random.Range(0.6f, 0.8f));
            
            List<Vector2Int> rectCells = new List<Vector2Int>();
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    rectCells.Add(new Vector2Int(x, y));
                }
            }
            
            // Shuffle and place cells
            for (int j = 0; j < cellsToFill && rectCells.Count > 0; j++)
            {
                int randomIndex = Random.Range(0, rectCells.Count);
                Vector2Int pos = rectCells[randomIndex];
                rectCells.RemoveAt(randomIndex);
                
                if (!occupiedPositions.Contains(pos))
                {
                    PlaceObstacle(pos.x, pos.y);
                    occupiedPositions.Add(pos);
                }
            }
        }
    }
    
    // Create perfect clear opportunities
    private void CreatePerfectClearPatterns(List<Vector2Int> occupiedPositions)
    {
        // Create patterns that can be cleared with 1-2 strategic blocks
        int patternType = Random.Range(0, 3);
        
        switch (patternType)
        {
            case 0: // L-shape pattern
                CreateLShapePattern(occupiedPositions);
                break;
            case 1: // T-shape pattern
                CreateTShapePattern(occupiedPositions);
                break;
            case 2: // Cross pattern
                CreateCrossPattern(occupiedPositions);
                break;
        }
    }
    
    private void CreateLShapePattern(List<Vector2Int> occupiedPositions)
    {
        // Create L-shape that can be completed with an L-block
        int startX = Random.Range(0, gridSize - 3);
        int startY = Random.Range(0, gridSize - 3);
        
        Vector2Int[] lShape = {
            new Vector2Int(startX, startY),
            new Vector2Int(startX + 1, startY),
            new Vector2Int(startX + 2, startY),
            new Vector2Int(startX, startY + 1),
            new Vector2Int(startX, startY + 2)
        };
        
        foreach (var pos in lShape)
        {
            if (!occupiedPositions.Contains(pos))
            {
                PlaceObstacle(pos.x, pos.y);
                occupiedPositions.Add(pos);
            }
        }
    }
    
    private void CreateTShapePattern(List<Vector2Int> occupiedPositions)
    {
        // Create T-shape that can be completed with a T-block
        int startX = Random.Range(0, gridSize - 3);
        int startY = Random.Range(0, gridSize - 2);
        
        Vector2Int[] tShape = {
            new Vector2Int(startX, startY),
            new Vector2Int(startX + 1, startY),
            new Vector2Int(startX + 2, startY),
            new Vector2Int(startX + 1, startY + 1)
        };
        
        foreach (var pos in tShape)
        {
            if (!occupiedPositions.Contains(pos))
            {
                PlaceObstacle(pos.x, pos.y);
                occupiedPositions.Add(pos);
            }
        }
    }
    
    private void CreateCrossPattern(List<Vector2Int> occupiedPositions)
    {
        // Create cross pattern that can be completed with specific blocks
        int centerX = Random.Range(1, gridSize - 2);
        int centerY = Random.Range(1, gridSize - 2);
        
        Vector2Int[] cross = {
            new Vector2Int(centerX, centerY),
            new Vector2Int(centerX - 1, centerY),
            new Vector2Int(centerX + 1, centerY),
            new Vector2Int(centerX, centerY - 1),
            new Vector2Int(centerX, centerY + 1)
        };
        
        foreach (var pos in cross)
        {
            if (!occupiedPositions.Contains(pos))
            {
                PlaceObstacle(pos.x, pos.y);
                occupiedPositions.Add(pos);
            }
        }
    }
    
    // Fill remaining obstacles to meet difficulty requirements
    private void FillRemainingObstacles(List<Vector2Int> occupiedPositions)
    {
        int targetObstacles = Random.Range(minObstacles, maxObstacles + 1);
        int currentObstacles = occupiedPositions.Count;
        
        if (currentObstacles >= targetObstacles) return;
        
        int obstaclesToAdd = targetObstacles - currentObstacles;
        
        for (int i = 0; i < obstaclesToAdd; i++)
        {
            int attempts = 0;
            while (attempts < 50)
            {
                int x = Random.Range(0, gridSize);
                int y = Random.Range(0, gridSize);
                Vector2Int pos = new Vector2Int(x, y);
                
                if (!occupiedPositions.Contains(pos))
                {
                    PlaceObstacle(x, y);
                    occupiedPositions.Add(pos);
                    break;
                }
                
                attempts++;
            }
        }
    }
}
