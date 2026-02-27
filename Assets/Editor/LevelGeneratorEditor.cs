using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Custom Inspector for LevelGenerator v4.0 (Graph-Based)
/// </summary>
[CustomEditor(typeof(LevelGenerator))]
public class LevelGeneratorEditor : Editor
{
    private LevelGenerator generator;
    private bool showGridPreview = false;
    private Vector2 gridScrollPos;
    
    // Cache for last generated level (since generator doesn't expose it)
    private List<LevelGenerator.SnakePlacement> lastGeneratedSnakes;
    private int lastGridWidth;
    private int lastGridHeight;
    
    // Serialized properties
    private SerializedProperty gridWidthProp;
    private SerializedProperty gridHeightProp;
    private SerializedProperty snakeCountProp;
    private SerializedProperty minLengthProp;
    private SerializedProperty maxLengthProp;
    private SerializedProperty curveChanceProp;
    private SerializedProperty snakeColorsProp;
    
    private void OnEnable()
    {
        generator = (LevelGenerator)target;
        
        gridWidthProp = serializedObject.FindProperty("gridWidth");
        gridHeightProp = serializedObject.FindProperty("gridHeight");
        snakeCountProp = serializedObject.FindProperty("snakeCount");
        minLengthProp = serializedObject.FindProperty("minLength");
        maxLengthProp = serializedObject.FindProperty("maxLength");
        curveChanceProp = serializedObject.FindProperty("curveChance");
        snakeColorsProp = serializedObject.FindProperty("snakeColors");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        DrawHeader();
        
        EditorGUILayout.Space(5);
        
        DrawGridConfiguration();
        
        EditorGUILayout.Space(5);
        
        DrawSnakeConfiguration();
        
        EditorGUILayout.Space(5);
        
        DrawColors();
        
        EditorGUILayout.Space(10);
        
        DrawGenerateButton();
        
        EditorGUILayout.Space(10);
        
        DrawLevelPreview();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private new void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        
        EditorGUILayout.LabelField("Wiggle Escape Level Generator", titleStyle);
        EditorGUILayout.LabelField("v4.0 - Graph-Based, Guaranteed Solvable", EditorStyles.centeredGreyMiniLabel);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawGridConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(gridWidthProp, new GUIContent("Width"));
        EditorGUILayout.PropertyField(gridHeightProp, new GUIContent("Height"));
        EditorGUILayout.EndHorizontal();
        
        // Quick presets
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Presets:", GUILayout.Width(50));
        
        if (GUILayout.Button("8x8", EditorStyles.miniButtonLeft))
        {
            gridWidthProp.intValue = 8;
            gridHeightProp.intValue = 8;
            snakeCountProp.intValue = 10;
        }
        if (GUILayout.Button("12x12", EditorStyles.miniButtonMid))
        {
            gridWidthProp.intValue = 12;
            gridHeightProp.intValue = 12;
            snakeCountProp.intValue = 20;
        }
        if (GUILayout.Button("20x20", EditorStyles.miniButtonMid))
        {
            gridWidthProp.intValue = 20;
            gridHeightProp.intValue = 20;
            snakeCountProp.intValue = 40;
        }
        if (GUILayout.Button("25x25", EditorStyles.miniButtonRight))
        {
            gridWidthProp.intValue = 25;
            gridHeightProp.intValue = 25;
            snakeCountProp.intValue = 50;
        }
        EditorGUILayout.EndHorizontal();
        
        int totalCells = gridWidthProp.intValue * gridHeightProp.intValue;
        EditorGUILayout.LabelField($"Total cells: {totalCells}", EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawSnakeConfiguration()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Snake Configuration", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(snakeCountProp, new GUIContent("Target Snakes"));
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(minLengthProp, new GUIContent("Min Length"));
        EditorGUILayout.PropertyField(maxLengthProp, new GUIContent("Max Length"));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.PropertyField(curveChanceProp, new GUIContent("Curve Chance"));
        
        EditorGUILayout.HelpBox(
            "Curve Chance: Probability for snake body to turn.\n" +
            "Higher = more curved snakes, Lower = straighter snakes.",
            MessageType.None
        );
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawColors()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Snake Colors", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Reset to Default", EditorStyles.miniButton, GUILayout.Width(100)))
        {
            snakeColorsProp.ClearArray();
            AddDefaultColors();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.PropertyField(snakeColorsProp, true);
        
        EditorGUILayout.EndVertical();
    }
    
    private void AddDefaultColors()
    {
        Color[] defaults = {
            Color.red, Color.green, Color.blue, 
            Color.yellow, Color.cyan, Color.magenta,
            new Color(1f, 0.6f, 0.2f, 1f),
            new Color(0.6f, 0.3f, 0.8f, 1f)
        };
        
        foreach (var color in defaults)
        {
            int index = snakeColorsProp.arraySize;
            snakeColorsProp.InsertArrayElementAtIndex(index);
            snakeColorsProp.GetArrayElementAtIndex(index).colorValue = color;
        }
    }
    
