#ifndef  CG_AS_INCLUDED
#define  CG_AS_INCLUDED

#define EPS                 1e-6
#define PI                  3.14159265359
#define INFINITY            1.0 / 0.0
#define PLANET_RADIUS       6371000
#define PLANET_CENTER       float3(0, -PLANET_RADIUS, 0)
#define ATMOSPHERE_HEIGHT   100000
#define RAYLEIGH_HEIGHT     (ATMOSPHERE_HEIGHT * 0.08)
#define MIE_HEIGHT          (ATMOSPHERE_HEIGHT * 0.012)

#define C_RAYLEIGH          (float3(5.802, 13.558, 33.100) * 1e-6)
#define C_MIE               (float3(3.996,  3.996,  3.996) * 1e-6)
#define C_OZONE             (float3(0.650,  1.881,  0.085) * 1e-6)

#define ATMOSPHERE_DENSITY  1
// 露出係数
#define EXPOSURE            20

// 視線と仮想大気球体の交点を計算する
float2 SphereIntersection (float3 rayStart, float3 rayDir)
{
	float sphereRadius=PLANET_RADIUS + ATMOSPHERE_HEIGHT;
	rayStart -= PLANET_CENTER;
	float a = dot(rayDir, rayDir);
	float b = 2.0 * dot(rayStart, rayDir);
	float c = dot(rayStart, rayStart) - (sphereRadius * sphereRadius);
	float d = b * b - 4 * a * c;
	if (d < 0)
	{
		return -1;
	}
	else
	{
		d = sqrt(d);
		return float2(-b - d, -b + d) / (2 * a);
	}
}


// Rayleigh散乱の関数値を計算する
float PhaseRayleigh (float costh)
{
	return 3 * (1 + costh*costh) / (16 * PI);
}

// Mie散乱の関数値を計算する
float PhaseMie (float costh, float g = 0.85)
{
	g = min(g, 0.9381);
	float k = 1.55*g - 0.55*g*g*g;
	float kcosth = k*costh;
	return (1 - k*k) / ((4 * PI) * (1-kcosth) * (1-kcosth));
}

// return:視線の始点から仮想惑星の表面までの距離を返す
float AtmosphereHeight (float3 positionWS)
{
	return distance(positionWS, PLANET_CENTER) - PLANET_RADIUS;
}
// 特定高さのRayleigh散乱密度を計算します
float DensityRayleigh (float h)
{
	return exp(-max(0, h / RAYLEIGH_HEIGHT));
}
// 特定高さのMie散乱密度を計算します
float DensityMie (float h)
{
	return exp(-max(0, h / MIE_HEIGHT));
}
// 特定高さのオゾン密度を計算します
float DensityOzone (float h)
{
	return max(0, 1 - abs(h - 25000.0) / 15000.0);
}

// 大気密度を計算する
float3 AtmosphereDensity (float h)
{
	return float3(DensityRayleigh(h), DensityMie(h), DensityOzone(h));
}

// 光学深度を計算する。光学深度は、光線が大気中で吸収および散乱される総量
float3 IntegrateOpticalDepth (float3 rayStart, float3 rayDir)
{
	// 交点を取得し、光の経路を得る。
	float2 intersection = SphereIntersection(rayStart, rayDir);
	float  rayLength    = intersection.y;

	// サンプリング数
	int    sampleCount  = 8;
	float  stepSize     = rayLength / sampleCount;
	
	float3 opticalDepth = 0;

	for (int i = 0; i < sampleCount; i++)
	{
		float3 localPosition = rayStart + rayDir * (i + 0.5) * stepSize;
		float  localHeight   = AtmosphereHeight(localPosition);
		float3 localDensity  = AtmosphereDensity(localHeight);
		// 光学的深度を累積計算する
		opticalDepth += localDensity * stepSize;
	}

	return opticalDepth;
}

// 光線の大気中での透過率を計算する
float3 Absorb (float3 opticalDepth)
{
	return exp(-(opticalDepth.x * C_RAYLEIGH + opticalDepth.y * C_MIE * 1.1 + opticalDepth.z * C_OZONE) * ATMOSPHERE_DENSITY);
}

// 大気散乱後の光線の色を計算する
float3 IntegrateScattering (float3 rayStart, float3 rayDir, float rayLength, float3 lightDir, float3 lightColor)
{
	// 視線の高さに基づいて大気サンプリング分布の指数を計算します。この数値は1（大気層の上部）から9（地表）までの範囲で、
	// 指数が高くなるほどサンプリングの間隔が密になる。
	float  rayHeight = AtmosphereHeight(rayStart);
	float  sampleDistributionExponent = 1 + saturate(1 - rayHeight / ATMOSPHERE_HEIGHT) * 9; 

	// 射線と仮想大気の交点を計算します
	float2 intersection = SphereIntersection(rayStart, rayDir);

	// 射線の長さを更新します
	rayLength = min(rayLength, intersection.y);
	
	 // 射線の始点が大気層の外にある場合、射線の始点を大気層の入り口に移動し、射線の長さを更新します。
	if (intersection.x > 0)
	{
		rayStart += rayDir * intersection.x;
		rayLength -= intersection.x;
	}
	// 視線方向（rayDir）と光線方向（lightDir）との間の余弦角を計算します。
	float  costh    = dot(rayDir, lightDir);

	// Rayleigh散乱の関数値を得る
	float  phaseR   = PhaseRayleigh(costh);

	// Mie 散乱の関数値を得る
	float  phaseM   = PhaseMie(costh);

	// 大気中での光学計算のサンプリングポイントの数を定義します
	int    sampleCount  =64;

	float3 opticalDepth = 0;
	float3 rayleigh     = 0;
	float3 mie          = 0;

	float  prevRayTime  = 0;

	for (int i = 0; i < sampleCount; i++)
	{ 
		// 現在のサンプリングポイントが射線方向における距離を計算します。
		float  rayTime = pow((float)i / sampleCount, sampleDistributionExponent) * rayLength;
		float  stepSize = (rayTime - prevRayTime);

		// 現在のサンプリングポイントの位置、高さ、および大気密度を計算します。
		float3 localPosition = rayStart + rayDir * rayTime;
		float  localHeight   = AtmosphereHeight(localPosition);
		float3 localDensity  = AtmosphereDensity(localHeight);

		// 射線の始点から現在のサンプリングポイントまでの累積光学深度を更新します。
		opticalDepth += localDensity * stepSize;

		// 射線の始点から現在のサンプリングポイントまでの透過率を得る。
		float3 viewTransmittance = Absorb(opticalDepth);

  		// 現在のサンプリングポイントから光線方向に沿った光学深度を得る。
		float3 opticalDepthlight  = IntegrateOpticalDepth(localPosition, lightDir);

		// 光源から現在のサンプリングポイントまでの光線の透過率を得る。
		float3 lightTransmittance = Absorb(opticalDepthlight);

   		// 現在のサンプリングポイントにおけるRayleigh 散乱とMie散乱の総量を得る。
		rayleigh += viewTransmittance * lightTransmittance * phaseR * localDensity.x * stepSize;
		mie      += viewTransmittance * lightTransmittance * phaseM * localDensity.y * stepSize;

		// サンプリングポイントを更新します。
		prevRayTime = rayTime;
	}
	
	// transmittance = Absorb(opticalDepth);

	// 大気散乱後の光線の色を返す
	return (rayleigh * C_RAYLEIGH + mie * C_MIE) * lightColor * EXPOSURE;
}

#endif