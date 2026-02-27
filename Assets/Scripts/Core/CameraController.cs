using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float baseMaxZoom = 15f;
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float extraZoomOutMargin = 1.2f; // Allow zooming out 20% beyond fit
    
    [Header("Bounds")]
    [SerializeField] private float boundsPadding = 2f;
    
    private Camera cam;
    private Vector3 touchStart;
    private float initialPinchDistance;
    private float initialOrthographicSize;
    
    private float gridMinX, gridMaxX, gridMinY, gridMaxY;
    
    // Dynamic max zoom calculated from grid size
    private float dynamicMaxZoom;
    
    private void Start()
    {
        cam = GetComponent<Camera>();
        
        if (cam == null)
        {
            Debug.LogError("CameraController requires a Camera component!");
            return;
        }
        
        dynamicMaxZoom = baseMaxZoom;
        
        // Wait a frame for grid to initialize, then fit to grid
        Invoke(nameof(FitCameraToGrid), 0.1f);
    }
    
    private void Update()
    {
        if (cam == null) return;
        
        // Handle mobile touch input
        if (Input.touchCount == 2)
        {
            HandlePinchZoom();
        }
        else if (Input.touchCount == 1)
        {
            HandlePan();
        }
        
        // Handle mouse input for testing in editor
        #if UNITY_EDITOR
        HandleMouseControls();
        #endif
        
        // Clamp camera position to bounds
        ClampCamera();
    }
    
    private void HandlePinchZoom()
    {
        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);
        
        if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
        {
            initialPinchDistance = Vector2.Distance(touch0.position, touch1.position);
            initialOrthographicSize = cam.orthographicSize;
        }
        else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
        {
            float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);
            float pinchDelta = initialPinchDistance - currentPinchDistance;
            
            float targetSize = initialOrthographicSize + (pinchDelta * zoomSpeed * 0.01f);
            cam.orthographicSize = Mathf.Clamp(targetSize, minZoom, dynamicMaxZoom);
        }
    }
    
    private void HandlePan()
    {
        Touch touch = Input.GetTouch(0);
        
        if (touch.phase == TouchPhase.Began)
        {
            touchStart = cam.ScreenToWorldPoint(touch.position);
            touchStart.z = 0;
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            Vector3 currentTouch = cam.ScreenToWorldPoint(touch.position);
            currentTouch.z = 0;
            
            Vector3 direction = touchStart - currentTouch;
            cam.transform.position += direction;
            
            touchStart = cam.ScreenToWorldPoint(touch.position);
            touchStart.z = 0;
        }
    }
    
    private void HandleMouseControls()
    {
        // Mouse wheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize - scroll * zoomSpeed * 5f,
                minZoom,
                dynamicMaxZoom
            );
        }
        
        // Middle mouse button drag
        if (Input.GetMouseButtonDown(2))
        {
            touchStart = cam.ScreenToWorldPoint(Input.mousePosition);
            touchStart.z = 0;
        }
        else if (Input.GetMouseButton(2))
        {
            Vector3 currentMouse = cam.ScreenToWorldPoint(Input.mousePosition);
            currentMouse.z = 0;
            
            Vector3 direction = touchStart - currentMouse;
            cam.transform.position += direction;
            
            touchStart = cam.ScreenToWorldPoint(Input.mousePosition);
            touchStart.z = 0;
        }
        
        // Right mouse button drag (alternative)
        if (Input.GetMouseButtonDown(1))
        {
            touchStart = cam.ScreenToWorldPoint(Input.mousePosition);
            touchStart.z = 0;
        }
        else if (Input.GetMouseButton(1))
        {
            Vector3 currentMouse = cam.ScreenToWorldPoint(Input.mousePosition);
            currentMouse.z = 0;
            
            Vector3 direction = touchStart - currentMouse;
            cam.transform.position += direction;
            
            touchStart = cam.ScreenToWorldPoint(Input.mousePosition);
            touchStart.z = 0;
        }
    }
    
    private void FitCameraToGrid()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogWarning("GridManager not found. Retrying...");
            Invoke(nameof(FitCameraToGrid), 0.1f);
            return;
        }
        
        // Calculate grid bounds in world space
        float gridWorldWidth = GridManager.Instance.gridWidth * GridManager.Instance.cellSize;
        float gridWorldHeight = GridManager.Instance.gridHeight * GridManager.Instance.cellSize;
        
        Vector3 gridCenter = GridManager.Instance.GridToWorldPosition(
            new Vector2Int(
                GridManager.Instance.gridWidth / 2,
                GridManager.Instance.gridHeight / 2
            )
        );
        
        // Set camera position to grid center
        cam.transform.position = new Vector3(gridCenter.x, gridCenter.y, cam.transform.position.z);
        
        // Calculate required orthographic size to fit entire grid
        float aspectRatio = (float)Screen.width / Screen.height;
        float requiredSizeForHeight = gridWorldHeight / 2f + boundsPadding;
        float requiredSizeForWidth = (gridWorldWidth / aspectRatio) / 2f + boundsPadding;
        
        float requiredSize = Mathf.Max(requiredSizeForHeight, requiredSizeForWidth);
        
        // Set dynamic max zoom to allow the grid to fit, plus extra margin
        dynamicMaxZoom = Mathf.Max(baseMaxZoom, requiredSize * extraZoomOutMargin);
        
        // Set camera to fit the grid (no clamping to maxZoom here - we want it to fit)
        cam.orthographicSize = requiredSize;
        
        // Store grid bounds for clamping
        gridMinX = gridCenter.x - gridWorldWidth / 2f;
        gridMaxX = gridCenter.x + gridWorldWidth / 2f;
        gridMinY = gridCenter.y - gridWorldHeight / 2f;
        gridMaxY = gridCenter.y + gridWorldHeight / 2f;
        
        Debug.Log($"Camera fitted to grid: {GridManager.Instance.gridWidth}×{GridManager.Instance.gridHeight}, ortho size: {requiredSize:F1}, max zoom: {dynamicMaxZoom:F1}");
    }
    
    private void ClampCamera()
    {
        if (gridMinX == 0 && gridMaxX == 0)
        {
            // Grid bounds not initialized yet
            return;
        }
        
        // Calculate camera bounds based on orthographic size
        float verticalSize = cam.orthographicSize;
        float horizontalSize = verticalSize * cam.aspect;
        
        // Calculate allowed camera movement range
        float minX = gridMinX - boundsPadding + horizontalSize;
        float maxX = gridMaxX + boundsPadding - horizontalSize;
        float minY = gridMinY - boundsPadding + verticalSize;
        float maxY = gridMaxY + boundsPadding - verticalSize;
        
        Vector3 pos = cam.transform.position;
        
        // Only clamp if bounds are valid (camera is zoomed in enough)
        if (minX <= maxX)
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }
        else
        {
            // Camera view is wider than grid - center it
            pos.x = (gridMinX + gridMaxX) / 2f;
        }
        
        if (minY <= maxY)
        {
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }
        else
        {
            // Camera view is taller than grid - center it
            pos.y = (gridMinY + gridMaxY) / 2f;
        }
        
        cam.transform.position = pos;
    }
    
    /// <summary>
    /// Public method to reset camera to fit grid
    /// </summary>
    public void ResetCamera()
    {
        FitCameraToGrid();
    }
    
    /// <summary>
    /// Call this when the grid size changes (e.g., loading a new level)
    /// </summary>
    public void OnGridChanged()
    {
        FitCameraToGrid();
    }
}
