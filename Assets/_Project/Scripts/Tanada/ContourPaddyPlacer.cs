using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Project.Scripts.Tanada
{
    public class TerraceDeformer : MonoBehaviour
    {
        [Header("必須設定")]
        [SerializeField] private UnityEngine.Terrain targetTerrain;
        [SerializeField] private Texture2D combinedMap;

        [Header("色と精度の設定")]
        [SerializeField] private Color roadColor = Color.yellow;
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
        [SerializeField, Range(0f, 1f)] private float placementThreshold = 0.3f;
        [SerializeField] private int terraceLevels = 150;
        
        [Header("マスク出力設定")]
        [SerializeField] private float borderSlopeThreshold = 0.005f;
        [SerializeField] private string outputMaskFileName = "PaddyFieldMask.png";
        [SerializeField] private int maskResolution = 1024;
        
        // ★★★ ここから追加 ★★★
        [Header("マスクの隙間埋め設定")]
        [Tooltip("有効にすると、マスク内の小さな黒い隙間を自動で埋めます")]
        [SerializeField] private bool enableHoleFilling = true;
        [Tooltip("隙間を判定する際の調査範囲（ピクセル数）。大きいほど広い隙間を埋めます")]
        [SerializeField, Range(1, 5)] private int holeFillRadius = 2;
        [Tooltip("調査範囲内の何割が棚田なら「隙間」と見なすか。大きいほど隙間が埋まりにくくなります")]
        [SerializeField, Range(0.1f, 1.0f)] private float holeFillThreshold = 0.6f;
        // ★★★ ここまで追加 ★★★

        private float[,] roadDistanceField;
        private float[,] originalHeightsBackup;

        [ContextMenu("1. 地形を棚田に変形し、マスクを生成")]
        public void DeformAndGenerateMask()
        {
            if (targetTerrain == null || combinedMap == null) { Debug.LogError("TerrainまたはCombined Mapが設定されていません！"); return; }

            TerrainData td = targetTerrain.terrainData;
            int heightmapRes = td.heightmapResolution;
            
            if (originalHeightsBackup == null) { Debug.Log("初回実行のため、元の地形をバックアップします。"); originalHeightsBackup = td.GetHeights(0, 0, heightmapRes, heightmapRes); }

            CalculateRoadDistanceField();
            float[,] processedHeights = CalculateProcessedHeightmap();
            td.SetHeights(0, 0, processedHeights);
            Debug.Log("地形の変形が完了しました。");
            
            GeneratePaddyMask(processedHeights);
        }
        
        [ContextMenu("2. バックアップから元の地形に復元")]
        public void RestoreOriginalHeights() { /* 変更なし */ }

        private void GeneratePaddyMask(float[,] processedHeights)
        {
            Texture2D vizMask = new Texture2D(maskResolution, maskResolution, TextureFormat.RGB24, false);
            Color[] pixels = new Color[maskResolution * maskResolution];
            int heightmapRes = processedHeights.GetLength(0);

            float[,] originalHeights = targetTerrain.terrainData.GetHeights(0, 0, heightmapRes, heightmapRes);

            for (int z = 0; z < maskResolution; z++)
            {
                for (int x = 0; x < maskResolution; x++)
                {
                    int hmX = (int)(((float)x / maskResolution) * heightmapRes);
                    int hmY = (int)(((float)z / maskResolution) * heightmapRes);
                
                    // この地点が変形されたかどうかで判定
                    if (Mathf.Approximately(processedHeights[hmY, hmX], originalHeights[hmY, hmX]))
                    {
                        pixels[z * maskResolution + x] = Color.black;
                        continue;
                    }

                    float currentH = processedHeights[hmY, hmX];
                    float rightH = processedHeights[hmY, Mathf.Min(hmX + 1, heightmapRes - 1)];
                    float downH = processedHeights[Mathf.Min(hmY + 1, heightmapRes - 1), hmX];
                    float localSlope = Mathf.Sqrt(Mathf.Pow(currentH - rightH, 2) + Mathf.Pow(currentH - downH, 2));

                    if (localSlope > borderSlopeThreshold) pixels[z * maskResolution + x] = Color.green;
                    else pixels[z * maskResolution + x] = Color.red;
                }
            }

            // ★★★ 隙間を埋める後処理を呼び出す ★★★
            if (enableHoleFilling)
            {
                pixels = FillMaskHoles(pixels, maskResolution, maskResolution, holeFillRadius, holeFillThreshold);
                Debug.Log("マスクの隙間埋め処理を実行しました。");
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
        
        // ★★★ ここから追加 ★★★
        /// <summary>
        /// マスク内の黒いピクセル（隙間）を、周囲が棚田であれば塗りつぶす
        /// </summary>
        private Color[] FillMaskHoles(Color[] pixels, int width, int height, int radius, float threshold)
        {
            Color[] originalPixels = (Color[])pixels.Clone(); // 読み取り用に元のピクセル情報をコピー
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 現在のピクセルが黒でなければ処理不要
                    if (originalPixels[y * width + x] != Color.black) continue;

                    int neighborCount = 0;
                    int paddyNeighborCount = 0;

                    // 指定された半径（radius）の範囲で周囲のピクセルを調査
                    for (int j = -radius; j <= radius; j++)
                    {
                        for (int i = -radius; i <= radius; i++)
                        {
                            int nx = x + i;
                            int ny = y + j;

                            // 画像の範囲内かチェック
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                neighborCount++;
                                // 周囲のピクセルが黒でなければ（赤か緑なら）棚田と見なす
                                if (originalPixels[ny * width + nx] != Color.black)
                                {
                                    paddyNeighborCount++;
                                }
                            }
                        }
                    }

                    // 周囲のピクセルに占める棚田の割合を計算
                    float paddyRatio = (float)paddyNeighborCount / neighborCount;

                    // 割合がしきい値を超えていれば、この黒いピクセルを「隙間」と判断し赤で塗りつぶす
                    if (paddyRatio >= threshold)
                    {
                        pixels[y * width + x] = Color.red; // 平地として塗りつぶす
                    }
                }
            }
            return pixels;
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