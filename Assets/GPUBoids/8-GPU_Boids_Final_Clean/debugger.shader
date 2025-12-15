Shader "Custom/DebugBoids"
{
    Properties
    {
        _Color ("Color", Color) = (0,1,0,1) // Default Green
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DebugPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint id : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // --- MINIMAL BOID STRUCT ---
            struct Boid
            {
                float3 position;
                float3 direction;
                float noise_offset;
                float speed;
                float frame;
                float next_frame;
                float frame_interpolation;
                float size;
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<Boid> boidBuffer;
                // We ignore vertex animation for this debug test
            #endif

            float4x4 _LookAtMatrix;
            float3   _BoidPosition;
            float    _BoidSize;

            float4x4 look_at_matrix_debug(float3 at, float3 eye, float3 up) {
                float3 zaxis = normalize(at - eye);
                float3 xaxis = normalize(cross(up, zaxis));
                float3 yaxis = cross(zaxis, xaxis);
                return float4x4(
                    xaxis.x, yaxis.x, zaxis.x, 0,
                    xaxis.y, yaxis.y, zaxis.y, 0,
                    xaxis.z, yaxis.z, zaxis.z, 0,
                    0, 0, 0, 1
                );
            }

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    Boid b = boidBuffer[unity_InstanceID];
                    _BoidPosition = b.position;
                    _BoidSize = b.size;
                    // Simple direction logic
                    float3 target = _BoidPosition + (normalize(b.direction) * -1.0);
                    _LookAtMatrix = look_at_matrix_debug(_BoidPosition, target, float3(0,1,0));
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 pos = input.positionOS.xyz;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    pos *= _BoidSize;
                    pos = mul(_LookAtMatrix, float4(pos, 1.0)).xyz;
                    pos += _BoidPosition;
                #endif

                output.positionCS = TransformWorldToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(0, 1, 0, 1); // Return GREEN
            }
            ENDHLSL
        }
    }
}