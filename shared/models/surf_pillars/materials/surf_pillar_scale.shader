
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth(); 
	ToolsVis( S_MODE_TOOLS_VIS );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
};

VS
{
	#include "common/vertex.hlsl"
	
	float3 g_vbottom_height_z < UiGroup( ",0/,0/0" ); Default3( 0,0,0 ); >;
	float3 g_vtop_height_z < UiGroup( ",0/,0/0" ); Default3( 0,0,0 ); >;
	
	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vVertexColor = v.vColor;

		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		
		float3 l_0 = g_vbottom_height_z;
		float3 l_1 = g_vtop_height_z;
		float3 l_2 = i.vVertexColor.rgb;
		float l_3 = l_2.y;
		float3 l_4 = lerp( l_0, l_1, l_3 );
		i.vPositionWs.xyz += l_4;
		i.vPositionPs.xyzw = Position3WsToPs( i.vPositionWs.xyz );
		
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	CreateInputTexture2D( unique_color, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( tiling_color, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( unique_normal, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( tiling_normal, Linear, 8, "NormalizeNormals", "_normal", ",0/,0/0", Default4( 0.50, 0.50, 1.00, 1.00 ) );
	CreateInputTexture2D( unique_rough, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( tiling_rough, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( unique_metal, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( tiling_metal, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( unique_ao, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	CreateInputTexture2D( tiling_ao, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tunique_color < Channel( RGBA, Box( unique_color ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_ttiling_color < Channel( RGBA, Box( tiling_color ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_tunique_normal < Channel( RGBA, Box( unique_normal ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_ttiling_normal < Channel( RGBA, Box( tiling_normal ), Linear ); OutputFormat( DXT5 ); SrgbRead( False ); >;
	Texture2D g_tunique_rough < Channel( RGBA, Box( unique_rough ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_ttiling_rough < Channel( RGBA, Box( tiling_rough ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_tunique_metal < Channel( RGBA, Box( unique_metal ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_ttiling_metal < Channel( RGBA, Box( tiling_metal ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_tunique_ao < Channel( RGBA, Box( unique_ao ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_ttiling_ao < Channel( RGBA, Box( tiling_ao ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
		
	float4 TexTriplanar_Color( in Texture2D tTex, in SamplerState sSampler, float3 vPosition, float3 vNormal )
	{
		float2 uvX = vPosition.zy;
		float2 uvY = vPosition.xz;
		float2 uvZ = vPosition.xy;
	
		float3 triblend = saturate(pow(vNormal, 4));
		triblend /= max(dot(triblend, half3(1,1,1)), 0.0001);
	
		half3 axisSign = vNormal < 0 ? -1 : 1;
	
		uvX.x *= axisSign.x;
		uvY.x *= axisSign.y;
		uvZ.x *= -axisSign.z;
	
		float4 colX = Tex2DS( tTex, sSampler, uvX );
		float4 colY = Tex2DS( tTex, sSampler, uvY );
		float4 colZ = Tex2DS( tTex, sSampler, uvZ );
	
		return colX * triblend.x + colY * triblend.y + colZ * triblend.z;
	}
	
	float3 TexTriplanar_Normal( in Texture2D tTex, in SamplerState sSampler, float3 vPosition, float3 vNormal )
	{
		float2 uvX = vPosition.zy;
		float2 uvY = vPosition.xz;
		float2 uvZ = vPosition.xy;
	
		float3 triblend = saturate( pow( vNormal, 4 ) );
		triblend /= max( dot( triblend, half3( 1, 1, 1 ) ), 0.0001 );
	
		half3 axisSign = vNormal < 0 ? -1 : 1;
	
		uvX.x *= axisSign.x;
		uvY.x *= axisSign.y;
		uvZ.x *= -axisSign.z;
	
		float3 tnormalX = DecodeNormal( Tex2DS( tTex, sSampler, uvX ).xyz );
		float3 tnormalY = DecodeNormal( Tex2DS( tTex, sSampler, uvY ).xyz );
		float3 tnormalZ = DecodeNormal( Tex2DS( tTex, sSampler, uvZ ).xyz );
	
		tnormalX.x *= axisSign.x;
		tnormalY.x *= axisSign.y;
		tnormalZ.x *= -axisSign.z;
	
		tnormalX = half3( tnormalX.xy + vNormal.zy, vNormal.x );
		tnormalY = half3( tnormalY.xy + vNormal.xz, vNormal.y );
		tnormalZ = half3( tnormalZ.xy + vNormal.xy, vNormal.z );
	
		return normalize(
			tnormalX.zyx * triblend.x +
			tnormalY.xzy * triblend.y +
			tnormalZ.xyz * triblend.z +
			vNormal
		);
	}
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m;
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = TransformNormal( i, float3( 0, 0, 1 ) );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = Tex2DS( g_tunique_color, g_sSampler0, i.vTextureCoords.xy );
		float4 l_1 = TexTriplanar_Color( g_ttiling_color, g_sSampler0, (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz) / 39.3701, normalize( i.vNormalWs.xyz ) );
		float3 l_2 = i.vVertexColor.rgb;
		float l_3 = l_2.x;
		float4 l_4 = lerp( l_0, l_1, l_3 );
		float4 l_5 = Tex2DS( g_tunique_normal, g_sSampler0, i.vTextureCoords.xy );
		float3 l_6 = TexTriplanar_Normal( g_ttiling_normal, g_sSampler0, (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz) / 39.3701, normalize( i.vNormalWs.xyz ) );
		float4 l_7 = lerp( l_5, float4( l_6, 0 ), l_3 );
		float4 l_8 = Tex2DS( g_tunique_rough, g_sSampler0, i.vTextureCoords.xy );
		float4 l_9 = TexTriplanar_Color( g_ttiling_rough, g_sSampler0, (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz) / 39.3701, normalize( i.vNormalWs.xyz ) );
		float4 l_10 = lerp( l_8, l_9, l_3 );
		float4 l_11 = Tex2DS( g_tunique_metal, g_sSampler0, i.vTextureCoords.xy );
		float4 l_12 = TexTriplanar_Color( g_ttiling_metal, g_sSampler0, (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz) / 39.3701, normalize( i.vNormalWs.xyz ) );
		float4 l_13 = lerp( l_11, l_12, l_3 );
		float4 l_14 = Tex2DS( g_tunique_ao, g_sSampler0, i.vTextureCoords.xy );
		float4 l_15 = TexTriplanar_Color( g_ttiling_ao, g_sSampler0, (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz) / 39.3701, normalize( i.vNormalWs.xyz ) );
		float4 l_16 = lerp( l_14, l_15, l_3 );
		
		m.Albedo = l_4.xyz;
		m.Opacity = 1;
		m.Normal = l_7.xyz;
		m.Roughness = l_10.x;
		m.Metalness = l_13.x;
		m.AmbientOcclusion = l_16.x;
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );
		
		return ShadingModelStandard::Shade( i, m );
	}
}
