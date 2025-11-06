// Unlit texture shader which casts shadow on Forward/Defered

Shader "Custom/UnlitShadow"
{
	Properties {
		// 🎨 추가된 부분: 머티리얼의 색상을 설정하는 속성
		_Color ("Color Tint", Color) = (1,1,1,1) 

		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}
	
	SubShader {
		Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
		LOD 100
		
		Pass {
			Lighting Off
			
			// 🎨 수정된 부분: _Color 속성을 사용하여 텍스처에 색조를 입힙니다.
			Color [_Color] 
			SetTexture [_MainTex] { combine texture * primary } 
		}
		
		// Pass to render object as a shadow caster
		Pass 
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			
			Fog {Mode Off}
			ZWrite On ZTest LEqual Cull Off
			Offset 1, 1
	
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"
	
			struct v2f { 
				V2F_SHADOW_CASTER;
			};
	
			v2f vert( appdata_base v )
			{
				v2f o;
				TRANSFER_SHADOW_CASTER(o)
				return o;
			}
	
			float4 frag( v2f i ) : COLOR
			{
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}

	}
	
}