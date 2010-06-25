//**************************************************************//
// Simbiosis FX ver 0.1 14/3/06
//**************************************************************//

//--------------------------------------------------------------//
// Textured Phong
//--------------------------------------------------------------//

float3 sunPosition;		// position of sun in sky
float4 sunAmbient;		// ambient light colour/intensity (i.e. sun reflected from terrain onto objects)
float4 sunDiffuse;		// sunlight colour/intensity
float4 sunSpecular;		// sunlight specular colour/intensity

float3 eyePosition;		// xyz of camera

float4x4 world;											// World matrix of object origin
float4x4 worldView : worldView;							// World * view matrix
float4x4 worldViewProjection : worldViewProjection;		// World * view * proj matrix

struct VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
   
};

struct VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 ViewDirection :   TEXCOORD2;					// camera direction in world coords
   float3 LightDirection :  TEXCOORD3;					// sun direction in world coords
   float Depth :			TEXCOORD4;					// Amount to filter sunlight due to depth (0-1)
   float Fog :				FOG;
};

VS_OUTPUT Textured_Phong_VS0( VS_INPUT Input )
{
   VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul( Input.Position, worldViewProjection  );		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul( Input.Position, world);
	
	// Get view and light directions by subtracting their positions from the vertex position
	Output.ViewDirection = eyePosition - fvObjectPosition;
	Output.LightDirection = sunPosition - fvObjectPosition;
	
	// Transform normal (i.e. rotate vector into world space without translating it)
	Output.Normal = normalize(mul(Input.Normal, (float3x3)world));	
	
	// Calculate a multiplier for depth effect (1 at surface). App must also set fog colour by camera depth 
	Output.Depth = clamp(mul(Input.Position.y/Input.Position.w, world) * 0.01f + 0.2f, 0.2f, 1.0f);		

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = 1.0f - length(Output.ViewDirection) * length(Output.ViewDirection) * 0.00003f;		// exponential fog
//	Output.Fog = 1.0f - length(Output.ViewDirection) * 0.005f;										// linear fog
	


	return( Output );
   
}



float4 matAmbient;						// ambient reflectivity of material
float4 matEmissive;						// emissive power of material
float4 matSpecular;						// shininess of material
float4 matDiffuse;						// diffuse component of material
const float fSpecularPower = 25.00;		// overall shininess const

texture baseTexture;					// Texture
bool isTextured;						// app sets to false if there's no texture

