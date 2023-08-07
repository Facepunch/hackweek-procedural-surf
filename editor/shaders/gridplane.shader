//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	Description = "Shader for drawing a grid that fades out away from a point";
}

//=========================================================================================================================
// Optional
//=========================================================================================================================
FEATURES
{
    #include "common/features.hlsl"
}

//=========================================================================================================================
COMMON
{
	#include "common/shared.hlsl"
    
    float g_flGridSize < UiType( VectorText ); Default( 8.0 ); UiGroup( "Grid,10/10" ); Attribute( "GridSize" ); >;

}

//=========================================================================================================================

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

//=========================================================================================================================

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

//=========================================================================================================================

VS
{
	#include "common/vertex.hlsl"
    
	//
	// Main
	//
	PixelInput MainVs( INSTANCED_SHADER_PARAMS( VertexInput i ) )
	{
		PixelInput o = ProcessVertex( i );

        o.vTextureCoords.xy /= g_flGridSize;

		// Add your vertex manipulation functions here
		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
    CreateInputTexture2D( Texture, Srgb, 8, "", "", "Color", Default3( 1.0, 1.0, 1.0 ) );
	CreateTexture2DInRegister( g_tColor, 0 ) < Channel( RGBA, None( Texture ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;
	TextureAttribute( RepresentativeTexture, g_tColor );
	
	float3 g_vFadeOrigin < UiType( VectorText ); Default3( 0.0, 0.0, 0.0 ); UiGroup( "Fade,10/10" ); Attribute( "FadeOrigin" ); >;
	float g_flFadeRadius < UiType( VectorText ); Default( 128.0 ); UiGroup( "Fade,10/20" ); Attribute( "FadeRadius" ); >;

    RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, ONE );

    #define DEPTH_STATE_ALREADY_SET
    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, true );
    RenderState( DepthFunc, LESS_EQUAL );
    RenderState( DepthBias, -250 );
    
	// Always write rgba
	RenderState( ColorWriteEnable0, RGBA );
	RenderState( FillMode, SOLID );

    RenderState( CullMode, NONE );
    
    BoolAttribute( translucent, true );

	struct PS_OUTPUT
	{
        float4 vColor : SV_Target0;
	};

	//
	// Main
	//
	PS_OUTPUT MainPs( PixelInput i )
	{
        PS_OUTPUT o;

        o.vColor.rgb = float3( 1.0, 1.0, 1.0 );
        o.vColor.a = sqrt( Tex2D( g_tColor, i.vTextureCoords.xy ).r );

        float3 vPositionWs = i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz;

        float dist = clamp( 1.0 - length( vPositionWs.xyz - g_vFadeOrigin.xyz ) / g_flFadeRadius, 0.0, 1.0 );

        o.vColor.a *= dist;
        
		return o;
	}
}
