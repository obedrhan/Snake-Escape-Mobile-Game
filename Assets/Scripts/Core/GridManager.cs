using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int gridWidth = 8;
    public int gridHeight = 10;
    public float cellSize = 1f;
    
    [Header("Visual Settings")]
    public GameObject gridCellPrefab;
    public Color gridColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
    
    private Dictionary<Vector2Int, Snake> occupancyMap = new Dictionary<Vector2Int, Snake>();
    private Transform gridParent;

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

    public void InitializeGrid(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        occupancyMap.Clear();
        
        CreateGridVisuals();
    }

    private void CreateGridVisuals()
    {
        // Clean up old grid
        if (gridParent != null)
        {
            Destroy(gridParent.gameObject);
        }
        
        gridParent = new GameObject("GridVisuals").transform;
        gridParent.SetParent(transform);
        
        // Create grid background
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cell.name = $"Cell_{x}_{y}";
                cell.transform.SetParent(gridParent);
                cell.transform.position = GridToWorldPosition(new Vector2Int(x, y));
                cell.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
                
                Renderer renderer = cell.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = gridColor;
                
                // Move grid behind snakes
                cell.transform.position += Vector3.back * 0.1f;
                
                Destroy(cell.GetComponent<Collider>());
            }
        }
    }

    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        return new Vector3(
            gridPos.x * cellSize - (gridWidth * cellSize) / 2f + cellSize / 2f,
            gridPos.y * cellSize - (gridHeight * cellSize) / 2f + cellSize / 2f,
            0f
        );
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x + (gridWidth * cellSize) / 2f - cellSize / 2f) / cellSize);
        int y = Mathf.RoundToInt((worldPos.y + (gridHeight * cellSize) / 2f - cellSize / 2f) / cellSize);
        return new Vector2Int(x, y);
    }

    public bool IsWithinBounds(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridWidth && gridPos.y >= 0 && gridPos.y < gridHeight;
    }

    public bool IsCellOccupied(Vector2Int gridPos)
    {
        return occupancyMap.ContainsKey(gridPos);
    }

    public void SetCellOccupancy(Vector2Int gridPos, Snake snake)
    {
        if (snake == null)
        {
            occupancyMap.Remove(gridPos);
        }
        else
        {
            occupancyMap[gridPos] = snake;
        }
    }

    public Snake GetSnakeAtCell(Vector2Int gridPos)
    {
        occupancyMap.TryGetValue(gridPos, out Snake snake);
        return snake;
    }

    public bool IsExitCell(Vector2Int gridPos)
    {
        // Exit cells are at the boundaries
        return gridPos.x == -1 || gridPos.x == gridWidth || 
               gridPos.y == -1 || gridPos.y == gridHeight;
    }

    public void ClearOccupancy()
    {
        occupancyMap.Clear();
    }
}
