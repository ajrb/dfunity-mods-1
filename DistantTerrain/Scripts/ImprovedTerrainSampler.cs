//Distant Terrain Mod for Daggerfall-Unity
//http://www.reddit.com/r/dftfu
//http://www.dfworkshop.net/
//Author: Michael Rauter (a.k.a. Nystul)
//License: MIT License (http://www.opensource.org/licenses/mit-license.php)

using UnityEngine;
using System;
using System.Collections;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DistantTerrain;

//namespace DaggerfallWorkshop
namespace DistantTerrain
{
    /// <summary>
    /// Default TerrainSampler for StreamingWorld.
    /// </summary>
    public class ImprovedTerrainSampler : TerrainSampler
    {
        // Scale factors for this sampler implementation
        public const float baseHeightScale = 8f; //12f; //16f; // 8f;        
        public const float defaultNoiseMapScale = 15f;
        public const float defaultExtraNoiseScale = 3f;
        // additional height noise based on climate      
        public const float noiseMapScaleClimateOcean = 0.0f;
        public const float noiseMapScaleClimateDesert = 2.0f; //1.25f;
        public const float noiseMapScaleClimateDesert2 = 32.0f;
        public const float noiseMapScaleClimateMountain = 15.0f;
        public const float noiseMapScaleClimateRainforest = 11.5f;
        public const float noiseMapScaleClimateSwamp = 3.8f;
        public const float noiseMapScaleClimateSubtropical = 3.35f; // 3.25f
        public const float noiseMapScaleClimateMountainWoods = 16.5f; // 12.5f
        public const float noiseMapScaleClimateWoodlands = 18.0f; //10.0f;
        public const float noiseMapScaleClimateHauntedWoodlands = 8.0f;
        public const float maxNoiseMapScale = 32.0f; //32f; //15f; //4f; //15f; //4f;
        // extra noise scale based on climate
        public const float extraNoiseScaleClimateOcean = 0.0f;
        public const float extraNoiseScaleClimateDesert = 29f; //7f;
        public const float extraNoiseScaleClimateDesert2 = 38f;
        public const float extraNoiseScaleClimateMountain = 62f;
        public const float extraNoiseScaleClimateRainforest = 16f;
        public const float extraNoiseScaleClimateSwamp = 20f;
        public const float extraNoiseScaleClimateSubtropical = 26f; // 17f
        public const float extraNoiseScaleClimateMountainWoods = 32f;
        public const float extraNoiseScaleClimateWoodlands = 24f;
        public const float extraNoiseScaleClimateHauntedWoodlands = 22f;

        public const float interpolationEndDistanceFromWaterForNoiseScaleMultiplier = 5.0f;

        //public const float extraNoiseScale = 3f; //10f; //3f;
        public const float scaledOceanElevation = 3.4f * baseHeightScale;
        public const float scaledBeachElevation = 5.0f * baseHeightScale;

        // Max terrain height of this sampler implementation
        public const float maxTerrainHeight = ImprovedWorldTerrain.maxHeightsExaggerationMultiplier * baseHeightScale * 128 + maxNoiseMapScale * 128 + 128;//1380f; //26115f;

        public override int Version
        {
            get { return 2; }
        }

        public ImprovedTerrainSampler()
        {
            HeightmapDimension = defaultHeightmapDimension;
            MaxTerrainHeight = maxTerrainHeight;
            OceanElevation = scaledOceanElevation;
            BeachElevation = scaledBeachElevation;
        }

        public override float TerrainHeightScale(int x, int y)
        {
            return ImprovedWorldTerrain.computeHeightMultiplier(x, y) * ImprovedTerrainSampler.baseHeightScale + this.GetNoiseMapScaleBasedOnClimate(x, y); // * 0.5f;
        }



