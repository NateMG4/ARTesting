using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ARFlockManager : MonoBehaviour
{
    [Header("Flock Reference")]
    [Tooltip("Reference to the single GPU Flock instance in the scene.")]
    public GPUFlock FlockInstance;
    
    [Header("AR Visualization")]
    [Tooltip("Visual indicator (e.g., ring) to show where the flock will spawn.")]
    public GameObject PlacementIndicator;

    private ARRaycastManager _raycastManager;
    private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private bool _hasPlacedFlock = false;

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
        
        // Ensure the flock starts hidden so we don't see it at (0,0,0) before tracking
        if (FlockInstance != null)
        {
            FlockInstance.gameObject.SetActive(false);
        }
    }



    void Update()
    {
        // 1. Raycast from screen center for Indicator
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        if (_raycastManager.Raycast(screenCenter, _hits, TrackableType.PlaneWithinPolygon))
        {
            UpdatePlacementIndicator(_hits[0].pose);
        }
        else
        {
            if (PlacementIndicator != null) PlacementIndicator.SetActive(false);
        }

        // 2. Handle Input (Tap) - Raycast from any active touch
        Vector2 inputPosition = Vector2.zero;
        bool isPressed = false;

        // Check Touch (Iterate all to be safe)
        if (Touchscreen.current != null) {
            foreach(var touch in Touchscreen.current.touches) {
                if (touch.press.wasPressedThisFrame) {
                    inputPosition = touch.position.ReadValue();
                    isPressed = true;
                    Debug.Log($"[ARFlockManager] Touch Detected at {inputPosition}");
                    break;
                }
            }
        }
        
        // Check Mouse (fallback)
        if (!isPressed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) {
            inputPosition = Mouse.current.position.ReadValue();
            isPressed = true;
            Debug.Log($"[ARFlockManager] Mouse Click at {inputPosition}");
        }

        // 3. Process Input if Press Detected AND NOT over UI
        if (isPressed)
        {
            if (IsPointerOverUI(inputPosition))
            {
                 Debug.Log($"[ARFlockManager] Ignored Input at {inputPosition} (Blocked by UI)");
                 return;
            }

            // Raycast from the INPUT position
            if (_raycastManager.Raycast(inputPosition, _hits, TrackableType.PlaneWithinPolygon))
            {
                PlaceOrMoveFlock(_hits[0].pose);
            }
            else
            {
                Debug.Log($"[ARFlockManager] Raycast failed at {inputPosition} (No Plane Hit)");
            }
        }
    }

    private bool IsPointerOverUI(Vector2 touchPos)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = touchPos;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private void UpdatePlacementIndicator(Pose pose)
    {
        if (PlacementIndicator != null)
        {
             PlacementIndicator.SetActive(true);
             PlacementIndicator.transform.position = pose.position;
             PlacementIndicator.transform.rotation = pose.rotation;
        }
    }

    private void PlaceOrMoveFlock(Pose pose)
    {
        if (FlockInstance == null) return;

        // USER REQUEST: Swarm center placed at the same height as the camera -> REVERTED for Slider Control
        // Vector3 spawnPos = pose.position;
        // if (Camera.main != null) ...
        
        // Just use the pose position (ground) for now, UI Controller will override Height (Y)
        Vector3 spawnPos = pose.position; 

        Debug.Log($"[ARFlockManager] Moving flock to {spawnPos}");

        if (!_hasPlacedFlock)
        {
            // First time placement: Enable and Respawn
            FlockInstance.gameObject.SetActive(true);
            FlockInstance.Respawn(spawnPos, pose.rotation);
            _hasPlacedFlock = true;
        }
        else
        {
            // Subsequent taps: Just move (Respawn)
            FlockInstance.Respawn(spawnPos, pose.rotation);
        }
    }
}
