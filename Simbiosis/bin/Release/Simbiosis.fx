//**************************************************************//
// Simbiosis FX ver 0.1 14/3/06
//**************************************************************//

#define SMAP_SIZE 512									// width & height of shadowmap texture
#define SHADOW_BIAS 0.001f								// bias to prevent z-fighting in shadows


//--------------------------------------------------------------//
// Globals for all shaders
//--------------------------------------------------------------//

//************ switches ***********
bool isTextured;										// app sets to false if there's no texture
bool isBump;											// app sets to false if there's no bump map
bool isSpotlit;											// true if we need to do spotlight calcs
bool isShadowed = true;									// true if we are drawing shadows (app reads this too to decide whether to create shadowmap)

//********* Lighting *********
// Sunlight
float3 sunPosition;										// position of sun in sky
float4 sunlight;										// raw sunlight colour/intensity (diffuse & specular)
float4 ambient;											// raw skylight colour/intensity (ambient)
float4x4 shadowMatrix;									// View * proj matrix for sun, for shadow projection
float4x4 sunProj;

// Spotlight
float4x4 spotVP;										// View * proj matrix for spotlight
texture spotTexture;									// light map texture

// Caustics and water
float4x4 causticMatrix;									// projection matrix for caustics

//********* Camera and world matrices *********
float3 eyePosition;										// xyz of camera
float4x4 world;											// World matrix of object origin
float4x4 worldViewProjection : worldViewProjection;		// World * view * proj matrix


//********* Materials and textures ************
float4 matAmbient;						// ambient reflectivity of material
float4 matEmissive;						// emissive power of material
float4 matSpecular;						// shininess of material
float4 matDiffuse;						// diffuse component of material

const float fSpecularPower = 25.00;		// overall shininess const

texture baseTexture;					// Colour texture
texture bumpTexture;					// Normal map
texture causticTexture;					// animated texture for caustics
texture envTexture;						// cube map texture for reflections
texture shadowTexture;					// shadowmap