        public override void GenerateSamples(ref MapPixelData mapPixel)
        {
            //System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            //long startTime = stopwatch.ElapsedMilliseconds;

            DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

            // Create samples arrays
            mapPixel.tilemapSamples = new TilemapSample[MapsFile.WorldMapTileDim, MapsFile.WorldMapTileDim];
            mapPixel.heightmapSamples = new float[HeightmapDimension, HeightmapDimension];

            // Divisor ensures continuous 0-1 range of tile samples
            float div = (float)(HeightmapDimension - 1) / 3f;

            // Read neighbouring height samples for this map pixel
            int mx = mapPixel.mapPixelX;
            int my = mapPixel.mapPixelY;

            // Seed random with terrain key
            UnityEngine.Random.InitState(TerrainHelper.MakeTerrainKey(mx, my));

            byte[,] shm = dfUnity.ContentReader.WoodsFileReader.GetHeightMapValuesRange(mx - 2, my - 2, 4);
            byte[,] lhm = dfUnity.ContentReader.WoodsFileReader.GetLargeHeightMapValuesRange(mx - 1, my, 3);

            float[,] baseHeightValue = new float[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int mapPixelX = Math.Max(0, Math.Min(mx + x - 2, WoodsFile.mapWidthValue));
                    int mapPixelY = Math.Max(0, Math.Min(my + y - 2, WoodsFile.mapHeightValue));

                    baseHeightValue[x, y] = shm[x,y] * ImprovedWorldTerrain.computeHeightMultiplier(mapPixelX, mapPixelY);
                }
            }

