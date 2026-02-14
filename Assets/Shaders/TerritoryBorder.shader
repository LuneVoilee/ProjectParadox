Shader "Map/TerritoryBorder"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
        };

        Varyings BorderVert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.color = input.color;
            return output;
        }

        half4 BorderFrag(Varyings input) : SV_Target
        {
            return input.color * _BaseColor;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2DPass"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BorderVert
            #pragma fragment BorderFrag
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlitPass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BorderVert
            #pragma fragment BorderFrag
            ENDHLSL
        }

        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode" = "Picking" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma editor_sync_compilation
            #pragma vertex SceneVert
            #pragma fragment ScenePickingFrag

            #define SCENEPICKINGPASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SelectionID;

            struct SceneAttributes
            {
                float4 positionOS : POSITION;
            };

            struct SceneVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            SceneVaryings SceneVert(SceneAttributes input)
            {
                SceneVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 ScenePickingFrag(SceneVaryings input) : SV_Target
            {
                return _SelectionID;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma editor_sync_compilation
            #pragma vertex SceneVert
            #pragma fragment SceneSelectionFrag

            #define SCENESELECTIONPASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            int _ObjectId;
            int _PassValue;

            struct SceneAttributes
            {
                float4 positionOS : POSITION;
            };

            struct SceneVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            SceneVaryings SceneVert(SceneAttributes input)
            {
                SceneVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 SceneSelectionFrag(SceneVaryings input) : SV_Target
            {
                return half4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}