sampler2D baseMap = sampler_state		// Sampler for colour texture
{
   Texture = (baseTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

sampler2D bumpMap = sampler_state		// Sampler for normal map texture
{
   Texture = (bumpTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

sampler2D spotMap = sampler_state		// sampler for light map texture
{
   Texture = (spotTexture);
   ADDRESSU = CLAMP;
   ADDRESSV = CLAMP;
   MINFILTER = NONE;
   MAGFILTER = NONE;
   MIPFILTER = NONE;
};

sampler2D causticMap = sampler_state		// sampler for caustics
{
   Texture = (causticTexture);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

samplerCUBE environmentMap = sampler_state		// sampler for reflections
{
   Texture = (envTexture);
   ADDRESSU = CLAMP;
   ADDRESSV = CLAMP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};

sampler2D shadowMap = sampler_state				// sampler for shadows
{
   Texture = (shadowTexture);
   BORDERCOLOR = 0xFFFFFFFF;					// outside the shadowmap is white (no shadow)
   ADDRESSU = BORDER;
   ADDRESSV = BORDER;
   MINFILTER = POINT;							// we post-filter
   MAGFILTER = POINT;
   MIPFILTER = POINT;
};


//--------------------------------------------------------------//
// Common VERTEX shader functions
//--------------------------------------------------------------//

// Calculate a multiplier for depth effect (1 at surface). This controls the amount of sunlight hitting the object.
// App should also set fog colour by camera depth for best effect (so that the water is bluer near the surface & green deep down).
// Result needs to go in Output.Depth element of VS
float DepthEffect(float3 vertexWorldPosition)
{
	return saturate(vertexWorldPosition.y / 80);	
}

// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
float Fog(float3 viewDirection)
{
	// exponential fog
	return 1.0f - length(viewDirection) * length(viewDirection) * 0.000036f;	
	// linear fog
	//return 1.0f - length(viewDirection) * 0.005f;										
}

// Spotlight texture coordinate is vertex transformed by spot matrix
float4 SpotUV(float4 vertexPosition)
{
	if (isSpotlit)
		return mul(vertexPosition, mul(world,spotVP));											// Project vertex onto texture space	
	else
		return 0;
}



//--------------------------------------------------------------//
// Common PIXEL shader functions
//--------------------------------------------------------------//

// Return colour/intensity of the spotlight at this point
float4 SpotColour(float4 spotCoord)						// spotlight UV & depth (.w) from vertex shader
{
	float4 colour;
	
	spotCoord.xy = spotCoord.xy / spotCoord.ww * 0.5f + 0.5f;				// cvt to real coords then into range 0-1

	// if .w is -ve then we're behind the light, so don't colour (prevents the 2nd image)
	// Use tex2D instead of tex2Dproj, because we've already converted from homogeneous coords in order to adjust the range
	// The second term uses .w as the distance to compute light brightness
	//colour = (spotCoord.w <= 0.0f) ? float4(0,0,0,0) : tex2D(spotMap, spotCoord) * saturate(1 - spotCoord.w * 0.02f);
	return (spotCoord.w <= 0.0f) ? float4(0,0,0,0) : tex2D(spotMap, spotCoord) * saturate(20.0f/spotCoord.w);
}





//--------------------------------------------------------------------------------------//
// Main technique HIGH LOD - bump-mapped, shadowed shader for terrain & creatures
//--------------------------------------------------------------------------------------//

struct Main_VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
   float3 Tangent :  TANGENT0;
   float3 Binormal : BINORMAL0;
};

struct Main_VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 ViewDirection :   TEXCOORD2;					// camera direction in world coords
   float3 LightDirection :  TEXCOORD3;					// sun direction in world coords
   float  Depth :			TEXCOORD4;					// Amount to filter sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD5;					// Spot light map texture coordinate + depth
   float4 CausticCoord :	TEXCOORD6;					// texture UV for caustics
   float  Fog :				FOG;						// fog level (0-1)
};

struct Main_PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
   float3 ViewDirection :   TEXCOORD2;
   float3 LightDirection:   TEXCOORD3;
   float  Depth :			TEXCOORD4;					// Brightness of sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD5;					// Spot light map texture coordinate
   float4 CausticCoord :	TEXCOORD6;					// texture UV for caustics
};


Main_VS_OUTPUT Main_VS0( Main_VS_INPUT Input )
{
   Main_VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition - fvObjectPosition;

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));
	
	// ----------------------------- bump-mapped ---------------------------
	if (isBump)
	{
		float3 fvBinormal       = mul( Input.Binormal, world );
		float3 fvTangent        = mul( Input.Tangent, world );
		  
		Output.ViewDirection.x  = dot( fvTangent, fvViewDirection );
		Output.ViewDirection.y  = dot( fvBinormal, fvViewDirection );
		Output.ViewDirection.z  = dot( fvNormal, fvViewDirection );

		Output.LightDirection.x  = dot( fvTangent, fvLightDirection );
		Output.LightDirection.y  = dot( fvBinormal, fvLightDirection );
		Output.LightDirection.z  = dot( fvNormal, fvLightDirection );
	}
	// ----------------------------- phong only ----------------------------
	else
	{
		Output.ViewDirection = fvViewDirection;
		Output.LightDirection = fvLightDirection;
	}
	
	// Write vertex normal (only used for Phong shading)
	Output.Normal = fvNormal;

	// Calculate a multiplier for depth effect (1 at surface)
	Output.Depth = DepthEffect(fvObjectPosition);

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(Output.ViewDirection);

	// Spotlight texture coordinate is vertex transformed by spot matrix
	Output.SpotCoord = SpotUV(Input.Position);

	// Caustic texture coordinate (like spotlight but wraps)
	Output.CausticCoord =  mul(Input.Position, mul(world,causticMatrix));

	return( Output );
   
}


float4 Main_PS0( Main_PS_INPUT Input ) : COLOR0
{      
	float3 fvNormal;
	
	if (isBump)
	{
		// This normal is in texture space (but for bump maps ViewDirection and LightDirection should also be in texture space)
		fvNormal = normalize( ( tex2D( bumpMap, Input.Texcoord ).xyz * 2.0f ) - 1.0f );
	}
	else
	{
		fvNormal = normalize(Input.Normal);
	}

	// Natural light is sunlight modulated by caustic modulated by depth	
	float4 caustic = (sunlight + tex2D(causticMap, Input.CausticCoord)) * Input.Depth;

	// Sunlight 
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvViewDirection = normalize(Input.ViewDirection);
	float fNDotL = dot(fvNormal, fvLightDirection);													// angle of normal relative to sun
	float3 fvReflection = normalize(((2.0f * fvNormal) * (fNDotL)) - fvLightDirection);				// angle of reflected light for specular
	float fRDotV = max(0.0f, dot(fvReflection, fvViewDirection));									// angle between viewer and reflected beam
	float4 fvTotalDiffuse = matDiffuse * caustic * fNDotL; 
	float4 fvTotalSpecular = matSpecular * caustic * pow( fRDotV, fSpecularPower );
	
	// plus ambient and emissive
	float4 fvTotalAmbient = matAmbient * ambient * Input.Depth; 
	float4 fvTotalEmissive = matEmissive;															// sun colour doesn't count for light sources
	
	// Spotlight 
	// [ASSUMPTION: The spot is on the camera, and therefore viewDirection = spotDirection 
	// (saves having to calculate spotDirection)]
	if (isSpotlit)
	{
		float4 spot = SpotColour(Input.SpotCoord);													// spot intensity/colour at this point
		fNDotL = dot(fvNormal, fvViewDirection);													// angle of normal relative to spotlight
		fvTotalDiffuse += matDiffuse * spot * fNDotL;												// add in diffuse component
		fvTotalSpecular += matSpecular * spot * pow( fNDotL, fSpecularPower );						// and narrower beam specular
		// Is this right? Can't I just add the spot colour???????????????????????
	}
	
	float4 colour = fvTotalAmbient + fvTotalEmissive + fvTotalDiffuse + fvTotalSpecular;			// add up the light colours/intensities
	
	// Combine material/light colours with texture colour if textured
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

//======================== Pass 2 - project the shadow map onto the scene ===================================

struct Main_VS1_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
};


