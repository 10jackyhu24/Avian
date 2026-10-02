using UnityEngine;

public static class WorldBiomeUtility {
    private const float DesertThreshold = 0.38f;
    private const float GrasslandThreshold = 0.64f;
    private const float RainforestMoistureThreshold = 0.55f;

    public static bool IsWorldPositionInBounds(Vector3 worldPosition, int worldSize) {
        int gridX = Mathf.RoundToInt(worldPosition.x) + worldSize / 2;
        int gridZ = Mathf.RoundToInt(worldPosition.z) + worldSize / 2;
        return gridX >= 0 && gridX < worldSize && gridZ >= 0 && gridZ < worldSize;
    }

    public static TerrainBiomeMask GetBiomeAtWorldPosition(
        int seed,
        Vector3 worldPosition,
        int worldSize,
        float biomeScale
    ) {
        int gridX = Mathf.Clamp(Mathf.RoundToInt(worldPosition.x) + worldSize / 2, 0, worldSize - 1);
        int gridZ = Mathf.Clamp(Mathf.RoundToInt(worldPosition.z) + worldSize / 2, 0, worldSize - 1);

        float biomeNoise = Mathf.PerlinNoise(
            (gridX + seed) * biomeScale,
            (gridZ + seed) * biomeScale
        );
        float moistureNoise = Mathf.PerlinNoise(
            (gridX + seed * 2) * (biomeScale * 1.5f),
            (gridZ + seed * 2) * (biomeScale * 1.5f)
        );

        return GetBiomeMask(GetBiomeIndex(biomeNoise, moistureNoise));
    }

    private static int GetBiomeIndex(float h, float m) {
        if (h < DesertThreshold) return 2;
        if (h < GrasslandThreshold) return 3;
        if (m >= RainforestMoistureThreshold) return 4;
        return 5;
    }

    private static TerrainBiomeMask GetBiomeMask(int biomeIndex) {
        return biomeIndex switch {
            0 => TerrainBiomeMask.DeepOcean,
            1 => TerrainBiomeMask.ShallowWater,
            2 => TerrainBiomeMask.Desert,
            3 => TerrainBiomeMask.Grassland,
            4 => TerrainBiomeMask.Rainforest,
            _ => TerrainBiomeMask.Mountain
        };
    }
}
