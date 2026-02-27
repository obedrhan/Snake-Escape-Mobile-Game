using UnityEngine;

public class InputController : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Handle mouse click or touch
        if (Input.GetMouseButtonDown(0))
        {
            HandleTap(Input.mousePosition);
        }
    }

    private void HandleTap(Vector3 screenPosition)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
        {
            return;
        }
        
        // Raycast to detect snake
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
        
        if (hit.collider != null)
        {
            // Check if we hit a snake segment
            Snake snake = hit.collider.GetComponentInParent<Snake>();
            
            if (snake != null)
            {
                TryMoveSnake(snake);
            }
        }
    }

    private void TryMoveSnake(Snake snake)
    {
        if (snake.IsMoving || snake.HasExited)
        {
            return;
        }
        
        bool canMove = snake.CanMove();
        
        if (canMove)
        {
            StartCoroutine(MoveSnakeAndCheckResult(snake));
        }
        else
        {
            // Blocked on first check - count as mistake
            GameManager.Instance?.OnMistake();
            
            // Visual feedback for blocked move
            StartCoroutine(ShakeSnake(snake));
        }
    }
    
    private System.Collections.IEnumerator MoveSnakeAndCheckResult(Snake snake)
    {
        // Start snake movement and wait for it to complete
        yield return StartCoroutine(snake.MoveSnake());
        
        // Check if the move was invalid (collision mid-movement)
        if (snake.LastMoveWasInvalid)
        {
            Debug.Log("✗ Move resulted in collision - counting as MISTAKE!");
            GameManager.Instance?.OnMistake();
        }
        else
        {
            Debug.Log("✓ Move completed successfully (no collision)");
        }
    }

    private System.Collections.IEnumerator ShakeSnake(Snake snake)
    {
        Vector3 originalPosition = snake.transform.position;
        float shakeDuration = 0.2f;
        float shakeAmount = 0.1f;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeAmount;
            float y = Random.Range(-1f, 1f) * shakeAmount;
            
            snake.transform.position = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        snake.transform.position = originalPosition;
    }
}
