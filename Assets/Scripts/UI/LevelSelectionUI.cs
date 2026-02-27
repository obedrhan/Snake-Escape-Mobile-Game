using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class LevelSelectionUI : MonoBehaviour
{
    public static LevelSelectionUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject levelSelectionPanel;
    public Transform levelButtonContainer;
    public GameObject levelButtonPrefab;
    
    [Header("Level Button Settings")]
    public Color completedLevelColor = Color.green;
    public Color inProgressLevelColor = Color.yellow;
    public Color lockedLevelColor = Color.gray;

    private List<Button> levelButtons = new List<Button>();
    private Sprite circleSprite; // Cached circle sprite for buttons

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
        // Wait a frame to ensure all managers are initialized
        StartCoroutine(DelayedInit());
    }
    
    private System.Collections.IEnumerator DelayedInit()
    {
        yield return null; // Wait one frame
        
        CreateLevelButtons();
        ShowLevelSelection();
    }

    private void CreateLevelButtons()
    {
        if (GameManager.Instance == null || GameManager.Instance.levels == null)
        {
            Debug.LogWarning("GameManager or levels list is null! Waiting...");
            return;
        }
        
        if (levelButtonContainer == null)
        {
            Debug.LogError("Level Button Container is not assigned!");
            return;
        }

        // Create circle sprite once
        if (circleSprite == null)
        {
            circleSprite = CreateCircleSprite();
        }

        // Clear existing buttons
        if (levelButtons != null)
        {
            foreach (var button in levelButtons)
            {
                if (button != null && button.gameObject != null)
                    Destroy(button.gameObject);
            }
            levelButtons.Clear();
        }
        else
        {
            levelButtons = new List<Button>();
        }

        // Create button for each level
        for (int i = 0; i < GameManager.Instance.levels.Count; i++)
        {
            int levelIndex = i; // Capture for lambda

            GameObject buttonObj;
            
            if (levelButtonPrefab != null)
            {
                // Use prefab if provided
                buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            }
            else
            {
                // Create circular button dynamically
                buttonObj = new GameObject($"LevelButton_{i + 1}");
                buttonObj.transform.SetParent(levelButtonContainer, false);
                
                Button button = buttonObj.AddComponent<Button>();
                Image buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.sprite = circleSprite; // Use circular sprite
                buttonImage.type = Image.Type.Simple;
                buttonImage.color = Color.white;
                
                RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(150, 150);
                
                // Add text child with larger, bolder text
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);
                TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = $"Level\n<size=150%>{i + 1}</size>";
                text.fontSize = 36; // Much larger font size
                text.fontStyle = FontStyles.Bold; // Bold text
                text.fontWeight = FontWeight.Black; // Extra bold weight
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.black;
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 10); // Padding
                textRect.offsetMax = new Vector2(-10, -10);
            }

            Button btn = buttonObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnLevelSelected(levelIndex));
                levelButtons.Add(btn);
            }

            // Update button appearance based on progress
            UpdateLevelButtonAppearance(levelIndex, buttonObj);
        }
    }
    
    /// <summary>
    /// Creates a circular sprite for level buttons
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 256;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        float center = size / 2f;
        float radius = size / 2f - 2f; // Small margin for anti-aliasing
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (distance <= radius - 1.5f)
                {
                    // Inside the circle - solid
                    pixels[y * size + x] = Color.white;
                }
                else if (distance <= radius + 0.5f)
                {
                    // Edge - anti-aliased
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
                else
                {
                    // Outside - transparent
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void UpdateLevelButtonAppearance(int levelIndex, GameObject buttonObj)
    {
        if (buttonObj == null) return;
        
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage == null) return;

        // Default to white (not started)
        Color buttonColor = Color.white;
        
        if (ProgressManager.Instance != null)
        {
            LevelProgress progress = ProgressManager.Instance.GetLevelProgress(levelIndex);

            if (progress != null)
            {
                if (progress.isCompleted)
                {
                    // Level completed
                    buttonColor = completedLevelColor;
                }
                else if (progress.hasStarted)
                {
                    // Level in progress
                    buttonColor = inProgressLevelColor;
                }
            }
        }
        
        buttonImage.color = buttonColor;
    }

    private void OnLevelSelected(int levelIndex)
    {
        Debug.Log($"Level {levelIndex + 1} selected");
        
        // Animate button press
        if (levelIndex < levelButtons.Count && levelButtons[levelIndex] != null)
        {
            RectTransform buttonRect = levelButtons[levelIndex].GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Punch scale animation
                buttonRect.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f)
                    .OnComplete(() => {
                        HideLevelSelection();
                        
                        // Make sure gameplay UI is ready before loading level
                        if (GameUI.Instance != null)
                        {
                            GameUI.Instance.HideGameplayUI();
                        }
                        
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.LoadLevelFromSelection(levelIndex);
                        }
                    });
            }
            else
            {
                // Fallback without animation
                HideLevelSelectionAndLoad(levelIndex);
            }
        }
        else
        {
            HideLevelSelectionAndLoad(levelIndex);
        }
    }
    
    private void HideLevelSelectionAndLoad(int levelIndex)
    {
        HideLevelSelection();
        
        if (GameUI.Instance != null)
        {
            GameUI.Instance.HideGameplayUI();
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevelFromSelection(levelIndex);
        }
    }

    public void ShowLevelSelection()
    {
        // Hide gameplay UI first (with null check)
        if (GameUI.Instance != null)
        {
            GameUI.Instance.HideGameplayUI();
        }
        
        if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(true);
        }
        
        // Refresh button appearances
        RefreshLevelButtons();
    }

    public void HideLevelSelection()
    {
        try
        {
            if (levelSelectionPanel != null)
            {
                levelSelectionPanel.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"HideLevelSelection error: {e.Message}");
        }
    }

    public void RefreshLevelButtons()
    {
        if (levelButtons == null) return;
        
        for (int i = 0; i < levelButtons.Count; i++)
        {
            if (levelButtons[i] != null && levelButtons[i].gameObject != null)
            {
                UpdateLevelButtonAppearance(i, levelButtons[i].gameObject);
            }
        }
    }
}