struct Main_VS1_OUTPUT 
{
   float4 Position :		POSITION0;
   float4 ShadowCoord :		TEXCOORD0;					// texture UV for shadowmap
   float  Fog :				FOG;
};

struct Main_PS1_INPUT 
{
   float4 ShadowCoord :		TEXCOORD0;					// texture UV for shadowmap projector
};


Main_VS1_OUTPUT Main_VS1( Main_VS1_INPUT Input )
{
   Main_VS1_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);

	// Shadowmap projector texture coordinate (depth value)
	//float4 shadowCoord =  mul(Input.Position, mul(mul(world,shadowMatrix),sunProj));
	float4 shadowCoord =  mul(Input.Position, mul(world, mul(shadowMatrix,sunProj)));
	Output.ShadowCoord = shadowCoord;

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = 1.0;

	return( Output );   
}


float4 Main_PS1( Main_PS1_INPUT Input ) : COLOR0
{      
	float4 colour = float4(0.0,0.0,0.0,0.0);
	
	// Compute projected coordinates in shadow map
	float2 ShadowTex = 0.5 * Input.ShadowCoord.xy / Input.ShadowCoord.ww + float2( 0.5, 0.5 );
	ShadowTex.y = 1.0f - ShadowTex.y;

    float2 texelpos = SMAP_SIZE * ShadowTex;							// transform to texel space
    
    // Get the sun-depth of the pixel
    float depth = Input.ShadowCoord.z / Input.ShadowCoord.w  - SHADOW_BIAS;
    
    //average the adjacent 4 pixels
    float shade = (tex2D( shadowMap, ShadowTex ) < depth)? 1.0f: 0.0f;  
    shade += (tex2D( shadowMap, ShadowTex + float2(1.0/SMAP_SIZE, 0) ) < depth)? 1.0f: 0.0f;  
    shade += (tex2D( shadowMap, ShadowTex + float2(0, 1.0/SMAP_SIZE) ) < depth)? 1.0f: 0.0f;  
    shade += (tex2D( shadowMap, ShadowTex + float2(1.0/SMAP_SIZE, 1.0/SMAP_SIZE) ) < depth)? 1.0f: 0.0f;  
    
    // Shadow is transparent black
	colour.a = shade / 16.0;											// smaller constant = darker shadows

	return(colour);
      
}




//--------------------------------------------------------------//
// Main technique MEDIUM LOD - no shadows
//--------------------------------------------------------------//

struct Main_VS2_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
   float3 Tangent :  TANGENT0;
   float3 Binormal : BINORMAL0;
};

struct Main_VS2_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 ViewDirection :   TEXCOORD2;					// camera direction in world coords
   float3 LightDirection :  TEXCOORD3;					// sun direction in world coords
   float  Depth :			TEXCOORD4;					// Amount to filter sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD5;					// Spot light map texture coordinate + depth
   float4 CausticCoord :	TEXCOORD6;					// texture UV for caustics
   float  Fog :				FOG;						// fog level (0-1)
};

struct Main_PS2_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
   float3 ViewDirection :   TEXCOORD2;
   float3 LightDirection:   TEXCOORD3;
   float  Depth :			TEXCOORD4;					// Brightness of sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD5;					// Spot light map texture coordinate
   float4 CausticCoord :	TEXCOORD6;					// texture UV for caustics
};


