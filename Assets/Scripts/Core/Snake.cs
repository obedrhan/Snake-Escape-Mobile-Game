using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class Snake : MonoBehaviour
{
    [Header("Snake Properties")]
    public Color snakeColor = Color.green;
    public Vector2Int exitDirection = Vector2Int.up;
    
    [Header("Movement Settings")]
    public float moveSpeed = 12f; // Increased from 6f for faster movement
    public float segmentFollowDistance = 0.7f; // Distance each segment maintains from the one in front
    
    // Grid state
    private List<Vector2Int> segments = new List<Vector2Int>();
    
    // Visual components
    private GameObject headObject;
    private List<GameObject> bodySegmentObjects = new List<GameObject>();
    private List<GameObject> connectorObjects = new List<GameObject>(); // Connectors between segments
    private GameObject directionIndicator;
    private GameObject highlightGlow;
    private GameObject exitPathLine;
    
    // Path history system - body segments follow exact path of head
    private List<PathPoint> pathHistory = new List<PathPoint>(); // Stores head's travel history
    private List<float> segmentDistances = new List<float>(); // Distance each segment has traveled along path
    private float headTravelDistance = 0f; // Total distance the head has traveled
    
    // Smooth movement
    private List<Vector3> segmentPositions = new List<Vector3>(); // Current visual positions
    private List<float> segmentRotations = new List<float>();
    private float segmentSize;
    private float segmentSpacing;
    private Sprite pillSprite;
    private Sprite connectorSprite;
    
    // State
    private bool isMoving = false;
    private bool hasExited = false;
    private bool lastMoveWasInvalid = false;
    private bool isHighlighted = false;
    private bool isAnimating = false;
    
    // Path point structure for recording head movement
    private struct PathPoint
    {
        public Vector3 position;
        public float rotation;
        public float distance; // Cumulative distance from start
        
        public PathPoint(Vector3 pos, float rot, float dist)
        {
            position = pos;
            rotation = rot;
            distance = dist;
        }
    }

    public bool IsMoving => isMoving;
    public bool HasExited => hasExited;
    public int SegmentCount => segments.Count;
    public bool LastMoveWasInvalid => lastMoveWasInvalid;

    public void InitializeSnake(List<Vector2Int> initialSegments, Color color, Vector2Int exit)
    {
        if (initialSegments == null || initialSegments.Count == 0)
        {
            Debug.LogError("InitializeSnake: initialSegments is null or empty!");
            return;
        }
        
        segments = new List<Vector2Int>(initialSegments);
        snakeColor = color;
        snakeColor.a = 1f;
        
        // CRITICAL: Always use the provided exit direction if it's valid
        // This ensures the snake moves in the direction planned by the LevelGenerator
        // The dependency graph validation depends on this being correct!
        if (exit != Vector2Int.zero)
        {
            // Normalize to unit direction
            exitDirection = new Vector2Int(
                exit.x == 0 ? 0 : (exit.x > 0 ? 1 : -1),
                exit.y == 0 ? 0 : (exit.y > 0 ? 1 : -1)
            );
        }
        else
        {
            // Fallback: Calculate from segment geometry only if no exit direction provided
            if (initialSegments.Count >= 2)
            {
                Vector2Int head = segments[0];
                Vector2Int neck = segments[1];
                Vector2Int calculated = head - neck;
                
                if (calculated.x != 0 && calculated.y != 0)
                {
                    if (Mathf.Abs(calculated.x) > Mathf.Abs(calculated.y))
                        exitDirection = new Vector2Int(calculated.x > 0 ? 1 : -1, 0);
                    else
                        exitDirection = new Vector2Int(0, calculated.y > 0 ? 1 : -1);
                }
                else
                {
                    exitDirection = new Vector2Int(
                        calculated.x == 0 ? 0 : (calculated.x > 0 ? 1 : -1),
                        calculated.y == 0 ? 0 : (calculated.y > 0 ? 1 : -1)
                    );
                }
            }
            else
            {
                // Single segment with no direction - default to up
                exitDirection = Vector2Int.up;
            }
            
            Debug.LogWarning($"Snake initialized without exit direction, calculated: {exitDirection}");
        }
        
        hasExited = false;
        segmentSize = GridManager.Instance.cellSize * 0.85f;
        segmentSpacing = GridManager.Instance.cellSize * segmentFollowDistance;
        
        // Create sprites once
        pillSprite = CreatePillSprite();
        connectorSprite = CreateConnectorSprite();
        
        InitializePositions();
        CreateVisuals();
        UpdateOccupancy();
    }
    
    private void InitializePositions()
    {
        segmentPositions.Clear();
        segmentRotations.Clear();
        pathHistory.Clear();
        segmentDistances.Clear();
        headTravelDistance = 0f;
        
        for (int i = 0; i < segments.Count; i++)
        {
            Vector3 pos = GridManager.Instance.GridToWorldPosition(segments[i]);
            segmentPositions.Add(pos);
            
            // Calculate rotation based on direction to next segment
            float rotation = CalculateSegmentRotation(i);
            segmentRotations.Add(rotation);
            
            // Initialize segment distances - each segment is spaced behind the head
            segmentDistances.Add(-i * segmentSpacing);
        }
        
        // Initialize path history with initial positions (in reverse order for proper following)
        // We need enough history so all body segments have positions to follow
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            Vector3 pos = GridManager.Instance.GridToWorldPosition(segments[i]);
            float rot = CalculateSegmentRotation(i);
            float dist = (segments.Count - 1 - i) * segmentSpacing;
            pathHistory.Add(new PathPoint(pos, rot, dist));
        }
        
        // Update head travel distance to match the last point
        if (pathHistory.Count > 0)
        {
            headTravelDistance = pathHistory[pathHistory.Count - 1].distance;
        }
    }
    
    private float CalculateSegmentRotation(int index)
    {
        if (index == 0)
        {
            return Mathf.Atan2(exitDirection.y, exitDirection.x) * Mathf.Rad2Deg - 90f;
        }
        
        // For body segments, calculate from grid positions if visual positions not available
        if (index > 0 && index < segments.Count)
        {
            Vector3 prevPos, currentPos;
            
            if (index < segmentPositions.Count && index - 1 < segmentPositions.Count)
            {
                // Use visual positions if available
                prevPos = segmentPositions[index - 1];
                currentPos = segmentPositions[index];
            }
            else
            {
                // Fall back to grid positions
                prevPos = GridManager.Instance.GridToWorldPosition(segments[index - 1]);
                currentPos = GridManager.Instance.GridToWorldPosition(segments[index]);
            }
            
            Vector3 dir = prevPos - currentPos;
            if (dir.magnitude > 0.01f)
            {
                return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }
        }
        
        return segmentRotations.Count > index ? segmentRotations[index] : 0f;
    }

    private void CreateVisuals()
    {
        ClearVisuals();
        
        if (GridManager.Instance == null) return;
        
        // Create BODY segments first (so they render behind connectors and head)
        for (int i = 1; i < segments.Count; i++)
        {
            GameObject bodySegment = CreateSegmentVisual($"Body_{i}", i, false);
            bodySegmentObjects.Add(bodySegment);
        }
        
        // Create CONNECTORS between all adjacent segments
        for (int i = 0; i < segments.Count - 1; i++)
        {
            GameObject connector = CreateConnector(i);
            connectorObjects.Add(connector);
        }
        
        // Create HEAD last (renders on top)
        headObject = CreateSegmentVisual("Head", 0, true);
        
        // Create direction indicator on head
        CreateDirectionIndicator();
        
        // Initial position update
        UpdateVisuals();
    }
    
    private GameObject CreateSegmentVisual(string name, int index, bool isHead)
    {
        GameObject segment = new GameObject(name);
        segment.transform.SetParent(transform);
        
        if (index < segmentPositions.Count)
        {
            segment.transform.position = segmentPositions[index];
        }
        
        SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
        renderer.sprite = pillSprite;
        
        // Head is slightly brighter, body gets darker toward tail
        if (isHead)
        {
            renderer.color = Color.Lerp(snakeColor, Color.white, 0.2f);
            renderer.sortingOrder = 10;
        }
        else
        {
            float t = (index - 1) / (float)Mathf.Max(1, segments.Count - 2);
            Color segColor = Color.Lerp(snakeColor, snakeColor * 0.75f, t * 0.5f);
            segColor.a = 1f;
            renderer.color = segColor;
            renderer.sortingOrder = 9 - index;
        }
        
        // Size - head slightly larger
        float scale = isHead ? segmentSize * 1.05f : segmentSize;
        segment.transform.localScale = new Vector3(scale, scale * 1.2f, 1f);
        
        // Rotation
        if (index < segmentRotations.Count)
        {
            segment.transform.rotation = Quaternion.Euler(0, 0, segmentRotations[index]);
        }
        
        // Add collider
        CapsuleCollider2D collider = segment.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.7f, 1f);
        
        return segment;
    }
    
    private GameObject CreateConnector(int fromIndex)
    {
        GameObject connector = new GameObject($"Connector_{fromIndex}");
        connector.transform.SetParent(transform);
        
        SpriteRenderer renderer = connector.AddComponent<SpriteRenderer>();
        renderer.sprite = connectorSprite;
        
        // Color matches the segment it connects from (gradient effect)
        float t = fromIndex / (float)Mathf.Max(1, segments.Count - 1);
        Color connColor = Color.Lerp(snakeColor, snakeColor * 0.75f, t * 0.5f);
        connColor.a = 1f;
        renderer.color = connColor;
        renderer.sortingOrder = 5; // Between body segments
        
        return connector;
    }
    
    private void UpdateConnectors()
    {
        for (int i = 0; i < connectorObjects.Count; i++)
        {
            if (connectorObjects[i] == null) continue;
            
            int fromIdx = i;
            int toIdx = i + 1;
            
            if (fromIdx >= segmentPositions.Count || toIdx >= segmentPositions.Count) continue;
            
            Vector3 fromPos = segmentPositions[fromIdx];
            Vector3 toPos = segmentPositions[toIdx];
            
            // Position at midpoint
            Vector3 midpoint = (fromPos + toPos) / 2f;
            connectorObjects[i].transform.position = midpoint;
            
            // Calculate rotation to face from one segment to the other
            Vector3 direction = toPos - fromPos;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            connectorObjects[i].transform.rotation = Quaternion.Euler(0, 0, angle);
            
            // Scale to bridge the gap
            float distance = direction.magnitude;
            float width = segmentSize * 0.85f; // Match segment width
            float length = distance + segmentSize * 0.3f; // Overlap into segments for seamless connection
            connectorObjects[i].transform.localScale = new Vector3(width, length, 1f);
        }
    }
    
    private void CreateDirectionIndicator()
    {
        if (headObject == null) return;
        
        directionIndicator = new GameObject("DirectionIndicator");
        directionIndicator.transform.SetParent(headObject.transform);
        directionIndicator.transform.localPosition = new Vector3(0, 0.15f, 0);
        directionIndicator.transform.localRotation = Quaternion.identity;
        
        SpriteRenderer indicatorRenderer = directionIndicator.AddComponent<SpriteRenderer>();
        indicatorRenderer.sprite = CreateArrowSprite();
        indicatorRenderer.color = Color.black; // Solid black for maximum visibility
        indicatorRenderer.sortingOrder = 15;
        
        float arrowSize = segmentSize * 0.7f; // Larger arrow for better visibility
        directionIndicator.transform.localScale = new Vector3(arrowSize, arrowSize, 1f);
    }
    
    private Color GetContrastColor(Color baseColor)
    {
        float brightness = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
        return brightness > 0.5f ? new Color(0.15f, 0.15f, 0.15f, 0.95f) : new Color(1f, 1f, 1f, 0.95f);
    }
    
    private void ClearVisuals()
    {
        if (headObject != null)
        {
            headObject.transform.DOKill();
            Destroy(headObject);
        }
        foreach (var segment in bodySegmentObjects)
        {
            if (segment != null)
            {
                segment.transform.DOKill();
                Destroy(segment);
            }
        }
        foreach (var connector in connectorObjects)
        {
            if (connector != null)
            {
                connector.transform.DOKill();
                Destroy(connector);
            }
        }
        bodySegmentObjects.Clear();
        connectorObjects.Clear();
        headObject = null;
    }
    
    private void Update()
    {
        if (isAnimating)
        {
            // Update body segments to follow the path history
            UpdateBodySegmentsFromPath();
            UpdateVisuals();
        }
    }
    
    /// <summary>
    /// Updates all body segment positions by sampling from the path history.
    /// Each segment finds its position along the recorded path based on distance.
    /// </summary>
    private void UpdateBodySegmentsFromPath()
    {
        if (pathHistory == null || pathHistory.Count < 2) return;
        if (segmentDistances == null || segmentDistances.Count == 0) return;
        
        // Update each body segment (skip head at index 0 - it's animated separately)
        for (int i = 1; i < segmentPositions.Count && i < segmentDistances.Count; i++)
        {
            // Calculate the target distance for this segment
            float targetDistance = headTravelDistance - (i * segmentSpacing);
            
            // Find the position on the path at this distance
            PathPoint interpolated = GetPositionOnPath(targetDistance);
            
            segmentPositions[i] = interpolated.position;
            if (i < segmentRotations.Count)
            {
                segmentRotations[i] = interpolated.rotation;
            }
        }
    }
    
    /// <summary>
    /// Gets an interpolated position and rotation from the path history at a given distance.
    /// </summary>
    private PathPoint GetPositionOnPath(float targetDistance)
    {
        if (pathHistory.Count == 0)
        {
            return new PathPoint(Vector3.zero, 0f, 0f);
        }
        
        if (pathHistory.Count == 1)
        {
            return pathHistory[0];
        }
        
        // Clamp to valid range
        if (targetDistance <= pathHistory[0].distance)
        {
            return pathHistory[0];
        }
        
        if (targetDistance >= pathHistory[pathHistory.Count - 1].distance)
        {
            return pathHistory[pathHistory.Count - 1];
        }
        
        // Binary search for the segment containing this distance
        int low = 0;
        int high = pathHistory.Count - 1;
        
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (pathHistory[mid].distance <= targetDistance)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        
        // Interpolate between low and high
        PathPoint p1 = pathHistory[low];
        PathPoint p2 = pathHistory[high];
        
        float segmentLength = p2.distance - p1.distance;
        if (segmentLength < 0.001f)
        {
            return p1;
        }
        
        float t = (targetDistance - p1.distance) / segmentLength;
        t = Mathf.Clamp01(t);
        
        return new PathPoint(
            Vector3.Lerp(p1.position, p2.position, t),
            Mathf.LerpAngle(p1.rotation, p2.rotation, t),
            targetDistance
        );
    }
    
    /// <summary>
    /// Adds a new point to the path history when the head moves.
    /// </summary>
    private void AddPathPoint(Vector3 position, float rotation)
    {
        // Calculate distance from last point
        float distanceFromLast = 0f;
        if (pathHistory.Count > 0)
        {
            distanceFromLast = Vector3.Distance(position, pathHistory[pathHistory.Count - 1].position);
        }
        
        headTravelDistance += distanceFromLast;
        pathHistory.Add(new PathPoint(position, rotation, headTravelDistance));
        
        // Clean up old path points that are no longer needed
        // Keep enough history for all segments plus some buffer
        float minRequiredDistance = headTravelDistance - (segments.Count + 2) * segmentSpacing;
        while (pathHistory.Count > 2 && pathHistory[0].distance < minRequiredDistance)
        {
            pathHistory.RemoveAt(0);
        }
    }
    
    private void UpdateVisuals()
    {
        // Update head
        if (headObject != null && segmentPositions.Count > 0 && segmentRotations.Count > 0)
        {
            headObject.transform.position = segmentPositions[0];
            headObject.transform.rotation = Quaternion.Euler(0, 0, segmentRotations[0]);
        }
        
        // Update body segments
        for (int i = 0; i < bodySegmentObjects.Count; i++)
        {
            int posIndex = i + 1;
            if (bodySegmentObjects[i] != null && 
                posIndex < segmentPositions.Count && 
                posIndex < segmentRotations.Count)
            {
                bodySegmentObjects[i].transform.position = segmentPositions[posIndex];
                bodySegmentObjects[i].transform.rotation = Quaternion.Euler(0, 0, segmentRotations[posIndex]);
            }
        }
        
        // Update connectors between segments
        UpdateConnectors();
    }

    public bool CanMove()
    {
        if (isMoving || hasExited) return false;
        
        Vector2Int headPos = segments[0];
        Vector2Int targetPos = headPos + exitDirection;
        
        if (GridManager.Instance.IsExitCell(targetPos)) return true;
        if (!GridManager.Instance.IsWithinBounds(targetPos)) return false;
        
        if (GridManager.Instance.IsCellOccupied(targetPos))
        {
            Vector2Int tail = segments[segments.Count - 1];
            if (targetPos == tail) return true;
            return false;
        }
        
        return true;
    }

    public IEnumerator MoveSnake()
    {
        lastMoveWasInvalid = false;
        
        if (!CanMove()) yield break;
        
        isMoving = true;
        isAnimating = true;
        
        List<Vector2Int> initialSegments = new List<Vector2Int>(segments);
        List<Vector3> initialPositions = new List<Vector3>(segmentPositions);
        List<float> initialRotations = new List<float>(segmentRotations);
        List<PathPoint> initialPathHistory = new List<PathPoint>(pathHistory);
        float initialHeadDistance = headTravelDistance;
        int stepsTaken = 0;
        
        while (true)
        {
            Vector2Int headGridPos = segments[0];
            Vector2Int newHeadGridPos = headGridPos + exitDirection;
            
            // Check if exiting
            if (GridManager.Instance.IsExitCell(newHeadGridPos))
            {
                lastMoveWasInvalid = false;
                yield return StartCoroutine(ExitSnake());
                yield break;
            }
            
            // Check collision
            bool pathClear = false;
            
            if (GridManager.Instance.IsWithinBounds(newHeadGridPos))
            {
                Snake blockingSnake = GridManager.Instance.GetSnakeAtCell(newHeadGridPos);
                
                if (blockingSnake == null)
                {
                    pathClear = true;
                }
                else if (blockingSnake == this)
                {
                    Vector2Int tail = segments[segments.Count - 1];
                    pathClear = (newHeadGridPos == tail);
                }
            }
            
            if (!pathClear)
            {
                // Hit obstacle
                lastMoveWasInvalid = true;
                PlayHitObstacleAnimation();
                
                if (stepsTaken > 0)
                {
                    yield return new WaitForSeconds(0.2f); // Reduced from 0.3f
                    
                    // Return to initial position
                    ClearOccupancy();
                    segments = new List<Vector2Int>(initialSegments);
                    UpdateOccupancy();
                    
                    // Restore path history
                    pathHistory = new List<PathPoint>(initialPathHistory);
                    headTravelDistance = initialHeadDistance;
                    
                    yield return StartCoroutine(AnimateToPositions(initialPositions, initialRotations, 0.25f));
                }
                
                isMoving = false;
                isAnimating = false;
                yield break;
            }
            
            // Valid move - update grid state
            ClearOccupancy();
            segments.Insert(0, newHeadGridPos);
            segments.RemoveAt(segments.Count - 1);
            UpdateOccupancy();
            
            stepsTaken++;
            
            // Animate head to new position - body follows automatically via path history
            Vector3 newHeadPos = GridManager.Instance.GridToWorldPosition(newHeadGridPos);
            yield return StartCoroutine(AnimateHeadMove(newHeadPos));
            
            // No extra delay between moves - continuous movement
        }
    }
    
    private IEnumerator AnimateHeadMove(Vector3 targetPos)
    {
        if (segmentPositions.Count == 0) yield break;
        
        Vector3 startPos = segmentPositions[0];
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        
        // Calculate head rotation toward movement direction
        Vector3 moveDir = targetPos - startPos;
        float targetRotation = segmentRotations.Count > 0 ? segmentRotations[0] : 0f;
        if (moveDir.magnitude > 0.01f)
        {
            targetRotation = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
        }
        
        // Add squash and stretch to head (only if head exists)
        Vector3 originalScale = headObject != null ? headObject.transform.localScale : Vector3.one;
        
        // Sample interval for path recording - record frequently for smooth following
        float pathSampleInterval = 0.016f; // ~60 fps
        float lastPathSampleTime = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Use smooth linear interpolation for consistent speed
            // Add subtle ease-out at the end for polish
            float easedT = EaseOutQuad(t);
            
            Vector3 newHeadPos = Vector3.Lerp(startPos, targetPos, easedT);
            
            if (segmentPositions.Count > 0)
            {
                segmentPositions[0] = newHeadPos;
                
                // Smoothly interpolate rotation
                if (segmentRotations.Count > 0)
                {
                    segmentRotations[0] = Mathf.LerpAngle(segmentRotations[0], targetRotation, Time.deltaTime * 15f);
                }
            }
            
            // Record path points at regular intervals for body to follow
            if (elapsed - lastPathSampleTime >= pathSampleInterval)
            {
                float currentRot = segmentRotations.Count > 0 ? segmentRotations[0] : targetRotation;
                AddPathPoint(newHeadPos, currentRot);
                lastPathSampleTime = elapsed;
            }
            
            // Subtle squash and stretch effect
            if (headObject != null)
            {
                float stretchPhase = Mathf.Sin(t * Mathf.PI);
                float stretch = 1f + stretchPhase * 0.08f; // Reduced for smoother look
                float squash = 1f - stretchPhase * 0.05f;
                headObject.transform.localScale = new Vector3(
                    originalScale.x * squash,
                    originalScale.y * stretch,
                    1f
                );
            }
            
            yield return null;
        }
        
        // Ensure final position is exact
        if (segmentPositions.Count > 0)
        {
            segmentPositions[0] = targetPos;
            if (segmentRotations.Count > 0)
            {
                segmentRotations[0] = targetRotation;
            }
        }
        
        // Record final path point
        AddPathPoint(targetPos, targetRotation);
        
        if (headObject != null)
        {
            headObject.transform.localScale = originalScale;
        }
        
        UpdateVisuals();
        UpdateExitPathLine();
    }
    
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
    
    private float EaseOutBack(float t, float overshoot)
    {
        float c1 = 1.70158f * overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    
    private IEnumerator AnimateToPositions(List<Vector3> positions, List<float> rotations, float duration)
    {
        List<Vector3> startPositions = new List<Vector3>(segmentPositions);
        List<float> startRotations = new List<float>(segmentRotations);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            
            for (int i = 0; i < segmentPositions.Count && i < positions.Count; i++)
            {
                segmentPositions[i] = Vector3.Lerp(startPositions[i], positions[i], t);
                if (i < segmentRotations.Count && i < rotations.Count)
                {
                    segmentRotations[i] = Mathf.LerpAngle(startRotations[i], rotations[i], t);
                }
            }
            
            UpdateVisuals();
            yield return null;
        }
        
        // Copy final positions
        for (int i = 0; i < positions.Count && i < segmentPositions.Count; i++)
        {
            segmentPositions[i] = positions[i];
        }
        for (int i = 0; i < rotations.Count && i < segmentRotations.Count; i++)
        {
            segmentRotations[i] = rotations[i];
        }
        
        UpdateVisuals();
    }

    private IEnumerator ExitSnake()
    {
        Debug.Log("Snake exiting...");
        
        while (segments.Count > 0)
        {
            Vector2Int oldHeadPos = segments[0];
            Vector2Int newHeadPos = oldHeadPos + exitDirection;
            
            ClearOccupancy();
            
            List<Vector2Int> newSegments = new List<Vector2Int>();
            newSegments.Add(newHeadPos);
            
            for (int i = 0; i < segments.Count - 1; i++)
            {
                newSegments.Add(segments[i]);
            }
            
            segments = newSegments;
            UpdateOccupancy();
            
            // Update position arrays to match segments
            while (segmentPositions.Count < segments.Count)
            {
                segmentPositions.Add(segmentPositions.Count > 0 ? segmentPositions[segmentPositions.Count - 1] : Vector3.zero);
            }
            while (segmentRotations.Count < segments.Count)
            {
                segmentRotations.Add(segmentRotations.Count > 0 ? segmentRotations[segmentRotations.Count - 1] : 0f);
            }
            while (segmentDistances.Count < segments.Count)
            {
                segmentDistances.Add(segmentDistances.Count > 0 ? segmentDistances[segmentDistances.Count - 1] - segmentSpacing : 0f);
            }
            
            // Animate head to new position - body follows via path history
            Vector3 newHeadWorldPos = GridManager.Instance.GridToWorldPosition(newHeadPos);
            yield return StartCoroutine(AnimateHeadMove(newHeadWorldPos));
            
            // Remove off-board segments
            List<int> toRemove = new List<int>();
            for (int i = 0; i < segments.Count; i++)
            {
                if (GridManager.Instance.IsExitCell(segments[i]))
                {
                    toRemove.Add(i);
                }
            }
            
            // Remove segments and visuals (from back to front to preserve indices)
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                int idx = toRemove[i];
                
                if (idx < segments.Count) segments.RemoveAt(idx);
                if (idx < segmentPositions.Count) segmentPositions.RemoveAt(idx);
                if (idx < segmentRotations.Count) segmentRotations.RemoveAt(idx);
                if (idx < segmentDistances.Count) segmentDistances.RemoveAt(idx);
                
                // Fade out and remove visual
                if (idx == 0 && headObject != null)
                {
                    GameObject headToDestroy = headObject;
                    headObject = null;
                    headToDestroy.transform.DOKill();
                    headToDestroy.transform.DOScale(0f, 0.12f).SetEase(Ease.InBack)
                        .OnComplete(() => { if (headToDestroy != null) Destroy(headToDestroy); });
                    
                    // Promote first body segment to be the new head visually
                    if (bodySegmentObjects.Count > 0 && bodySegmentObjects[0] != null)
                    {
                        headObject = bodySegmentObjects[0];
                        bodySegmentObjects.RemoveAt(0);
                        headObject.name = "Head";
                        
                        // Add direction indicator to new head
                        if (directionIndicator != null)
                        {
                            directionIndicator.transform.SetParent(headObject.transform);
                            directionIndicator.transform.localPosition = new Vector3(0, 0.15f, 0);
                            directionIndicator.transform.localRotation = Quaternion.identity;
                        }
                        
                        // Update sorting order
                        SpriteRenderer sr = headObject.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            sr.sortingOrder = 10;
                            sr.color = Color.Lerp(snakeColor, Color.white, 0.2f);
                        }
                    }
                }
                else if (idx > 0)
                {
                    int bodyIdx = idx - 1;
                    if (bodyIdx >= 0 && bodyIdx < bodySegmentObjects.Count)
                    {
                        GameObject obj = bodySegmentObjects[bodyIdx];
                        bodySegmentObjects.RemoveAt(bodyIdx);
                        if (obj != null)
                        {
                            obj.transform.DOKill();
                            obj.transform.DOScale(0f, 0.12f).SetEase(Ease.InBack)
                                .OnComplete(() => { if (obj != null) Destroy(obj); });
                        }
                    }
                }
                
                // Remove connector associated with this segment
                if (idx > 0 && idx - 1 < connectorObjects.Count)
                {
                    GameObject conn = connectorObjects[idx - 1];
                    connectorObjects.RemoveAt(idx - 1);
                    if (conn != null)
                    {
                        conn.transform.DOKill();
                        conn.transform.DOScale(0f, 0.08f).SetEase(Ease.InBack)
                            .OnComplete(() => { if (conn != null) Destroy(conn); });
                    }
                }
                else if (idx == 0 && connectorObjects.Count > 0)
                {
                    // When head exits, remove the first connector
                    GameObject conn = connectorObjects[0];
                    connectorObjects.RemoveAt(0);
                    if (conn != null)
                    {
                        conn.transform.DOKill();
                        conn.transform.DOScale(0f, 0.08f).SetEase(Ease.InBack)
                            .OnComplete(() => { if (conn != null) Destroy(conn); });
                    }
                }
            }
            
            // Shorter wait for faster exit animation
            yield return new WaitForSeconds(0.02f);
        }
        
        hasExited = true;
        isAnimating = false;
        gameObject.SetActive(false);
        
        Debug.Log("Snake fully exited!");
        GameManager.Instance?.OnSnakeExited();
    }

    public void PlayHitObstacleAnimation()
    {
        // Shake all segments
        if (headObject != null)
        {
            headObject.transform.DOKill();
            headObject.transform.DOShakePosition(0.25f, 0.2f, 25, 90, false, true);
            headObject.transform.DOShakeRotation(0.25f, new Vector3(0, 0, 15), 20, 90);
        }
        
        for (int i = 0; i < bodySegmentObjects.Count; i++)
        {
            if (bodySegmentObjects[i] != null)
            {
                float delay = (i + 1) * 0.02f;
                bodySegmentObjects[i].transform.DOKill();
                bodySegmentObjects[i].transform.DOShakePosition(0.2f, 0.15f, 20, 90, false, true)
                    .SetDelay(delay);
                bodySegmentObjects[i].transform.DOShakeRotation(0.2f, new Vector3(0, 0, 10), 15, 90)
                    .SetDelay(delay);
            }
        }
        
        // Shake connectors too
        for (int i = 0; i < connectorObjects.Count; i++)
        {
            if (connectorObjects[i] != null)
            {
                float delay = i * 0.02f;
                connectorObjects[i].transform.DOKill();
                connectorObjects[i].transform.DOShakePosition(0.2f, 0.12f, 18, 90, false, true)
                    .SetDelay(delay);
            }
        }
    }

    // Sprite creation
    private Sprite CreatePillSprite()
    {
        int width = 64;
        int height = 80;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        float cornerRadius = width / 2f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distFromCenter = 0f;
                
                if (y < cornerRadius)
                {
                    // Bottom rounded part
                    float dx = x - width / 2f;
                    float dy = y - cornerRadius;
                    distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                }
                else if (y > height - cornerRadius)
                {
                    // Top rounded part
                    float dx = x - width / 2f;
                    float dy = y - (height - cornerRadius);
                    distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                }
                else
                {
                    // Middle straight part
                    distFromCenter = Mathf.Abs(x - width / 2f);
                }
                
                if (distFromCenter <= cornerRadius - 1)
                {
                    pixels[y * width + x] = Color.white;
                }
                else if (distFromCenter <= cornerRadius)
                {
                    // Anti-aliased edge
                    float alpha = cornerRadius - distFromCenter;
                    pixels[y * width + x] = new Color(1, 1, 1, alpha);
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
    
    private Sprite CreateArrowSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        
        // Draw a bold, solid arrow (▲ style) - thick and highly visible
        int centerX = size / 2;
        int topY = size - 6;      // Arrow tip closer to top
        int bottomY = 8;           // Arrow base closer to bottom
        int baseWidth = 44;        // Wider base for bold look
        
        // Draw filled triangle
        for (int y = bottomY; y <= topY; y++)
        {
            float t = (y - bottomY) / (float)(topY - bottomY);
            int halfWidth = (int)(baseWidth * (1f - t) / 2f);
            
            for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
            {
                if (x >= 0 && x < size)
                {
                    pixels[y * size + x] = Color.white;
                }
            }
        }
        
        // Add anti-aliasing on edges for smoother look
        for (int y = bottomY; y <= topY; y++)
        {
            float t = (y - bottomY) / (float)(topY - bottomY);
            float exactHalfWidth = baseWidth * (1f - t) / 2f;
            int halfWidth = (int)exactHalfWidth;
            
            // Left edge anti-alias
            int leftEdge = centerX - halfWidth - 1;
            if (leftEdge >= 0 && leftEdge < size)
            {
                float alpha = exactHalfWidth - halfWidth;
                pixels[y * size + leftEdge] = new Color(1, 1, 1, alpha * 0.5f);
            }
            
            // Right edge anti-alias
            int rightEdge = centerX + halfWidth + 1;
            if (rightEdge >= 0 && rightEdge < size)
            {
                float alpha = exactHalfWidth - halfWidth;
                pixels[y * size + rightEdge] = new Color(1, 1, 1, alpha * 0.5f);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    private Sprite CreateConnectorSprite()
    {
        // Simple rounded rectangle for seamless connection
        int width = 64;
        int height = 64;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        float cornerRadius = 8f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distFromEdge = 0f;
                
                // Check corners
                if (x < cornerRadius && y < cornerRadius)
                {
                    // Bottom-left corner
                    float dx = cornerRadius - x;
                    float dy = cornerRadius - y;
                    distFromEdge = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                }
                else if (x > width - cornerRadius && y < cornerRadius)
                {
                    // Bottom-right corner
                    float dx = x - (width - cornerRadius);
                    float dy = cornerRadius - y;
                    distFromEdge = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                }
                else if (x < cornerRadius && y > height - cornerRadius)
                {
                    // Top-left corner
                    float dx = cornerRadius - x;
                    float dy = y - (height - cornerRadius);
                    distFromEdge = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                }
                else if (x > width - cornerRadius && y > height - cornerRadius)
                {
                    // Top-right corner
                    float dx = x - (width - cornerRadius);
                    float dy = y - (height - cornerRadius);
                    distFromEdge = Mathf.Sqrt(dx * dx + dy * dy) - cornerRadius;
                }
                else
                {
                    // Inside the rectangle (not in corner)
                    distFromEdge = -1f;
                }
                
                if (distFromEdge < 0)
                {
                    pixels[y * width + x] = Color.white;
                }
                else if (distFromEdge < 1f)
                {
                    // Anti-aliased edge
                    float alpha = 1f - distFromEdge;
                    pixels[y * width + x] = new Color(1, 1, 1, alpha);
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    // Grid occupancy
    private void UpdateOccupancy()
    {
        if (GridManager.Instance == null) return;
        
        foreach (var segment in segments)
        {
            if (GridManager.Instance.IsWithinBounds(segment))
            {
                GridManager.Instance.SetCellOccupancy(segment, this);
            }
        }
    }

    private void ClearOccupancy()
    {
        foreach (var segment in segments)
        {
            GridManager.Instance.SetCellOccupancy(segment, null);
        }
    }

    public List<Vector2Int> GetSegments()
    {
        return new List<Vector2Int>(segments);
    }
    
    public void ForceCleanup()
    {
        if (segments != null && segments.Count > 0)
        {
            ClearOccupancy();
            segments.Clear();
        }
    }

    // Highlight system
    public void SetHighlight(bool highlight)
    {
        isHighlighted = highlight;
        
        if (highlight)
        {
            if (highlightGlow == null && headObject != null)
            {
                highlightGlow = new GameObject("HighlightGlow");
                highlightGlow.transform.SetParent(headObject.transform);
                highlightGlow.transform.localPosition = Vector3.zero;
                highlightGlow.transform.localRotation = Quaternion.identity;
                
                SpriteRenderer glowRenderer = highlightGlow.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = pillSprite;
                glowRenderer.color = new Color(1f, 1f, 0f, 0.4f);
                glowRenderer.sortingOrder = 2;
                
                highlightGlow.transform.localScale = Vector3.one * 1.4f;
                
                StartCoroutine(PulseGlow());
            }
        }
        else
        {
            if (highlightGlow != null)
            {
                Destroy(highlightGlow);
                highlightGlow = null;
            }
        }
    }
    
    private IEnumerator PulseGlow()
    {
        float time = 0f;
        
        while (isHighlighted && highlightGlow != null)
        {
            time += Time.deltaTime * 3f;
            float alpha = 0.4f + Mathf.Sin(time) * 0.15f;
            float scale = 1.4f + Mathf.Sin(time * 0.7f) * 0.1f;
            
            SpriteRenderer glowRenderer = highlightGlow.GetComponent<SpriteRenderer>();
            if (glowRenderer != null)
            {
                Color glowColor = glowRenderer.color;
                glowColor.a = alpha;
                glowRenderer.color = glowColor;
            }
            highlightGlow.transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
    }

    // Exit path visualization
    public void SetExitPathVisible(bool visible)
    {
        if (visible)
        {
            if (exitPathLine == null && segments.Count > 0)
            {
                exitPathLine = new GameObject("ExitPathLine");
                exitPathLine.transform.SetParent(transform);
                
                LineRenderer lineRenderer = exitPathLine.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;
                lineRenderer.positionCount = 2;
                lineRenderer.sortingOrder = 0;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                
                Color lineColor = snakeColor;
                lineColor.a = 0.4f;
                lineRenderer.startColor = lineColor;
                lineRenderer.endColor = lineColor;
                
                UpdateExitPathLine();
            }
        }
        else
        {
            if (exitPathLine != null)
            {
                Destroy(exitPathLine);
                exitPathLine = null;
            }
        }
    }
    
    private Vector3 CalculateExitPoint()
    {
        if (segments.Count == 0 || GridManager.Instance == null)
            return Vector3.zero;
        
        Vector2Int currentPos = segments[0];
        
        while (GridManager.Instance.IsWithinBounds(currentPos))
        {
            currentPos += exitDirection;
        }
        
        return GridManager.Instance.GridToWorldPosition(currentPos);
    }
    
    public void UpdateExitPathLine()
    {
        if (exitPathLine != null && segmentPositions.Count > 0)
        {
            LineRenderer lineRenderer = exitPathLine.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, segmentPositions[0]);
                lineRenderer.SetPosition(1, CalculateExitPoint());
            }
        }
    }

    private void OnDestroy()
    {
        // Kill all DOTween animations on this snake before destroying
        KillAllTweens();
        
        if (highlightGlow != null) Destroy(highlightGlow);
        if (exitPathLine != null) Destroy(exitPathLine);
        
        ClearVisuals();
        
        if (segments != null && segments.Count > 0 && GridManager.Instance != null)
        {
            ClearOccupancy();
        }
    }
    
    private void KillAllTweens()
    {
        // Kill tweens on head
        if (headObject != null)
        {
            headObject.transform.DOKill();
        }
        
        // Kill tweens on all body segments
        foreach (var segment in bodySegmentObjects)
        {
            if (segment != null)
            {
                segment.transform.DOKill();
            }
        }
        
        // Kill tweens on all connectors
        foreach (var connector in connectorObjects)
        {
            if (connector != null)
            {
                connector.transform.DOKill();
            }
        }
        
        // Kill tweens on highlight glow
        if (highlightGlow != null)
        {
            highlightGlow.transform.DOKill();
        }
    }
    
    public void ForceStopAnimations()
    {
        isAnimating = false;
        isMoving = false;
        StopAllCoroutines();
        KillAllTweens();
        
        // Snap segments to their grid positions
        if (segments != null && GridManager.Instance != null)
        {
            for (int i = 0; i < segments.Count && i < segmentPositions.Count; i++)
            {
                segmentPositions[i] = GridManager.Instance.GridToWorldPosition(segments[i]);
            }
            UpdateVisuals();
        }
    }
}
