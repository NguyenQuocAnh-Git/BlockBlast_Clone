using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Simplified, deterministic MapGenerator tuned for an 8x8 game grid.
/// Replaces all prior random/parameterized behavior with a fixed-generation
/// pattern designed to be solvable by the lookahead spawner in a small number of steps.
/// - No parameters to tune at runtime required.
/// - Generates deterministic obstacles that create multiple near-full rows/cols
///   with aligned gaps so multi-step lookahead can plan clears quickly.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    private GridView gridView;
    private const int GRID_SIZE = 8;

    // Public accessor used elsewhere
    public int GridSize => GRID_SIZE;

    void Awake()
    {
        gridView = FindObjectOfType<GridView>();
        // Ensure gridView exists
        if (gridView == null)
        {
            Debug.LogWarning("MapGenerator: GridView not found in scene.");
            return;
        }
    }

    void Start()
    {
        // Generate a procedural, varied map on start (guarantees no fully-filled row/column)
        GenerateProceduralMap();
    }

    // Public API kept for compatibility
    public void GenerateRandomMap()
    {
        GenerateProceduralMap();
    }

    // Procedural map generation with variation while ensuring no row/column is fully filled.
    private void GenerateProceduralMap()
    {
        if (gridView == null) return;

        gridView.ClearGrid();

        var occupied = new HashSet<Vector2Int>();

        // TARGET: place obstacle shapes (from TetrisShapes / ShapeDatabase) across the grid evenly
        int targetOccupied = Random.Range(30, 38); // desired number of occupied cells
        int placedCells = 0;

        // Build candidate shapes pool (prefer medium/large from ShapeDatabase; fallback to base shapes)
        var pool = new List<List<Vector2Int>>();
        if (ShapeDatabase.MediumVariations != null) pool.AddRange(ShapeDatabase.MediumVariations);
        if (ShapeDatabase.LargeVariations != null) pool.AddRange(ShapeDatabase.LargeVariations);
        if (pool.Count == 0)
        {
            // fallback to TetrisShapes base definitions
            foreach (var s in TetrisShapes.Shapes) pool.Add(new List<Vector2Int>(s));
        }

        // Shuffle pool for variation
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int attempts = 0;
        int maxAttempts = 1000;
        // For even distribution we prefer anchors with low local density
        while (placedCells < targetOccupied && attempts < maxAttempts)
        {
            attempts++;
            var shape = pool[Random.Range(0, pool.Count)];
            if (shape == null || shape.Count == 0) continue;
            var norm = TetrisShapes.NormalizeShape(new List<Vector2Int>(shape));
            // compute bbox
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in norm)
            {
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            int ax = Random.Range(0, GRID_SIZE - width + 1);
            int ay = Random.Range(0, GRID_SIZE - height + 1);

            // Check placement feasibility and local density
            bool canPlace = true;
            int localNeighbors = 0;
            var cellsToPlace = new List<Vector2Int>();
            foreach (var p in norm)
            {
                int gx = ax + p.x;
                int gy = ay + p.y;
                if (gridView.IsCellOccupied(gx, gy)) { canPlace = false; break; }
                cellsToPlace.Add(new Vector2Int(gx, gy));
                // count neighbors in 1-cell radius
                for (int nx = gx - 1; nx <= gx + 1; nx++)
                    for (int ny = gy - 1; ny <= gy + 1; ny++)
                        if (nx >= 0 && nx < GRID_SIZE && ny >= 0 && ny < GRID_SIZE)
                            if (gridView.IsCellOccupied(nx, ny)) localNeighbors++;
            }
            if (!canPlace) continue;
            // prefer low-density spots
            if (localNeighbors > Mathf.Max(1, norm.Count / 2)) continue;

            // Place shape: mark cells occupied
            foreach (var c in cellsToPlace)
            {
                PlaceOccupiedCell(c.x, c.y);
                occupied.Add(c);
                placedCells++;
            }
        }

        // If not enough cells placed, fill remaining single cells spread out
        if (placedCells < targetOccupied)
        {
            for (int x = 0; x < GRID_SIZE && placedCells < targetOccupied; x++)
            {
                for (int y = 0; y < GRID_SIZE && placedCells < targetOccupied; y++)
                {
                    if (!gridView.IsCellOccupied(x, y))
                    {
                        // avoid clustering: ensure few adjacent occupied
                        int neigh = CountNeighbors(x, y);
                        if (neigh > 1) continue;
                        PlaceOccupiedCell(x, y);
                        occupied.Add(new Vector2Int(x, y));
                        placedCells++;
                    }
                }
            }
        }

        // Optionally add a few scattered obstacles in rows 0..5 to vary layouts
        int extra = Random.Range(0, 3);
        for (int k = 0; k < extra; k++)
        {
            int rx = Random.Range(0, GRID_SIZE);
            int ry = Random.Range(0, 6);
            if (!occupied.Contains(new Vector2Int(rx, ry)))
            {
                PlaceObstacle(rx, ry);
                occupied.Add(new Vector2Int(rx, ry));
            }
        }

        // Post-process: ensure no row or column is completely full. If found, remove one random cell from it.
        // Rows
        for (int y = 0; y < GRID_SIZE; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < GRID_SIZE; x++)
            {
                if (!gridView.IsCellOccupied(x, y)) { rowFull = false; break; }
            }
            if (rowFull)
            {
                // remove random occupied cell in this row
                int rx = Random.Range(0, GRID_SIZE);
                RemoveObstacleIfPresent(rx, y);
            }
        }

        // Columns
        for (int x = 0; x < GRID_SIZE; x++)
        {
            bool colFull = true;
            for (int y = 0; y < GRID_SIZE; y++)
            {
                if (!gridView.IsCellOccupied(x, y)) { colFull = false; break; }
            }
            if (colFull)
            {
                int ry = Random.Range(0, GRID_SIZE);
                RemoveObstacleIfPresent(x, ry);
            }
        }

        // Fix isolated empty cells (completely surrounded orthogonally)
        FixIsolatedEmptyCells();

        Debug.Log("MapGenerator: Procedural 8x8 map generated (no full rows/cols, no isolated empties).");
    }

    // Place a single-cell obstacle (occupied) at grid coords
    private void PlaceObstacle(int x, int y)
    {
        if (gridView == null) return;
        if (x < 0 || x >= GRID_SIZE || y < 0 || y >= GRID_SIZE) return;
        // Use single-cell BlockData (existing type) to mark cell as occupied
        BlockData obstacle = new BlockData(new List<Vector2Int> { Vector2Int.zero });
        gridView.ApplyBlock(obstacle, x, y, Color.white);
    }

    private void RemoveObstacleIfPresent(int x, int y)
    {
        if (gridView == null) return;
        if (x < 0 || x >= GRID_SIZE || y < 0 || y >= GRID_SIZE) return;
        // If the cell is currently occupied, clear it by setting grid data to 0 via ClearGrid() patching.
        // Since GridView doesn't expose a direct ClearCell, we simulate by clearing whole grid area then re-applying.
        // Simpler approach: toggle by clearing then re-applying other obstacles — but here we can directly set GridData.
        var gv = gridView;
        var gridData = gv.GridData;
        gridData[x, y] = 0;
        // Update renderer to empty
        var sr = gv.GetCellRenderer(x, y);
        if (sr != null)
        {
            sr.sprite = gv.gridSprite;
            sr.color = Color.white;
        }
    }

    // Place occupied cell directly and update visuals
    private void PlaceOccupiedCell(int x, int y)
    {
        if (gridView == null) return;
        if (x < 0 || x >= GRID_SIZE || y < 0 || y >= GRID_SIZE) return;
        var gridData = gridView.GridData;
        gridData[x, y] = 1;
        var sr = gridView.GetCellRenderer(x, y);
        if (sr != null)
        {
            sr.sprite = gridView.cellSprite;
            sr.color = Color.white;
        }
    }

    // Count occupied neighbors in 1-cell radius
    private int CountNeighbors(int x, int y)
    {
        int cnt = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx >= 0 && nx < GRID_SIZE && ny >= 0 && ny < GRID_SIZE)
                {
                    if (gridView.IsCellOccupied(nx, ny)) cnt++;
                }
            }
        }
        return cnt;
    }

    // Fix empty cells that are in isolated components (connected component size == 1)
    private void FixIsolatedEmptyCells()
    {
        if (gridView == null) return;
        int gridSize = GRID_SIZE;
        bool changed = true;
        int passes = 0;
        int maxPasses = 10;
        while (changed && passes < maxPasses)
        {
            changed = false;
            passes++;

            var visited = new bool[gridSize, gridSize];
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (visited[x, y]) continue;
                    if (gridView.IsCellOccupied(x, y)) { visited[x, y] = true; continue; }

                    // BFS to collect empty connected component (4-connected)
                    var comp = new List<Vector2Int>();
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        var dirs = new (int dx, int dy)[] { (1,0),(-1,0),(0,1),(0,-1) };
                        foreach (var d in dirs)
                        {
                            int nx = cur.x + d.dx;
                            int ny = cur.y + d.dy;
                            if (nx < 0 || nx >= gridSize || ny < 0 || ny >= gridSize) continue;
                            if (visited[nx, ny]) continue;
                            if (gridView.IsCellOccupied(nx, ny)) { visited[nx, ny] = true; continue; }
                            visited[nx, ny] = true;
                            q.Enqueue(new Vector2Int(nx, ny));
                        }
                    }

                    // If component size == 1, it's an isolated empty cell; open a neighbor
                    if (comp.Count == 1)
                    {
                        var cell = comp[0];
                        // find orthogonal occupied neighbors to remove
                        var neighbors = new List<Vector2Int>();
                        var dirs = new (int dx, int dy)[] { (1,0),(-1,0),(0,1),(0,-1) };
                        foreach (var d in dirs)
                        {
                            int nx = cell.x + d.dx;
                            int ny = cell.y + d.dy;
                            if (nx < 0 || nx >= gridSize || ny < 0 || ny >= gridSize) continue;
                            if (gridView.IsCellOccupied(nx, ny)) neighbors.Add(new Vector2Int(nx, ny));
                        }
                        if (neighbors.Count > 0)
                        {
                            // choose neighbor with minimal neighbor count to minimize introducing new isolates
                            Vector2Int best = neighbors[0];
                            int bestScore = int.MaxValue;
                            foreach (var n in neighbors)
                            {
                                int score = CountNeighbors(n.x, n.y);
                                if (score < bestScore) { bestScore = score; best = n; }
                            }
                            RemoveObstacleIfPresent(best.x, best.y);
                            changed = true;
                        }
                    }
                }
            }
        }
    }

    // Return empty cells for spawner/selector
    public List<Vector2Int> GetEmptyCells()
    {
        var empties = new List<Vector2Int>();
        if (gridView == null) return empties;
        for (int x = 0; x < GRID_SIZE; x++)
            for (int y = 0; y < GRID_SIZE; y++)
                if (gridView.CanPlace(new BlockData(new List<Vector2Int> { Vector2Int.zero }), x, y))
                    empties.Add(new Vector2Int(x, y));
        return empties;
    }
}
