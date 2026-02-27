using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Level", menuName = "Wiggle Escape/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Grid Configuration")]
    public int gridWidth = 8;
    public int gridHeight = 10;
    
    [Header("Snake Configuration")]
    public List<SnakeData> snakes = new List<SnakeData>();
    
    [System.Serializable]
    public class SnakeData
    {
        public Color color = Color.green;
        public List<Vector2Int> segments = new List<Vector2Int>();
        public Vector2Int exitDirection = Vector2Int.up; // up(0,1), down(0,-1), left(-1,0), right(1,0)
    }
}