    private void DrawGenerateButton()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        
        if (GUILayout.Button("GENERATE LEVEL", buttonStyle, GUILayout.Height(45)))
        {
            EditorUtility.DisplayProgressBar("Generating Level", "Please wait...", 0.5f);
            
            try
            {
                Undo.RecordObject(generator, "Generate Level");
                
                // Generate with random seed
                int seed = System.Environment.TickCount;
                var snakes = generator.GenerateLevel(seed);
                
                EditorUtility.SetDirty(generator);
                
                if (snakes != null && snakes.Count > 0)
                {
                    // Cache the result for preview
                    lastGeneratedSnakes = snakes;
                    lastGridWidth = gridWidthProp.intValue;
                    lastGridHeight = gridHeightProp.intValue;
                    showGridPreview = true;
                    
                    EditorUtility.DisplayDialog(
                        "Level Generated!",
                        $"Successfully generated a {lastGridWidth}x{lastGridHeight} level\n" +
                        $"with {snakes.Count} snakes!",
                        "OK"
                    );
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Generation Failed",
                        "Could not generate level. Check console for details.",
                        "OK"
                    );
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawLevelPreview()
    {
        if (lastGeneratedSnakes == null || lastGeneratedSnakes.Count == 0)
        {
            EditorGUILayout.HelpBox("No level generated yet. Click 'Generate Level' to create one.", MessageType.Info);
            return;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Generated Level", EditorStyles.boldLabel);
        
        // Stats
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Grid: {lastGridWidth} × {lastGridHeight}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Snakes: {lastGeneratedSnakes.Count}", GUILayout.Width(80));
        
        int totalSegments = 0;
        foreach (var snake in lastGeneratedSnakes)
        {
            totalSegments += snake.segments.Count;
        }
        float density = (float)totalSegments / (lastGridWidth * lastGridHeight);
        EditorGUILayout.LabelField($"Density: {density:P1}");
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // Grid Preview
        if (lastGridWidth <= 30 && lastGridHeight <= 30)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            showGridPreview = EditorGUILayout.Foldout(showGridPreview, "Grid Preview", true);
            
            if (showGridPreview)
            {
                DrawGridPreview();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // Save Button
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Save Level", EditorStyles.boldLabel);
        
        GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        
        if (GUILayout.Button("SAVE AS ASSET", GUILayout.Height(35)))
        {
            SaveLevelAsAsset();
        }
        
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawGridPreview()
    {
        if (lastGeneratedSnakes == null) return;
        
        int[,] gridView = new int[lastGridWidth, lastGridHeight];
        
        for (int x = 0; x < lastGridWidth; x++)
        {
            for (int y = 0; y < lastGridHeight; y++)
            {
                gridView[x, y] = -1;
            }
        }
        
        for (int i = 0; i < lastGeneratedSnakes.Count; i++)
        {
            foreach (var seg in lastGeneratedSnakes[i].segments)
            {
                if (seg.x >= 0 && seg.x < lastGridWidth && seg.y >= 0 && seg.y < lastGridHeight)
                {
                    gridView[seg.x, seg.y] = i;
                }
            }
        }
        
        float maxWidth = EditorGUIUtility.currentViewWidth - 40;
        float cellSize = Mathf.Min(15, maxWidth / lastGridWidth);
        
        float gridHeight = cellSize * lastGridHeight;
        
        gridScrollPos = EditorGUILayout.BeginScrollView(gridScrollPos, GUILayout.Height(Mathf.Min(gridHeight + 20, 300)));
        
        for (int y = lastGridHeight - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            
            for (int x = 0; x < lastGridWidth; x++)
            {
                int snakeIndex = gridView[x, y];
                
                Rect rect = GUILayoutUtility.GetRect(cellSize, cellSize);
                
                if (snakeIndex >= 0 && snakeIndex < lastGeneratedSnakes.Count)
                {
                    Color snakeColor = lastGeneratedSnakes[snakeIndex].color;
                    EditorGUI.DrawRect(rect, snakeColor);
                    
                    var snake = lastGeneratedSnakes[snakeIndex];
                    if (snake.segments.Count > 0 && snake.segments[0] == new Vector2Int(x, y))
                    {
                        Rect arrowRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
                        GUI.Label(arrowRect, GetDirectionArrow(snake.exitDirection), EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                }
                
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.black);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.HelpBox("Arrows show snake head directions", MessageType.None);
    }
    
    private string GetDirectionArrow(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return "↑";
        if (dir == Vector2Int.down) return "↓";
        if (dir == Vector2Int.left) return "←";
        if (dir == Vector2Int.right) return "→";
        return "?";
    }
    
    private void SaveLevelAsAsset()
    {
        if (lastGeneratedSnakes == null || lastGeneratedSnakes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Level", "Please generate a level first!", "OK");
            return;
        }
        
        string directory = "Assets/ScriptableObjects/Levels";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }
        
        string defaultName = $"Generated_{lastGridWidth}x{lastGridHeight}_{lastGeneratedSnakes.Count}snakes";
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Generated Level",
            defaultName,
            "asset",
            "Choose where to save the level",
            directory
        );
        
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        
        // Create LevelData asset
        LevelData levelData = ScriptableObject.CreateInstance<LevelData>();
        levelData.gridWidth = lastGridWidth;
        levelData.gridHeight = lastGridHeight;
        levelData.snakes = new List<LevelData.SnakeData>();
        
        foreach (var snake in lastGeneratedSnakes)
        {
            LevelData.SnakeData snakeData = new LevelData.SnakeData
            {
                color = snake.color,
                segments = new List<Vector2Int>(snake.segments),
                exitDirection = snake.exitDirection
            };
            levelData.snakes.Add(snakeData);
        }
        
        AssetDatabase.CreateAsset(levelData, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorGUIUtility.PingObject(levelData);
        Selection.activeObject = levelData;
        
        EditorUtility.DisplayDialog(
            "Level Saved!",
            $"Level saved to:\n{path}",
            "OK"
        );
        
        Debug.Log($"Level saved to: {path}");
    }
}
