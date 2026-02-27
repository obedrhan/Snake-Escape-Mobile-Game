using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("UI References")]
    public GameObject gameplayPanel; // Container for all gameplay UI elements
    public Transform lifeIndicatorContainer;
    public TextMeshProUGUI levelText;
    public GameObject levelCompletePanel;
    public GameObject gameOverPanel;
    
    [Header("Buttons")]
    public Button restartButton;
    public Button nextLevelButton;
    public Button refreshButton;
    public Button hintButton;
    public Button backButton;
    public Button showPathsButton;
    
    [Header("Life Indicator Settings")]
    public Color activeLifeColor = Color.red;
    public Color lostLifeColor = new Color(1f, 0f, 0f, 0.3f); // Transparent red
    public float indicatorSize = 50f;
    public float indicatorSpacing = 10f;
    
    private List<Image> lifeIndicators = new List<Image>();
    private int maxLives = 3;

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
        // Hide panels at start
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Setup button listeners
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClicked);
        
        if (hintButton != null)
            hintButton.onClick.AddListener(OnHintClicked);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
        
        if (showPathsButton != null)
            showPathsButton.onClick.AddListener(OnShowPathsClicked);
        
        // Start with gameplay UI hidden (use safe method)
        SafeHideGameplayUI();
        
        // Initialize life indicators
        if (GameManager.Instance != null)
        {
            maxLives = GameManager.Instance.maxMistakes;
            CreateLifeIndicators(maxLives);
        }
    }
    
    private void SafeHideGameplayUI()
    {
        // Safer version that just sets active to false without calling other methods
        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(false);
        }
    }

    private void CreateLifeIndicators(int count)
    {
        // Clear existing indicators
        foreach (var indicator in lifeIndicators)
        {
            if (indicator != null)
                Destroy(indicator.gameObject);
        }
        lifeIndicators.Clear();
        
        if (lifeIndicatorContainer == null)
        {
            Debug.LogError("Life Indicator Container is not assigned!");
            return;
        }
        
        // Create new indicators
        for (int i = 0; i < count; i++)
        {
            GameObject indicatorObj = new GameObject($"LifeIndicator_{i}", typeof(Image));
            indicatorObj.transform.SetParent(lifeIndicatorContainer, false);
            
            Image indicator = indicatorObj.GetComponent<Image>();
            
            // Create circular sprite
            indicator.sprite = CreateCircleSprite();
            indicator.color = activeLifeColor;
            
            // Set size
            RectTransform rectTransform = indicator.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
            
            lifeIndicators.Add(indicator);
        }
    }
    
    private Sprite CreateCircleSprite()
    {
        // Create a simple circle texture
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void UpdateLivesUI(int remainingLives)
    {
        for (int i = 0; i < lifeIndicators.Count; i++)
        {
            if (i < remainingLives)
            {
                // Active life
                lifeIndicators[i].color = activeLifeColor;
            }
            else
            {
                // Lost life - check if this is the one being lost now
                if (lifeIndicators[i].color == activeLifeColor)
                {
                    // This life is being lost - play break animation
                    PlayLifeBreakAnimation(lifeIndicators[i]);
                }
                else
                {
                    lifeIndicators[i].color = lostLifeColor;
                }
            }
        }
    }
    
    private void PlayLifeBreakAnimation(Image lifeIndicator)
    {
        if (lifeIndicator == null) return;
        
        RectTransform rect = lifeIndicator.GetComponent<RectTransform>();
        
        // Kill any existing tweens
        rect.DOKill();
        lifeIndicator.DOKill();
        
        // Create break/crack sequence
        Sequence breakSequence = DOTween.Sequence();
        
        // Flash red
        breakSequence.Append(lifeIndicator.DOColor(Color.white, 0.05f));
        
        // Shake violently
        breakSequence.Join(rect.DOShakeScale(0.3f, 0.5f, 20, 90));
        breakSequence.Join(rect.DOShakeRotation(0.3f, new Vector3(0, 0, 30), 20, 90));
        
        // Shrink and fade
        breakSequence.Append(rect.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad));
        breakSequence.Append(rect.DOScale(0.8f, 0.2f).SetEase(Ease.InBack));
        
        // Fade to lost color
        breakSequence.Join(lifeIndicator.DOColor(lostLifeColor, 0.2f));
        
        // Reset scale
        breakSequence.Append(rect.DOScale(1f, 0.1f));
    }

    public void UpdateLevelUI(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Level {level}";
        }
    }

    public void ShowLevelComplete()
    {
        // Hide gameplay UI elements (but keep the panel active for the level complete overlay)
        HideGameplayElements();
        
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            
            // Animate the panel
            RectTransform panelRect = levelCompletePanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.zero;
                panelRect.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
            }
        }
        
        // Play confetti!
        if (ConfettiManager.Instance != null)
        {
            ConfettiManager.Instance.PlayConfetti();
        }
    }

    public void ShowGameOver()
    {
        // Hide gameplay UI elements (but keep the panel active for the game over overlay)
        HideGameplayElements();
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            
            // Animate the panel with a shake
            RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.one * 1.5f;
                panelRect.DOScale(1f, 0.3f).SetEase(Ease.OutBounce);
                panelRect.DOShakeRotation(0.5f, new Vector3(0, 0, 10), 10, 90);
            }
        }
    }
    
    private void HideGameplayElements()
    {
        // Hide gameplay buttons and indicators, but not the entire panel
        try
        {
            if (refreshButton != null && refreshButton.gameObject != null)
                refreshButton.gameObject.SetActive(false);
            if (hintButton != null && hintButton.gameObject != null)
                hintButton.gameObject.SetActive(false);
            if (backButton != null && backButton.gameObject != null)
                backButton.gameObject.SetActive(false);
            if (showPathsButton != null && showPathsButton.gameObject != null)
                showPathsButton.gameObject.SetActive(false);
            if (lifeIndicatorContainer != null && lifeIndicatorContainer.gameObject != null)
                lifeIndicatorContainer.gameObject.SetActive(false);
            if (levelText != null && levelText.gameObject != null)
                levelText.gameObject.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"HideGameplayElements error: {e.Message}");
        }
    }
    
    private void ShowGameplayElements()
    {
        // Show gameplay buttons and indicators
        try
        {
            if (refreshButton != null && refreshButton.gameObject != null)
                refreshButton.gameObject.SetActive(true);
            if (hintButton != null && hintButton.gameObject != null)
                hintButton.gameObject.SetActive(true);
            if (backButton != null && backButton.gameObject != null)
                backButton.gameObject.SetActive(true);
            if (showPathsButton != null && showPathsButton.gameObject != null)
                showPathsButton.gameObject.SetActive(true);
            if (lifeIndicatorContainer != null && lifeIndicatorContainer.gameObject != null)
                lifeIndicatorContainer.gameObject.SetActive(true);
            if (levelText != null && levelText.gameObject != null)
                levelText.gameObject.SetActive(true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ShowGameplayElements error: {e.Message}");
        }
    }

    private void OnRestartClicked()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Show gameplay elements again
        ShowGameplayElements();
        
        GameManager.Instance?.RestartLevel();
    }

    private void OnNextLevelClicked()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Show gameplay elements again
        ShowGameplayElements();
        
        GameManager.Instance?.LoadNextLevel();
    }
    
    private void OnRefreshClicked()
    {
        GameManager.Instance?.RestartLevel();
    }
    
    private void OnHintClicked()
    {
        GameManager.Instance?.ToggleHint();
    }
    
    private void OnBackClicked()
    {
        GameManager.Instance?.ReturnToLevelSelection();
    }
    
    private void OnShowPathsClicked()
    {
        GameManager.Instance?.ToggleExitPaths();
    }
    
    public void ShowGameplayUI()
    {
        try
        {
            if (gameplayPanel != null)
            {
                gameplayPanel.SetActive(true);
            }
            
            // Make sure all gameplay elements are visible
            ShowGameplayElements();
            
            // Hide any open result panels
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ShowGameplayUI error: {e.Message}");
        }
    }
    
    public void HideGameplayUI()
    {
        try
        {
            if (gameplayPanel != null)
            {
                gameplayPanel.SetActive(false);
            }
            
            // Also hide any open panels
            if (levelCompletePanel != null)
                levelCompletePanel.SetActive(false);
            
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"HideGameplayUI error: {e.Message}");
        }
    }
}
