using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelProgress
{
    public int levelIndex;
    public bool hasStarted;
    public bool isCompleted;
    public int mistakeCount;
    public List<SnakeState> snakeStates = new List<SnakeState>();
}

[System.Serializable]
public class SnakeState
{
    public List<Vector2Int> segments = new List<Vector2Int>();
    public bool hasExited;
}

public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    private Dictionary<int, LevelProgress> levelProgressData = new Dictionary<int, LevelProgress>();
    private const string PROGRESS_KEY_PREFIX = "LevelProgress_";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveLevelProgress(int levelIndex, int mistakes, List<Snake> activeSnakes)
    {
        LevelProgress progress = new LevelProgress
        {
            levelIndex = levelIndex,
            hasStarted = true,
            isCompleted = false,
            mistakeCount = mistakes,
            snakeStates = new List<SnakeState>()
        };

        // Save snake states
        foreach (var snake in activeSnakes)
        {
            if (snake != null)
            {
                SnakeState state = new SnakeState
                {
                    segments = new List<Vector2Int>(snake.GetSegments()),
                    hasExited = snake.HasExited
                };
                progress.snakeStates.Add(state);
            }
        }

        levelProgressData[levelIndex] = progress;
        
        // Save to PlayerPrefs
        string json = JsonUtility.ToJson(progress);
        PlayerPrefs.SetString(PROGRESS_KEY_PREFIX + levelIndex, json);
        PlayerPrefs.Save();
        
        Debug.Log($"Progress saved for Level {levelIndex + 1}");
    }

    public LevelProgress GetLevelProgress(int levelIndex)
    {
        // Check memory cache first
        if (levelProgressData.ContainsKey(levelIndex))
        {
            return levelProgressData[levelIndex];
        }

        // Load from PlayerPrefs
        string key = PROGRESS_KEY_PREFIX + levelIndex;
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            LevelProgress progress = JsonUtility.FromJson<LevelProgress>(json);
            levelProgressData[levelIndex] = progress;
            return progress;
        }

        // Return empty progress
        return new LevelProgress { levelIndex = levelIndex, hasStarted = false, isCompleted = false };
    }

    public void MarkLevelCompleted(int levelIndex)
    {
        LevelProgress progress = GetLevelProgress(levelIndex);
        progress.isCompleted = true;
        progress.hasStarted = true;
        
        levelProgressData[levelIndex] = progress;
        
        string json = JsonUtility.ToJson(progress);
        PlayerPrefs.SetString(PROGRESS_KEY_PREFIX + levelIndex, json);
        PlayerPrefs.Save();
        
        Debug.Log($"Level {levelIndex + 1} marked as completed!");
    }

    public void ClearLevelProgress(int levelIndex)
    {
        string key = PROGRESS_KEY_PREFIX + levelIndex;
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
        }
        
        if (levelProgressData.ContainsKey(levelIndex))
        {
            levelProgressData.Remove(levelIndex);
        }
        
        Debug.Log($"Progress cleared for Level {levelIndex + 1}");
    }

    public void ClearAllProgress()
    {
        // Clear all level progress
        foreach (var key in levelProgressData.Keys)
        {
            PlayerPrefs.DeleteKey(PROGRESS_KEY_PREFIX + key);
        }
        
        levelProgressData.Clear();
        PlayerPrefs.Save();
        
        Debug.Log("All progress cleared!");
    }

    public bool HasSavedProgress(int levelIndex)
    {
        LevelProgress progress = GetLevelProgress(levelIndex);
        return progress.hasStarted && !progress.isCompleted && progress.snakeStates.Count > 0;
    }
}
