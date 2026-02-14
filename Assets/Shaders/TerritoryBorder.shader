Shader "Map/TerritoryBorder"
{
    Properties
    {
        _OwnerTex ("Owner Texture", 2D) = "black" {}
        _PaletteTex ("Palette Texture", 2D) = "white" {}
        _BorderWidth ("Border Width", Float) = 0.04
        _GlowWidth ("Glow Width", Float) = 0.12
        _GlowStrength ("Glow Strength", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            // 使用 URP 的 Core.hlsl 替代 UnityCG.cginc
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //#include "UnityCG.cginc"

            sampler2D _OwnerTex;
            sampler2D _PaletteTex;
            float4 _MapSize;
            float _BorderWidth;
            float _GlowWidth;
            float _GlowStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 cell : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.cell = v.uv2;
                return o;
            }

            int SampleOwner(float2 cell)
            {
                if (cell.x < 0.0 || cell.x >= _MapSize.x || cell.y < 0.0 || cell.y >= _MapSize.y)
                {
                    return 0;
                }

                float2 uv = (cell + 0.5) * _MapSize.zw;
                float value = tex2D(_OwnerTex, uv).r;
                return (int)floor(value * 255.0 + 0.5);
            }

            float3 SamplePalette(int ownerId)
            {
                float u = (ownerId + 0.5) / 256.0;
                return tex2D(_PaletteTex, float2(u, 0.5)).rgb;
            }

            float4 frag(v2f i) : SV_Target
            {
                int owner = SampleOwner(i.cell);
                if (owner == 0)
                {
                    return 0;
                }

                int row = (int)floor(i.cell.y + 0.5);
                int parity = row & 1;

                int2 east = int2(1, 0);
                int2 west = int2(-1, 0);
                int2 ne = parity == 0 ? int2(0, -1) : int2(1, -1);
                int2 nw = parity == 0 ? int2(-1, -1) : int2(0, -1);
                int2 se = parity == 0 ? int2(0, 1) : int2(1, 1);
                int2 sw = parity == 0 ? int2(-1, 1) : int2(0, 1);

                int diffE = SampleOwner(i.cell + east) == owner ? 0 : 1;
                int diffW = SampleOwner(i.cell + west) == owner ? 0 : 1;
                int diffNE = SampleOwner(i.cell + ne) == owner ? 0 : 1;
                int diffNW = SampleOwner(i.cell + nw) == owner ? 0 : 1;
                int diffSE = SampleOwner(i.cell + se) == owner ? 0 : 1;
                int diffSW = SampleOwner(i.cell + sw) == owner ? 0 : 1;

                float2 uv = i.uv;
                float invSqrt2 = 0.70710678;

                float dW = uv.x;
                float dE = 1.0 - uv.x;
                float dNW = (uv.x + 0.75 - uv.y) * invSqrt2;
                float dNE = (1.5 - (uv.x + uv.y)) * invSqrt2;
                float dSW = (uv.x + uv.y - 0.25) * invSqrt2;
                float dSE = (uv.y - uv.x + 0.75) * invSqrt2;

                float inside = min(min(min(min(min(dW, dE), dNW), dNE), dSW), dSE);
                float aa = fwidth(inside) + 1e-5;
                float insideMask = saturate(inside / aa);
                if (insideMask <= 0.0)
                {
                    return 0;
                }

                float edgeE = diffE * smoothstep(_BorderWidth, 0.0, dE);
                float edgeW = diffW * smoothstep(_BorderWidth, 0.0, dW);
                float edgeNE = diffNE * smoothstep(_BorderWidth, 0.0, dNE);
                float edgeNW = diffNW * smoothstep(_BorderWidth, 0.0, dNW);
                float edgeSE = diffSE * smoothstep(_BorderWidth, 0.0, dSE);
                float edgeSW = diffSW * smoothstep(_BorderWidth, 0.0, dSW);

                float glowE = diffE * smoothstep(_GlowWidth, 0.0, dE);
                float glowW = diffW * smoothstep(_GlowWidth, 0.0, dW);
                float glowNE = diffNE * smoothstep(_GlowWidth, 0.0, dNE);
                float glowNW = diffNW * smoothstep(_GlowWidth, 0.0, dNW);
                float glowSE = diffSE * smoothstep(_GlowWidth, 0.0, dSE);
                float glowSW = diffSW * smoothstep(_GlowWidth, 0.0, dSW);

                float border = max(max(max(edgeE, edgeW), max(edgeNE, edgeNW)), max(edgeSE, edgeSW));
                float glow = max(max(max(glowE, glowW), max(glowNE, glowNW)), max(glowSE, glowSW));

                float3 color = SamplePalette(owner);
                float intensity = border + glow * _GlowStrength;
                float alpha = saturate(intensity) * insideMask;
                float3 rgb = color * intensity;

                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
