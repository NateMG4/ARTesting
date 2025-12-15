using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class AffectorVisualizer : MonoBehaviour
{
    public GPUFlock Flock;
    public ARRaycastManager RaycastManager;
    public Material VisualizerMaterial; // User can assign this in Inspector
    
    private LineRenderer _lineRenderer;
    private GameObject _childRing;
    private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private const int Resolution = 50; 

    void Start()
    {
        if (Flock == null) Flock = FindObjectOfType<GPUFlock>();
        if (RaycastManager == null) RaycastManager = FindObjectOfType<ARRaycastManager>();

        var childTransform = transform.Find("VisualizerRing");
        if (childTransform != null) {
            _childRing = childTransform.gameObject;
        } else {
            _childRing = new GameObject("VisualizerRing");
            _childRing.transform.parent = transform;
            _childRing.transform.localPosition = Vector3.zero;
        }

        // Rotate for Z-UP
        _childRing.transform.rotation = Quaternion.Euler(90, 0, 0);

        _lineRenderer = _childRing.GetComponent<LineRenderer>();
        if (_lineRenderer == null) _lineRenderer = _childRing.AddComponent<LineRenderer>();

        _lineRenderer.positionCount = Resolution;
        _lineRenderer.useWorldSpace = false; 
        _lineRenderer.loop = true;
        _lineRenderer.alignment = LineAlignment.TransformZ; 
        
        // Force width to 0.2 (20cm) as requested (Default is 1.0 which makes a donut)
        _lineRenderer.widthMultiplier = 0.2f; 
        
        // Material Assignment Logic
        if (VisualizerMaterial != null) {
            _lineRenderer.material = VisualizerMaterial;
        }
        else if (_lineRenderer.sharedMaterial == null) {
             // Try standard URP Unlit, fall back to pink if missing, but at least warn
             Shader s = Shader.Find("Universal Render Pipeline/Unlit");
             if (s == null) s = Shader.Find("Sprites/Default");
             
             if (s != null) {
                Material fallback = new Material(s);
                fallback.color = new Color(0, 1, 1, 0.6f); 
                _lineRenderer.material = fallback;
             }
        }
        
        // Generate the circle points ONCE in local space since radius is now effectively constant relative to the camera center
        // Wait, radius depends on AffectorDistance which might change? Better to update points in Update if dynamic.
        // But shape is constant circle. 
    }

    void Update()
    {
        if (Flock == null || RaycastManager == null || _childRing == null) return;
        
        // Ensure child rotation stays correct
        _childRing.transform.rotation = Quaternion.Euler(90, 0, 0);

        bool visualizerActive = false;
        
        // Constant Cylinder Radius
        float totalRadius = Flock.AffectorDistance * 2f; 

        if (Flock.UseCameraAffector && Flock.CameraTransform != null)
        {
            Ray downRay = new Ray(Flock.CameraTransform.position, Vector3.down);
            
            if (RaycastManager.Raycast(downRay, _hits, TrackableType.PlaneWithinPolygon))
            {
                Pose hitPose = _hits[0].pose;
                // Position the ring specifically on the ground hit point
                _childRing.transform.position = hitPose.position + new Vector3(0, 0.01f, 0);

                // Re-draw circle if radius changed, or just scale the object?
                // Scaling object is cheaper/cleaner.
                // Circle with radius 1:
                // diameter = 2.
                // We want diameter = totalRadius * 2? No, totalRadius is the radius (dist + dist).
                // So radius = totalRadius.
                
                // Let's just regenerate points to be safe and explicit
                DrawCircleLocal(totalRadius);
                visualizerActive = true;
            }
        }
        
        _lineRenderer.enabled = visualizerActive;
    }

    void DrawCircleLocal(float radius)
    {
        float angleStep = 360f / Resolution;
        _lineRenderer.positionCount = Resolution; // Ensure count
        
        for (int i = 0; i < Resolution; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius; // In Local Space with Z-Up rotation, Y is the other planar axis.
            
            // Local Space: Z is up (normal). X and Y are indeed the plane.
            _lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}
