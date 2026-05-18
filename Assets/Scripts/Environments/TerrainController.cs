using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainController : MonoBehaviour {
    [Header("Basic settings")]
    public GameObject cubePrefab; // 拖入你剛做好的 Cube Prefab
    public int size = 50;         // 地圖大小 (50x50)
    public float scale = 0.05f;   // 縮放比例 (相當於之前的 Zoom)
    public float heightMultiplier = 10f; // 讓高山凸起的倍率 (3D 模式使用)

    [Header("2D Map settings")]
    public bool is2DMap = true;           // 是否使用 2D 地圖模式
    public float wallHeight = 1.5f;       // 牆壁的高度 (不要太高)
    public float waterDepth = 0.2f;       // 水深 (往下微凹的高度，設為 0 則與地面同高)


    [Header("Noise settings")]
    public float offsetX = 0f;
    public float offsetY = 0f;

    // 顏色定義
    private Color deepOcean = new Color(0f, 0.2f, 0.4f);      // 深藍
    private Color shallowWater = new Color(0.2f, 0.4f, 1f);   // 淺藍
    private Color desert = new Color(0.9f, 0.8f, 0.5f);       // 沙漠黃
    private Color grassland = new Color(0.3f, 1f, 0.3f);      // 草地綠
    private Color rainforest = new Color(0f, 0.5f, 0f);       // 雨林深綠
    private Color mountain = new Color(0.4f, 0.2f, 0f);       // 高山棕
    private Color snowPeak = Color.white;                     // 雪山白

    public void GenerateWorld(int seed) {
        // 自動尋找或創建名為 "Terrain" 的父物件
        GameObject terrainParent = GameObject.Find("Terrain");
        if (terrainParent == null) {
            terrainParent = new GameObject("Terrain");
        }

        // 雙層迴圈生成 X 與 Z 軸的地形 (Unity 中 3D 平面通常是 X 和 Z)
        for (int x = 0; x < size; x++) {
            for (int z = 0; z < size; z++) {
                // 計算高度與濕度雜訊 (疊加兩層 Octaves 簡化版)
                float h = CalculateNoise(x, z, seed, scale);
                float m = CalculateNoise(x, z, seed + 100, scale * 1.5f);

                // 決定生態域與顏色
                Color biomeColor = GetBiomeColor(h, m);

                // 決定 Y 軸高度與 Y 軸縮放 (2D 與 3D 模式兼容)
                float yPos = 0f;
                float yScale = 1f;

                if (is2DMap) {
                    if (h > 0.65f) { // 山脈與雪山作為牆壁
                        yScale = wallHeight;
                        yPos = (wallHeight - 1f) / 2f;
                    } else { // 其它所有地板 (水域、沙漠、草地、雨林) 高度皆相同
                        yPos = 0f;
                        yScale = 1f;
                    }
                } else {
                    // 原本的 3D 地形高度起伏
                    if (h >= 0.4f) {
                        yPos = (h - 0.4f) * heightMultiplier; 
                    }
                }

                // 實例化方塊
                Vector3 position = new Vector3(x, yPos, z);
                GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);
                
                // 設定縮放 (如果是 2D 模式的牆壁或水域，調整 Y 軸縮放以完美貼地)
                newCube.transform.localScale = new Vector3(1f, yScale, 1f);

                // 設定父物件為 Terrain
                newCube.transform.parent = terrainParent.transform;

                // 改變方塊顏色
                newCube.GetComponent<Renderer>().material.color = biomeColor;
            }
        }
    }

    // 計算分形雜訊 (Fractal Noise)
    float CalculateNoise(int x, int z, int currentSeed, float currentScale)
    {
        // 加入偏移量與 Seed 讓每次生成不同
        float xCoord = (x + offsetX + currentSeed) * currentScale;
        float zCoord = (z + offsetY + currentSeed) * currentScale;

        // Unity 內建的柏林雜訊回傳值為 0.0 ~ 1.0 之間
        float noiseValue = Mathf.PerlinNoise(xCoord, zCoord);
        return noiseValue;
    }

    // 判斷生態域邏輯 (移植自 Python 版本)
    Color GetBiomeColor(float h, float m)
    {
        if (h < 0.4f)
        {
            return (h < 0.25f) ? deepOcean : shallowWater;
        }
        else if (h > 0.65f)
        {
            return (m > 0.55f) ? snowPeak : mountain;
        }
        else
        {
            if (m < 0.35f) return desert;
            else if (m < 0.65f) return grassland;
            else return rainforest;
        }
    }
}