            float[,] waterMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    if (shm[x, y] <= 2) // mappixel is water
                        waterMap[x, y] = 0.0f;
                    else
                        waterMap[x, y] = 1.0f;
                }
            }

            float[,] climateMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int mapPixelX = Math.Max(0, Math.Min(mx + x - 2, WoodsFile.mapWidthValue));
                    int mapPixelY = Math.Max(0, Math.Min(my + y - 2, WoodsFile.mapHeightValue));                    
                    climateMap[x, y] = GetNoiseMapScaleBasedOnClimate(mapPixelX, mapPixelY);
                }
            }

            float[,] waterDistanceMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int mapPixelX = Math.Max(0, Math.Min(mx + x - 2, WoodsFile.mapWidthValue));
                    int mapPixelY = Math.Max(0, Math.Min(my + y - 2, WoodsFile.mapHeightValue));
                    waterDistanceMap[x, y] = (float)Math.Sqrt(ImprovedWorldTerrain.MapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);
                }
            }

            float[,] noiseHeightMultiplierMap = new float[4, 4];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    // interpolation multiplier taking near coast map pixels into account
                    // (multiply with 0 at coast line and 1 at interpolationEndDistanceFromWaterForNoiseScaleMultiplier)
                    float multFact = (Mathf.Min(interpolationEndDistanceFromWaterForNoiseScaleMultiplier, waterDistanceMap[x, y]) / interpolationEndDistanceFromWaterForNoiseScaleMultiplier);

                    // blend watermap with climatemap taking into account multFact
                    noiseHeightMultiplierMap[x, y] = waterMap[x, y] * climateMap[x, y] * multFact;
                }
            }

            //float[,] noiseHeightMultiplierMap = new float[4, 4];
            //for (int y = 0; y < 4; y++)
            //{
            //    for (int x = 0; x < 4; x++)
            //    {
            //        int mapPixelX = Math.Max(0, Math.Min(mx + x - 2, WoodsFile.mapWidthValue));
            //        int mapPixelY = Math.Max(0, Math.Min(my + y - 2, WoodsFile.mapHeightValue));

            //        float climateValue = GetNoiseMapScaleBasedOnClimate(mapPixelX, mapPixelY);

            //        float waterDistance = (float)Math.Sqrt(ImprovedWorldTerrain.MapDistanceSquaredFromWater[mapPixelY * WoodsFile.mapWidthValue + mapPixelX]);

            //        float waterValue;
            //        if (shm[x, y] <= 2) // mappixel is water
            //            waterValue = 0.0f;
            //        else
            //            waterValue = 1.0f;

            //        // interpolation multiplier taking near coast map pixels into account
            //        // (multiply with 0 at coast line and 1 at interpolationEndDistanceFromWaterForNoiseScaleMultiplier)
            //        float multFact = (Mathf.Min(interpolationEndDistanceFromWaterForNoiseScaleMultiplier, waterDistance) / interpolationEndDistanceFromWaterForNoiseScaleMultiplier);

            //        // blend watermap with climatemap taking into account multFact
            //        noiseHeightMultiplierMap[x, y] = waterValue * climateValue * multFact;
            //    }
            //}

            float extraNoiseScaleBasedOnClimate = GetExtraNoiseScaleBasedOnClimate(mx, my);

            // Extract height samples for all chunks
            float averageHeight = 0;
            float maxHeight = float.MinValue;
            float baseHeight, noiseHeight;
            float x1, x2, x3, x4;
            int dim = HeightmapDimension;
            //mapPixel.heightmapSamples = new float[dim, dim];
            for (int y = 0; y < dim; y++)
            {
                for (int x = 0; x < dim; x++)
                {
                    float rx = (float)x / div;
                    float ry = (float)y / div;
                    int ix = Mathf.FloorToInt(rx);
                    int iy = Mathf.FloorToInt(ry);
                    float sfracx = (float)x / (float)(dim - 1);
                    float sfracy = (float)y / (float)(dim - 1);
                    float fracx = (float)(x - ix * div) / div;
                    float fracy = (float)(y - iy * div) / div;
                    float scaledHeight = 0;

                    // Bicubic sample small height map for base terrain elevation
                    x1 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 3], baseHeightValue[1, 3], baseHeightValue[2, 3], baseHeightValue[3, 3], sfracx);
                    x2 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 2], baseHeightValue[1, 2], baseHeightValue[2, 2], baseHeightValue[3, 2], sfracx);
                    x3 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 1], baseHeightValue[1, 1], baseHeightValue[2, 1], baseHeightValue[3, 1], sfracx);
                    x4 = TerrainHelper.CubicInterpolator(baseHeightValue[0, 0], baseHeightValue[1, 0], baseHeightValue[2, 0], baseHeightValue[3, 0], sfracx);
                    baseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);
                    scaledHeight += baseHeight * baseHeightScale;

                    // Bicubic sample large height map for noise mask over terrain features
                    x1 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 0], lhm[ix + 1, iy + 0], lhm[ix + 2, iy + 0], lhm[ix + 3, iy + 0], fracx);
                    x2 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 1], lhm[ix + 1, iy + 1], lhm[ix + 2, iy + 1], lhm[ix + 3, iy + 1], fracx);
                    x3 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 2], lhm[ix + 1, iy + 2], lhm[ix + 2, iy + 2], lhm[ix + 3, iy + 2], fracx);
                    x4 = TerrainHelper.CubicInterpolator(lhm[ix, iy + 3], lhm[ix + 1, iy + 3], lhm[ix + 2, iy + 3], lhm[ix + 3, iy + 3], fracx);
                    noiseHeight = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, fracy);

                    x1 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 3], noiseHeightMultiplierMap[1, 3], noiseHeightMultiplierMap[2, 3], noiseHeightMultiplierMap[3, 3], sfracx);
                    x2 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 2], noiseHeightMultiplierMap[1, 2], noiseHeightMultiplierMap[2, 2], noiseHeightMultiplierMap[3, 2], sfracx);
                    x3 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 1], noiseHeightMultiplierMap[1, 1], noiseHeightMultiplierMap[2, 1], noiseHeightMultiplierMap[3, 1], sfracx);
                    x4 = TerrainHelper.CubicInterpolator(noiseHeightMultiplierMap[0, 0], noiseHeightMultiplierMap[1, 0], noiseHeightMultiplierMap[2, 0], noiseHeightMultiplierMap[3, 0], sfracx);
                    float noiseHeightMultiplier = TerrainHelper.CubicInterpolator(x1, x2, x3, x4, sfracy);

                    scaledHeight += noiseHeight * noiseHeightMultiplier;                  

                    // Additional noise mask for small terrain features at ground level
                    // small terrain features' height scale should depend on climate of map pixel
                    float extraNoiseScale = extraNoiseScaleBasedOnClimate;
                    // prevent seams between different climate map pixels
                    if (x <= 0 || y <= 0 || x >= dim - 1 || y >= dim - 1)
                    {
                        extraNoiseScale = defaultExtraNoiseScale;
                    }
                    int noisex = mapPixel.mapPixelX * (HeightmapDimension - 1) + x;
                    int noisey = (MapsFile.MaxMapPixelY - mapPixel.mapPixelY) * (HeightmapDimension - 1) + y;
                    float lowFreq = TerrainHelper.GetNoise(noisex, noisey, 0.3f, 0.5f, 0.5f, 1);
                    float highFreq = TerrainHelper.GetNoise(noisex, noisey, 0.9f, 0.5f, 0.5f, 1);
                    scaledHeight += (lowFreq * highFreq) * extraNoiseScale;

                    // Clamp lower values to ocean elevation
                    if (scaledHeight < scaledOceanElevation)
                        scaledHeight = scaledOceanElevation;

                    // Accumulate average height
                    averageHeight += scaledHeight;

                    // Get max height
                    if (scaledHeight > maxHeight)
                        maxHeight = scaledHeight;

                    // Set sample
                    float height = Mathf.Clamp01(scaledHeight / MaxTerrainHeight);
                    mapPixel.heightmapSamples[y, x] = height;
                }
            }

            // Average and max heights are passed back for locations
            mapPixel.averageHeight = (averageHeight /= (float)(dim * dim)) / MaxTerrainHeight;
            mapPixel.maxHeight = maxHeight / MaxTerrainHeight;

            //long totalTime = stopwatch.ElapsedMilliseconds - startTime;
            //DaggerfallUnity.LogMessage(string.Format("GenerateSamples took: {0}ms", totalTime), true);
        }

        private float GetNoiseMapScaleBasedOnClimate(int mapPixelX, int mapPixelY)
        {
            int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);
            float noiseMapScaleClimate = defaultNoiseMapScale;
            switch (worldClimate)
            {
                case (int)MapsFile.Climates.Ocean:
                    noiseMapScaleClimate = noiseMapScaleClimateOcean;
                    break;
                case (int)MapsFile.Climates.Desert:
                    noiseMapScaleClimate = noiseMapScaleClimateDesert;
                    break;
                case (int)MapsFile.Climates.Desert2:
                    noiseMapScaleClimate = noiseMapScaleClimateDesert2;
                    break;
                case (int)MapsFile.Climates.Mountain:
                    noiseMapScaleClimate = noiseMapScaleClimateMountain;
                    break;
                case (int)MapsFile.Climates.Rainforest:
                    noiseMapScaleClimate = noiseMapScaleClimateRainforest;
                    break;
                case (int)MapsFile.Climates.Swamp:
                    noiseMapScaleClimate = noiseMapScaleClimateSwamp;
                    break;
                case (int)MapsFile.Climates.Subtropical:
                    noiseMapScaleClimate = noiseMapScaleClimateSubtropical;
                    break;
                case (int)MapsFile.Climates.MountainWoods:
                    noiseMapScaleClimate = noiseMapScaleClimateMountainWoods;
                    break;
                case (int)MapsFile.Climates.Woodlands:
                    noiseMapScaleClimate = noiseMapScaleClimateWoodlands;
                    break;
                case (int)MapsFile.Climates.HauntedWoodlands:
                    noiseMapScaleClimate = noiseMapScaleClimateHauntedWoodlands;
                    break;
            }
            return noiseMapScaleClimate;
        }

        private float GetExtraNoiseScaleBasedOnClimate(int mapPixelX, int mapPixelY)
        {
            // small terrain features' height should depend on climate of map pixel
            float extraNoiseScale = defaultExtraNoiseScale;
            int worldClimate = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetClimateIndex(mapPixelX, mapPixelY);

            switch (worldClimate)
            {
                case (int)MapsFile.Climates.Ocean:
                    extraNoiseScale = extraNoiseScaleClimateOcean;
                    break;
                case (int)MapsFile.Climates.Desert:
                    extraNoiseScale = extraNoiseScaleClimateDesert;
                    break;
                case (int)MapsFile.Climates.Desert2:
                    extraNoiseScale = extraNoiseScaleClimateDesert2;
                    break;
                case (int)MapsFile.Climates.Mountain:
                    extraNoiseScale = extraNoiseScaleClimateMountain;
                    break;
                case (int)MapsFile.Climates.Rainforest:
                    extraNoiseScale = extraNoiseScaleClimateRainforest;
                    break;
                case (int)MapsFile.Climates.Swamp:
                    extraNoiseScale = extraNoiseScaleClimateSwamp;
                    break;
                case (int)MapsFile.Climates.Subtropical:
                    extraNoiseScale = extraNoiseScaleClimateSubtropical;
                    break;
                case (int)MapsFile.Climates.MountainWoods:                    
                    extraNoiseScale = extraNoiseScaleClimateMountainWoods;
                    break;
                case (int)MapsFile.Climates.Woodlands:                    
                    extraNoiseScale = extraNoiseScaleClimateWoodlands;
                    break;
                case (int)MapsFile.Climates.HauntedWoodlands:                    
                    extraNoiseScale = extraNoiseScaleClimateHauntedWoodlands;
                    break;
            }
            return extraNoiseScale;
        }
    }
}