Main_VS2_OUTPUT Main_VS2( Main_VS2_INPUT Input )
{
   Main_VS2_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition - fvObjectPosition;

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));
	
	// ----------------------------- bump-mapped ---------------------------
	if (isBump)
	{
		float3 fvBinormal       = mul( Input.Binormal, world );
		float3 fvTangent        = mul( Input.Tangent, world );
		  
		Output.ViewDirection.x  = dot( fvTangent, fvViewDirection );
		Output.ViewDirection.y  = dot( fvBinormal, fvViewDirection );
		Output.ViewDirection.z  = dot( fvNormal, fvViewDirection );

		Output.LightDirection.x  = dot( fvTangent, fvLightDirection );
		Output.LightDirection.y  = dot( fvBinormal, fvLightDirection );
		Output.LightDirection.z  = dot( fvNormal, fvLightDirection );
	}
	// ----------------------------- phong only ----------------------------
	else
	{
		Output.ViewDirection = fvViewDirection;
		Output.LightDirection = fvLightDirection;
	}
	
	// Write vertex normal (only used for Phong shading)
	Output.Normal = fvNormal;

	// Calculate a multiplier for depth effect (1 at surface)
	Output.Depth = DepthEffect(fvObjectPosition);

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(Output.ViewDirection);

	// Spotlight texture coordinate is vertex transformed by spot matrix
	Output.SpotCoord = SpotUV(Input.Position);

	// Caustic texture coordinate (like spotlight but wraps)
	Output.CausticCoord =  mul(Input.Position, mul(world,causticMatrix));

	return( Output );
   
}


float4 Main_PS2( Main_PS2_INPUT Input ) : COLOR0
{      
	float3 fvNormal;
	
	if (isBump)
	{
		// This normal is in texture space (but for bump maps ViewDirection and LightDirection should also be in texture space)
		fvNormal = normalize( ( tex2D( bumpMap, Input.Texcoord ).xyz * 2.0f ) - 1.0f );
	}
	else
	{
		fvNormal = normalize(Input.Normal);
	}

	// Natural light is sunlight modulated by caustic modulated by depth	
	float4 caustic = (sunlight + tex2D(causticMap, Input.CausticCoord)) * Input.Depth;

	// Sunlight 
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvViewDirection = normalize(Input.ViewDirection);
	float fNDotL = dot(fvNormal, fvLightDirection);													// angle of normal relative to sun
	float3 fvReflection = normalize(((2.0f * fvNormal) * (fNDotL)) - fvLightDirection);				// angle of reflected light for specular
	float fRDotV = max(0.0f, dot(fvReflection, fvViewDirection));									// angle between viewer and reflected beam
	float4 fvTotalDiffuse = matDiffuse * caustic * fNDotL; 
	float4 fvTotalSpecular = matSpecular * caustic * pow( fRDotV, fSpecularPower );
	
	// plus ambient and emissive
	float4 fvTotalAmbient = matAmbient * ambient * Input.Depth; 
	float4 fvTotalEmissive = matEmissive;															// sun colour doesn't count for light sources
	
	// Spotlight 
	// [ASSUMPTION: The spot is on the camera, and therefore viewDirection = spotDirection 
	// (saves having to calculate spotDirection)]
	if (isSpotlit)
	{
		float4 spot = SpotColour(Input.SpotCoord);													// spot intensity/colour at this point
		fNDotL = dot(fvNormal, fvViewDirection);													// angle of normal relative to spotlight
		fvTotalDiffuse += matDiffuse * spot * fNDotL;												// add in diffuse component
		fvTotalSpecular += matSpecular * spot * pow( fNDotL, fSpecularPower );						// and narrower beam specular
		// Is this right? Can't I just add the spot colour???????????????????????
	}
	
	float4 colour = fvTotalAmbient + fvTotalEmissive + fvTotalDiffuse + fvTotalSpecular;			// add up the light colours/intensities
	
	// Combine material/light colours with texture colour if textured
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
// Main technique LOW LOD - no shadows or bump-mapping,
// no caustics or spotlight
//--------------------------------------------------------------//

struct Main_VS3_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
};

struct Main_VS3_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 ViewDirection :   TEXCOORD2;					// camera direction in world coords
   float3 LightDirection :  TEXCOORD3;					// sun direction in world coords
   float  Depth :			TEXCOORD4;					// Amount to filter sunlight due to depth (0-1)
   float  Fog :				FOG;						// fog level (0-1)
};

struct Main_PS3_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
   float3 ViewDirection :   TEXCOORD2;
   float3 LightDirection:   TEXCOORD3;
   float  Depth :			TEXCOORD4;					// Brightness of sunlight due to depth (0-1)
};


