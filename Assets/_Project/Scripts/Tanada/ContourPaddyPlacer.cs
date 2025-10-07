using System.IO;
using UnityEngine;

namespace _Project.Scripts.Tanada
{
    /// <summary>
    /// 【可視化版】条件に合う場所に生成される棚田の「平地」と「境」を
    /// 色分けしたマスク画像を生成します。地形は変更しません。
    /// </summary>
    public class TerraceVisualizer : MonoBehaviour
    {
        [Header("必須設定")]
        [SerializeField] private UnityEngine.Terrain targetTerrain;
        [Tooltip("道(黄)、川(青)、等高線(白)が含まれるマップ。インポート設定で「Read/Write Enabled」にしてください")]
        [SerializeField] private Texture2D combinedMap;

        [Header("色と精度の設定")]
        [SerializeField] private Color roadColor = Color.yellow;
        [Tooltip("指定色とどのくらい近い色までを対象と見なすか")]
        [SerializeField, Range(0f, 1f)] private float colorMatchThreshold = 0.3f;

        [Header("1. 道路からの距離の条件")]
        [SerializeField] private float minRoadDistance = 10f;
        [SerializeField] private float optimalRoadDistance = 30f;
        [SerializeField] private float maxRoadDistance = 100f;

        [Header("2. 標高（中腹）の条件")]
        [SerializeField] private float minPaddyAltitude = 50f;
        [SerializeField] private float optimalPaddyAltitude = 100f;
        [SerializeField] private float maxPaddyAltitude = 150f;

        [Header("3. 傾斜の条件")]
        [SerializeField] private float minPaddySlope = 10f;
        [SerializeField] private float maxPaddySlope = 45f;

        [Header("棚田の形状設定")]
        [Tooltip("この総合スコア以上の場所に棚田を生成します")]
        [SerializeField, Range(0f, 1f)] private float placementThreshold = 0.3f;
        [Tooltip("生成する段の総数。大きいほど細かい段になる")]
        [SerializeField] private int terraceLevels = 150;

        [Header("可視化設定")]
        [Tooltip("この傾斜より急な場所を「境」としてグレーで描画します")]
        [SerializeField] private float borderSlopeThreshold = 0.005f;
        [SerializeField] private string outputMaskFileName = "TerraceVisualization.png";
        [SerializeField] private int maskResolution = 1024;

        private float[,] roadDistanceField;

        [ContextMenu("Generate Terrace Visualization Mask")]
        public void Generate()
        {
            if (targetTerrain == null || combinedMap == null)
            {
                Debug.LogError("TerrainまたはCombined Mapが設定されていません！");
                return;
            }

            CalculateRoadDistanceField();

            // ステップ1: 加工後のハイトマップをメモリ上で計算する
            float[,] processedHeights = CalculateProcessedHeightmap();

            // ステップ2: 計算したハイトマップを分析して可視化マスクを生成する
            GenerateVisualizationMask(processedHeights);
        }

        /// <summary>
        /// マップ画像を元に、各ピクセルから最も近い道路までの距離を計算する
        /// </summary>
        private void CalculateRoadDistanceField()
        {
            Debug.Log("道路距離マップの計算を開始します...");
            int res = combinedMap.width;
            roadDistanceField = new float[res, res];
            Vector2[,] roadPixels = new Vector2[res, res];

            // パス1: 初期化
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    Color pixelColor = combinedMap.GetPixel(x, z);
                    bool isRoad = ColorDistance(pixelColor, roadColor) < colorMatchThreshold;
                    roadDistanceField[x, z] = isRoad ? 0 : float.MaxValue;
                    roadPixels[x, z] = isRoad ? new Vector2(x, z) : Vector2.zero;
                }
            }

