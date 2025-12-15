# High-Performance AR Boid Simulation - Final Report

## 1. Project Overview
This project implements a high-performance **GPU-based Boid simulation** integrated into an **Augmented Reality (AR)** environment. The system is capable of simulating and rendering thousands of autonomous agents (birds/fish) that interact with each other and the physical world in real-time. By leveraging Compute Shaders for behavioral logic and GPU Instancing for rendering, the simulation achieves high framerates on mobile devices, offering a seamless AR experience.

## 2. Attribution & Sources
This project builds upon established high-quality technical foundations:
*   **Boid Flocking Logic**: The core GPU-based Reynolds Boids algorithm (Separation, Alignment, Cohesion) is derived from [Shinao/Unity-GPU-Boids](https://github.com/Shinao/Unity-GPU-Boids).
*   **AR Framework**: The AR interaction, plane detection, and Raycasting mechanics are based on the **Unity AR Mobile Template**.

## 3. Technical Implementation - GPU Flocking

### Compute Shaders
The simulation logic is entirely offloaded to the GPU using `Boid.compute`.
*   **Algorithm**: The shader implements the classic Reynolds behavioral model:
    *   **Separation**: Steer to avoid crowding local flockmates.
    *   **Alignment**: Steer towards the average heading of local flockmates.
    *   **Cohesion**: Steer to move towards the average position (center of mass) of local flockmates.
*   **Performance Strategy**:
    *   The system uses a **Brute-Force (N^2)** neighbor search approach. While computationally expensive (O(N^2)), running this on the GPU allows for massive parallelization (e.g., 256 threads per group), enabling the simulation of thousands of agents which would otherwise bottleneck the CPU.
    *   **Affectors**: The compute shader supports dynamic external forces (Attractors/Repellers) with configurable influence shapes (Spherical or Cylindrical axes).

### Data Synchronization
Data is efficiently synchronized between the CPU and GPU using `ComputeBuffers`.
*   **`GPUBoid` Struct**: Closely packed data structure containing `position`, `direction`, `speed`, `noise_offset`, and animation frame data (`frame`, `next_frame`, `frame_interpolation`).

## 4. Visual Rendering

### Procedural Instancing
To render thousands of agents without CPU overhead, the project utilizes **GPU Instancing**.
*   **`DrawMeshInstancedIndirect`**: This command (called in `GPUFlock.cs`) instructs the GPU to draw the entire flock in a single draw call.
*   **Shader Setup**: The `Boids.shader` uses `#pragma instancing_options procedural:setup` to fetch per-instance data (position, rotation, scale) directly from the `StructuredBuffer<Boid>` in the Vertex Shader.

### High-Performance Vertex Animation
Instead of using standard `SkinnedMeshRenderer` components which are CPU-heavy:
1.  **Baking**: Animation frames are baked into a `VertexAnimationBuffer` (a huge `Vector4` array) at startup.
2.  **Playback**: The Vertex Shader looks up the correct vertex positions for the current and next frame based on the agent's `frame` time.
3.  **Interpolation**: The shader performs linear interpolation (`lerp`) between frames (`frame` and `next_frame`) using `frame_interpolation` to ensure smooth animation at any speed.

## 5. AR Integration

### AR Foundation & Environment
*   **Scale**: The simulation uses a custom scale factor (approx. `0.003f`) to ensure the flock and its behaviors fit comfortably on a tabletop AR setting.
*   **Plane Detection**: The `ARFlockManager` utilizes `ARRaycastManager` to detect physical surfaces (tables, floors).

### Interaction Logic
*   **Placement**: Users can tap to spawn or move the flock. The system performs a Raycast against detected AR planes to find the valid 3D world coordinates. A UI blocking mechanism (`IsPointerOverUI`) ensures interactions with on-screen controls do not accidentally move the flock.
*   **Visual Feedback**: `AffectorVisualizer.cs` provides cues to the user. It draws a dynamic ring (using a `LineRenderer` with `TransformZ` alignment) projected onto the physical ground directly beneath the device. This "ground footprint" visualization resolves depth perception issues inherent in AR, allowing users to clearly see the boundary of their interaction cylinder.

## 6. Interactive Features

### Camera Interaction (The "Predator" Mechanic)
*   **Cylindrical Affector**: The user's device acts as a physical dynamic affector. A **Cylindrical Axis** (`axis = 1`) logic is used, creating an infinite vertical "pillar" of influence. This ensures the user interacts with the flock regardless of their specific device height relative to the boids.
*   **Runtime UI Controls**: An `AffectorUIController` linked to screen sliders allows real-time tuning of:
    *   **Force**: Seamlessly transitioning between Attraction (pulling boids in) and Repulsion (scattering them). A fix to the Compute Shader logic ensures positive repulsion force scales linearly with the slider input.
    *   **Distance**: dynamic adjustment of the interaction radius.