Main_VS3_OUTPUT Main_VS3( Main_VS3_INPUT Input )
{
   Main_VS3_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition - fvObjectPosition;

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));
	
	Output.ViewDirection = fvViewDirection;
	Output.LightDirection = fvLightDirection;
	
	// Write vertex normal
	Output.Normal = fvNormal;

	// Calculate a multiplier for depth effect (1 at surface)
	Output.Depth = DepthEffect(fvObjectPosition);

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(Output.ViewDirection);

	return( Output );
   
}

float4 Main_PS3( Main_PS3_INPUT Input ) : COLOR0
{      
	float3 fvNormal;
	
	fvNormal = normalize(Input.Normal);

	// Sunlight (no caustics in this LOD)
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvViewDirection = normalize(Input.ViewDirection);
	float fNDotL = dot(fvNormal, fvLightDirection);													// angle of normal relative to sun
	float3 fvReflection = normalize(((2.0f * fvNormal) * (fNDotL)) - fvLightDirection);				// angle of reflected light for specular
	float fRDotV = max(0.0f, dot(fvReflection, fvViewDirection));									// angle between viewer and reflected beam
	float4 fvTotalDiffuse = matDiffuse * sunlight * fNDotL; 
	float4 fvTotalSpecular = matSpecular * sunlight * pow( fRDotV, fSpecularPower );
	
	// plus ambient and emissive
	float4 fvTotalAmbient = matAmbient * ambient * Input.Depth; 
	float4 fvTotalEmissive = matEmissive;															// sun colour doesn't count for light sources
	
	float4 colour = fvTotalAmbient + fvTotalEmissive + fvTotalDiffuse + fvTotalSpecular;			// add up the light colours/intensities
	
	// Combine material/light colours with texture colour if textured
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
// Render the shadow map to a fp texture
//--------------------------------------------------------------//

struct Shadow_VS_INPUT
{
	float4 Position : POSITION0;
};

struct Shadow_VS_OUTPUT
{
   float4 Position : POSITION0;
   float Depth : TEXCOORD0;
   float  Fog :				FOG;						// fog level (0-1)
};

Shadow_VS_OUTPUT Shadow_VS0( Shadow_VS_INPUT Input )
{
   Shadow_VS_OUTPUT Output;
   
	float4 outpos =  mul(Input.Position, mul(world, mul(shadowMatrix,sunProj)));
	Output.Position = outpos;
	
	Output.Depth = outpos.z / outpos.w;	
	
	Output.Fog = 1.0;								// should switch fog off when rendering shadowmap!
	return(Output);
}


float4 Shadow_PS0( float Depth : TEXCOORD0) : COLOR
{      
    return Depth;
}

















//--------------------------------------------------------------//
// Scenery technique - simple lighting for sprites
//--------------------------------------------------------------//


struct Scenery_VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
};

struct Scenery_VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 LightDirection :  TEXCOORD2;					// sun direction in world coords
   float Depth :			TEXCOORD3;					// Amount to filter sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD4;					// Spot light map texture coordinate + depth
   float Fog :				FOG;						// fog level (0-1)
};

struct Scenery_PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
   float3 LightDirection:   TEXCOORD2;
   float Depth :			TEXCOORD3;					// Brightness of sunlight due to depth (0-1)
   float4 SpotCoord :       TEXCOORD4;					// Spot light map texture coordinate
};



Scenery_VS_OUTPUT Scenery_VS0( Scenery_VS_INPUT Input )
{
   Scenery_VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition - fvObjectPosition;
	Output.LightDirection = fvLightDirection;

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));
		
	// Write vertex normal
	Output.Normal = fvNormal;

	// Calculate a multiplier for depth effect (1 at surface).  
	Output.Depth = DepthEffect(fvObjectPosition);

	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(fvViewDirection);

	// Spotlight texture coordinate is vertex transformed by spot world*view*proj matrix
	Output.SpotCoord = SpotUV(Input.Position);

	return( Output );  
}



float4 Scenery_PS0( Scenery_PS_INPUT Input ) : COLOR0
{      
	float3 fvNormal = normalize(Input.Normal);
	float3 fvLightDirection = normalize(Input.LightDirection);									// normalise interpolated vectors
	float fNDotL = dot(fvNormal, fvLightDirection);												// angle of normal relative to sun
	
	float4 fvTotalAmbient = matAmbient * ambient * Input.Depth; 
	float4 fvTotalDiffuse = matDiffuse * sunlight * fNDotL * Input.Depth; 

	// Add texture colour from the spotlight if any
	if (isSpotlit)
	{
		fvTotalAmbient += matAmbient * SpotColour(Input.SpotCoord);								// only apply as ambient light
	}
	
	// add up the light colours/intensities
	float4 colour = fvTotalAmbient + fvTotalDiffuse;											
	
	// Combine material/light colours with texture colour
	float4 fvBaseColor = tex2D( baseMap, Input.Texcoord );										// get the texel
	colour *= fvBaseColor;																		// multiply by light
	colour.a = fvBaseColor.a;																	// replace the alpha with the texture alpha

	// Combine all the colours
	return( saturate( colour ) );
      
}







