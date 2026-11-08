// Full-screen effect: film grain + light vignette always; as THE HOST closes in the edges darken then turn red,
// pulse with the heartbeat, blur toward the edges, fringe and lose colour. _Wake adds a heavy all-over blur for waking up.
Shader "Hidden/VaultDanger"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize;
            float _Danger, _Blur, _Pulse, _Flash, _Grain, _Vig, _Wake;

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float aspect = _MainTex_TexelSize.z / max(1.0, _MainTex_TexelSize.w);
                float r = length(float2(d.x * aspect, d.y)) / length(float2(0.5 * aspect, 0.5)); // 0 centre .. 1 corner

                // radial blur toward the centre, strongest at the edges
                float amt = _Blur * r * r * 0.07;
                float3 col = 0;
                [unroll] for (int k = 0; k < 6; k++) col += tex2D(_MainTex, 0.5 + d * (1.0 - amt * (k / 5.0))).rgb;
                col /= 6.0;

                // chromatic fringe
                float ca = _Blur * r * 0.006;
                col.r = lerp(col.r, tex2D(_MainTex, 0.5 + d * (1.0 + ca)).r, 0.7);
                col.b = lerp(col.b, tex2D(_MainTex, 0.5 + d * (1.0 - ca)).b, 0.7);

                // waking up: two rings of taps around each pixel, wide while groggy, gone once awake
                if (_Wake > 0.001)
                {
                    float2 rad = float2(1.0 / aspect, 1.0) * _Wake * 0.035;
                    float3 acc = col;
                    [unroll] for (int j = 0; j < 12; j++)
                    {
                        float a = j * 0.5236 + (j & 1) * 0.26;
                        float ring = (j & 1) ? 1.0 : 0.5;
                        acc += tex2D(_MainTex, i.uv + float2(cos(a), sin(a)) * rad * ring).rgb;
                    }
                    col = lerp(col, acc / 13.0, saturate(_Wake * 1.5)) * (1.0 - _Wake * 0.35);
                }

                // colour drains under stress
                float l = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, l.xxx, saturate(_Danger * 0.45));

                // vignette: black at rest, red as it gets close, pulsing with the heartbeat
                float vig = smoothstep(0.45 - _Danger * 0.28, 1.05, r) * (_Vig + _Danger * 0.8 + _Pulse * 0.25 * _Danger);
                float3 edge = lerp(float3(0, 0, 0), float3(0.32, 0.0, 0.0), saturate(_Danger * 1.3 + _Flash));
                col = lerp(col, edge, saturate(vig));
                col += float3(0.45, 0.0, 0.0) * _Flash * smoothstep(0.35, 1.0, r);

                // film grain
                float g = hash(i.uv * _ScreenParams.xy + frac(_Time.y * 37.0) * 113.0) - 0.5;
                col += g * _Grain;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
