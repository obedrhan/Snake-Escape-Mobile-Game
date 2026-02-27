using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Settings")]
    public List<LevelData> levels = new List<LevelData>();
    private int currentLevelIndex = -1; // -1 means no level loaded
    
    [Header("Game Rules")]
    public int maxMistakes = 3;
    
    [Header("References")]
    public GameObject snakePrefab;
    public Transform snakeContainer;
    
    private List<Snake> activeSnakes = new List<Snake>();
    private int mistakeCount = 0;
    private int snakesExited = 0;
    private int totalSnakesInLevel = 0; // Track total snakes for completion check
    private bool isGameOver = false;
    private Snake hintedSnake = null;
    private bool showingExitPaths = false;

    public int MistakeCount => mistakeCount;
    public int RemainingLives => maxMistakes - mistakeCount;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Don't auto-start any level - wait for level selection
        Debug.Log("GameManager initialized. Waiting for level selection...");
    }

    public void LoadLevelFromSelection(int levelIndex)
    {
        // Check if there's saved progress
        if (ProgressManager.Instance != null && ProgressManager.Instance.HasSavedProgress(levelIndex))
        {
            LoadLevelWithProgress(levelIndex);
        }
        else
        {
            LoadLevel(levelIndex);
        }
    }

    private void LoadLevelWithProgress(int levelIndex)
    {
        LevelProgress progress = ProgressManager.Instance.GetLevelProgress(levelIndex);
        
        if (progress == null || progress.snakeStates.Count == 0)
        {
            LoadLevel(levelIndex);
            return;
        }

        ClearLevel();
        
        // Load level from pre-made levels list
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            Debug.LogError($"Invalid level index: {levelIndex}");
            return;
        }
        
        LevelData levelData = levels[levelIndex];
        
        currentLevelIndex = levelIndex;
        mistakeCount = progress.mistakeCount;
        snakesExited = 0;
        isGameOver = false;
        totalSnakesInLevel = levelData.snakes.Count; // Set total snake count
        
        // Initialize grid
        GridManager.Instance.InitializeGrid(levelData.gridWidth, levelData.gridHeight);
        
        // Reset camera to fit the new grid
        var cameraController = Camera.main?.GetComponent<CameraController>();
        cameraController?.OnGridChanged();
        
        // Count how many snakes have already exited
        int exitedCount = 0;
        
        // Spawn snakes from saved state
        for (int i = 0; i < progress.snakeStates.Count; i++)
        {
            SnakeState snakeState = progress.snakeStates[i];
            
            if (snakeState.hasExited)
            {
                exitedCount++;
                continue; // Skip exited snakes
            }
            
            if (i >= levelData.snakes.Count)
                continue;
            
            LevelData.SnakeData originalSnakeData = levelData.snakes[i];
            
            GameObject snakeObj = new GameObject($"Snake_{activeSnakes.Count}");
            snakeObj.transform.SetParent(snakeContainer);
            snakeObj.transform.position = Vector3.zero;
            
            Snake snake = snakeObj.AddComponent<Snake>();
            snake.InitializeSnake(snakeState.segments, originalSnakeData.color, originalSnakeData.exitDirection);
            
            activeSnakes.Add(snake);
        }
        
        snakesExited = exitedCount;
        
        // Update UI
        GameUI.Instance?.UpdateLivesUI(RemainingLives);
        GameUI.Instance?.UpdateLevelUI(currentLevelIndex + 1);
        GameUI.Instance?.ShowGameplayUI();
        
        Debug.Log($"Level {currentLevelIndex + 1} loaded from saved progress. Mistakes: {mistakeCount}, Snakes exited: {snakesExited}");
    }

    private void LoadLevel(int levelIndex)
    {
        ClearLevel();
        
        // Load level from pre-made levels list
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            Debug.LogError($"Invalid level index: {levelIndex}. Make sure to add levels to the Levels list!");
            return;
        }
        
        LevelData levelData = levels[levelIndex];
        
        currentLevelIndex = levelIndex;
        mistakeCount = 0;
        snakesExited = 0;
        isGameOver = false;
        totalSnakesInLevel = levelData.snakes.Count; // Set total snake count
        
        // Initialize grid
        GridManager.Instance.InitializeGrid(levelData.gridWidth, levelData.gridHeight);
        
        // Reset camera to fit the new grid
        var cameraController = Camera.main?.GetComponent<CameraController>();
        cameraController?.OnGridChanged();
        
        // Spawn snakes
        foreach (var snakeData in levelData.snakes)
        {
            GameObject snakeObj = new GameObject($"Snake_{activeSnakes.Count}");
            snakeObj.transform.SetParent(snakeContainer);
            snakeObj.transform.position = Vector3.zero;
            
            Snake snake = snakeObj.AddComponent<Snake>();
            snake.InitializeSnake(snakeData.segments, snakeData.color, snakeData.exitDirection);
            
            activeSnakes.Add(snake);
        }
        
        // Update UI
        GameUI.Instance?.UpdateLivesUI(RemainingLives);
        GameUI.Instance?.UpdateLevelUI(currentLevelIndex + 1);
        GameUI.Instance?.ShowGameplayUI();
        
        Debug.Log($"Level {currentLevelIndex + 1} loaded with {activeSnakes.Count} snakes");
    }
    
    public void ReturnToLevelSelection()
    {
        // Save current progress before returning
        if (currentLevelIndex >= 0 && !isGameOver && activeSnakes.Count > 0)
        {
            ProgressManager.Instance?.SaveLevelProgress(currentLevelIndex, mistakeCount, activeSnakes);
        }
        
        // Clear hint
        if (hintedSnake != null)
        {
            hintedSnake.SetHighlight(false);
            hintedSnake = null;
        }
        
        // Hide exit paths
        HideExitPaths();
        
        // Clear level
        ClearLevel();
        currentLevelIndex = -1;
        
        // Show level selection UI
        GameUI.Instance?.HideGameplayUI();
        LevelSelectionUI.Instance?.ShowLevelSelection();
        
        Debug.Log("Returned to level selection");
    }

    private void ClearLevel()
    {
        // Clear all snake segments from the grid BEFORE destroying them
        // This ensures clean grid state for the next level
        foreach (var snake in activeSnakes)
        {
            if (snake != null && snake.gameObject != null)
            {
                // Stop all animations and coroutines
                snake.ForceStopAnimations();
                // Force clear this snake's occupancy immediately
                snake.ForceCleanup();
            }
        }
        
        // Now clear the entire grid (should already be empty)
        GridManager.Instance.ClearOccupancy();
        
        // Destroy all snake GameObjects
        foreach (var snake in activeSnakes)
        {
            if (snake != null)
            {
                Destroy(snake.gameObject);
            }
        }
        activeSnakes.Clear();
        
        Debug.Log("Level cleared - grid reset complete");
    }

    public void OnSnakeExited()
    {
        snakesExited++;
        
        Debug.Log($"Snake exited! ({snakesExited}/{totalSnakesInLevel})");
        
        // Clear hint when a snake exits
        if (hintedSnake != null)
        {
            hintedSnake.SetHighlight(false);
            hintedSnake = null;
        }
        
        // Check against total snakes in level, not active snakes count
        if (snakesExited >= totalSnakesInLevel)
        {
            OnLevelComplete();
        }
    }

    public void OnMistake()
    {
        mistakeCount++;
        
        Debug.Log($"Mistake! ({mistakeCount}/{maxMistakes})");
        
        GameUI.Instance?.UpdateLivesUI(RemainingLives);
        
        if (mistakeCount >= maxMistakes)
        {
            OnGameOver();
        }
    }

    private void OnLevelComplete()
    {
        Debug.Log("Level Complete!");
        isGameOver = true;
        
        // Mark level as completed
        if (currentLevelIndex >= 0)
        {
            ProgressManager.Instance?.MarkLevelCompleted(currentLevelIndex);
        }
        
        GameUI.Instance?.ShowLevelComplete();
    }

    private void OnGameOver()
    {
        Debug.Log("Game Over!");
        isGameOver = true;
        
        GameUI.Instance?.ShowGameOver();
    }

    public void RestartLevel()
    {
        // Clear hint before restarting
        if (hintedSnake != null)
        {
            hintedSnake.SetHighlight(false);
            hintedSnake = null;
        }
        
        // Hide exit paths
        HideExitPaths();
        
        LoadLevel(currentLevelIndex);
    }

    public void ToggleHint()
    {
        // If hint is already active, turn it off
        if (hintedSnake != null)
        {
            hintedSnake.SetHighlight(false);
            hintedSnake = null;
            Debug.Log("Hint OFF");
            return;
        }
        
        // Find a snake that can escape
        Snake escapeableSnake = FindEscapeableSnake();
        
        if (escapeableSnake != null)
        {
            hintedSnake = escapeableSnake;
            hintedSnake.SetHighlight(true);
            Debug.Log($"Hint: Highlighted snake that can escape!");
        }
        else
        {
            Debug.Log("No snake can currently escape!");
        }
    }
    
    private Snake FindEscapeableSnake()
    {
        // Find all snakes that can actually escape (clear path to border)
        List<Snake> escapeableSnakes = new List<Snake>();
        
        foreach (var snake in activeSnakes)
        {
            if (snake != null && !snake.HasExited && !snake.IsMoving)
            {
                // Check if this snake has a completely clear path to the border
                if (CanSnakeEscape(snake))
                {
                    escapeableSnakes.Add(snake);
                }
            }
        }
        
        // Return a random escapeable snake (or null if none)
        if (escapeableSnakes.Count > 0)
        {
            int randomIndex = Random.Range(0, escapeableSnakes.Count);
            return escapeableSnakes[randomIndex];
        }
        
        return null;
    }

    /// <summary>
    /// Checks if a snake has a completely clear path to escape (reach the border).
    /// The snake can only escape if there are NO obstacles in its entire exit path.
    /// </summary>
    private bool CanSnakeEscape(Snake snake)
    {
        if (snake == null) return false;
        
        List<Vector2Int> segments = snake.GetSegments();
        if (segments == null || segments.Count == 0) return false;
        
        Vector2Int headPos = segments[0];
        Vector2Int exitDir = snake.exitDirection;
        HashSet<Vector2Int> ownBody = new HashSet<Vector2Int>(segments);
        
        // Raycast from head to border
        Vector2Int checkPos = headPos + exitDir;
        
        while (GridManager.Instance.IsWithinBounds(checkPos))
        {
            // Check if there's any obstacle (another snake)
            if (GridManager.Instance.IsCellOccupied(checkPos))
            {
                Snake blockingSnake = GridManager.Instance.GetSnakeAtCell(checkPos);
                
                // If it's our own tail that we'll vacate, that's okay
                if (blockingSnake == snake)
                {
                    // Check if this is a position we'll vacate as we move
                    // Only the tail cells can be "vacated" during movement
                    // But actually, as we move forward, we vacate our tail
                    // This is complex - for simplicity, if we hit ourselves, not escapeable
                    if (!ownBody.Contains(checkPos))
                    {
                        return false;
                    }
                    // If it's our own body, we can pass through as we slither
                }
                else
                {
                    // Another snake is blocking us
                    return false;
                }
            }
            
            checkPos += exitDir;
        }
        
        // Reached border without hitting any obstacle
        return true;
    }

    public void LoadNextLevel()
    {
        int nextLevel = currentLevelIndex + 1;
        
        if (nextLevel < levels.Count)
        {
            LoadLevel(nextLevel);
        }
        else
        {
            Debug.Log("All levels completed! Restarting from level 1.");
            LoadLevel(0);
        }
    }

    private void Update()
    {
        // Only allow hotkeys if a level is loaded
        if (currentLevelIndex < 0)
            return;
        
        // Quick restart with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
        
        // Next level with N key (for testing)
        if (Input.GetKeyDown(KeyCode.N))
        {
            LoadNextLevel();
        }
        
        // Back to menu with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToLevelSelection();
        }
    }
    
    public List<Snake> GetActiveSnakes()
    {
        return new List<Snake>(activeSnakes);
    }
    
    public void ToggleExitPaths()
    {
        showingExitPaths = !showingExitPaths;
        
        foreach (var snake in activeSnakes)
        {
            if (snake != null && !snake.HasExited)
            {
                snake.SetExitPathVisible(showingExitPaths);
            }
        }
        
        Debug.Log($"Exit paths: {(showingExitPaths ? "ON" : "OFF")}");
    }
    
    public void HideExitPaths()
    {
        showingExitPaths = false;
        
        foreach (var snake in activeSnakes)
        {
            if (snake != null)
            {
                snake.SetExitPathVisible(false);
            }
        }
    }
}
