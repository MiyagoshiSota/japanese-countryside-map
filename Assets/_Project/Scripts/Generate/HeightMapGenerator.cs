using UnityEngine;
using System.Collections.Generic;

// ★平坦なエリアの情報をまとめるためのクラス
[System.Serializable]
public class PlateauArea
{
    public string name; // インスペクタで分かりやすくするための名前
    public RectInt rect;
    [Range(0, 1)]
    public float height;
    public int falloff;
}

public class HeightMapGenerator : MonoBehaviour
{
    [Header("Map Dimensions")] public int mapWidth = 256;
    public int mapHeight = 256;

    [Header("Noise Settings")] public float noiseScale = 20f;
    public int octaves = 4;
    [Range(0, 1)] public float persistence = 0.5f;
    public float lacunarity = 2f;
    public Vector2 offset;

    [Header("Pathfinding & Road")] public bool generateRoad = true;
    public Vector2Int startPoint = new Vector2Int(10, 10);
    public Vector2Int endPoint = new Vector2Int(240, 240);
    [Tooltip("道の幅（ピクセル単位）")] public int roadWidth = 5; // ★幅が十分あるか確認！
    [Tooltip("道の端のなだらかさ")] public float roadShoulderFalloff = 2f;
    [Tooltip("傾斜に対するペナルティ係数。大きいほど坂を避ける。")] public float slopePenaltyMultiplier = 50f; // ★追加

    [Header("References")] public TerrainGenerator terrainGenerator;
    
    [Header("Plateau Settings")] // Plateau (高原) という名前で設定項目を追加
    [Tooltip("平坦なエリアを生成するかどうか")]
    public bool createPlateau = true;

    [Tooltip("平坦にするエリアの位置とサイズ")]
    public RectInt plateauArea = new RectInt(100, 100, 50, 50);

    [Tooltip("平坦なエリアの高さ (0-1の範囲)")]
    [Range(0, 1)]
    public float plateauHeight = 0.5f;

    [Tooltip("平坦なエリアの端をどのくらい滑らかにするか")]
    public int plateauFalloff = 10;
    
    [Header("Plateau Settings")]
    [Tooltip("生成する平坦なエリアのリスト")]
    public List<PlateauArea> plateauAreas; // ★単一の高原設定をリストに変更

    private void Start()
    {
        GenerateMap();
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Map")]
#endif
    void GenerateMap()
    {
        // 1. 基本的なハイトマップを生成
        float[,] heightMap = GenerateInitialHeightMap();
        
        foreach (PlateauArea area in plateauAreas)
        {
            for (int y = area.rect.yMin; y < area.rect.yMax; y++)
            {
                for (int x = area.rect.xMin; x < area.rect.xMax; x++)
                {
                    if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                    {
                        // 四方の境界線からの距離を計算
                        float distToEdgeX = Mathf.Min(x - area.rect.xMin, area.rect.xMax - 1 - x);
                        float distToEdgeY = Mathf.Min(y - area.rect.yMin, area.rect.yMax - 1 - y);
                        float closestDistToEdge = Mathf.Min(distToEdgeX, distToEdgeY);

                        // falloffの範囲内であれば、0-1のブレンド率を計算
                        float blendFactor = Mathf.Clamp01(closestDistToEdge / area.falloff);
                        
                        // 元の地形の高さと、目標の高さをブレンド
                        float originalHeight = heightMap[x, y];
                        heightMap[x, y] = Mathf.Lerp(area.height, originalHeight, blendFactor);
                    }
                }
            }
        }
        
        Texture2D roadMap = null;
        List<Vector2Int> roadPath = Pathfinder.FindPath(heightMap, startPoint, endPoint, slopePenaltyMultiplier);
        
        // 2. 道路を生成する場合
        if (generateRoad)
        {
            if (roadPath != null)
            {
                Debug.Log($"経路が見つかりました。長さ: {roadPath.Count} ノード");

                roadMap = new Texture2D(mapWidth, mapHeight);
                roadMap.wrapMode = TextureWrapMode.Clamp;
                Color[] roadMapColors = new Color[mapWidth * mapHeight];
                for (int i = 0; i < roadMapColors.Length; i++) roadMapColors[i] = Color.black;

                // --- ここからが修正された描画ループ ---
                foreach (Vector2Int pathPoint in roadPath)
                {
                    // 円形に描画するための半径を設定
                    int drawRadius = roadWidth + (int)roadShoulderFalloff + 1;
                    for (int x = -drawRadius; x <= drawRadius; x++)
                    {
                        for (int y = -drawRadius; y <= drawRadius; y++)
                        {
                            // 円形のブラシを実現するために、中心からの距離を計算
                            float distanceToCenter = Mathf.Sqrt(x * x + y * y);

                            int currentX = pathPoint.x + x;
                            int currentY = pathPoint.y + y;

                            // マップ範囲内かチェック
                            if (currentX >= 0 && currentX < mapWidth && currentY >= 0 && currentY < mapHeight)
                            {
                                // 道の中心からの距離に応じて平坦化と描画を行う
                                if (distanceToCenter <= roadWidth)
                                {
                                    // 道の中心部分（純粋な白）
                                    heightMap[currentX, currentY] = heightMap[pathPoint.x, pathPoint.y];
                                    roadMapColors[currentY * mapWidth + currentX] = Color.white;
                                }
                                else if (distanceToCenter <= roadWidth + roadShoulderFalloff)
                                {
                                    // 道の端のなだらかな部分（白から黒へのグラデーション）
                                    float blendFactor = (distanceToCenter - roadWidth) / roadShoulderFalloff;
                                    float roadHeight = heightMap[pathPoint.x, pathPoint.y];

                                    // この時点での高さを取得（元のハイトマップをコピーしておくとより正確）
                                    float originalHeight = heightMap[currentX, currentY];
                                    heightMap[currentX, currentY] = Mathf.Lerp(roadHeight, originalHeight, blendFactor);

                                    // 既に白い部分を上書きしないようにする
                                    if (roadMapColors[currentY * mapWidth + currentX].r < 1.0f - blendFactor)
                                    {
                                        roadMapColors[currentY * mapWidth + currentX] =
                                            Color.Lerp(Color.white, Color.black, blendFactor);
                                    }
                                }
                            }
                        }
                    }
                }

                roadMap.SetPixels(roadMapColors);
                roadMap.Apply();
            }
            else
            {
                Debug.LogWarning("経路が見つかりませんでした。");
            }
        }

#if UNITY_EDITOR
        if (roadMap != null)
        {
            byte[] bytes = roadMap.EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.dataPath + "/../Debug_RoadMap.png", bytes);
            Debug.Log("Debug_RoadMap.png をプロジェクトフォルダに保存しました。");
        }
#endif


        // 3. TerrainGeneratorに最終的なデータを渡す
        if (terrainGenerator != null)
        {
            terrainGenerator.GenerateTerrain(heightMap, roadMap, roadPath);         }
    }

    float[,] GenerateInitialHeightMap()
    {
        float[,] heightMap = new float[mapWidth, mapHeight];
        // ... (以前のハイトマップ生成ロジック) ...
        float maxPossibleHeight = 0;
        float amplitudeForNormalization = 1f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amplitudeForNormalization;
            amplitudeForNormalization *= persistence;
        }

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x / noiseScale) * frequency + offset.x;
                    float sampleY = (y / noiseScale) * frequency + offset.y;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                heightMap[x, y] = (noiseHeight + maxPossibleHeight) / (maxPossibleHeight * 2);
            }
        }

        return heightMap;
    }
}