//--------------------------------------------------------------//
// Water technique - bump-mapped shader for under-surface of sea
//--------------------------------------------------------------//

struct Water_VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
   float3 Tangent :  TANGENT0;
   float3 Binormal : BINORMAL0;
};

struct Water_VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 ViewDirection :   TEXCOORD1;					// eye to vertex direction for reflection mapping
   float3 LightDirection :  TEXCOORD2;					// sun direction in world coords
   float Fog :				FOG;						// fog level (0-1)
};

struct Water_PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 ViewDirection :   TEXCOORD1;
   float3 LightDirection:   TEXCOORD2;
};


Water_VS_OUTPUT Water_VS0( Water_VS_INPUT Input )
{
   Water_VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition;									// light comes from underneath! So use sun direction but -ve Y
	fvLightDirection.y = -100.0f;
	fvLightDirection -= fvObjectPosition;	

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));

	// Send *real* eye to vertex direction for reflection mapping (unlike Main, where bump mapping sends view direction in tangent space)
	Output.ViewDirection  = eyePosition - fvObjectPosition;

	// ----------------------------- bump-mapped ---------------------------
	float3 fvBinormal       = mul( Input.Binormal, world );
	float3 fvTangent        = mul( Input.Tangent, world );
	  
	Output.LightDirection.x  = dot( fvTangent, fvLightDirection );
	Output.LightDirection.y  = dot( fvBinormal, fvLightDirection );
	Output.LightDirection.z  = dot( fvNormal, fvLightDirection );
	
	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(Output.ViewDirection);

	return( Output );
   
}


float4 Water_PS0( Water_PS_INPUT Input ) : COLOR0
{      
	float3 fvNormal;
	float4 colour;
	
	// This normal is in texture space (but for bump maps ViewDirection and LightDirection should also be in texture space)
	fvNormal = normalize( ( tex2D( bumpMap, Input.Texcoord ).xyz * 2.0f ) - 1.0f );
	
	/////////////////////// Enable this line to get a temporary flat surface for checking the cube map ////////////////////
	//fvNormal = normalize(float3(0.0f,-1.0f,0.0f)); 

	// Sunlight 
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvViewDirection = normalize(Input.ViewDirection);
	float fNDotL = dot(fvNormal, fvLightDirection);													// angle of normal relative to sun
	float4 fvTotalDiffuse = matDiffuse * sunlight * fNDotL; 
		
	//getting the reflection vector and a surface to eye vector
    float3 reflectVector = reflect(fvViewDirection, fvNormal);
    
     //calculate the colour from the cubemap & light
    const float SHININESS = 0.75f;																	// 1 = 100% reflective, 0 = matt
    colour = texCUBE(environmentMap, reflectVector) * SHININESS + fvTotalDiffuse;
	
	// Adjust the alpha so that water is transparent looking straight up, and reflective looking sideways
	//colour.a = matDiffuse.a;										// just material alpha	
	//colour.a = 1.2f+fvViewDirection.y;							// linear
	colour.a = 1.4f-(fvViewDirection.y*fvViewDirection.y);			// exponential

	// Combine all the colours
	return( saturate( colour ) );
      
}


//--------------------------------------------------------------//
// Water technique LOD2 - no reflections
//--------------------------------------------------------------//

struct Water_VS1_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
   float3 Tangent :  TANGENT0;
   float3 Binormal : BINORMAL0;
};

struct Water_VS1_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float3 ViewDirection :   TEXCOORD1;					// eye to vertex direction for reflection mapping
   float3 LightDirection :  TEXCOORD2;					// sun direction in world coords
   float Fog :				FOG;						// fog level (0-1)
};

struct Water_PS1_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 ViewDirection :   TEXCOORD1;
   float3 LightDirection:   TEXCOORD2;
};


