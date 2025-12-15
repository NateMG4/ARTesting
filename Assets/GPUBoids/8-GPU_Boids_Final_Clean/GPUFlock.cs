using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;

public class GPUFlock : MonoBehaviour {
    public struct GPUBoid
    {
        public Vector3 position;
        public Vector3 direction;
        public float noise_offset;
        public float speed;
        public float frame;
        public float next_frame;
        public float frame_interpolation;
        public float size;
    }

    public struct GPUAffector {
        public Vector3 position;
        public float force;
        public float distance;
        public int axis;
        public Vector2 padding;
    }

    public ComputeShader _ComputeFlock;
    public GameObject TargetBoidToGPUSkin;
    public Transform Target;
    public Mesh BoidMesh;
    public Material BoidMaterial;

    private SkinnedMeshRenderer BoidSMR;
    private Animator _Animator;
    public AnimationClip _AnimationClip;
    private int NbFramesInAnimation;

    public bool UseAffectors;
    [Tooltip("If true, the Main Camera acts as a dynamic affector (Repel/Attract)")]
    public bool UseCameraAffector = false;
    public Transform CameraTransform;

    public TextAsset DrawingAffectors;
    public bool UseMeshAffectors = false;
    public Mesh MeshAffectors;    
    public float ScaleDrawingAffectors = 0.003f; // AR Scale
    public bool ReverseYAxisDrawingAffectors = true;
    public Vector3 DrawingAffectorsOffset;
    public bool DrawDrawingAffectors = true;
    private int NbAffectors = 0;

    public int BoidsCount;
    public int StepBoidCheckNeighbours = 1;
    public float SpawnRadius = 0.5f; // AR Scale
    public float RotationSpeed = 10f; // Smaller objects turn faster
    public float BoidSpeed = 0.6f; // AR Scale
    public float NeighbourDistance = 0.2f; // AR Scale
    public float BoidSpeedVariation = 0.9f;
    public float BoidFrameSpeed = 10f;
    public bool FrameInterpolation = true;
    public float AffectorForce = 0.2f; // AR Scale
    public float AffectorDistance = 0.2f; // AR Scale
    public float MaxAffectorFullAxisSize = 2f; // AR Scale
    private GPUBoid[] boidsData;
    private GPUAffector[] Affectors = new GPUAffector[1];

    private int kernelHandle;
    private ComputeBuffer BoidBuffer;
    private ComputeBuffer AffectorBuffer;
    private ComputeBuffer VertexAnimationBuffer;
    private ComputeBuffer _drawArgsBuffer;

    private Bounds InfiniteBounds = new Bounds(Vector3.zero, Vector3.one * 9999);

    private const int THREAD_GROUP_SIZE = 256;

    void Start()
    {
        Init();
    }

    private void Init()
    {
        if (BoidBuffer != null) return; // Already initialized

        // Safety check for material
        if (BoidMaterial == null) {
            Debug.LogError("BoidMaterial is missing!");
            return;
        }
        BoidMaterial = new Material(BoidMaterial);
        
        _drawArgsBuffer = new ComputeBuffer(
            1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
        );

        _drawArgsBuffer.SetData(new uint[5] {
            BoidMesh.GetIndexCount(0), (uint) BoidsCount, 0, 0, 0
        });

        this.boidsData = new GPUBoid[this.BoidsCount];
        this.kernelHandle = _ComputeFlock.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
            this.boidsData[i] = this.CreateBoidData();

        BoidBuffer = new ComputeBuffer(BoidsCount, Marshal.SizeOf(typeof(GPUBoid)));
        BoidBuffer.SetData(this.boidsData);

        GenerateSkinnedAnimationForGPUBuffer();

        // --- FIXED AFFECTOR LOGIC ---
        if (UseCameraAffector)
        {
            if (CameraTransform == null && Camera.main != null)
                CameraTransform = Camera.main.transform;

            NbAffectors = 1;
            Affectors = new GPUAffector[1];
            
            // 1. Initialize the Buffer FIRST
            if (AffectorBuffer != null) AffectorBuffer.Release();
            AffectorBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(GPUAffector)));

            // 2. Set Initial Data
            Vector3 camPos = CameraTransform != null ? CameraTransform.position : Vector3.zero;
            Vector3 relativePos = (Target != null ? Target.position : transform.position) - camPos;

