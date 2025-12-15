Shader "Custom/BoidsURP"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        // ------------------------------------------------------------------
        //  Forward Pass (Lighting)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            
            // --- MISSING LINES FIXED HERE ---
            #pragma vertex vert
            #pragma fragment frag
            // --------------------------------

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // Custom Keywords
            #pragma multi_compile __ FRAME_INTERPOLATION
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                uint   id           : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float _Glossiness;
                float _Metallic;
            CBUFFER_END

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);

            // --- BOID DATA ---
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
                StructuredBuffer<float4> vertexAnimation;
            #endif

            // Global variables (populated in setup)
            float4x4 _LookAtMatrix;
            float3   _BoidPosition;
            float    _BoidSize;
            int      _CurrentFrame;
            int      _NextFrame;
            float    _FrameInterpolation;
            
            // Global Uniform sent from C#
            int NbFrames; 

            float4x4 look_at_matrix(float3 at, float3 eye, float3 up) {
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
                    _BoidSize     = b.size;
                    
                    float3 target = _BoidPosition + (b.direction * -1.0);
                    _LookAtMatrix = look_at_matrix(_BoidPosition, target, float3(0.0, 1.0, 0.0));

                    _CurrentFrame = (int)b.frame;
                    #ifdef FRAME_INTERPOLATION
                        _NextFrame = (int)b.next_frame;
                        _FrameInterpolation = b.frame_interpolation;
                    #endif
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 pos = input.positionOS.xyz;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    int frameOffset = (int)input.id * NbFrames;

                    #ifdef FRAME_INTERPOLATION
                        float4 v1 = vertexAnimation[frameOffset + _CurrentFrame];
                        float4 v2 = vertexAnimation[frameOffset + _NextFrame];
                         pos = lerp(v1.xyz, v2.xyz, _FrameInterpolation);
                    #else
                        pos = vertexAnimation[frameOffset + _CurrentFrame].xyz;
                    #endif

                    pos *= _BoidSize;
                    pos = mul(_LookAtMatrix, float4(pos, 1.0)).xyz;
                    pos += _BoidPosition;
                #endif

                // Calculations
                output.positionWS = pos;
                output.positionCS = TransformWorldToHClip(pos);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                   output.normalWS = mul((float3x3)_LookAtMatrix, input.normalOS);
                #else
                   output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                #endif

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS   = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha  = albedo.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Glossiness;
                
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Shadow Caster Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            
            // --- MISSING LINES FIXED HERE TOO ---
            #pragma vertex vert
            #pragma fragment frag
            // ------------------------------------

            #pragma multi_compile_shadowcaster
            #pragma multi_compile __ FRAME_INTERPOLATION
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint   id           : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Copied Boid Struct & Vars for Shadow Pass
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
                StructuredBuffer<float4> vertexAnimation;
            #endif

            float4x4 _LookAtMatrix;
            float3   _BoidPosition;
            float    _BoidSize;
            int      _CurrentFrame;
            int      _NextFrame;
            float    _FrameInterpolation;
            int      NbFrames;

            float4x4 look_at_matrix(float3 at, float3 eye, float3 up) {
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
                    _BoidSize     = b.size;
                    float3 target = _BoidPosition + (b.direction * -1.0);
                    _LookAtMatrix = look_at_matrix(_BoidPosition, target, float3(0.0, 1.0, 0.0));
                    _CurrentFrame = (int)b.frame;
                    #ifdef FRAME_INTERPOLATION
                        _NextFrame = (int)b.next_frame;
                        _FrameInterpolation = b.frame_interpolation;
                    #endif
                #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 pos = input.positionOS.xyz;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    int frameOffset = (int)input.id * NbFrames;
                    #ifdef FRAME_INTERPOLATION
                        float4 v1 = vertexAnimation[frameOffset + _CurrentFrame];
                        float4 v2 = vertexAnimation[frameOffset + _NextFrame];
                         pos = lerp(v1.xyz, v2.xyz, _FrameInterpolation);
                    #else
                        pos = vertexAnimation[frameOffset + _CurrentFrame].xyz;
                    #endif

                    pos *= _BoidSize;
                    pos = mul(_LookAtMatrix, float4(pos, 1.0)).xyz;
                    pos += _BoidPosition;
                #endif

                output.positionCS = TransformWorldToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}