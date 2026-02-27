using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ConfettiManager : MonoBehaviour
{
    public static ConfettiManager Instance { get; private set; }

    [Header("Confetti Settings")]
    public int confettiCount = 50;
    public float burstDuration = 2f;
    public float spreadRadius = 5f;
    public float confettiSize = 0.3f;
    
    [Header("Colors")]
    public Color[] confettiColors = new Color[]
    {
        Color.red,
        Color.yellow,
        Color.green,
        Color.blue,
        Color.magenta,
        Color.cyan,
        new Color(1f, 0.5f, 0f), // Orange
        new Color(1f, 0.8f, 0f)  // Gold
    };
    
    private List<GameObject> activeConfetti = new List<GameObject>();
    private Camera mainCamera;

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
        mainCamera = Camera.main;
    }

    public void PlayConfetti()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Clear any existing confetti
        ClearConfetti();
        
        // Get screen center in world coordinates
        Vector3 screenCenter = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));
        screenCenter.z = 0;
        
        // Create confetti burst
        for (int i = 0; i < confettiCount; i++)
        {
            CreateConfettiPiece(screenCenter);
        }
    }

    private void CreateConfettiPiece(Vector3 origin)
    {
        // Create confetti GameObject
        GameObject confetti = GameObject.CreatePrimitive(PrimitiveType.Quad);
        confetti.name = "Confetti";
        confetti.transform.SetParent(transform);
        
        // Remove collider
        Collider col = confetti.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        
        // Random starting position near center
        Vector3 startPos = origin + new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            0
        );
        confetti.transform.position = startPos;
        
        // Random size
        float size = confettiSize * Random.Range(0.5f, 1.5f);
        confetti.transform.localScale = new Vector3(size, size, 1f);
        
        // Random rotation
        confetti.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        
        // Random color
        Renderer renderer = confetti.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = confettiColors[Random.Range(0, confettiColors.Length)];
            renderer.sortingOrder = 100;
        }
        
        activeConfetti.Add(confetti);
        
        // Animate confetti
        AnimateConfetti(confetti);
    }

    private void AnimateConfetti(GameObject confetti)
    {
        if (confetti == null) return;
        
        // Random target position (burst outward)
        Vector3 targetPos = confetti.transform.position + new Vector3(
            Random.Range(-spreadRadius, spreadRadius),
            Random.Range(-spreadRadius, spreadRadius),
            0
        );
        
        // Add gravity effect - go up first, then down
        Vector3 midPoint = confetti.transform.position + new Vector3(
            Random.Range(-spreadRadius * 0.5f, spreadRadius * 0.5f),
            Random.Range(2f, 4f), // Go up
            0
        );
        
        // Create path
        Vector3[] path = new Vector3[] { midPoint, targetPos + Vector3.down * 3f };
        
        // Movement animation
        confetti.transform.DOPath(path, burstDuration, PathType.CatmullRom)
            .SetEase(Ease.OutQuad);
        
        // Rotation animation
        confetti.transform.DORotate(
            new Vector3(0, 0, Random.Range(360f, 720f)),
            burstDuration,
            RotateMode.FastBeyond360
        ).SetEase(Ease.Linear);
        
        // Scale animation (shrink at end)
        confetti.transform.DOScale(0f, burstDuration * 0.3f)
            .SetDelay(burstDuration * 0.7f)
            .SetEase(Ease.InQuad);
        
        // Fade out
        Renderer renderer = confetti.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            Color startColor = renderer.material.color;
            DOTween.To(
                () => renderer.material.color.a,
                x => {
                    Color c = renderer.material.color;
                    c.a = x;
                    renderer.material.color = c;
                },
                0f,
                burstDuration * 0.3f
            ).SetDelay(burstDuration * 0.7f);
        }
        
        // Destroy after animation
        Destroy(confetti, burstDuration + 0.5f);
    }

    public void ClearConfetti()
    {
        foreach (var confetti in activeConfetti)
        {
            if (confetti != null)
            {
                confetti.transform.DOKill();
                Destroy(confetti);
            }
        }
        activeConfetti.Clear();
    }

    private void OnDestroy()
    {
        ClearConfetti();
    }
}