            Affectors[0] = new GPUAffector { 
                position = relativePos,
                force = AffectorForce,
                distance = AffectorDistance,
                axis = 1 // 1 means Y-Axis Cylinder
            };
            AffectorBuffer.SetData(Affectors);
        }
        else if (UseAffectors) {
            if (UseMeshAffectors) {
                var bounds = MeshAffectors.bounds;
                var scaledVertices = MeshAffectors.vertices.Select(v => (v) * (ReverseYAxisDrawingAffectors ? -1 : 1)  * ScaleDrawingAffectors + DrawingAffectorsOffset).ToArray();
                GenerateDrawingAffectors(scaledVertices, 0, 0, 3);
            }
            else {
                var dataToPaths = new PointsFromData();
                dataToPaths.GeneratePointsFrom(DrawingAffectors, DrawingAffectorsOffset, new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, ScaleDrawingAffectors);
                GenerateDrawingAffectors(dataToPaths.Points.ToArray());
            }
        }
        else
        {
            // Default buffer if no affectors used
            if (AffectorBuffer != null) AffectorBuffer.Release();
            AffectorBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(GPUAffector)));
        }

        SetComputeData();
        SetMaterialData();

        if (DrawILoveUnity && !UseCameraAffector)
            StartCoroutine(DrawILoveUnityForever());
    }

    public bool DrawILoveUnity = false;
    public TextAsset EyeDrawing;
    public TextAsset HeartDrawing;
    public TextAsset UnityDrawing;
    IEnumerator DrawILoveUnityForever() {
        var dataToPaths = new PointsFromData();
        // AR Scales: Reduced by ~10x to fit on a table (Images are likely ~100-500px, so 0.003 * 100 = 0.3m = 30cm)
        // Offsets: Reduced to be close to the flock center (0.2m up)
        
        // Eye
        dataToPaths.GeneratePointsFrom(EyeDrawing, new Vector3(0, 0.1f, -0.1f), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.003f);
        var eyePoints = dataToPaths.Points.ToArray();
        
        // Heart
        dataToPaths.GeneratePointsFrom(HeartDrawing, new Vector3(0, 0.2f, -0.1f), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.005f);
        var heartPoints = dataToPaths.Points.ToArray();
        
        // Unity Logo
        dataToPaths.GeneratePointsFrom(UnityDrawing, new Vector3(0, 0, 0), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.01f);
        var unityPoints = dataToPaths.Points.ToArray();
        
        yield return new WaitForSeconds(3f);
        while (true) {
            GenerateDrawingAffectors(eyePoints, 0, 0, 0);
            yield return new WaitForSeconds(3f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(0.5f);
            GenerateDrawingAffectors(heartPoints, 0, 0, 0);
            yield return new WaitForSeconds(3f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(0.5f);
            GenerateDrawingAffectors(unityPoints, 2, 0, 0);
            yield return new WaitForSeconds(4f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(2f);
        }
    }

    GPUBoid CreateBoidData()
    {
        GPUBoid boidData = new GPUBoid();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.direction = rot.eulerAngles;
        boidData.noise_offset = Random.value * 1000.0f;
        boidData.size = Random.Range(0.05f, 0.15f); // AR Size (5cm to 15cm)
        return boidData;
    }

    private void GenerateDrawingAffectors(Vector3[] points, float force = 0, float distance = 0, int axis = 0) {
        if (AffectorBuffer != null)
            AffectorBuffer.Release();

        NbAffectors = points.Length;
        System.Array.Resize(ref Affectors, NbAffectors);

        Affectors = points.Select(p => {
            var affector = new GPUAffector();
            affector.position = p;
            affector.force = force;
            affector.distance = distance;
            affector.axis = axis;
            return affector;
        }).ToArray();

        if (DrawDrawingAffectors) {
            foreach(var point in points) {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = new Vector3(1,1,1);
                go.transform.position = point;
            }
        }

        AffectorBuffer = new ComputeBuffer(NbAffectors, Marshal.SizeOf(typeof(GPUAffector)));
        AffectorBuffer.SetData(Affectors);
    }

    void SetComputeData() {
        // Update Camera Affector Position
        if (UseCameraAffector && AffectorBuffer != null)
        {
             if (CameraTransform == null && Camera.main != null)
                CameraTransform = Camera.main.transform;

            if (CameraTransform != null)
            {
                // Shader Logic: FlockPos - AffectorPos = Destination
                // We want Destination = CameraPos
                // So AffectorPos = FlockPos - CameraPos
                Affectors[0].position = Target.position - CameraTransform.position;
                
                Affectors[0].force = AffectorForce;       // Synchronize with inspector if changed at runtime
                Affectors[0].distance = AffectorDistance;
                Affectors[0].axis = 1; // 1 means Y-Axis Cylinder (Vertical Cylinder)
                AffectorBuffer.SetData(Affectors);
            }
        }

        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        _ComputeFlock.SetFloat("BoidFrameSpeed", BoidFrameSpeed);
        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
        _ComputeFlock.SetInt("NbFrames", NbFramesInAnimation);
        _ComputeFlock.SetInt("NbAffectors", NbAffectors);
        _ComputeFlock.SetFloat("AffectorForce", AffectorForce);
        _ComputeFlock.SetFloat("AffectorDistance", AffectorDistance);
        _ComputeFlock.SetFloat("MaxAffectorFullAxisSize", MaxAffectorFullAxisSize);
        _ComputeFlock.SetInt("StepBoidCheckNeighbours", StepBoidCheckNeighbours);
        _ComputeFlock.SetBuffer(this.kernelHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.SetBuffer(this.kernelHandle, "affectorBuffer", AffectorBuffer);
    }

    void SetMaterialData() {
        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);

        if (FrameInterpolation && !BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.EnableKeyword("FRAME_INTERPOLATION");
        if (!FrameInterpolation && BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.DisableKeyword("FRAME_INTERPOLATION");

        BoidMaterial.SetInt("NbFrames", NbFramesInAnimation);
    }


    // Execution order should be the lowest possible
    void Update() {
        SetComputeData();
        SetMaterialData();

        _ComputeFlock.Dispatch(this.kernelHandle, this.BoidsCount / THREAD_GROUP_SIZE + 1, 1, 1);
    }

    // Execution order should be the highest possible
    void LateUpdate() {
        // Update bounds to always center on the flock so they aren't culled
        InfiniteBounds.center = transform.position;
        Graphics.DrawMeshInstancedIndirect(BoidMesh, 0, BoidMaterial, InfiniteBounds, _drawArgsBuffer, 0);
    }

    void OnDestroy()
    {
        if (BoidBuffer != null) BoidBuffer.Release();
        if (AffectorBuffer != null) AffectorBuffer.Release();
        if (_drawArgsBuffer != null) _drawArgsBuffer.Release();
        if (VertexAnimationBuffer != null) VertexAnimationBuffer.Release();
    }

    private void GenerateSkinnedAnimationForGPUBuffer()
    {
        if (_AnimationClip == null) {
            CreateOneFrameAnimationData();
            return;
        }

        BoidSMR = TargetBoidToGPUSkin.GetComponentInChildren<SkinnedMeshRenderer>();
        _Animator = TargetBoidToGPUSkin.GetComponentInChildren<Animator>();
        int iLayer = 0;
        AnimatorStateInfo aniStateInfo = _Animator.GetCurrentAnimatorStateInfo(iLayer);

        Mesh bakedMesh = new Mesh();
        float sampleTime = 0;
        float perFrameTime = 0;

        NbFramesInAnimation = Mathf.ClosestPowerOfTwo((int)(_AnimationClip.frameRate * _AnimationClip.length));
        perFrameTime = _AnimationClip.length / NbFramesInAnimation;

        var vertexCount = BoidSMR.sharedMesh.vertexCount;
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * NbFramesInAnimation, Marshal.SizeOf(typeof(Vector4)));
        Vector4[] vertexAnimationData = new Vector4[vertexCount * NbFramesInAnimation];
        for (int i = 0; i < NbFramesInAnimation; i++)
        {
            _Animator.Play(aniStateInfo.shortNameHash, iLayer, sampleTime);
            _Animator.Update(0f);

            BoidSMR.BakeMesh(bakedMesh);

            for(int j = 0; j < vertexCount; j++)
            {
                Vector3 vertex = bakedMesh.vertices[j];
                vertexAnimationData[(j * NbFramesInAnimation) +  i] = vertex;
            }

            sampleTime += perFrameTime;
        }

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);

        TargetBoidToGPUSkin.SetActive(false);
    }

    private void CreateOneFrameAnimationData() {
        var vertexCount = BoidMesh.vertexCount;
        NbFramesInAnimation = 1;
        Vector4[] vertexAnimationData = new Vector4[vertexCount * NbFramesInAnimation];
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * NbFramesInAnimation, Marshal.SizeOf(typeof(Vector4)));
        for(int j = 0; j < vertexCount; j++)
            vertexAnimationData[(j * NbFramesInAnimation)] = BoidMesh.vertices[j];

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);
        TargetBoidToGPUSkin.SetActive(false);
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        // Ensure initialized if called before Start
        Init();

        transform.position = position;
        transform.rotation = rotation;
        
        if (Target != null)
            Target.position = position; // Move the target to the new center as well
        
        Debug.Log($"[GPUFlock] Respawning at {position}. Resetting {BoidsCount} boids.");
        ResetBoids();
    }

    public void ResetBoids()
    {
        // Re-initialize boids at the new location
        for (int i = 0; i < this.BoidsCount; i++)
            this.boidsData[i] = this.CreateBoidData();

        if (BoidBuffer != null)
            BoidBuffer.SetData(this.boidsData);
    }
    void OnDrawGizmos()
    {
        if (UseCameraAffector && Affectors != null && Affectors.Length > 0 && Target != null)
        {
            // Reverse the math to see world position: 
            // Shader sees: FlockPos - AffectorPos
            // So: WorldPos = FlockPos - AffectorPos
            Vector3 debugPos = Target.position - Affectors[0].position;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(debugPos, Affectors[0].distance);
            Gizmos.DrawLine(Target.position, debugPos);
        }
    }
}