using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Simulation-Based Level Generator
/// 
/// ALGORITHM:
/// 1. GenerateLevel (Main Loop): 
///    - Continuously selects random empty cells
///    - Creates candidate snakes using random walk (may be curved)
///    - Early elimination via IsFacingAnotherSnake check
/// 
/// 2. TryCommitSnake (Critical Section):
///    - Temporarily places snake on grid
///    - SIMULATES actual gameplay to verify level is solvable
///    - If simulation fails (deadlock), rejects the snake
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    private const string VERSION = "### LevelGenerator v8.0 - SIMULATION VERIFIED ###";

    #region Configuration

    [Header("Grid Configuration")]
    [Range(5, 50)] public int gridWidth = 25;
    [Range(5, 50)] public int gridHeight = 25;

    [Header("Snake Configuration")]
    [Range(1, 200)] public int snakeCount = 50;
    [Range(2, 10)] public int minLength = 3;
    [Range(2, 15)] public int maxLength = 8;
    [Range(0f, 1f)] public float curveChance = 0.4f;

    [Header("Visualization")]
    public List<Color> snakeColors = new List<Color> 
    { 
        Color.red, Color.green, Color.blue, 
        Color.yellow, Color.cyan, Color.magenta 
    };

    #endregion

    #region Data Structures

    public class SnakePlacement
    {
        public int id;
        public List<Vector2Int> segments = new List<Vector2Int>();
        public Vector2Int exitDirection;
        public Color color;
        public Vector2Int Head => segments[0];
    }

    // For simulation
    private class SimSnake
    {
        public int id;
        public List<Vector2Int> segments;
        public Vector2Int exitDirection;
        public bool hasExited;

        public SimSnake(SnakePlacement source)
        {
            id = source.id;
            segments = new List<Vector2Int>(source.segments);
            exitDirection = source.exitDirection;
            hasExited = false;
        }

        public Vector2Int Head => segments[0];
    }

    #endregion

    #region Internal State

    private int[,] grid; // -1: Empty, >=0: Snake ID
    private List<SnakePlacement> placedSnakes;
    private System.Random rng;

    private static readonly Vector2Int[] Directions = 
    { 
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left 
    };

    #endregion

    #region Public API

    [ContextMenu("Generate Level")]
    public void GenerateLevelWrapper()
    {
        GenerateLevel(System.Environment.TickCount);
    }

    public List<SnakePlacement> GenerateLevel(int seed)
    {
        Debug.Log($"{VERSION} - Starting generation with seed {seed}...");
        
        Initialize(seed);

        int attempts = 0;
        int maxAttempts = snakeCount * 200;

        // === MAIN GENERATION LOOP ===
        while (placedSnakes.Count < snakeCount && attempts < maxAttempts)
        {
            attempts++;

            Vector2Int startPos = GetRandomEmptyCell();
            if (startPos.x == -1) 
            {
                Debug.Log("Grid is full, stopping generation.");
                break;
            }

            int targetLength = rng.Next(minLength, maxLength + 1);

            SnakePlacement candidate = CreateCandidateSnake(startPos, targetLength);

            if (candidate != null)
            {
                TryCommitSnake(candidate);
            }
        }

        FinalizeGeneration();

        return placedSnakes;
    }

    #endregion

    #region Initialization

    private void Initialize(int seed)
    {
        rng = new System.Random(seed);
        
        grid = new int[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = -1;
            }
        }

        placedSnakes = new List<SnakePlacement>();
    }

    private void FinalizeGeneration()
    {
        Debug.Log($"Generation complete. Placed: {placedSnakes.Count}/{snakeCount}. Density: {CalculateDensity():P1}");

        // Final verification using simulation
        if (SimulateSolvability(placedSnakes))
        {
            Debug.Log("SUCCESS: Level verified solvable via simulation!");
        }
        else
        {
            Debug.LogError("CRITICAL: Final level is NOT solvable! This should not happen.");
        }
    }

    #endregion

    #region Snake Creation (Random Walk Algorithm)

    private SnakePlacement CreateCandidateSnake(Vector2Int headPos, int targetLength)
    {
        var shuffledDirections = Directions.OrderBy(x => rng.Next()).ToList();

        foreach (var exitDir in shuffledDirections)
        {
            // Early elimination: facing snakes
            if (IsFacingAnotherSnake(headPos, exitDir))
            {
                continue;
            }

            List<Vector2Int> body = BuildSnakeBodyRandomWalk(headPos, exitDir, targetLength);

            if (body != null && body.Count >= minLength)
            {
                if (IsSelfBlocking(body, exitDir))
                {
                    continue;
                }

                return new SnakePlacement
                {
                    id = placedSnakes.Count,
                    segments = body,
                    exitDirection = exitDir,
                    color = snakeColors[placedSnakes.Count % snakeColors.Count]
                };
            }
        }

        return null;
    }

    private List<Vector2Int> BuildSnakeBodyRandomWalk(Vector2Int head, Vector2Int exitDir, int targetLength)
    {
        List<Vector2Int> segments = new List<Vector2Int> { head };
        HashSet<Vector2Int> usedCells = new HashSet<Vector2Int> { head };
        
        Vector2Int currentPos = head;
        Vector2Int growDirection = -exitDir;

        for (int i = 1; i < targetLength; i++)
        {
            List<Vector2Int> validMoves = new List<Vector2Int>();

            if (IsValidBodyCell(currentPos + growDirection, usedCells))
            {
                validMoves.Add(growDirection);
            }

            if (rng.NextDouble() < curveChance || validMoves.Count == 0)
            {
                Vector2Int perp1 = new Vector2Int(growDirection.y, growDirection.x);
                Vector2Int perp2 = new Vector2Int(-growDirection.y, -growDirection.x);

                if (IsValidBodyCell(currentPos + perp1, usedCells))
                {
                    validMoves.Add(perp1);
                }
                if (IsValidBodyCell(currentPos + perp2, usedCells))
                {
                    validMoves.Add(perp2);
                }
            }

            if (validMoves.Count == 0)
            {
                return segments.Count >= minLength ? segments : null;
            }

            Vector2Int selectedMove = validMoves[rng.Next(validMoves.Count)];
            Vector2Int nextPos = currentPos + selectedMove;

            segments.Add(nextPos);
            usedCells.Add(nextPos);
            currentPos = nextPos;
            growDirection = selectedMove;
        }

        return segments;
    }

    private bool IsValidBodyCell(Vector2Int pos, HashSet<Vector2Int> currentSnakeCells)
    {
        if (!IsInBounds(pos)) return false;
        if (grid[pos.x, pos.y] != -1) return false;
        if (currentSnakeCells.Contains(pos)) return false;
        return true;
    }

    private bool IsSelfBlocking(List<Vector2Int> body, Vector2Int exitDir)
    {
        HashSet<Vector2Int> bodySet = new HashSet<Vector2Int>(body);
        Vector2Int pos = body[0] + exitDir;

        while (IsInBounds(pos))
        {
            if (bodySet.Contains(pos))
            {
                return true;
            }
            pos += exitDir;
        }

        return false;
    }

    #endregion

    #region Early Elimination: IsFacingAnotherSnake

    /// <summary>
    /// Checks the ENTIRE exit path for any snake facing opposite direction.
    /// Two snakes facing each other on the same axis = permanent deadlock.
    /// </summary>
    private bool IsFacingAnotherSnake(Vector2Int headPos, Vector2Int exitDir)
    {
        Vector2Int oppositeDir = -exitDir;
        HashSet<int> checkedSnakes = new HashSet<int>();
        Vector2Int checkPos = headPos + exitDir;

        while (IsInBounds(checkPos))
        {
            int cellValue = grid[checkPos.x, checkPos.y];
            
            if (cellValue != -1 && !checkedSnakes.Contains(cellValue))
            {
                checkedSnakes.Add(cellValue);
                SnakePlacement otherSnake = placedSnakes[cellValue];
                
                if (otherSnake.exitDirection == oppositeDir)
                {
                    if (IsOnExitPath(headPos, exitDir, otherSnake.Head))
                    {
                        return true;
                    }
                }
            }
            
            checkPos += exitDir;
        }

        return false;
    }

    private bool IsOnExitPath(Vector2Int start, Vector2Int exitDir, Vector2Int target)
    {
        Vector2Int diff = target - start;
        
        if (exitDir.x != 0)
        {
            if (diff.y != 0) return false;
            return (exitDir.x > 0 && diff.x > 0) || (exitDir.x < 0 && diff.x < 0);
        }
        else
        {
            if (diff.x != 0) return false;
            return (exitDir.y > 0 && diff.y > 0) || (exitDir.y < 0 && diff.y < 0);
        }
    }

    #endregion

    #region Critical Section: TryCommitSnake with Simulation

    /// <summary>
    /// CRITICAL SECTION: Attempts to permanently place a snake.
    /// Uses SIMULATION to verify the level remains solvable.
    /// </summary>
    private bool TryCommitSnake(SnakePlacement newSnake)
    {
        // Step 1: Temporarily place snake on grid
        PlaceSnakeOnGrid(newSnake);
        placedSnakes.Add(newSnake);

        // Step 2: SIMULATE gameplay to verify solvability
        if (!SimulateSolvability(placedSnakes))
        {
            // Simulation failed - level would be unsolvable
            RemoveSnakeFromGrid(newSnake);
            placedSnakes.RemoveAt(placedSnakes.Count - 1);
            return false;
        }

        // Simulation passed - level is solvable
        return true;
    }

    private void PlaceSnakeOnGrid(SnakePlacement snake)
    {
        foreach (var segment in snake.segments)
        {
            grid[segment.x, segment.y] = snake.id;
        }
    }

    private void RemoveSnakeFromGrid(SnakePlacement snake)
    {
        foreach (var segment in snake.segments)
        {
            grid[segment.x, segment.y] = -1;
        }
    }

    #endregion

    #region Solvability Simulation

    /// <summary>
    /// Simulates actual gameplay to verify the level is solvable.
    /// 
    /// Algorithm:
    /// 1. Create a simulation copy of the grid and snakes
    /// 2. Find a snake with a completely clear path to exit
    /// 3. Remove that snake from the simulation
    /// 4. Repeat until all snakes are removed OR no snake can escape
    /// 5. If all snakes removed = solvable, otherwise = deadlock
    /// </summary>
    private bool SimulateSolvability(List<SnakePlacement> snakes)
    {
        if (snakes == null || snakes.Count == 0) return true;

        // Create simulation state
        int[,] simGrid = new int[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                simGrid[x, y] = -1;
            }
        }

        List<SimSnake> simSnakes = new List<SimSnake>();
        foreach (var snake in snakes)
        {
            SimSnake simSnake = new SimSnake(snake);
            simSnakes.Add(simSnake);
            
            foreach (var segment in simSnake.segments)
            {
                simGrid[segment.x, segment.y] = simSnake.id;
            }
        }

        // Simulate until all snakes escape or deadlock
        int remainingSnakes = simSnakes.Count;
        int maxIterations = snakes.Count * 2; // Safety limit
        int iterations = 0;

        while (remainingSnakes > 0 && iterations < maxIterations)
        {
            iterations++;
            bool anyEscaped = false;

            // Find a snake that can escape
            foreach (var simSnake in simSnakes)
            {
                if (simSnake.hasExited) continue;

                if (CanSimSnakeEscape(simSnake, simGrid))
                {
                    // Remove this snake from simulation
                    SimRemoveSnake(simSnake, simGrid);
                    simSnake.hasExited = true;
                    remainingSnakes--;
                    anyEscaped = true;
                    break; // Start over to find next escapeable snake
                }
            }

            if (!anyEscaped)
            {
                // No snake can escape - DEADLOCK!
                return false;
            }
        }

        // All snakes escaped - level is solvable
        return remainingSnakes == 0;
    }

    /// <summary>
    /// Checks if a simulated snake has a completely clear path to the border.
    /// </summary>
    private bool CanSimSnakeEscape(SimSnake snake, int[,] simGrid)
    {
        Vector2Int pos = snake.Head + snake.exitDirection;
        HashSet<Vector2Int> ownBody = new HashSet<Vector2Int>(snake.segments);

        while (IsInBounds(pos))
        {
            int cellValue = simGrid[pos.x, pos.y];
            
            if (cellValue != -1 && cellValue != snake.id)
            {
                // Another snake is blocking
                return false;
            }

            pos += snake.exitDirection;
        }

        return true;
    }

    /// <summary>
    /// Removes a snake from the simulation grid.
    /// </summary>
    private void SimRemoveSnake(SimSnake snake, int[,] simGrid)
    {
        foreach (var segment in snake.segments)
        {
            if (IsInBounds(segment))
            {
                simGrid[segment.x, segment.y] = -1;
            }
        }
    }

    #endregion

    #region Utility Methods

    private Vector2Int GetRandomEmptyCell()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == -1)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (emptyCells.Count == 0)
        {
            return new Vector2Int(-1, -1);
        }

        return emptyCells[rng.Next(emptyCells.Count)];
    }

    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
    }

    private float CalculateDensity()
    {
        int occupiedCells = 0;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] != -1)
                {
                    occupiedCells++;
                }
            }
        }
        return (float)occupiedCells / (gridWidth * gridHeight);
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (grid == null) return;

        DrawGrid();
        DrawSnakes();
    }

    private void DrawGrid()
    {
        Gizmos.color = Color.gray;
        
        for (int x = 0; x <= gridWidth; x++)
        {
            Gizmos.DrawLine(new Vector3(x, 0, 0), new Vector3(x, 0, gridHeight));
        }
        
        for (int y = 0; y <= gridHeight; y++)
        {
            Gizmos.DrawLine(new Vector3(0, 0, y), new Vector3(gridWidth, 0, y));
        }
    }

    private void DrawSnakes()
    {
        if (placedSnakes == null) return;

        foreach (var snake in placedSnakes)
        {
            Gizmos.color = snake.color;
            foreach (var segment in snake.segments)
            {
                Vector3 worldPos = new Vector3(segment.x + 0.5f, 0.5f, segment.y + 0.5f);
                Gizmos.DrawCube(worldPos, Vector3.one * 0.9f);
            }

            Gizmos.color = Color.white;
            Vector3 headWorld = new Vector3(snake.Head.x + 0.5f, 1f, snake.Head.y + 0.5f);
            Vector3 exitVector = new Vector3(snake.exitDirection.x, 0, snake.exitDirection.y);
            
            Gizmos.DrawSphere(headWorld, 0.2f);
            Gizmos.DrawLine(headWorld, headWorld + exitVector * 0.8f);
        }
    }

    #endregion
}