sampler2D baseMap = sampler_state
{
   Texture = (baseTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

struct PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
   float3 ViewDirection :   TEXCOORD2;
   float3 LightDirection:   TEXCOORD3;
   float Depth :			TEXCOORD4;					// Brightness of sunlight due to depth (0-1)
};

float4 Textured_Phong_PS0( PS_INPUT Input ) : COLOR0
{      
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvNormal = normalize(Input.Normal);
	float3 fvViewDirection = normalize(Input.ViewDirection);

	float  fNDotL = dot(fvNormal, fvLightDirection);												// angle of normal relative to sun
	
	float3 fvReflection = normalize(((2.0f * fvNormal) * (fNDotL)) - fvLightDirection);				// angle of reflected light for specular
	float  fRDotV = max(0.0f, dot(fvReflection, fvViewDirection));									// angle between viewer and reflected beam
	
	float4 fvTotalAmbient = matAmbient * sunAmbient * Input.Depth; 
	float4 fvTotalEmissive = matEmissive;															// sun colour doesn't count for light sources
	float4 fvTotalDiffuse = matDiffuse * sunDiffuse * fNDotL * Input.Depth; 
	float4 fvTotalSpecular = matSpecular * sunSpecular * pow( fRDotV, fSpecularPower ) * Input.Depth;
	
	float4 colour = fvTotalAmbient + fvTotalEmissive + fvTotalDiffuse + fvTotalSpecular;			// add up the light colours/intensities
	
	// Combine material/light colours with texture colour if tetured
	if (isTextured)
	{
		float4 fvBaseColor = tex2D( baseMap, Input.Texcoord );										// get the texel
		colour *= fvBaseColor;																		// multiply by light
		colour.a = fvBaseColor.a;																	// replace the alpha with the texture alpha
	}
	else	// if not textured, just replace the alpha with the diffuse material alpha (Truespace sets untextured material transparency here)
	{
		colour.a = matDiffuse.a;
	}

	// Combine all the colours
	return( saturate( colour ) );
      
}



//--------------------------------------------------------------//
// Textured Bump
//--------------------------------------------------------------//


struct Textured_Bump_VS_INPUT 
{
   float4 Position : POSITION0;
   float2 Texcoord : TEXCOORD0;
   float3 Normal :   NORMAL0;
   float3 Binormal : BINORMAL0;
   float3 Tangent :  TANGENT0;
   
};

struct Textured_Bump_VS_OUTPUT 
{
   float4 Position :        POSITION0;
   float2 Texcoord :        TEXCOORD0;
   float3 ViewDirection :   TEXCOORD1;
   float3 LightDirection:   TEXCOORD2;
   
};

Textured_Bump_VS_OUTPUT  Textured_Bump_VS0( Textured_Bump_VS_INPUT Input )
{
   Textured_Bump_VS_OUTPUT Output;

   Output.Position         = mul( worldViewProjection, Input.Position );
   Output.Texcoord         = Input.Texcoord;
   
   float3 fvObjectPosition = mul( worldView, Input.Position );
   
   float3 fvViewDirection  = eyePosition - fvObjectPosition;
   float3 fvLightDirection = sunPosition - fvObjectPosition;
     
   float3 fvNormal         = mul( worldView, Input.Normal );
   float3 fvBinormal       = mul( worldView, Input.Binormal );
   float3 fvTangent        = mul( worldView, Input.Tangent );
      
   Output.ViewDirection.x  = dot( fvTangent, fvViewDirection );
   Output.ViewDirection.y  = dot( fvBinormal, fvViewDirection );
   Output.ViewDirection.z  = dot( fvNormal, fvViewDirection );
   
   Output.LightDirection.x  = dot( fvTangent, fvLightDirection );
   Output.LightDirection.y  = dot( fvBinormal, fvLightDirection );
   Output.LightDirection.z  = dot( fvNormal, fvLightDirection );
   
   return( Output );
   
}



sampler2D Textured_Bump_baseMap = sampler_state
{
   Texture = (baseTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};
texture bumpTexture;

sampler2D bumpMap = sampler_state
{
   Texture = (bumpTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

struct Textured_Bump_PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 ViewDirection :   TEXCOORD1;
   float3 LightDirection:   TEXCOORD2;
   
};

float4 Textured_Bump_PS0( Textured_Bump_PS_INPUT Input ) : COLOR0
{      
   float3 fvLightDirection = normalize( Input.LightDirection );
   float3 fvNormal         = normalize( ( tex2D( bumpMap, Input.Texcoord ).xyz * 2.0f ) - 1.0f );
   float  fNDotL           = dot( fvNormal, fvLightDirection ); 
   
   float3 fvReflection     = normalize( ( ( 2.0f * fvNormal ) * ( fNDotL ) ) - fvLightDirection ); 
   float3 fvViewDirection  = normalize( Input.ViewDirection );
   float  fRDotV           = max( 0.0f, dot( fvReflection, fvViewDirection ) );
   
   float4 fvBaseColor      = tex2D( baseMap, Input.Texcoord );
   
   float4 fvTotalAmbient   = matAmbient * fvBaseColor; 
   float4 fvTotalDiffuse   = matDiffuse * fNDotL * fvBaseColor; 
   float4 fvTotalSpecular  = matSpecular * pow( fRDotV, fSpecularPower );
   
   return( saturate( fvTotalAmbient + fvTotalDiffuse + fvTotalSpecular ) );
      
}




//--------------------------------------------------------------//
// Technique Section for Effect Group 1
//--------------------------------------------------------------//
technique Textured_Phong
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Textured_Phong_VS0();
      PixelShader = compile ps_2_0 Textured_Phong_PS0();
   }

}

technique Textured_Bump
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Textured_Bump_VS0();
      PixelShader = compile ps_2_0 Textured_Bump_PS0();
   }

}

