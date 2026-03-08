using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance;
    
    [Header("Combo Settings")]
    public int comboRounds = 2; // 2 spawn rounds
    public int comboBlocks = 6; // 6 blocks total
    public bool enableComboSystem = true;
    
    [Header("2-Round Bonus System")]
    public bool enableTwoRoundBonus = true;
    public int bonusPointsPerLine = 50; // Extra points for clearing lines in combo
    public int bonusPointsPerCombo = 100; // Bonus for completing combo
    public bool trackLineClearsInCombo = true;
    
    // Tracking
    private int currentSpawnRound = 0;
    private List<BlockPlacementInfo> recentPlacements = new List<BlockPlacementInfo>();
    private GridView gridView;
    private bool comboOpportunityActive = false;
    private int linesClearedInCurrentCombo = 0;
    
    // Events
    public System.Action<int> OnComboOpportunityAvailable;
    public System.Action OnComboCompleted;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        gridView = FindObjectOfType<GridView>();
    }
    
    // Called when new blocks are spawned
    public void OnNewBlocksSpawned()
    {
        currentSpawnRound++;
        
        // Clean old placements beyond combo rounds
        CleanOldPlacements();
        
        // Check for combo opportunities
        CheckComboOpportunity();
        
        // Reset line clear tracking for new round if needed
        if (currentSpawnRound > comboRounds)
        {
            linesClearedInCurrentCombo = 0;
        }
    }
    
    // Called when a block is placed
    public void OnBlockPlaced(BlockData blockData, Vector2Int position, Color blockColor)
    {
        if (!enableComboSystem) return;
        
        BlockPlacementInfo placement = new BlockPlacementInfo
        {
            blockData = blockData,
            position = position,
            color = blockColor,
            spawnRound = currentSpawnRound,
            timestamp = Time.time
        };
        
        recentPlacements.Add(placement);
        
        // Check if this placement completes a combo
        CheckComboCompletion(placement);
        
        // Clean old placements
        CleanOldPlacements();
    }
    
    // Remove placements older than combo rounds
    private void CleanOldPlacements()
    {
        int minRound = currentSpawnRound - comboRounds + 1;
        recentPlacements.RemoveAll(p => p.spawnRound < minRound);
    }
    
    // Check if there's a combo opportunity with current blocks
    private void CheckComboOpportunity()
    {
        if (!enableComboSystem) return;
        
        // Get current available blocks
        BlockSpawnController spawnController = FindObjectOfType<BlockSpawnController>();
        if (spawnController == null) return;
        
        List<BlockData> availableBlocks = GetCurrentAvailableBlocks(spawnController);
        if (availableBlocks.Count == 0) return;
        
        // Check if these blocks can create a combo
        if (CanCreateComboWithAvailableBlocks(availableBlocks))
        {
            comboOpportunityActive = true;
            OnComboOpportunityAvailable?.Invoke(recentPlacements.Count);
            
            // Visual feedback removed - combo system works silently
        }
        else
        {
            comboOpportunityActive = false;
        }
    }
    
    // Get current available blocks from spawn controller
    private List<BlockData> GetCurrentAvailableBlocks(BlockSpawnController spawnController)
    {
        List<BlockData> availableBlocks = new List<BlockData>();
        
        foreach (Transform child in spawnController.transform)
        {
            DragBlockController dragController = child.GetComponent<DragBlockController>();
            if (dragController != null && dragController.BlockData != null)
            {
                availableBlocks.Add(dragController.BlockData);
            }
        }
        
        return availableBlocks;
    }
    
    // Check if available blocks can create a combo
    private bool CanCreateComboWithAvailableBlocks(List<BlockData> availableBlocks)
    {
        if (gridView == null) return false;
        
        // Simulate placing all available blocks in different combinations
        // This is a simplified version - in practice you'd want more sophisticated logic
        
        foreach (BlockData block in availableBlocks)
        {
            // Try all possible positions for this block
            for (int x = 0; x <= gridView.GridSize - 5; x++)
            {
                for (int y = 0; y <= gridView.GridSize - 5; y++)
                {
                    if (gridView.CanPlace(block, x, y))
                    {
                        // Check if placing this block would lead to a line clear
                        // when combined with recent placements
                        if (WouldCreateComboWithRecentPlacements(block, x, y))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        
        return false;
    }
    
    // Check if placing a block would create a combo with recent placements
    private bool WouldCreateComboWithRecentPlacements(BlockData block, int x, int y)
    {
        if (gridView == null) return false;
        
        // Create temporary grid state with recent placements
        int[,] tempGrid = (int[,])gridView.GridData.Clone();
        
        // Apply recent placements to temp grid
        foreach (var placement in recentPlacements)
        {
            ApplyPlacementToTempGrid(tempGrid, placement);
        }
        
        // Apply current block to temp grid
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (block.mask[i, j] == 1)
                {
                    int gx = x + i;
                    int gy = y + j;
                    if (gx >= 0 && gx < gridView.GridSize && gy >= 0 && gy < gridView.GridSize)
                    {
                        tempGrid[gx, gy] = 1;
                    }
                }
            }
        }
        
        // Check if any row or column is now full
        return HasFullRowOrColumn(tempGrid);
    }
    
    // Apply a placement to temporary grid
    private void ApplyPlacementToTempGrid(int[,] tempGrid, BlockPlacementInfo placement)
    {
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (placement.blockData.mask[i, j] == 1)
                {
                    int gx = placement.position.x + i;
                    int gy = placement.position.y + j;
                    if (gx >= 0 && gx < gridView.GridSize && gy >= 0 && gy < gridView.GridSize)
                    {
                        tempGrid[gx, gy] = 1;
                    }
                }
            }
        }
    }
    
    // Check if grid has any full row or column
    private bool HasFullRowOrColumn(int[,] grid)
    {
        int size = gridView.GridSize;
        
        // Check rows
        for (int y = 0; y < size; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y] == 0)
                {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull) return true;
        }
        
        // Check columns
        for (int x = 0; x < size; x++)
        {
            bool colFull = true;
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0)
                {
                    colFull = false;
                    break;
                }
            }
            if (colFull) return true;
        }
        
        return false;
    }
    
    // Check if a placement completes a combo
    private void CheckComboCompletion(BlockPlacementInfo placement)
    {
        // Check if we have enough blocks in the combo window
        int blocksInWindow = recentPlacements.Count;
        
        if (blocksInWindow >= comboBlocks)
        {
            // Check if these blocks actually cleared lines
            // This would be called after GridView processes the placement
            StartCoroutine(DelayedComboCheck(placement));
        }
    }
    
    // Delayed check to allow GridView to process line clears
    private IEnumerator DelayedComboCheck(BlockPlacementInfo placement)
    {
        yield return new WaitForSeconds(0.1f);
        
        // Check if any lines were cleared by this placement
        // In a complete implementation, you'd track this more precisely
        OnComboCompleted?.Invoke();
        
        // Reset for next combo
        recentPlacements.Clear();
        comboOpportunityActive = false;
    }
    
    // Called when lines are cleared - enhanced for 2-round bonus
    public void OnLinesCleared(int rowsCleared, int colsCleared)
    {
        if (!enableComboSystem || !enableTwoRoundBonus) return;
        
        int totalLines = rowsCleared + colsCleared;
        linesClearedInCurrentCombo += totalLines;
        
        // Award bonus points for line clears during combo window
        if (totalLines > 0 && recentPlacements.Count > 0)
        {
            int bonusPoints = totalLines * bonusPointsPerLine;
            
            // Extra bonus if we're in a combo opportunity
            if (comboOpportunityActive)
            {
                bonusPoints += bonusPointsPerCombo;
            }
            else
            {
            }
            
            // Add bonus to game score
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScoreForPlacement(0, rowsCleared, colsCleared);
            }
        }
    }
    
    // Get combo progress for UI
    public int GetComboProgress()
    {
        return recentPlacements.Count;
    }
    
    public int GetComboTarget()
    {
        return comboBlocks;
    }
    
    public bool IsComboOpportunityActive()
    {
        return comboOpportunityActive;
    }
}

// Data structure for tracking block placements
[System.Serializable]
public class BlockPlacementInfo
{
    public BlockData blockData;
    public Vector2Int position;
    public Color color;
    public int spawnRound;
    public float timestamp;
}
