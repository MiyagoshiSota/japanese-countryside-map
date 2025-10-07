    using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Tanada
{
    /// <summary>
    /// 条件に合う場所の地形を実際に変形させて棚田を生成します。
    /// </summary>
    public class TerraceDeformer : MonoBehaviour
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
        
        // ★★★ ここから追加 ★★★
        [Header("マスク出力設定")]
        [Tooltip("この傾斜より急な場所を「境」として描画します")]
        [SerializeField] private float borderSlopeThreshold = 0.005f;
        [Tooltip("出力するマスク画像のファイル名")]
        [SerializeField] private string outputMaskFileName = "PaddyFieldMask.png";
        [Tooltip("マスク画像の解像度")]
        [SerializeField] private int maskResolution = 1024;
        // ★★★ ここまで追加 ★★★

        // --- 内部データ ---
        private float[,] roadDistanceField;
        private float[,] originalHeightsBackup; // 地形を復元するためのバックアップ

        [ContextMenu("1. 地形を棚田に変形し、マスクを生成")]
        public void DeformAndGenerateMask()
        {
            if (targetTerrain == null || combinedMap == null)
            {
                Debug.LogError("TerrainまたはCombined Mapが設定されていません！");
                return;
            }

            TerrainData td = targetTerrain.terrainData;
            int heightmapRes = td.heightmapResolution;

            // 元の地形をバックアップ（初回のみ）
            if (originalHeightsBackup == null)
            {
                Debug.Log("初回実行のため、元の地形をバックアップします。");
                originalHeightsBackup = td.GetHeights(0, 0, heightmapRes, heightmapRes);
            }

            CalculateRoadDistanceField();
            
            float[,] processedHeights = CalculateProcessedHeightmap();

            // 地形に適用
            td.SetHeights(0, 0, processedHeights);
            Debug.Log("地形の変形が完了しました。");
            
            // ★★★ マスク生成処理を呼び出す ★★★
            GeneratePaddyMask(processedHeights);
        }
        
        [ContextMenu("2. バックアップから元の地形に復元")]
        public void RestoreOriginalHeights()
        {
            // (このメソッドは変更なし)
            if (originalHeightsBackup == null) { Debug.LogWarning("復元できる地形のバックアップがありません。"); return; }
            if (targetTerrain != null) { targetTerrain.terrainData.SetHeights(0, 0, originalHeightsBackup); Debug.Log("バックアップから元の地形に復元しました。"); }
        }

        // ★★★ ここから追加 ★★★
        /// <summary>
        /// 加工後のハイトマップを分析し、「平地」と「境」を色分けしたマスクを生成する
        /// </summary>
        private void GeneratePaddyMask(float[,] processedHeights)
        {
            TerrainData td = targetTerrain.terrainData;
            Texture2D vizMask = new Texture2D(maskResolution, maskResolution, TextureFormat.RGB24, false);
            Color[] pixels = new Color[maskResolution * maskResolution];
            int heightmapRes = processedHeights.GetLength(0);

            float[,] originalHeights = td.GetHeights(0, 0, heightmapRes, heightmapRes);

            for (int z = 0; z < maskResolution; z++)
            {
                for (int x = 0; x < maskResolution; x++)
                {
                    int hmX = (int)(((float)x / maskResolution) * heightmapRes);
                    int hmY = (int)(((float)z / maskResolution) * heightmapRes);
                
                    if (Mathf.Approximately(processedHeights[hmY, hmX], originalHeights[hmY, hmX]))
                    {
                        pixels[z * maskResolution + x] = Color.black; // 棚田でないエリア (R:0, G:0)
                        continue;
                    }

                    float currentH = processedHeights[hmY, hmX];
                    float rightH = processedHeights[hmY, Mathf.Min(hmX + 1, heightmapRes - 1)];
                    float downH = processedHeights[Mathf.Min(hmY + 1, heightmapRes - 1), hmX];
                    float localSlope = Mathf.Sqrt(Mathf.Pow(currentH - rightH, 2) + Mathf.Pow(currentH - downH, 2));

                    if (localSlope > borderSlopeThreshold)
                    {
                        pixels[z * maskResolution + x] = Color.green; // 「境」 (R:0, G:1)
                    }
                    else
                    {
                        pixels[z * maskResolution + x] = Color.red; // 「平地」 (R:1, G:0)
                    }
                }
            }

            vizMask.SetPixels(pixels);
            vizMask.Apply();
            byte[] bytes = vizMask.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, outputMaskFileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"棚田マスク画像を {path} に出力しました。");
            
            #if UNITY_EDITOR
            AssetDatabase.Refresh();
            #endif
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

            // (元スクリプトと同じなので内容は省略)
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { Color pixelColor = combinedMap.GetPixel(x, z); bool isRoad = ColorDistance(pixelColor, roadColor) < colorMatchThreshold; roadDistanceField[x, z] = isRoad ? 0 : float.MaxValue; roadPixels[x, z] = isRoad ? new Vector2(x, z) : Vector2.zero; }
            for (int step = res / 2; step > 0; step /= 2) { for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) { int nx = x + i * step; int nz = z + j * step; if (nx >= 0 && nx < res && nz >= 0 && nz < res) { float dist = Vector2.Distance(new Vector2(x, z), roadPixels[nx, nz]); if (dist < roadDistanceField[x, z]) { roadDistanceField[x, z] = dist; roadPixels[x, z] = roadPixels[nx, nz]; } } } }
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

                    int distFieldX = (int)(u * (roadDistanceField.GetLength(0) - 1));
                    int distFieldZ = (int)(v * (roadDistanceField.GetLength(1) - 1));
                    float distMeters = (roadDistanceField[distFieldX, distFieldZ] / roadDistanceField.GetLength(0)) * td.size.x;
                    float roadScore = CalculateScore(distMeters, minRoadDistance, optimalRoadDistance, maxRoadDistance);

                    float altitude = td.GetInterpolatedHeight(u, v) + terrainPos.y;
                    float altitudeScore = CalculateScore(altitude, minPaddyAltitude, optimalPaddyAltitude, maxPaddyAltitude);

                    float slope = td.GetSteepness(u, v);
                    float slopeScore = (slope >= minPaddySlope && slope <= maxPaddySlope) ? 1.0f : 0.0f;
                
                    float combinedScore = roadScore * altitudeScore * slopeScore;

                    // スコアが閾値を超えた場所のハイトマップを段々にする
                    if (combinedScore >= placementThreshold)
                    {
                        float currentHeight = originalHeights[z, x];
                        processedHeights[z, x] = Mathf.Round(currentHeight * terraceLevels) / terraceLevels;
                    }
                }
            }
            return processedHeights;
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