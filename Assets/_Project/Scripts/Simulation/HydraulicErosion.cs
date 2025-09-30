using UnityEngine;
using System.IO;

/// <summary>
/// Terrainにアタッチして、水力侵食シミュレーションに基づいたフローマップ（侵食度マップ）を生成します。
/// 元のTerrainの高さは変更しません。
/// </summary>
public class HydraulicErosion : MonoBehaviour
{
    [Header("Erosion Settings")]
    [Tooltip("侵食シミュレーションを実行する雨粒の総数")]
    [SerializeField] private int numParticles = 70000;
    
    [Tooltip("各雨粒が地形を流れ落ちる最大ステップ数")]
    [SerializeField, Range(4, 128)] private int maxLifetime = 80; // 棘山対策: 値を増やす

    [Tooltip("水の慣性。0に近いほどまっすぐ流れ、1に近いほど地形に沿う")]
    [SerializeField, Range(0f, 1f)] private float inertia = 0.1f;

    [Tooltip("土砂の運搬許容量を決める係数")]
    [SerializeField] private float sedimentCapacityFactor = 3f; // 棘山対策: 値を少し減らす

    [Tooltip("土砂運搬許容量の最低値")]
    [SerializeField] private float minSedimentCapacity = 0.01f;

    [Tooltip("地形を削る速度")]
    [SerializeField, Range(0f, 1f)] private float erodeSpeed = 0.1f; // 棘山対策: 値を減らす
    
    [Tooltip("土砂を堆積させる速度")]
    [SerializeField, Range(0f, 1f)] private float depositSpeed = 0.2f; // 棘山対策: 値を増やす

    [Tooltip("水の蒸発率")]
    [SerializeField, Range(0f, 1f)] private float evaporateSpeed = 0.005f; // 棘山対策: 値を減らす

    [Tooltip("重力")]
    [SerializeField] private float gravity = 4f;

    [Header("Advanced Settings")]
    [Tooltip("侵食/堆積を適用する半径。1以上で結果が滑らかになる")]
    [SerializeField, Range(0, 8)] private int erosionRadius = 3; // 棘山対策: 半径を導入

    [Header("Export Settings")]
    [Tooltip("出力する侵食度マップ画像ファイル名 (黒が侵食なし、白が侵食大)")]
    [SerializeField] private string flowMapExportFileName = "ErosionFlowMap.png";

    // --- Private Variables ---
    private Terrain terrain;
    private TerrainData terrainData;
    private float[,] heightMap;
    private int heightmapResolution;
    private float[,] erosionMap;
    private float maxErosion = 0f;

    /// <summary>
    /// インスペクターのコンテキストメニューから実行するメイン関数
    /// </summary>
    [ContextMenu("Generate Erosion Flow Map")]
    public void GenerateFlowMap()
    {
        terrain = GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("Terrainコンポーネントが見つかりません。Terrainにアタッチしてください。");
            return;
        }

        terrainData = terrain.terrainData;
        heightmapResolution = terrainData.heightmapResolution;
        
        // 元の地形データを読み込む
        heightMap = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
        
        // 侵食度マップを初期化
        erosionMap = new float[heightmapResolution, heightmapResolution];
        maxErosion = 0f;

        // 指定された回数だけ雨粒シミュレーションを繰り返す
        for (int i = 0; i < numParticles; i++)
        {
            if (i % 1000 == 0)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayProgressBar("Erosion Simulation", $"Processing Particle {i}/{numParticles}", (float)i / numParticles);
                #endif
            }
            
            // 雨粒の初期化
            float posX = Random.Range(0, heightmapResolution - 1);
            float posY = Random.Range(0, heightmapResolution - 1);
            Vector2 direction = Vector2.zero;
            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            // 雨粒の寿命までループ
            for (int lifetime = 0; lifetime < maxLifetime; lifetime++)
            {
                // 現在地の高さと傾斜を計算
                GradientInfo gradientInfo = CalculateHeightAndGradient(posX, posY);

                // 慣性を考慮して進行方向を更新
                direction = (direction * inertia - gradientInfo.gradient * (1 - inertia)).normalized;
                
                // 速度を更新
                speed = Mathf.Sqrt(speed * speed + gradientInfo.heightDifference * gravity);
                
                // 位置を更新
                posX += direction.x;
                posY += direction.y;

                // 位置更新の直後に範囲外チェック
                if (posX < 0 || posX >= heightmapResolution - 1 || posY < 0 || posY >= heightmapResolution - 1)
                {
                    break;
                }

                // 土砂運搬許容量を計算
                float sedimentCapacity = Mathf.Max(-gradientInfo.heightDifference, minSedimentCapacity) * speed * water * sedimentCapacityFactor;

                // 侵食または堆積
                if (sediment > sedimentCapacity || gradientInfo.heightDifference > 0)
                {
                    // 堆積
                    float amountToDeposit = (gradientInfo.heightDifference > 0)
                        ? Mathf.Min(sediment, gradientInfo.heightDifference)
                        : (sediment - sedimentCapacity) * depositSpeed;
                    sediment -= amountToDeposit;
                    // このモードでは地形を変更しない
                }
                else
                {
                    // 侵食
                    float amountToErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -gradientInfo.heightDifference);
                    sediment += amountToErode;
                    
                    // 侵食量をマップに記録
                    RecordErosion(posX, posY, amountToErode);
                }

