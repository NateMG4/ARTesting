using UnityEngine;
using UnityEngine.UI;

public class AffectorUIController : MonoBehaviour
{
    [Header("References")]
    public GPUFlock Flock;
    public Slider ForceSlider;
    public Slider DistanceSlider;
    public Slider HeightSlider;

    [Header("Settings")]
    public float MinForce = -1f;
    public float MaxForce = 1f;
    public float MinDistance = 0.1f;
    public float MaxDistance = 1.0f; // 1 meter radius seems reasonably large for AR table
    public float MinHeight = -1.0f;
    public float MaxHeight = 2.0f;

    void Start()
    {
        if (Flock == null) Flock = FindObjectOfType<GPUFlock>();

        // Setup Force Slider
        if (ForceSlider != null)
        {
            ForceSlider.minValue = MinForce;
            ForceSlider.maxValue = MaxForce;
            if (Flock != null) ForceSlider.value = Flock.AffectorForce;
            
            ForceSlider.onValueChanged.AddListener(OnForceChanged);
        }

        // Setup Distance Slider
        if (DistanceSlider != null)
        {
            DistanceSlider.minValue = MinDistance;
            DistanceSlider.maxValue = MaxDistance;
            if (Flock != null) DistanceSlider.value = Flock.AffectorDistance;

            DistanceSlider.onValueChanged.AddListener(OnDistanceChanged);
        }

        // Setup Height Slider
        if (HeightSlider != null)
        {
            HeightSlider.minValue = MinHeight;
            HeightSlider.maxValue = MaxHeight;
            if (Flock != null) HeightSlider.value = Flock.transform.position.y;
            
            HeightSlider.onValueChanged.AddListener(OnHeightChanged);
        }
    }

    void Update()
    {
        // Continuous enforcement of height if slider exists
        // This ensures that if the user taps to place (changing Y), it snaps back to the slider value.
        if (Flock != null && HeightSlider != null)
        {
            float targetY = HeightSlider.value;
            if (Mathf.Abs(Flock.transform.position.y - targetY) > 0.001f)
            {
                Vector3 pos = Flock.transform.position;
                pos.y = targetY;
                Flock.transform.position = pos;
            }
        }
    }

    void OnForceChanged(float val)
    {
        if (Flock != null) Flock.AffectorForce = val;
    }

    void OnDistanceChanged(float val)
    {
        if (Flock != null) Flock.AffectorDistance = val;
    }

    void OnHeightChanged(float val)
    {
        if (Flock != null)
        {
            Vector3 pos = Flock.transform.position;
            pos.y = val;
            Flock.transform.position = pos;
        }
    }
}
