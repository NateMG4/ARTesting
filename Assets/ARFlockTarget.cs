using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ARFlockTarget : MonoBehaviour
{
    [Header("The Flock Manager")]
    public GameObject FlockSystem; // The entire GPU_Flock_Final object
    public Transform FlockCenterTarget; // The "Target" child object the boids follow

    [Header("AR Settings")]
    public GameObject PlacementIndicator; // Optional: A visual ring to show where you are aiming

    private ARRaycastManager _raycastManager;
    private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private bool _hasPlacedFlock = false;

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
        
        // Ensure the flock is hidden at start so it doesn't fly around the camera origin
        if (FlockSystem != null)
            FlockSystem.SetActive(false);
    }

    void Update()
    {
        // 1. Raycast from the center of the screen
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        
        if (_raycastManager.Raycast(screenCenter, _hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = _hits[0].pose;

            // Update the placement indicator (visual ring)
            if (PlacementIndicator != null)
            {
                PlacementIndicator.SetActive(true);
                PlacementIndicator.transform.position = hitPose.position;
                PlacementIndicator.transform.rotation = hitPose.rotation;
            }

            // 2. Check for Touch Input to Place the Flock
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                PlaceFlock(hitPose.position);
            }
        }
        else
        {
            // Hide indicator if no plane is detected
            if (PlacementIndicator != null) PlacementIndicator.SetActive(false);
        }
    }

    private void PlaceFlock(Vector3 position)
    {
        // If this is the first time placing, turn the flock on
        if (!_hasPlacedFlock)
        {
            FlockSystem.SetActive(true);
            _hasPlacedFlock = true;
        }

        // Move the "Target" (the magnet the boids follow) to the real-world surface
        FlockCenterTarget.position = position;

        // Optional: Move the actual simulation root there too if needed, 
        // but moving the Target is usually enough for the boids to fly over.
    }
}