Water_VS1_OUTPUT Water_VS1( Water_VS1_INPUT Input )
{
   Water_VS1_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);		// WAS REVERSED IN RENDERMONKEY!
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	float3 fvViewDirection  = eyePosition - fvObjectPosition;
	float3 fvLightDirection = sunPosition;									// light comes from underneath! So use sun direction but -ve Y
	fvLightDirection.y = -100.0f;
	fvLightDirection -= fvObjectPosition;	

	// Transform normal (i.e. rotate vector into world space without translating it)
	float3 fvNormal = normalize(mul(Input.Normal, (float3x3)world));

	// Send *real* eye to vertex direction for reflection mapping (unlike Main, where bump mapping sends view direction in tangent space)
	Output.ViewDirection  = eyePosition - fvObjectPosition;

	// ----------------------------- bump-mapped ---------------------------
	float3 fvBinormal       = mul( Input.Binormal, world );
	float3 fvTangent        = mul( Input.Tangent, world );
	  
	Output.LightDirection.x  = dot( fvTangent, fvLightDirection );
	Output.LightDirection.y  = dot( fvBinormal, fvLightDirection );
	Output.LightDirection.z  = dot( fvNormal, fvLightDirection );
	
	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = Fog(Output.ViewDirection);

	return( Output );
   
}


float4 Water_PS1( Water_PS1_INPUT Input ) : COLOR0
{      
	float3 fvNormal;
	float4 colour;
	
	// This normal is in texture space (but for bump maps ViewDirection and LightDirection should also be in texture space)
	fvNormal = normalize( ( tex2D( bumpMap, Input.Texcoord ).xyz * 2.0f ) - 1.0f );
	
	/////////////////////// Enable this line to get a temporary flat surface for checking the cube map ////////////////////
	//fvNormal = normalize(float3(0.0f,-1.0f,0.0f)); 

	// Sunlight 
	float3 fvLightDirection = normalize(Input.LightDirection);										// normalise interpolated vectors
	float3 fvViewDirection = normalize(Input.ViewDirection);
	float fNDotL = dot(fvNormal, fvLightDirection);													// angle of normal relative to sun
	float4 fvTotalDiffuse = matDiffuse * sunlight * fNDotL; 
		
    colour = fvTotalDiffuse;
    colour.a = 1.0;
	
	// Combine all the colours
	return( saturate( colour ) );
      
}








//--------------------------------------------------------------//
// Skybox technique
//--------------------------------------------------------------//


struct Skybox_VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
};

struct Skybox_VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float2 Texcoord :        TEXCOORD0;					// Base texture coordinate
   float Fog :				FOG;						// fog level (0-1)
};

struct Skybox_PS_INPUT 
{
   float2 Texcoord :        TEXCOORD0;
   float3 Normal :          TEXCOORD1;
};



Skybox_VS_OUTPUT Skybox_VS0( Skybox_VS_INPUT Input )
{
   Skybox_VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);	
    
    // Pass texture UV straight through
	Output.Texcoord = Input.Texcoord;
	
	// Fog is CONSTANT (0=distant, 1=no fog)
	Output.Fog = 1.0f;

	return( Output );  
}



float4 Skybox_PS0( Skybox_PS_INPUT Input ) : COLOR0
{      
	// Use sunlight level to control brightness of skybox
	// Combine light with texture colour
	float4 colour = tex2D( baseMap, Input.Texcoord ) * sunlight;	
	colour.a = 1.0f;
	return( saturate( colour ) );
      
}




//----------------------------------------------------------------------------//
// Marker technique - simple shading for transparent marker cones and spheres
//----------------------------------------------------------------------------//


struct Marker_VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float2 Texcoord : TEXCOORD0;
};

struct Marker_VS_OUTPUT 
{
   float4 Position :        POSITION0;					// position of vertex in homogeneous space
   float3 Normal :          TEXCOORD1;					// vertex normal transformed into world space
   float3 ViewDirection :  TEXCOORD2;					// sun direction in world coords
   float Fog :				FOG;						// fog level (0-1)
};

struct Marker_PS_INPUT 
{
   float3 Normal :          TEXCOORD1;
   float3 ViewDirection:   TEXCOORD2;
};



Marker_VS_OUTPUT Marker_VS0( Marker_VS_INPUT Input )
{
   Marker_VS_OUTPUT Output;

	// Transform the position from object space to homogeneous projection space
    Output.Position = mul(Input.Position, worldViewProjection);
    
	// Get vertex position in world coordinates
	float3 fvObjectPosition = mul(Input.Position, world);

	// Get view and light directions by subtracting their positions from the vertex position
	Output.ViewDirection = eyePosition - fvObjectPosition;

	// Transform normal (i.e. rotate vector into world space without translating it)
	Output.Normal = normalize(mul(Input.Normal, (float3x3)world));
		
	// Fog is proportional to distance from camera to vertex (0=distant, 1=no fog)
	Output.Fog = 1.0; /////////////Fog(fvViewDirection);

	return( Output );  
}



