Shader "Hidden/Transition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white"
        _MaskTexture("Mask Texture", 2D) = "white"
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma vertex vert
        #pragma fragment frag

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };
        
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        sampler2D _MaskTexture;

        float4 _MainTex_TexelSize;
        float4 _MainTex_ST;
        float _RandomNumbers[] = {0.0, 0.0, 0.0, 0.0};

        int _BlurStrength;
        float _ActivatedAt;
        float _CurrentTimestamp;
        int _RandomEffect;

        float _EffectTime = 2;
        float _FadeTime = 2;
        float _SlideTime = 2;

        float rand_1_05(in float2 uv, in float C = 43758.5453)
        {
            float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * C));
            return abs(noise.x + noise.y) * 0.5;
        }

        Varyings vert(Attributes IN)    
        {           
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            return OUT;
        }

        half4 warriorAntsEffect(Varyings IN, float t) {
            // Get some random noise
            float noiseX = -1 + 2 * rand_1_05(IN.uv, 45312.5930);
            float noiseY = -1 + 2 * rand_1_05(IN.uv);

            float2 wobbledUV = IN.uv;       
            wobbledUV.x += t * noiseX;       
            wobbledUV.y += t * noiseY;
            half4 textureValue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, wobbledUV);

            return textureValue;
        }
        
        half4 wobbleEffect(Varyings IN, float t) {
            float directions = 8.0; // Large value better quality but slower
            float quality = 3.0; // Same as directions
            float size = round(64.0 * t); // how far do we travel?
            float PI_d = 3.1415682 * 2;

            // screen is not square
            float2 radius = float2(size / _ScreenParams.x, size / _ScreenParams.y);

            half4 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

            // Radial-blur-ish (https://www.shadertoy.com/view/XsfSDs)
            int temp = 0;
            for(float d = 0.0; d < 1/PI_d; d += PI_d/directions) {
                for(float i = 1.0 /quality; i < 1.0; i += 1.0 / quality) {
                    float2 _uv = IN.uv + float2(cos(d), cos(d-3.14))*radius*i;
                    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv);
                    temp++;
                }
            }

            // average out result for display
            result /= temp; 

            return result;
        }

        // Rotate points around the center depending on how far they are from the center
        half4 swivelEffect(Varyings IN, float t) {
            float distanceFromCenter = length(IN.uv - float2(0.5,0.5));

            float2 o = float2(0.5, 0.5);
            float maxRotation = 3.14159 * 2;
            float theta = maxRotation * distanceFromCenter * t;

            float2 p = IN.uv;
            float2 new_p = float2(0,0);
            new_p.x = cos(theta) * (p.x - o.x) - sin(theta) * (p.y - o.y) + o.x;
            new_p.y = sin(theta) * (p.x - o.x) + cos(theta) * (p.y - o.y) + o.y;

            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, new_p);
        }

        // Helper for wrapping uv coords that go outside the bounds, wrapping back to 0 
        float2 wrapUV(float2 uv) {
            float2 _uv = uv;
            if(_uv.x > 1) _uv.x = _uv.x - 1;
            else if(_uv.x < 0) _uv.x = 1 + _uv.x;
            if(_uv.y > 1) _uv.y = _uv.y - 1;
            else if(_uv.y < 0) _uv.y = 1 + _uv.y;
            return _uv; 
        }

        half4 meltEffect(Varyings IN, float t) {
            // Weight UV coord downards, weigh coordinates closer to anchor points down harder
            float2 anchorPoints[] = {
                float2(_RandomNumbers[0], _RandomNumbers[1]),
                float2(_RandomNumbers[2], _RandomNumbers[3]),
                float2(_RandomNumbers[1], _RandomNumbers[2]),
            };

            float maxDistanceFromAnchor = 0.8;
            float distances[3] = {0,0,0};
            
            // Measure distnaces to anchor points
            // we don't go over maxDistnaceFromAnchor
            float l0 = length(IN.uv - anchorPoints[0]);
            float l1 = length(IN.uv - anchorPoints[1]);
            float l2 = length(IN.uv - anchorPoints[2]);
            distances[0] = min(l0, maxDistanceFromAnchor);
            distances[1] = min(l1, maxDistanceFromAnchor);
            distances[2] = min(l2, maxDistanceFromAnchor);

            // Sum total distance
            float summed = distances[0] + distances[1] + distances[2];

            // If the point is very far 
            float relative = summed / (maxDistanceFromAnchor*3);

            float weight = 1.5;
            float2 uv = IN.uv;
            uv.y += (1 - relative) * weight * t;

            uv = wrapUV(uv);

            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
        }

        // Inspiration https://www.shadertoy.com/view/ts2Xz3
        half4 mosaicEffect(Varyings IN, float t) {
            float aspect = _ScreenParams.x / _ScreenParams.y;
            float texSize = _ScreenParams.x;
            float2 offset = float2((aspect - 1.0) / 2.0, 0.0);

            // Scale the mosaic tile size by t (0 -> 1)
            float mosaicSize = floor((t+0.1) * 0.1 * texSize.x);
            float2 mosaicBlockSize = float2(mosaicSize, mosaicSize);

            // get the coords relative to the window size
            float2 texCoord = floor(IN.uv * texSize);
            // we are still in texture space
            float2 blockStart = texCoord;
            // grab the top left pixel (disregard floating point remainders)
            blockStart.x -= fmod(blockStart.x, mosaicBlockSize.x);
            blockStart.y -= fmod(blockStart.y, mosaicBlockSize.y);

            // scale back down to "UV-space" and we get the mosaic effect
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blockStart / texSize);
        }

        float hash(float2 p)  
        {
            p  = 50.0*frac( p*0.3183099 + float2(0.71,0.113));
            return -1.0+2.0*frac( p.x*p.y*(p.x+p.y) );
        }
        
        // Translated from: https://www.shadertoy.com/view/lsf3WH
        float valueNoise(float2 p )
        {
            float2 i = floor( p );
            float2 f = frac( p );
            float2 u = f*f*(3.0-2.0*f);

            return lerp( lerp( hash( i + float2(0.0,0.0) ), 
                            hash( i + float2(1.0,0.0) ), u.x),      
                        lerp( hash( i + float2(0.0,1.0) ), 
                            hash( i + float2(1.0,1.0) ), u.x), u.y);
        }

        half4 fogEffect(Varyings IN, float t) {
            float aspect = _ScreenParams.x / _ScreenParams.y;

            float2x2 m = {1.6,  1.2, -1.2,  1.6};
            float2 _uv = float2(IN.uv.x * aspect, IN.uv.y - _CurrentTimestamp*0.5) * 4.0;
            float f = 0.5 * valueNoise(_uv); 
            _uv = mul(m, _uv);
            f += 0.25 * valueNoise(_uv);
            _uv = mul(m, _uv);
            f += 0.125 * valueNoise(_uv); 
            _uv = mul(m, _uv);
            f += 0.0625 * valueNoise(_uv);
            f = 0.5 + 0.5 * f; // -1,1 to 0,1
        
            // We now have a value noise with 4 octabes "f"
            // How can we use this to get a smoke effect thing?     
            // Blend method, simply blend between the normal frame and the noise frame with value t
            // float _t = min(1 - t, 0.8);
            float _t = 1 - t;
            float w = smoothstep(0, t, IN.uv.y);
            f *= w;     
            half4 _ForegroundColor = half4(150.0 / 255.0, 19.0 / 255.0, 8.0 / 255.0, 1.0);

            return lerp(half4(_ForegroundColor.xyz*f, 1.0), SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv), half4(_t, _t, _t, 0));
        }

        half4 maskTransition(Varyings IN, float t) {
            // We have a value between 0->1 t
            // If t > value at the mask tex2D(_MaskTexture, IN.uv).x then render black otherwise render texture
            float maskValue = tex2D(_MaskTexture, IN.uv).x;

            half4 normTexture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
            // float result = step(t, maskValue);
            float result = smoothstep(0, t, maskValue); 
            return result * normTexture + (1 - result) * half4(0, 0, 0, 1);
        }

        float lum(float2 at) {
            // ( (0.3 * R) + (0.59 * G) + (0.11 * B) )
            half4 text = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, at);
            return 0.3 * text.x + 0.59 * text.y + 0.11 * text.z;
        }

        float lum_rgb(float3 rgb) {
            return 0.3 * rgb.x + 0.59 * rgb.y + 0.11 * rgb.z;
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            half4 frag(Varyings IN) : SV_TARGET
            {
                float relativeTime = (_CurrentTimestamp - _ActivatedAt) / _EffectTime;
                float clampedRelativeValue = min(1, relativeTime);

                // There seems to be a bug with fogEffect being played after another effect
                // The screen turns completely black 

                half4 textureValue = half4(0,0,0,1);
                // return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                if(_RandomEffect == 0)
                    textureValue = warriorAntsEffect(IN, clampedRelativeValue);
                else if(_RandomEffect == 1)
                    textureValue = wobbleEffect(IN, clampedRelativeValue); 
                else if(_RandomEffect == 2)
                    textureValue = swivelEffect(IN, clampedRelativeValue);
                else if(_RandomEffect == 3)
                    textureValue = meltEffect(IN, clampedRelativeValue);
                else if(_RandomEffect == 4)
                    textureValue = mosaicEffect(IN, clampedRelativeValue);
                else if(_RandomEffect == 5)
                    textureValue = fogEffect(IN, clampedRelativeValue);
                else if(_RandomEffect == 6)
                    textureValue = maskTransition(IN, clampedRelativeValue);

                // Smoothstep between maximumTime, maximumTime + fadeTime
                float elapsed = _CurrentTimestamp - _ActivatedAt;
                half4 foreground = half4(20.0 / 255.0, 19.0 / 255.0, 8.0 / 255.0, 1.0);
                float smoothAlpha = smoothstep(_EffectTime-_EffectTime*0.7, _EffectTime+_FadeTime, elapsed);

                // An effect where we reveal the screen coming from the left, as a slide in showing the draft screen ready to be used
                // Smoothstep between maximumTime + fadeTime, maximumTime + fadeTime + slideTime
                float smoothSlide = smoothstep(_EffectTime + _FadeTime, _EffectTime + _FadeTime + _SlideTime, elapsed);
                float slideAlpha = step(1-smoothSlide, IN.uv.x*IN.uv.x);
                
                return (1 - smoothAlpha) * textureValue + slideAlpha * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
            }
            ENDHLSL
        }

        // Second pass for the soble edge detection
        Pass
        {
            HLSLPROGRAM
            half4 frag(Varyings IN) : SV_TARGET
            {
                // Edge detection inspired from article: https://www.alanzucconi.com/2022/04/19/edge-detection/

                // _MainTex_TexelSize
                // Sample the four neighbouring points around our pixel
                float3 center = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                float3 up = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, _MainTex_TexelSize.y) ).rgb;
                float3 down = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, _MainTex_TexelSize.y) ).rgb;
                float3 left = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(_MainTex_TexelSize.x, 0) ).rgb;
                float3 right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(_MainTex_TexelSize.x, 0) ).rgb;

                // Luminosity at each pixel
                float center_lum = lum_rgb(center);
                float up_lum = lum_rgb(up);
                float down_lum = lum_rgb(down);
                float left_lum = lum_rgb(left);
                float right_lum = lum_rgb(right);   

                // Laplacian filter
                float L_lum = saturate(up_lum + down_lum + left_lum + right_lum - 4 * center_lum);

                float max_threshold = 0.01;
                L_lum = step(max_threshold, L_lum);
                half3 foreground = half3(20.0 / 255.0, 19.0 / 255.0, 8.0 / 255.0);
                half3 beige = half3(118.0/255.0, 119.0/255.0, 80.0/255.0);
                half3 fire = half3(1.0, 119.0/255.0, 0);
                half3 red = half3(1.0, 0.3, 0.1);
                
                float elapsed = _CurrentTimestamp - _ActivatedAt;
                float relativeTime = (_CurrentTimestamp - _ActivatedAt) / _EffectTime;
                float clampedRelativeValue = min(1, relativeTime);

                // An effect where we reveal the screen coming from the left, as a slide in showing the draft screen ready to be used
                // Smoothstep between maximumTime + fadeTime, maximumTime + fadeTime + slideTime
                float smoothSlide = smoothstep(_EffectTime + _FadeTime, _EffectTime + _FadeTime + _SlideTime, elapsed);
                float slideAlpha = step(1-smoothSlide, IN.uv.x*IN.uv.x);

                half4 edge =  half4( (L_lum * beige + (1-L_lum)*foreground), 1.0);
                edge = lerp(half4(center,1), edge, clampedRelativeValue);
                return half4(center, 1);
                return (1-slideAlpha) * edge + slideAlpha * half4(center, 1);
            }
            ENDHLSL
        }
    }
}