            // パス2 & 3: 高速な距離計算（Jump Floodingの簡易版）
            for (int step = res / 2; step > 0; step /= 2)
            {
                for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int nx = x + i * step;
                    int nz = z + j * step;
                    if (nx >= 0 && nx < res && nz >= 0 && nz < res)
                    {
                        float dist = Vector2.Distance(new Vector2(x, z), roadPixels[nx, nz]);
                        if (dist < roadDistanceField[x, z])
                        {
                            roadDistanceField[x, z] = dist;
                            roadPixels[x, z] = roadPixels[nx, nz];
                        }
                    }
                }
            }
            Debug.Log("道路距離マップの計算が完了しました。");
        }

        /// <summary>
        /// 条件に基づいて加工した場合のハイトマップを計算して返す
        /// </summary>
        private float[,] CalculateProcessedHeightmap()
        {
            TerrainData td = targetTerrain.terrainData;
            Vector3 terrainPos = targetTerrain.transform.position;
            int heightmapRes = td.heightmapResolution;

            float[,] originalHeights = td.GetHeights(0, 0, heightmapRes, heightmapRes);
            float[,] processedHeights = (float[,])originalHeights.Clone();

            for (int z = 0; z < heightmapRes; z++)
            {
                for (int x = 0; x < heightmapRes; x++)
                {
                    float u = (float)x / heightmapRes;
                    float v = (float)z / heightmapRes;
                
                    // --- 川エリアの除外はしない ---

                    // --- 各スコアの計算 ---
                    int distFieldX = (int)(u * (roadDistanceField.GetLength(0) - 1));
                    int distFieldZ = (int)(v * (roadDistanceField.GetLength(1) - 1));
                    float distMeters = (roadDistanceField[distFieldX, distFieldZ] / roadDistanceField.GetLength(0)) * td.size.x;
                    float roadScore = CalculateScore(distMeters, minRoadDistance, optimalRoadDistance, maxRoadDistance);

                    float altitude = td.GetInterpolatedHeight(u, v) + terrainPos.y;
                    float altitudeScore = CalculateScore(altitude, minPaddyAltitude, optimalPaddyAltitude, maxPaddyAltitude);

                    float slope = td.GetSteepness(u, v);
                    float slopeScore = (slope >= minPaddySlope && slope <= maxPaddySlope) ? 1.0f : 0.0f;
                
                    float combinedScore = roadScore * altitudeScore * slopeScore;

                    if (combinedScore >= placementThreshold)
                    {
                        float currentHeight = originalHeights[z, x];
                        processedHeights[z, x] = Mathf.Round(currentHeight * terraceLevels) / terraceLevels;
                    }
                }
            }
            return processedHeights;
        }
    
        /// <summary>
        /// 加工後のハイトマップを分析し、「段」と「境」を色分けしたマスクを生成する
        /// </summary>
        private void GenerateVisualizationMask(float[,] processedHeights)
        {
            TerrainData td = targetTerrain.terrainData;
            Texture2D vizMask = new Texture2D(maskResolution, maskResolution, TextureFormat.RGB24, false);
            Color[] pixels = new Color[maskResolution * maskResolution];
            int heightmapRes = processedHeights.GetLength(0);

            // 元のハイトマップを取得（比較用）
            float[,] originalHeights = td.GetHeights(0, 0, heightmapRes, heightmapRes);

            for (int z = 0; z < maskResolution; z++)
            {
                for (int x = 0; x < maskResolution; x++)
                {
                    int hmX = (int)(((float)x / maskResolution) * heightmapRes);
                    int hmY = (int)(((float)z / maskResolution) * heightmapRes);
                
                    // 加工されていないエリアは黒にする
                    if (Mathf.Approximately(processedHeights[hmY, hmX], originalHeights[hmY, hmX]))
                    {
                        pixels[z * maskResolution + x] = Color.black;
                        continue;
                    }

                    // 隣接ピクセルとの高さの差（傾斜）を計算
                    float currentH = processedHeights[hmY, hmX];
                    float rightH = processedHeights[hmY, Mathf.Min(hmX + 1, heightmapRes - 1)];
                    float downH = processedHeights[Mathf.Min(hmY + 1, heightmapRes - 1), hmX];
                
                    // ハイトマップの傾斜を計算。値のスケールが小さいので閾値も小さくなる
                    float localSlope = Mathf.Sqrt(Mathf.Pow(currentH - rightH, 2) + Mathf.Pow(currentH - downH, 2));

                    // 傾斜に応じて色分け
                    if (localSlope > borderSlopeThreshold)
                    {
                        pixels[z * maskResolution + x] = Color.gray; // 境
                    }
                    else
                    {
                        pixels[z * maskResolution + x] = Color.white; // 平地
                    }
                }
            }

            vizMask.SetPixels(pixels);
            vizMask.Apply();
            byte[] bytes = vizMask.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, outputMaskFileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"棚田可視化マスク画像を {path} に出力しました。");
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    
        // スコア計算用のヘルパー関数
        private float CalculateScore(float value, float min, float optimal, float max)
        {
            if (value < min || value > max) return 0f;
            float distFromOptimal = Mathf.Abs(value - optimal);
            float range = (value < optimal) ? (optimal - min) : (max - optimal);
            if (range <= 0) return 1f;
            return Mathf.Clamp01(1.0f - distFromOptimal / range);
        }
    
        // 色の近似度を判定するヘルパー関数
        private float ColorDistance(Color c1, Color c2)
        {
            float r = c1.r - c2.r;
            float g = c1.g - c2.g;
            float b = c1.b - c2.b;
            return Mathf.Sqrt(r * r + g * g + b * b);
        }
    }
}