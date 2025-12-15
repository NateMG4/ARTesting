# AI Coding Guidelines for ARTesting Unity Project

## Architecture Overview
This Unity project implements GPU-accelerated boid simulation for AR environments using the Universal Render Pipeline (URP). Core components:
- **GPUFlock.cs**: Manages boid lifecycle, compute shader dispatch, and rendering via `Graphics.DrawMeshInstancedIndirect`
- **Boid.compute**: Compute shader handling flocking behaviors (separation, alignment, cohesion) and affector influences
- **Boids.shader**: URP surface shader with procedural instancing for efficient rendering of thousands of animated boids

## Key Patterns
- **Data Structures**: `GPUBoid` struct in C# mirrors `Boid` struct in HLSL; ensure field alignment matches `Marshal.SizeOf`
- **Compute Buffers**: Use `ComputeBuffer` for GPU data transfer; always release in `OnDestroy` to prevent memory leaks
- **Procedural Instancing**: Set `boidBuffer` and `vertexAnimation` buffers on materials; use `unity_InstanceID` in vertex shaders
- **Vertex Animation**: Bake skinned mesh animations to `Vector4[]` arrays stored in compute buffers; index as `vertexAnimation[vertexId * NbFrames + frame]`
- **Frame Interpolation**: Enable `FRAME_INTERPOLATION` keyword for smooth animation blending between frames
- **Affectors**: Influence boid paths with `GPUAffector` structs; support axis-constrained forces for 2D drawing effects

## Development Workflow
- **Setup**: Attach `GPUFlock` component to GameObject; assign compute shader, mesh, material, and animation clip
- **Animation Baking**: Use `SkinnedMeshRenderer.BakeMesh()` to capture vertex positions across animation frames
- **Shader Globals**: Set `NbFrames` via `Shader.SetGlobalInt("_NbFrames_Global")` before rendering
- **Indirect Drawing**: Configure `_drawArgsBuffer` with mesh index count and instance count for batched rendering
- **Performance**: Dispatch compute shaders in `Update()` with thread groups sized to `BoidsCount / 256 + 1`

## Conventions
- **Thread Groups**: Use 256 threads per group in compute shaders (`#define GROUP_SIZE 256`)
- **Bounds**: Use infinite bounds `new Bounds(Vector3.zero, Vector3.one * 9999)` for instanced rendering
- **Noise**: Implement Perlin-like noise in compute shaders for natural boid variation
- **Affector Drawing**: Parse text assets to `Vector3[]` points for path-based boid influences

## Dependencies
- Requires URP 14.0+ for shader compatibility
- AR Foundation packages for XR integration
- Compute shader support (DX11+ or Metal/Vulkan)