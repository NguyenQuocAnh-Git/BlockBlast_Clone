using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class SmartBlockSpawner : MonoBehaviour
{
    [Header("Smart Spawning Settings")]
    public bool enableSmartSpawning = true;
    [Range(0f, 1f)]
    public float baseFittingBlockChance = 0.3f; // Base 30% chance to spawn fitting blocks
    [Range(0f, 1f)]
    public float baseScoringBlockChance = 0.7f; // Base 70% chance to spawn scoring blocks
    
    [Header("Dynamic Difficulty")]
    public bool enableDynamicDifficulty = true;
    public int scoreThresholdStep = 600; // Mỗi 600 điểm điều chỉnh difficulty
    public float maxFittingChance = 0.8f; // Tối đa 80% fitting blocks
    public float difficultyTransitionSpeed = 0.2f; // Tốc độ chuyển tiếp difficulty (tăng nhanh hơn)
    
    [Header("Block Selection - Enhanced for Large Blocks")]
    public bool preferSmallerBlocks = false; // Giữ false để ưu tiên block lớn
    public bool preferFittingBlocks = true;
    public int minBlockComplexity = 4; // Giảm xuống 4 - cho phép block 4+ ô
    public int maxBlockComplexity = 9; // Maximum cells in a block
    
    [Header("Large Block Priority - Enhanced")]
    public bool enableLargeBlockBonus = true;
    public float largeBlockMultiplier = 25f; // Tăng lên 25x để ưu tiên block cực lớn
    public bool forceLargeBlocks = true;
    public int largeBlockThreshold = 5; // Block 5+ ô được coi là lớn
    public float superLargeBlockBonus = 50f; // Bonus cho block 8+ ô
    public float ultraLargeBlockBonus = 100f; // Bonus cho block 9 ô
    
    [Header("Anti-Deadlock System - NEW")]
    public bool enableAntiDeadlock = true;
    public int minEmptyCellsForLargeBlock = 12; // Tối thiểu 12 ô trống mới spawn block lớn
    public float largeBlockDeadlockThreshold = 0.3f; // 30% space ratio mới cho block lớn
    public bool preferMediumWhenCrowded = true; // Khi grid đông, ưu tiên block vừa
    public int crowdedThreshold = 20; // 20+ ô đã đặt là grid đông
    public float mediumBlockPreferenceWhenCrowded = 0.7f; // 70% ưu tiên block vừa khi đông
    
    [Header("2-Round Combo System")]
    public bool enableTwoRoundCombo = true;
    public int comboRoundWindow = 2; // 2 lượt spawn
    public int comboBlockTarget = 6; // 6 blocks total
    public float comboBlockPriorityMultiplier = 30f; // Ưu tiên cao cho block có khả năng combo
    public bool trackRecentPlacements = true;
    
    [Header("Shape Diversity")]
    public bool ensureShapeVariety = false; // Tắt để ưu tiên block lớn hơn
    public float maxSameShapeRatio = 0.8f; // Tăng lên 80% cho phép nhiều block cùng size
    
    [Header("Grid Clear Priority - NEW")]
    public bool enableGridClearPriority = true;
    public float gridClearBonusMultiplier = 15f; // Bonus cho block có khả năng clear nhiều lines
    public int minLinesForGridClearBonus = 3; // Tối thiểu 3 lines để được bonus
    public bool preferMultiDirectionClears = true; // Ưu tiên block clear cả hàng và cột
    
    // References
    private MapGenerator mapGenerator;
    private GridView gridView;
    private GameManager gameManager;
    private float currentFittingChance; // Tỷ lệ fitting hiện tại
    
    // Cache system để tránh tính toán lại
    private static Dictionary<string, List<List<Vector2Int>>> _fittingBlocksCache = new Dictionary<string, List<List<Vector2Int>>>();
    private static Dictionary<string, Dictionary<List<Vector2Int>, float>> _blockScoresCache = new Dictionary<string, Dictionary<List<Vector2Int>, float>>();
    private static Dictionary<string, int> _fullLinesCache = new Dictionary<string, int>();
    private string _lastGridHash = "";
    private int _lastScore = -1;
    
    // Grid change detection - chỉ clear cache khi grid thực sự thay đổi
    private string _lastProcessedGridHash = "";
    private int _lastProcessedScore = -1;
    
    // Smart shape testing optimization
    private List<List<Vector2Int>> _preFilteredShapes = new List<List<Vector2Int>>();
    private Dictionary<int, List<List<Vector2Int>>> _shapesBySize = new Dictionary<int, List<List<Vector2Int>>>();
    private bool _shapeDatabaseInitialized = false;
    private int _maxShapesToTest = 8; // Reduced from 12 for performance
    
    // 2-Round Combo Tracking
    private List<List<Vector2Int>> recentSpawnedBlocks = new List<List<Vector2Int>>();
    private int currentComboRound = 0;
    private int blocksInCurrentCombo = 0;
    
    void Awake()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        gridView = FindObjectOfType<GridView>();
        gameManager = FindObjectOfType<GameManager>();
        currentFittingChance = baseFittingBlockChance; // Khởi tạo với giá trị base
        
        // Initialize shape database for smart testing
        InitializeShapeDatabase();
    }
    
    /// <summary>
    /// Initialize shape database for smart testing optimization
    /// </summary>
    private void InitializeShapeDatabase()
    {
        if (_shapeDatabaseInitialized) return;
        
        // Pre-categorize shapes by size for faster lookup
        _shapesBySize.Clear();
        
        foreach (var shape in ShapeDatabase.AllShapeVariations)
        {
            int size = shape.Count;
            if (!_shapesBySize.ContainsKey(size))
            {
                _shapesBySize[size] = new List<List<Vector2Int>>();
            }
            _shapesBySize[size].Add(shape);
        }
        
        // Sort each size category by priority (larger first)
        foreach (var kvp in _shapesBySize)
        {
            kvp.Value.Sort((a, b) => b.Count.CompareTo(a.Count));
        }
        
        _shapeDatabaseInitialized = true;
    }
    
    /// <summary>
    /// Tính hash nhanh từ empty cells để cache
    /// </summary>
    private string CalculateGridHash(List<Vector2Int> emptyCells)
    {
        StringBuilder sb = new StringBuilder(emptyCells.Count * 4);
        
        // Sắp xếp để đảm bảo hash consistency
        var sortedCells = emptyCells.OrderBy(c => c.x * 8 + c.y).ToList();
        foreach (var cell in sortedCells)
        {
            sb.Append($"{cell.x},{cell.y};");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Clear tất cả caches (để test/debug)
    /// </summary>
    public void ClearAllCaches()
    {
        _fittingBlocksCache.Clear();
        _blockScoresCache.Clear();
        _fullLinesCache.Clear();
        _lastGridHash = "";
        _lastScore = -1;
    }
    
    /// <summary>
    /// Get cache statistics (để monitoring)
    /// </summary>
    public string GetCacheStats()
    {
        return $"Fitting Cache: {_fittingBlocksCache.Count}, Scores Cache: {_blockScoresCache.Count}, FullLines Cache: {_fullLinesCache.Count}";
    }

    private static List<Vector2Int> NormalizeShape(List<Vector2Int> shape)
    {
        if (shape == null || shape.Count == 0) return shape;

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (var pos in shape)
        {
            minX = Mathf.Min(minX, pos.x);
            minY = Mathf.Min(minY, pos.y);
        }

        Vector2Int offset = new Vector2Int(-minX, -minY);
        List<Vector2Int> normalized = new List<Vector2Int>(shape.Count);
        foreach (var pos in shape)
        {
            normalized.Add(pos + offset);
        }

        return normalized;
    }

    private static string ShapeSignature(List<Vector2Int> shape)
    {
        if (shape == null) return string.Empty;
        List<Vector2Int> sorted = new List<Vector2Int>(shape);
        sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return string.Join(";", sorted);
    }
    
    // Get smart block list dựa trên current grid state với cache optimization
    public List<List<Vector2Int>> GetSmartBlockList(int blockCount)
    {
        List<List<Vector2Int>> selectedBlocks = new List<List<Vector2Int>>();
        
        if (!enableSmartSpawning || mapGenerator == null || gridView == null)
        {
            // Fallback to random blocks
            return GetRandomBlocks(blockCount);
        }
        
        // Get empty cells
        List<Vector2Int> emptyCells = mapGenerator.GetEmptyCells();
        
        // OPTIMIZATION: Chỉ clear cache khi grid thực sự thay đổi
        string currentGridHash = CalculateGridHash(emptyCells);
        int currentScore = gameManager != null ? gameManager.GetCurrentScore() : 0;
        
        if (currentGridHash != _lastProcessedGridHash || currentScore != _lastProcessedScore)
        {
            // Grid đã thay đổi, clear cache cũ
            ClearOptimizationCaches();
            _lastProcessedGridHash = currentGridHash;
            _lastProcessedScore = currentScore;
        }
        
        if (emptyCells.Count == 0)
        {
            // No empty cells, return random blocks (game over situation)
            return GetRandomBlocks(blockCount);
        }
        
        // Track combo rounds
        if (enableTwoRoundCombo)
        {
            TrackComboRound();
        }
        
        // Analyze grid và select appropriate blocks
        for (int i = 0; i < blockCount; i++)
        {
            List<Vector2Int> selectedBlock = SelectSmartBlockCached(emptyCells);
            selectedBlocks.Add(selectedBlock);
            
            // Track for combo system
            if (enableTwoRoundCombo && trackRecentPlacements)
            {
                recentSpawnedBlocks.Add(selectedBlock);
                blocksInCurrentCombo++;
            }
        }
        
        // Đảm bảo đa dạng shapes nếu được bật
        if (ensureShapeVariety)
        {
            selectedBlocks = EnsureShapeVariety(selectedBlocks, emptyCells);
        }
        
        return selectedBlocks;
    }
    
    // Select smart block với cache optimization
    private List<Vector2Int> SelectSmartBlockCached(List<Vector2Int> emptyCells)
    {
        // Cập nhật tỷ lệ fitting dựa trên điểm số hiện tại
        UpdateDynamicDifficulty();
        
        // ANTI-DEADLOCK: Kiểm tra không gian trước khi spawn block lớn
        if (enableAntiDeadlock)
        {
            int occupiedCells = 64 - emptyCells.Count;
            float spaceRatio = (float)emptyCells.Count / 64f;
            
            // Grid quá đông -> ưu tiên block vừa/nhỏ
            if (occupiedCells >= crowdedThreshold)
            {
                if (Random.value < mediumBlockPreferenceWhenCrowded)
                {
                    return SelectMediumBlock(emptyCells);
                }
            }
            
            // Không đủ không gian cho block lớn
            if (emptyCells.Count < minEmptyCellsForLargeBlock || spaceRatio < largeBlockDeadlockThreshold)
            {
                return SelectSmallerBlock(emptyCells);
            }
        }
        
        // Xác định loại block sẽ spawn: fitting hay scoring
        bool spawnFittingBlock = Random.value < currentFittingChance;
        
        if (spawnFittingBlock)
        {
            // fitting%: Ưu tiên block lớn nhất có thể đặt vào grid
            List<List<Vector2Int>> fittingBlocks = FindFittingBlocksCached(emptyCells);
            
            if (fittingBlocks.Count == 0)
            {
                return ShapeDatabase.GetRandomShapeVariation();
            }
            
            // Ưu tiên block lớn nhất trong các fitting blocks
            Dictionary<List<Vector2Int>, float> scores = GetBlockScoresCached(fittingBlocks, emptyCells);
            
            var selectedBlock = SelectBlockByScore(scores);
            return selectedBlock;
        }
        else
        {
            // scoring%: Spawn block có thể gây điểm (scoring)
            return SelectScoringBlockCached(emptyCells);
        }
    }
    
    // NEW: Chọn block vừa khi grid quá đông
    private List<Vector2Int> SelectMediumBlock(List<Vector2Int> emptyCells)
    {
        List<List<Vector2Int>> fittingBlocks = FindFittingBlocksCached(emptyCells);
        
        // Lọc chỉ block vừa (4-6 ô)
        List<List<Vector2Int>> mediumBlocks = fittingBlocks.Where(block => 
            block.Count >= 4 && block.Count <= 6).ToList();
        
        if (mediumBlocks.Count > 0)
        {
            // Ưu tiên block vừa lớn nhất trong số medium
            Dictionary<List<Vector2Int>, float> scores = new Dictionary<List<Vector2Int>, float>();
            foreach (var block in mediumBlocks)
            {
                float s = CalculateEnhancedScore(block, emptyCells);
                // Bonus cho medium block khi grid đông
                s += 20f; // +20 điểm để khuyến khích medium block
                scores[block] = s;
            }
            
            var selectedBlock = SelectBlockByScore(scores);
            return selectedBlock;
        }
        
        // Fallback đến block nhỏ hơn
        return SelectSmallerBlock(emptyCells);
    }
    
    // NEW: Chọn block nhỏ khi không gian hạn chế
    private List<Vector2Int> SelectSmallerBlock(List<Vector2Int> emptyCells)
    {
        List<List<Vector2Int>> fittingBlocks = FindFittingBlocksCached(emptyCells);
        
        // Ưu tiên block nhỏ nhất có thể (2-4 ô)
        List<List<Vector2Int>> smallBlocks = fittingBlocks.Where(block => 
            block.Count >= 2 && block.Count <= 4).ToList();
        
        if (smallBlocks.Count > 0)
        {
            // Sắp xếp theo kích thước tăng dần để ưu tiên block nhỏ nhất
            smallBlocks.Sort((a, b) => a.Count.CompareTo(b.Count));
            
            // Chọn block nhỏ nhất với weighted random (vẫn ưu tiên nhỏ nhất)
            var selectedBlock = smallBlocks[Random.Range(0, Mathf.Min(3, smallBlocks.Count))];
            
            return selectedBlock;
        }
        
        return ShapeDatabase.GetRandomShapeVariation();
    }
    
    /// <summary>
    /// Select scoring block với cache optimization
    /// </summary>
    private List<Vector2Int> SelectScoringBlockCached(List<Vector2Int> emptyCells)
    {
        // Analyze empty cell patterns (placeable blocks)
        List<List<Vector2Int>> fittingBlocks = FindFittingBlocksCached(emptyCells);
        
        if (fittingBlocks.Count == 0)
        {
            return ShapeDatabase.GetRandomShapeVariation();
        }

        // Ưu tiên block lớn nhất có thể clear tốt
        if (forceLargeBlocks)
        {
            // Tìm các block lớn (5+ ô) có thể đặt vào
            var largeBlocks = fittingBlocks.Where(block => block.Count >= largeBlockThreshold).ToList();
            
            if (largeBlocks.Count > 0)
            {
                // Ưu tiên block lớn nhất trong scoring
                fittingBlocks = largeBlocks;
            }
        }
        
        // Priority: maximize newly-completed full rows+cols (best placement)
        int baselineFullLines = CountFullLinesCached(emptyCells, out int[] baseRowCounts, out int[] baseColCounts);

        Dictionary<List<Vector2Int>, int> bestDeltaByBlock = new Dictionary<List<Vector2Int>, int>();
        int maxDelta = 0;

        // Optimization: chỉ test top 15 blocks để giảm tính toán (giảm từ 20)
        var topBlocks = fittingBlocks.OrderByDescending(b => b.Count).Take(15).ToList();
        
        foreach (var block in topBlocks)
        {
            int bestDelta = EvaluateBestNewFullLinesOptimized(block, emptyCells, baselineFullLines, baseRowCounts, baseColCounts);
            bestDeltaByBlock[block] = bestDelta;
            if (bestDelta > maxDelta) maxDelta = bestDelta;
        }

        List<List<Vector2Int>> priorityCandidates = topBlocks;
        if (maxDelta > 0)
        {
            priorityCandidates = topBlocks.Where(b => bestDeltaByBlock[b] == maxDelta).ToList();
        }

        // Secondary scoring inside the chosen priority group
        Dictionary<List<Vector2Int>, float> scores = GetBlockScoresCached(priorityCandidates, emptyCells);
        
        var selectedScoringBlock = SelectBlockByScore(scores);
        
        return selectedScoringBlock;
    }
    
    /// <summary>
    /// Count full lines với cache optimization và improved persistence
    /// </summary>
    private int CountFullLinesCached(List<Vector2Int> emptyCells, out int[] rowCounts, out int[] colCounts)
    {
        string gridHash = CalculateGridHash(emptyCells);
        
        if (_fullLinesCache.ContainsKey(gridHash))
        {
            // Recalculate row/col counts từ cache
            rowCounts = new int[8];
            colCounts = new int[8];
            
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    if (!emptyCells.Contains(new Vector2Int(x, y)))
                    {
                        rowCounts[y]++;
                        colCounts[x]++;
                    }
                }
            }
            
            int full = 0;
            for (int i = 0; i < 8; i++)
            {
                if (rowCounts[i] == 8) full++;
                if (colCounts[i] == 8) full++;
            }
            
            return full;
        }
        
        rowCounts = new int[8];
        colCounts = new int[8];

        // occupied = all cells NOT in emptyCells
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (!emptyCells.Contains(new Vector2Int(x, y)))
                {
                    rowCounts[y]++;
                    colCounts[x]++;
                }
            }
        }

        int fullLines = 0;
        for (int i = 0; i < 8; i++)
        {
            if (rowCounts[i] == 8) fullLines++;
            if (colCounts[i] == 8) fullLines++;
        }
        
        _fullLinesCache[gridHash] = fullLines;
        
        // OPTIMIZATION: Tăng cache size limit và sử dụng LRU-style cleanup (tăng từ 200 lên 300)
        if (_fullLinesCache.Count > 300) // Tăng từ 200 lên 300
        {
            // Remove oldest entries thay vì chỉ remove first entry
            var keysToRemove = _fullLinesCache.Keys.Take(100).ToList(); // Giữ lại 200 entries
            foreach (var key in keysToRemove)
            {
                _fullLinesCache.Remove(key);
            }
        }

        return fullLines;
    }

    /// <summary>
    /// Evaluate best new full lines với optimization
    /// </summary>
    private int EvaluateBestNewFullLinesOptimized(
        List<Vector2Int> block,
        List<Vector2Int> emptyCells,
        int baselineFullLines,
        int[] baseRowCounts,
        int[] baseColCounts)
    {
        int bestDelta = 0;
        var boundingBox = GetShapeBoundingBox(block);
        
        // Optimization: reuse hash set if empty cells haven't changed
        HashSet<Vector2Int> emptySet;
        if (_lastEmptyCells != emptyCells)
        {
            _lastEmptyCells = emptyCells;
            _lastEmptySet = new HashSet<Vector2Int>(emptyCells);
        }
        emptySet = _lastEmptySet;
        
        int minTestX = Mathf.Max(0, -boundingBox.minX);
        int maxTestX = Mathf.Min(8 - boundingBox.width, 8);
        int minTestY = Mathf.Max(0, -boundingBox.minY);
        int maxTestY = Mathf.Min(8 - boundingBox.height, 8);
        
        for (int anchorX = minTestX; anchorX <= maxTestX; anchorX++)
        {
            for (int anchorY = minTestY; anchorY <= maxTestY; anchorY++)
            {
                if (!CanPlaceShapeAtOptimized(block, anchorX, anchorY, emptySet))
                    continue;

                int[] rowCounts = (int[])baseRowCounts.Clone();
                int[] colCounts = (int[])baseColCounts.Clone();

                foreach (var cell in block)
                {
                    int worldX = anchorX + cell.x;
                    int worldY = anchorY + cell.y;

                    // CanPlaceShapeAtOptimized guarantees bounds and emptiness
                    rowCounts[worldY]++;
                    colCounts[worldX]++;
                }

                int full = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (rowCounts[i] == 8) full++;
                    if (colCounts[i] == 8) full++;
                }

                int delta = full - baselineFullLines;
                if (delta > bestDelta) bestDelta = delta;
                
                // Early exit nếu perfect delta
                if (bestDelta >= block.Count) return bestDelta;
            }
        }

        return bestDelta;
    }

        
    /// <summary>
    /// Kiểm tra đặt shape tại vị trí cụ thể (optimized with HashSet)
    /// </summary>
    private bool CanPlaceShapeAtExactPositionOptimized(List<Vector2Int> shape, int startX, int startY, HashSet<Vector2Int> emptySet)
    {
        int gridSize = 8;
        
        foreach (var offset in shape)
        {
            int x = startX + offset.x;
            int y = startY + offset.y;
            
            if (x < 0 || x >= gridSize || y < 0 || y >= gridSize || !emptySet.Contains(new Vector2Int(x, y)))
            {
                return false;
            }
        }
        
        return true;
    }

    private float CalculateSecondaryScore(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        float score = 0f;

        // GRID CLEAR PRIORITY - Tính toán khả năng clear grid
        if (enableGridClearPriority)
        {
            int clearPotential = CalculateGridClearPotential(block, emptyCells);
            if (clearPotential >= minLinesForGridClearBonus)
            {
                score += clearPotential * gridClearBonusMultiplier;
                
                // Bonus thêm cho multi-direction clears
                if (preferMultiDirectionClears && CanClearMultipleDirections(block, emptyCells))
                {
                    score += gridClearBonusMultiplier * 2f; // Double bonus cho multi-direction
                }
            }
        }

        // LARGE BLOCK PRIORITY - Ưu tiên block lớn nhất
        if (!preferSmallerBlocks)
        {
            if (enableLargeBlockBonus && block.Count >= largeBlockThreshold)
            {
                // Ultra bonus cho block 9 ô
                if (block.Count >= 9)
                {
                    score += block.Count * 15f * ultraLargeBlockBonus;
                }
                // Super bonus cho block 8+ ô
                else if (block.Count >= 8)
                {
                    score += block.Count * 12f * superLargeBlockBonus;
                }
                // Extra bonus cho block 6-7 ô
                else if (block.Count >= 6)
                {
                    score += block.Count * 10f * largeBlockMultiplier;
                }
                // Bonus thường cho block 5 ô
                else
                {
                    score += block.Count * 8f * largeBlockMultiplier;
                }
            }
            else if (block.Count >= 4)
            {
                // Bonus vừa phải cho block 4 ô
                score += block.Count * 6f;
            }
            else if (block.Count >= 3)
            {
                // Giảm điểm cho block 3 ô
                if (forceLargeBlocks)
                {
                    score += block.Count * 0.5f; // Rất giảm điểm
                }
                else
                {
                    score += block.Count * 2f;
                }
            }
            else
            {
                // Phạt rất nặng block < 3 ô
                score -= (3 - block.Count) * 50f; // Phạt 50 điểm cho mỗi ô thiếu
            }
        }
        else
        {
            // Logic cũ cho trường hợp prefer smaller (hiếm khi dùng)
            score += (maxBlockComplexity - block.Count) * 10f;
        }

        // Prefer blocks with more flexibility (more valid placements)
        int validPositions = CountValidPositions(block, emptyCells);
        score += validPositions * 3f; // Tăng từ 2f lên 3f

        // Small random factor - giảm để ưu tiên logic hơn
        score += Random.Range(0f, 2f); // Giảm từ 5f xuống 2f

        return score;
    }
    
    // NEW: Tính toán khả năng clear grid của block
    private int CalculateGridClearPotential(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        int maxClears = 0;
        
        // Thử tất cả các vị trí có thể đặt block
        for (int anchorX = 0; anchorX < 8; anchorX++)
        {
            for (int anchorY = 0; anchorY < 8; anchorY++)
            {
                if (CanPlaceShapeAt(block, anchorX, anchorY, emptyCells))
                {
                    int clears = SimulateLineClears(block, anchorX, anchorY, emptyCells);
                    maxClears = Mathf.Max(maxClears, clears);
                }
            }
        }
        
        return maxClears;
    }
    
    // NEW: Kiểm tra block có thể clear cả hàng và cột không
    private bool CanClearMultipleDirections(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        for (int anchorX = 0; anchorX < 8; anchorX++)
        {
            for (int anchorY = 0; anchorY < 8; anchorY++)
            {
                if (CanPlaceShapeAt(block, anchorX, anchorY, emptyCells))
                {
                    var result = SimulateDirectionalClears(block, anchorX, anchorY, emptyCells);
                    int rowsCleared = result.Item1;
                    int colsCleared = result.Item2;
                    if (rowsCleared > 0 && colsCleared > 0)
                    {
                        return true; // Clear cả hàng và cột
                    }
                }
            }
        }
        return false;
    }
    
    // NEW: Mô phỏng số lines có thể clear
    private int SimulateLineClears(List<Vector2Int> block, int anchorX, int anchorY, List<Vector2Int> emptyCells)
    {
        var result = SimulateDirectionalClears(block, anchorX, anchorY, emptyCells);
        return result.Item1 + result.Item2;
    }
    
    // NEW: Mô phỏng directional clears
    private System.Tuple<int, int> SimulateDirectionalClears(List<Vector2Int> block, int anchorX, int anchorY, List<Vector2Int> emptyCells)
    {
        bool[,] tempGrid = new bool[8, 8];
        
        // Fill với occupied cells hiện tại
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                tempGrid[x, y] = !emptyCells.Contains(new Vector2Int(x, y));
            }
        }
        
        // Add block vào temp grid
        foreach (var cell in block)
        {
            int worldX = anchorX + cell.x;
            int worldY = anchorY + cell.y;
            
            if (worldX >= 0 && worldX < 8 && worldY >= 0 && worldY < 8)
            {
                tempGrid[worldX, worldY] = true;
            }
        }
        
        // Đếm full rows và columns
        int rowsCleared = 0;
        int colsCleared = 0;
        
        for (int i = 0; i < 8; i++)
        {
            bool rowFull = true;
            bool colFull = true;
            
            for (int j = 0; j < 8; j++)
            {
                if (!tempGrid[j, i]) rowFull = false;
                if (!tempGrid[i, j]) colFull = false;
            }
            
            if (rowFull) rowsCleared++;
            if (colFull) colsCleared++;
        }
        
        return System.Tuple.Create(rowsCleared, colsCleared);
    }
    /// <summary>
    /// Find fitting blocks với cache optimization và improved persistence
    /// </summary>
    private List<List<Vector2Int>> FindFittingBlocksCached(List<Vector2Int> emptyCells)
    {
        string gridHash = CalculateGridHash(emptyCells);
        
        if (_fittingBlocksCache.ContainsKey(gridHash))
        {
            return _fittingBlocksCache[gridHash];
        }
        
        List<List<Vector2Int>> fittingBlocks = FindFittingBlocksOptimized(emptyCells);
        _fittingBlocksCache[gridHash] = fittingBlocks;
        
        // OPTIMIZATION: Tăng cache size limit và sử dụng LRU-style cleanup (tăng từ 100 lên 150)
        if (_fittingBlocksCache.Count > 150) // Tăng từ 100 lên 150
        {
            // Remove oldest entries thay vì chỉ remove first entry
            var keysToRemove = _fittingBlocksCache.Keys.Take(50).ToList(); // Giữ lại 100 entries
            foreach (var key in keysToRemove)
            {
                _fittingBlocksCache.Remove(key);
            }
        }
        
        return fittingBlocks;
    }
    
    /// <summary>
    /// Find fitting blocks với cache optimization, smart filtering, and early exit
    /// </summary>
    private List<List<Vector2Int>> FindFittingBlocksOptimized(List<Vector2Int> emptyCells)
    {
        List<List<Vector2Int>> fittingBlocks = new List<List<Vector2Int>>();
        HashSet<string> seen = new HashSet<string>();
        
        // SMART FILTERING: Get relevant shapes based on empty cells count
        var relevantShapes = GetRelevantShapes(emptyCells.Count);
        
        // OPTIMIZATION: Early exit khi đã tìm đủ blocks
        int targetBlockCount = 12; // Reduced from 15 for better performance
        int foundBlocks = 0;
        
        foreach (var shape in relevantShapes)
        {
            if (foundBlocks >= targetBlockCount) break; // Early exit
            
            // Skip shapes that are too large for available space
            if (shape.Count > emptyCells.Count) continue;
            
            List<Vector2Int> baseShape = NormalizeShape(new List<Vector2Int>(shape));
            
            // Test only 2 rotations (0 and 90 degrees) for performance
            List<List<Vector2Int>> testVariants = new List<List<Vector2Int>>
            {
                baseShape,
                NormalizeShape(TetrisShapes.RotateShape(baseShape))
            };

            foreach (var variant in testVariants)
            {
                if (foundBlocks >= targetBlockCount) break; // Early exit
                
                string sig = ShapeSignature(variant);
                if (!seen.Add(sig)) continue; // Skip duplicates

                if (variant.Count > maxBlockComplexity || variant.Count < minBlockComplexity)
                    continue;

                // OPTIMIZATION: Quick bounding box check trước khi full placement test
                var boundingBox = GetShapeBoundingBox(variant);
                if (boundingBox.width > 8 || boundingBox.height > 8)
                    continue;

                if (CanPlaceShapeAnywhereOptimized(variant, emptyCells))
                {
                    fittingBlocks.Add(variant);
                    foundBlocks++;
                }
            }
        }
        
        return fittingBlocks;
    }
    
    /// <summary>
    /// Get relevant shapes based on empty cells count (smart filtering)
    /// </summary>
    private List<List<Vector2Int>> GetRelevantShapes(int emptyCellsCount)
    {
        List<List<Vector2Int>> relevantShapes = new List<List<Vector2Int>>();
        
        // Determine optimal size range based on empty cells
        int minSize = Mathf.Max(minBlockComplexity, 2);
        int maxSize = Mathf.Min(emptyCellsCount, maxBlockComplexity);
        
        // Prioritize larger shapes first, but include some smaller ones for variety
        var sizeKeys = _shapesBySize.Keys.OrderByDescending(k => k).ToList();
        
        int shapesAdded = 0;
        foreach (int size in sizeKeys)
        {
            if (size < minSize || size > maxSize) continue;
            
            var shapesOfSize = _shapesBySize[size];
            int shapesToAdd = Mathf.Min(shapesOfSize.Count, _maxShapesToTest - shapesAdded);
            
            for (int i = 0; i < shapesToAdd && shapesAdded < _maxShapesToTest; i++)
            {
                relevantShapes.Add(shapesOfSize[i]);
                shapesAdded++;
            }
            
            if (shapesAdded >= _maxShapesToTest) break;
        }
        
        // If we don't have enough shapes, add some from the optimal size range
        if (relevantShapes.Count < 4)
        {
            int optimalSize = Mathf.Min(emptyCellsCount, 6); // Optimal size around 4-6
            if (_shapesBySize.ContainsKey(optimalSize))
            {
                var optimalShapes = _shapesBySize[optimalSize];
                foreach (var shape in optimalShapes)
                {
                    if (!relevantShapes.Contains(shape) && relevantShapes.Count < 8)
                    {
                        relevantShapes.Add(shape);
                    }
                }
            }
        }
        
        return relevantShapes;
    }

    private static List<Vector2Int> FlipHorizontal(List<Vector2Int> shape)
    {
        if (shape == null) return null;
        List<Vector2Int> flipped = new List<Vector2Int>(shape.Count);
        foreach (var pos in shape)
        {
            flipped.Add(new Vector2Int(-pos.x, pos.y));
        }
        return flipped;
    }

    private static List<Vector2Int> FlipVertical(List<Vector2Int> shape)
    {
        if (shape == null) return null;
        List<Vector2Int> flipped = new List<Vector2Int>(shape.Count);
        foreach (var pos in shape)
        {
            flipped.Add(new Vector2Int(pos.x, -pos.y));
        }
        return flipped;
    }
    
    /// <summary>
    /// Get block scores với cache optimization và improved persistence
    /// </summary>
    private Dictionary<List<Vector2Int>, float> GetBlockScoresCached(List<List<Vector2Int>> fittingBlocks, List<Vector2Int> emptyCells)
    {
        string gridHash = CalculateGridHash(emptyCells);
        
        if (_blockScoresCache.ContainsKey(gridHash))
        {
            return _blockScoresCache[gridHash];
        }
        
        Dictionary<List<Vector2Int>, float> scores = new Dictionary<List<Vector2Int>, float>();
        foreach (var block in fittingBlocks)
        {
            float s = CalculateEnhancedScore(block, emptyCells);
            scores[block] = s;
        }
        
        _blockScoresCache[gridHash] = scores;
        
        // OPTIMIZATION: Tăng cache size limit và sử dụng LRU-style cleanup (tăng từ 50 lên 100)
        if (_blockScoresCache.Count > 100) // Tăng từ 50 lên 100
        {
            // Remove oldest entries thay vì chỉ remove first entry
            var keysToRemove = _blockScoresCache.Keys.Take(30).ToList(); // Giữ lại 70 entries
            foreach (var key in keysToRemove)
            {
                _blockScoresCache.Remove(key);
            }
        }
        
        return scores;
    }
    
    /// <summary>
    /// Check if shape có thể đặt ở bất kỳ vị trí nào (optimized với early exits)
    /// </summary>
    private bool CanPlaceShapeAnywhereOptimized(List<Vector2Int> shape, List<Vector2Int> emptyCells)
    {
        // EARLY EXIT 1: Shape too large to fit in grid
        if (shape.Count > emptyCells.Count)
            return false;
            
        // EARLY EXIT 2: Quick bounding box check
        var boundingBox = GetShapeBoundingBox(shape);
        if (boundingBox.width > 8 || boundingBox.height > 8)
            return false;
        
        // OPTIMIZATION: Reuse hash set if empty cells haven't changed
        HashSet<Vector2Int> emptySet;
        if (_lastEmptyCells != emptyCells)
        {
            _lastEmptyCells = emptyCells;
            _lastEmptySet = new HashSet<Vector2Int>(emptyCells);
        }
        emptySet = _lastEmptySet;
        
        // OPTIMIZATION: Giảm số positions test với smart bounds
        int minTestX = Mathf.Max(0, -boundingBox.minX);
        int maxTestX = Mathf.Min(8 - boundingBox.width, 8);
        int minTestY = Mathf.Max(0, -boundingBox.minY);
        int maxTestY = Mathf.Min(8 - boundingBox.height, 8);
        
        // OPTIMIZATION: Early exit khi tìm thấy vị trí đầu tiên
        for (int anchorX = minTestX; anchorX <= maxTestX; anchorX++)
        {
            for (int anchorY = minTestY; anchorY <= maxTestY; anchorY++)
            {
                if (CanPlaceShapeAtOptimized(shape, anchorX, anchorY, emptySet))
                {
                    return true; // Early exit - found valid position
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Clear caches khi cần thiết (chỉ khi grid thực sự thay đổi)
    /// </summary>
    private void ClearOptimizationCaches()
    {
        // Chỉ clear cache khi grid thực sự thay đổi, không phải mỗi lần spawn
        _boundingBoxCache.Clear();
        _lastEmptyCells = null;
        _lastEmptySet = null;
        
        // KHÔNG clear fitting blocks cache và scores cache ở đây
        // Chúng sẽ được quản lý bởi size limits trong các method riêng
    }
    
    // Cache for shape bounding boxes to avoid recalculation
    private Dictionary<string, (int minX, int minY, int width, int height)> _boundingBoxCache = new Dictionary<string, (int, int, int, int)>();
    
    // Cache for empty cells hash set to avoid repeated creation
    private List<Vector2Int> _lastEmptyCells;
    private HashSet<Vector2Int> _lastEmptySet;
    
    /// <summary>
    /// Get bounding box của shape với caching để tối ưu testing
    /// </summary>
    private (int minX, int minY, int width, int height) GetShapeBoundingBox(List<Vector2Int> shape)
    {
        string signature = ShapeSignature(shape);
        
        if (_boundingBoxCache.TryGetValue(signature, out var cached))
        {
            return cached;
        }
        
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var point in shape)
        {
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }
        
        var result = (minX, minY, maxX - minX + 1, maxY - minY + 1);
        _boundingBoxCache[signature] = result;
        return result;
    }
    
    /// <summary>
    /// Check if shape có thể đặt tại vị trí cụ thể (optimized)
    /// </summary>
    private bool CanPlaceShapeAtOptimized(List<Vector2Int> shape, int anchorX, int anchorY, HashSet<Vector2Int> emptySet)
    {
        foreach (var cell in shape)
        {
            int worldX = anchorX + cell.x;
            int worldY = anchorY + cell.y;
            
            // Check bounds
            if (worldX < 0 || worldX >= 8 || worldY < 0 || worldY >= 8)
            {
                return false;
            }
            
            // Check if cell is empty (O(1) lookup)
            if (!emptySet.Contains(new Vector2Int(worldX, worldY)))
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Check if shape có thể đặt tại vị trí cụ thể
    private bool CanPlaceShapeAt(List<Vector2Int> shape, int anchorX, int anchorY, List<Vector2Int> emptyCells)
    {
        foreach (var cell in shape)
        {
            int worldX = anchorX + cell.x;
            int worldY = anchorY + cell.y;
            
            // Check bounds
            if (worldX < 0 || worldX >= 8 || worldY < 0 || worldY >= 8)
            {
                return false;
            }
            
            // Check if cell is empty
            if (!emptyCells.Contains(new Vector2Int(worldX, worldY)))
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Select block từ fitting blocks với intelligence
    private List<Vector2Int> SelectFromFittingBlocks(List<List<Vector2Int>> fittingBlocks, List<Vector2Int> emptyCells)
    {
        if (fittingBlocks.Count == 1)
        {
            return fittingBlocks[0];
        }
        
        // Score mỗi block dựa trên various factors
        Dictionary<List<Vector2Int>, float> blockScores = new Dictionary<List<Vector2Int>, float>();
        
        foreach (var block in fittingBlocks)
        {
            float score = CalculateBlockScore(block, emptyCells);
            blockScores[block] = score;
        }
        
        // Select block với highest score (hoặc weighted random)
        return SelectBlockByScore(blockScores);
    }
    
    // Calculate score cho block
    private float CalculateBlockScore(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        float score = 0f;
        
        // Factor 1: Block size (prefer smaller blocks)
        if (preferSmallerBlocks)
        {
            score += (maxBlockComplexity - block.Count) * 10f;
        }
        
        // Factor 2: Number of valid positions
        int validPositions = CountValidPositions(block, emptyCells);
        score += validPositions * 5f;
        
        // Factor 3: Line clearing potential
        float lineClearPotential = CalculateLineClearPotential(block, emptyCells);
        score += lineClearPotential * 15f;
        
        // Factor 4: Random factor để tránh quá predictable
        score += Random.Range(0f, 10f);
        
        return score;
    }
    
    // Count valid positions cho block
    private int CountValidPositions(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        int count = 0;
        
        for (int anchorX = 0; anchorX < 8; anchorX++)
        {
            for (int anchorY = 0; anchorY < 8; anchorY++)
            {
                if (CanPlaceShapeAt(block, anchorX, anchorY, emptyCells))
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    // Calculate line clearing potential
    private float CalculateLineClearPotential(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        float potential = 0f;
        
        // Simulate placing block ở best position
        for (int anchorX = 0; anchorX < 8; anchorX++)
        {
            for (int anchorY = 0; anchorY < 8; anchorY++)
            {
                if (CanPlaceShapeAt(block, anchorX, anchorY, emptyCells))
                {
                    // Check if this placement would clear lines
                    int linesCleared = SimulateLineClear(block, anchorX, anchorY, emptyCells);
                    potential += linesCleared;
                }
            }
        }
        
        return potential;
    }
    
    // Simulate line clearing
    private int SimulateLineClear(List<Vector2Int> block, int anchorX, int anchorY, List<Vector2Int> emptyCells)
    {
        // Create temp grid state
        bool[,] tempGrid = new bool[8, 8];
        
        // Fill with current occupied cells
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                tempGrid[x, y] = !emptyCells.Contains(new Vector2Int(x, y));
            }
        }
        
        // Add block to temp grid
        foreach (var cell in block)
        {
            int worldX = anchorX + cell.x;
            int worldY = anchorY + cell.y;
            
            if (worldX >= 0 && worldX < 8 && worldY >= 0 && worldY < 8)
            {
                tempGrid[worldX, worldY] = true;
            }
        }
        
        // Count full rows and columns
        int linesCleared = 0;
        
        // Check rows
        for (int y = 0; y < 8; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < 8; x++)
            {
                if (!tempGrid[x, y])
                {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull) linesCleared++;
        }
        
        // Check columns
        for (int x = 0; x < 8; x++)
        {
            bool colFull = true;
            for (int y = 0; y < 8; y++)
            {
                if (!tempGrid[x, y])
                {
                    colFull = false;
                    break;
                }
            }
            if (colFull) linesCleared++;
        }
        
        return linesCleared;
    }
    
    // Select block by score với weighted random
    private List<Vector2Int> SelectBlockByScore(Dictionary<List<Vector2Int>, float> blockScores)
    {
        // Sort by score
        var sortedBlocks = blockScores.OrderByDescending(kvp => kvp.Value).ToList();
        
        // Weighted random - higher score = higher chance
        float totalScore = sortedBlocks.Sum(kvp => kvp.Value);
        float randomValue = Random.Range(0f, totalScore);
        
        float currentScore = 0f;
        foreach (var kvp in sortedBlocks)
        {
            currentScore += kvp.Value;
            if (randomValue <= currentScore)
            {
                return kvp.Key;
            }
        }
        
        // Fallback to highest score
        return sortedBlocks[0].Key;
    }
    
    // Get random blocks (fallback)
    private List<List<Vector2Int>> GetRandomBlocks(int count)
    {
        List<List<Vector2Int>> blocks = new List<List<Vector2Int>>();
        
        for (int i = 0; i < count; i++)
        {
            blocks.Add(TetrisShapes.GetRandomShapeWithRotation());
        }
        
        return blocks;
    }
    
    // Đảm bảo đa dạng shapes trong danh sách blocks
    private List<List<Vector2Int>> EnsureShapeVariety(List<List<Vector2Int>> blocks, List<Vector2Int> emptyCells)
    {
        // Tắt shape variety để ưu tiên block lớn hơn
        if (!ensureShapeVariety || blocks.Count <= 1) return blocks;
        
        // Đếm số lượng blocks theo size
        Dictionary<int, int> sizeCount = new Dictionary<int, int>();
        foreach (var block in blocks)
        {
            int size = block.Count;
            if (sizeCount.ContainsKey(size))
                sizeCount[size]++;
            else
                sizeCount[size] = 1;
        }
        
        // Kiểm tra nếu có size nào vượt quá ratio cho phép
        int maxSameCount = Mathf.CeilToInt(blocks.Count * maxSameShapeRatio);
        bool hasVarietyIssue = false;
        
        foreach (var kvp in sizeCount)
        {
            if (kvp.Value > maxSameCount)
            {
                hasVarietyIssue = true;
                break;
            }
        }
        
        if (!hasVarietyIssue) return blocks; // Đã đa dạng, không cần thay đổi
        
        
        // Tìm các size có sẵn và tạo variety
        List<int> availableSizes = new List<int> { 3, 4, 5, 6, 9 }; // Các sizes có sẵn
        List<List<Vector2Int>> variedBlocks = new List<List<Vector2Int>>();
        
        // Giữ lại một số blocks gốc
        List<List<Vector2Int>> originalBlocks = new List<List<Vector2Int>>(blocks);
        
        for (int i = 0; i < blocks.Count; i++)
        {
            if (i < originalBlocks.Count)
            {
                variedBlocks.Add(originalBlocks[i]);
            }
            else
            {
                // Thêm block với size khác để tạo variety
                int targetSize = availableSizes[i % availableSizes.Count];
                var newBlock = FindBlockWithSize(targetSize, emptyCells);
                if (newBlock != null)
                {
                    variedBlocks.Add(newBlock);
                }
                else
                {
                    variedBlocks.Add(TetrisShapes.GetRandomShapeWithRotation());
                }
            }
        }
        
        return variedBlocks;
    }
    
    // Tìm block với size cụ thể có thể fit vào grid
    private List<Vector2Int> FindBlockWithSize(int targetSize, List<Vector2Int> emptyCells)
    {
        // Tìm tất cả fitting blocks với size cụ thể
        var fittingBlocks = FindFittingBlocksCached(emptyCells);
        var targetSizeBlocks = fittingBlocks.Where(block => block.Count == targetSize).ToList();
        
        if (targetSizeBlocks.Count > 0)
        {
            return targetSizeBlocks[Random.Range(0, targetSizeBlocks.Count)];
        }
        
        // Nếu không có fitting blocks, trả về random block với size đúng
        var allShapes = TetrisShapes.Shapes;
        var targetShapes = allShapes.Where(shape => shape.Count == targetSize).ToList();
        
        if (targetShapes.Count > 0)
        {
            var shape = targetShapes[Random.Range(0, targetShapes.Count)];
            return ShapeDatabase.GetRandomShapeVariation();
        }
        
        return null;
    }
    
    // Cập nhật dynamic difficulty dựa trên điểm số
    private void UpdateDynamicDifficulty()
    {
        if (!enableDynamicDifficulty || gameManager == null)
        {
            currentFittingChance = baseFittingBlockChance;
            return;
        }
        
        // Lấy điểm số hiện tại
        int currentScore = gameManager.GetCurrentScore();
        
        // Tính số bước difficulty đã đạt được
        int difficultySteps = currentScore / scoreThresholdStep;
        
        // Tính tỷ lệ fitting mục tiêu (tăng dần theo điểm số)
        float targetFittingChance = Mathf.Min(
            baseFittingBlockChance + (difficultySteps * difficultyTransitionSpeed),
            maxFittingChance
        );
        
        // Giới hạn ở 50% cho đến khi đạt đủ 600 điểm (1 step)
        if (targetFittingChance > 0.5f && difficultySteps == 1)
        {
            targetFittingChance = 0.5f;
        }
        
        // Làm mượt chuyển tiếp để tránh thay đổi đột ngột
        currentFittingChance = Mathf.Lerp(currentFittingChance, targetFittingChance, 0.1f);
        
    }
    
    // Track combo rounds for 2-round bonus system
    private void TrackComboRound()
    {
        currentComboRound++;
        
        // Reset combo tracking if we exceed the window
        if (currentComboRound > comboRoundWindow)
        {
            currentComboRound = 1;
            blocksInCurrentCombo = 0;
            recentSpawnedBlocks.Clear();
            
        }
        
        // Check if we're approaching the combo target
        if (blocksInCurrentCombo >= comboBlockTarget - 2)
        {
        }
    }
    
    // Check if current block selection can contribute to combo
    private bool CanContributeToCombo(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        if (!enableTwoRoundCombo || blocksInCurrentCombo >= comboBlockTarget)
            return false;
        
        // Check if this block can clear lines when combined with recent blocks
        int clearPotential = CalculateGridClearPotential(block, emptyCells);
        return clearPotential >= 1; // At least can clear 1 line
    }
    
    // Enhanced scoring with combo priority
    private float CalculateEnhancedScore(List<Vector2Int> block, List<Vector2Int> emptyCells)
    {
        float baseScore = CalculateSecondaryScore(block, emptyCells);
        
        // Add combo bonus if applicable
        if (enableTwoRoundCombo && CanContributeToCombo(block, emptyCells))
        {
            // Higher bonus for blocks that can complete the combo
            if (blocksInCurrentCombo >= comboBlockTarget - 1)
            {
                baseScore += comboBlockPriorityMultiplier * 2f; // Double bonus for final block
            }
            else
            {
                baseScore += comboBlockPriorityMultiplier;
            }
            
        }
        
        return baseScore;
    }
    
    // Public method to get combo status
    public int GetComboProgress()
    {
        return blocksInCurrentCombo;
    }
    
    public int GetComboTarget()
    {
        return comboBlockTarget;
    }
    
    public int GetCurrentComboRound()
    {
        return currentComboRound;
    }
    
    // Reset combo tracking (called when combo is completed or failed)
    public void ResetComboTracking()
    {
        currentComboRound = 0;
        blocksInCurrentCombo = 0;
        recentSpawnedBlocks.Clear();
        
    }
}