float4 Marker_PS0( Marker_PS_INPUT Input ) : COLOR0
{      
	float3 fvNormal = normalize(Input.Normal);
	float3 fvViewDirection = normalize(Input.ViewDirection);									// normalise interpolated vectors
	float fNDotL = dot(fvNormal, fvViewDirection);												// angle of normal relative to *eye*
	
//	float4 colour = matEmissive * 0.25 + matDiffuse * fNDotL; 	
//	colour.a = matDiffuse.a;																	// replace the alpha with the diffuse alpha

	// Simulate the effect of scattered light
	// The closer the normal is to the eye direction, the thicker the beam is at that point, so the more
	// dust/water it scatters from. Adjust the alpha to simulate this. (It doesn't work for beams that face
	// toward or away from camera, but no big deal). Should look ok for spheres too.
	float4 colour = matEmissive; 	
	colour.a = fNDotL/2+0.25;

	// Combine all the colours
	return( saturate( colour ) );
      
}









//--------------------------------------------------------------//
// Main technique for creatures and terrain
// LOD1 - shadows, phong or bump-mapped, with or without spotlighting.
// Includes fog and depth-controlled sunlight
//--------------------------------------------------------------//
technique Main
{
   pass Pass_0
   {
 		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		//CullMode = None;
		ZEnable = true;
		ZWriteEnable = true;
		//ZFunc = LESSEQUAL;
		//DepthBias = 0;
		//SlopeScaleDepthBias = 1;
		VertexShader = compile vs_2_0 Main_VS0();
		PixelShader = compile ps_2_0 Main_PS0();
   } 
   
   pass Pass_1
   {
 		//AlphaBlendEnable = true;
		//SrcBlend = SrcAlpha;
		//DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		//ZEnable = true;
		//ZWriteEnable = false;						// disable z writes
 		//ZFunc = EQUAL;
		//DepthBias = 0;
 		//SlopeScaleDepthBias = 1;
		VertexShader = compile vs_2_0 Main_VS1();
		PixelShader = compile ps_2_0 Main_PS1();
   }

}

//--------------------------------------------------------------//
// Main technique for creatures and terrain
// LOD2 - single pass - no shadows
//--------------------------------------------------------------//
technique Main2
{
   pass Pass_0
   {
 		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		ZEnable = true;
		ZWriteEnable = true;
		VertexShader = compile vs_2_0 Main_VS2();
		PixelShader = compile ps_2_0 Main_PS2();
   } 
}

//--------------------------------------------------------------//
// Main technique for creatures and terrain
// LOD3 - single pass - no shadows, no bump-mapping
//--------------------------------------------------------------//
technique Main3
{
   pass Pass_0
   {
 		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		ZEnable = true;
		ZWriteEnable = true;
		VertexShader = compile vs_2_0 Main_VS3();
		PixelShader = compile ps_2_0 Main_PS3();
   } 
}

//--------------------------------------------------------------//
// Scenery technique
// Simple shading, texture, with or without spotlighting.
// Includes fog and depth-controlled sunlight
//--------------------------------------------------------------//
technique Scenery
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Scenery_VS0();
      PixelShader = compile ps_2_0 Scenery_PS0();
   }

}

//--------------------------------------------------------------//
// Water technique - LOD1
//--------------------------------------------------------------//
technique Water
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Water_VS0();
      PixelShader = compile ps_2_0 Water_PS0();
   }

}

//--------------------------------------------------------------//
// Water technique - LOD2 - no reflections
//--------------------------------------------------------------//
technique Water1
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Water_VS1();
      PixelShader = compile ps_2_0 Water_PS1();
   }

}

//--------------------------------------------------------------//
// Skybox technique
//--------------------------------------------------------------//
technique Skybox
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Skybox_VS0();
      PixelShader = compile ps_2_0 Skybox_PS0();
   }

}

//--------------------------------------------------------------//
// Shadow technique
//--------------------------------------------------------------//
technique Shadow
{
   pass Pass_0
   {
 		AlphaBlendEnable = false;
		ZEnable = true;
		ZWriteEnable = true;
      VertexShader = compile vs_2_0 Shadow_VS0();
      PixelShader = compile ps_2_0 Shadow_PS0();
   }

}

//--------------------------------------------------------------//
// Marker technique
//--------------------------------------------------------------//
technique Marker
{
   pass Pass_0
   {
      VertexShader = compile vs_2_0 Marker_VS0();
      PixelShader = compile ps_2_0 Marker_PS0();
   }

}