                // 水を蒸発させる
                water *= (1 - evaporateSpeed);
            }
        }

        // 侵食度マップを画像として保存
        SaveErosionFlowMapToFile();
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.ClearProgressBar();
        #endif
        Debug.Log("Erosion Flow Map generation complete!");
    }

    /// <summary>
    /// 各地点での侵食量をerosionMapに記録する (半径適用版)
    /// </summary>
    private void RecordErosion(float posX, float posY, float amount)
    {
        int centerX = (int)posX;
        int centerY = (int)posY;

        // erosionRadiusの範囲でループ
        for (int y = centerY - erosionRadius; y <= centerY + erosionRadius; y++)
        {
            for (int x = centerX - erosionRadius; x <= centerX + erosionRadius; x++)
            {
                // 範囲外チェック
                if (x >= 0 && x < heightmapResolution && y >= 0 && y < heightmapResolution)
                {
                    // 中心からの距離に応じて重みを計算 (中心が一番強く、離れるほど弱く)
                    float distance = new Vector2(x - posX, y - posY).magnitude;
                    if (distance <= erosionRadius)
                    {
                        float weight = 1 - (distance / erosionRadius); // 線形減衰
                        float weightedAmount = amount * weight;
                        erosionMap[y, x] += weightedAmount;
                        
                        // 最大侵食量を更新（正規化のため）
                        if (erosionMap[y, x] > maxErosion)
                        {
                            maxErosion = erosionMap[y, x];
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 侵食度マップをPNG画像として保存する
    /// </summary>
    private void SaveErosionFlowMapToFile()
    {
        int width = heightmapResolution;
        int height = heightmapResolution;
        
        Texture2D texture = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 侵食量を0〜1に正規化
                float erosionValue = (maxErosion > 0) ? erosionMap[y, x] / maxErosion : 0f;
                texture.SetPixel(x, y, new Color(erosionValue, erosionValue, erosionValue, 1));
            }
        }
        
        texture.Apply();

        byte[] bytes = texture.EncodeToPNG();
        
        string path = Path.Combine(Application.dataPath, flowMapExportFileName);
        
        File.WriteAllBytes(path, bytes);

        Debug.Log($"Erosion Flow Map saved to: {path}");
        
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }

    /// <summary>
    /// 指定した座標の高さと傾斜を計算する
    /// </summary>
    private GradientInfo CalculateHeightAndGradient(float posX, float posY)
    {
        int x = (int)posX;
        int y = (int)posY;
        
        float cellOffsetX = posX - x;
        float cellOffsetY = posY - y;

        float h00 = heightMap[y, x];
        float h10 = (x + 1 < heightmapResolution) ? heightMap[y, x + 1] : h00;
        float h01 = (y + 1 < heightmapResolution) ? heightMap[y + 1, x] : h00;
        float h11 = (x + 1 < heightmapResolution && y + 1 < heightmapResolution) ? heightMap[y + 1, x + 1] : h00;
        
        float height = (h00 * (1 - cellOffsetX) + h10 * cellOffsetX) * (1 - cellOffsetY) +
                       (h01 * (1 - cellOffsetX) + h11 * cellOffsetX) * cellOffsetY;

        float gradientX = h10 - h00;
        float gradientY = h01 - h00;
        
        GradientInfo info;
        info.gradient = new Vector2(gradientX, gradientY);
        info.heightDifference = height - (h00 + h10 + h01 + h11) / 4f;

        return info;
    }
    
    /// <summary>
    /// 高さ・傾斜情報を格納する構造体
    /// </summary>
    private struct GradientInfo
    {
        public Vector2 gradient;
        public float heightDifference;
    }
}