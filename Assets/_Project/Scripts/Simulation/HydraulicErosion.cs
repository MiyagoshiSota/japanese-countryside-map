using UnityEngine;
using System.IO;

public class HydraulicErosion : MonoBehaviour
{
    [Header("Erosion Settings")]
    [Tooltip("侵食シミュレーションを実行する雨粒の総数")]
    [SerializeField] private int numParticles = 70000;
    [Tooltip("各雨粒が地形を流れ落ちる最大ステップ数")]
    [SerializeField, Range(4, 128)] private int maxLifetime = 80;
    [Tooltip("水の慣性。0に近いほどまっすぐ流れ、1に近いほど地形に沿う")]
    [SerializeField, Range(0f, 1f)] private float inertia = 0.1f;
    [Tooltip("土砂の運搬許容量を決める係数")]
    [SerializeField] private float sedimentCapacityFactor = 3f;
    [Tooltip("土砂運搬許容量の最低値")]
    [SerializeField] private float minSedimentCapacity = 0.01f;
    [Tooltip("地形を削る速度")]
    [SerializeField, Range(0f, 1f)] private float erodeSpeed = 0.3f; // 強めに削る場合 0.3, 弱めなら 0.05
    [Tooltip("土砂を堆積させる速度")]
    [SerializeField, Range(0f, 1f)] private float depositSpeed = 0.15f;
    [Tooltip("水の蒸発率")]
    [SerializeField, Range(0f, 1f)] private float evaporateSpeed = 0.01f;
    [Tooltip("重力")]
    [SerializeField] private float gravity = 4f;

    [Header("Advanced Settings")]
    [Tooltip("侵食/堆積を適用する半径。大きいほど滑らかになる")]
    [SerializeField, Range(0, 8)] private int erosionRadius = 3;

    [Header("Export Settings")]
    [Tooltip("出力する侵食後のハイトマップ名")]
    [SerializeField] private string erodedHeightmapName = "ErodedHeightmap.png";
    [Tooltip("出力する侵食度マップ（フローマップ）名")]
    [SerializeField] private string flowMapExportFileName = "ErosionFlowMap.png";

    // --- Private Variables ---
    private Terrain terrain;
    private TerrainData terrainData;
    private float[,] heightMap;
    private int heightmapResolution;
    private float[,] erosionMap;
    private float maxErosion = 0f;

    [ContextMenu("Run Full Erosion (Save Heightmap & Flowmap)")]
    public void RunFullErosion()
    {
        RunSimulation(true);
    }

    [ContextMenu("Generate Flowmap Only")]
    public void GenerateFlowmapOnly()
    {
        RunSimulation(false);
    }

    private void RunSimulation(bool applyToTerrain)
    {
        terrain = GetComponent<Terrain>();
        if (terrain == null) { Debug.LogError("Terrainコンポーネントが見つかりません。"); return; }

        terrainData = terrain.terrainData;
        heightmapResolution = terrainData.heightmapResolution;
        
        heightMap = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
        
        erosionMap = new float[heightmapResolution, heightmapResolution];
        maxErosion = 0f;

        for (int i = 0; i < numParticles; i++)
        {
            if (i % 1000 == 0) {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayProgressBar("Erosion Simulation", $"Processing Particle {i}/{numParticles}", (float)i / numParticles);
                #endif
            }
            
            float posX = Random.Range(0, heightmapResolution - 1);
            float posY = Random.Range(0, heightmapResolution - 1);
            Vector2 direction = Vector2.zero;
            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            for (int lifetime = 0; lifetime < maxLifetime; lifetime++)
            {
                GradientInfo gradientInfo = CalculateHeightAndGradient(posX, posY);
                direction = (direction * inertia - gradientInfo.gradient * (1 - inertia)).normalized;
                speed = Mathf.Sqrt(speed * speed + gradientInfo.heightDifference * gravity);
                posX += direction.x;
                posY += direction.y;

                if (posX < 0 || posX >= heightmapResolution - 1 || posY < 0 || posY >= heightmapResolution - 1) break;

                float sedimentCapacity = Mathf.Max(-gradientInfo.heightDifference, minSedimentCapacity) * speed * water * sedimentCapacityFactor;

                if (sediment > sedimentCapacity || gradientInfo.heightDifference > 0)
                {
                    float amountToDeposit = (gradientInfo.heightDifference > 0) ? Mathf.Min(sediment, gradientInfo.heightDifference) : (sediment - sedimentCapacity) * depositSpeed;
                    sediment -= amountToDeposit;
                    if (applyToTerrain) {
                        DistributeHeight(posX, posY, amountToDeposit); // 堆積
                    }
                }
                else
                {
                    float amountToErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -gradientInfo.heightDifference);
                    sediment += amountToErode;
                    if (applyToTerrain) {
                        DistributeHeight(posX, posY, -amountToErode); // 侵食
                    }
                    RecordErosion(posX, posY, amountToErode);
                }
                water *= (1 - evaporateSpeed);
            }
        }
        
        if (applyToTerrain)
        {
            Debug.Log("Applying changes to terrain and saving heightmap...");
            terrainData.SetHeights(0, 0, heightMap);
            SaveHeightmapTexture(heightMap, erodedHeightmapName);
        }

        Debug.Log("Saving flow map...");
        SaveErosionFlowMapToFile();
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.ClearProgressBar();
        #endif
        Debug.Log("Simulation complete!");
    }
     
    private void DistributeHeight(float posX, float posY, float amount)
    {
        int x = (int)posX;
        int y = (int)posY;
        float cellOffsetX = posX - x;
        float cellOffsetY = posY - y;

        heightMap[y, x] += amount * (1 - cellOffsetX) * (1 - cellOffsetY);
        if (x + 1 < heightmapResolution) heightMap[y, x + 1] += amount * cellOffsetX * (1 - cellOffsetY);
        if (y + 1 < heightmapResolution) heightMap[y + 1, x] += amount * (1 - cellOffsetX) * cellOffsetY;
        if (x + 1 < heightmapResolution && y + 1 < heightmapResolution) heightMap[y + 1, x + 1] += amount * cellOffsetX * cellOffsetY;
    }

    private void SaveHeightmapTexture(float[,] map, string fileName)
    {
        Texture2D texture = new Texture2D(heightmapResolution, heightmapResolution);
        for (int y = 0; y < heightmapResolution; y++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                float h = map[y, x];
                texture.SetPixel(x, y, new Color(h, h, h, 1));
            }
        }
        texture.Apply();
        SaveTexture(texture, fileName);
    }
     
    private void SaveTexture(Texture2D texture, string fileName)
    {
        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"Saved texture to: {path}");
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
     
    private void RecordErosion(float posX, float posY, float amount)
    {
        int centerX = (int)posX;
        int centerY = (int)posY;

        for (int y = centerY - erosionRadius; y <= centerY + erosionRadius; y++)
        {
            for (int x = centerX - erosionRadius; x <= centerX + erosionRadius; x++)
            {
                if (x >= 0 && x < heightmapResolution && y >= 0 && y < heightmapResolution)
                {
                    float distance = new Vector2(x - posX, y - posY).magnitude;
                    if (distance <= erosionRadius)
                    {
                        float weight = 1 - (distance / erosionRadius);
                        float weightedAmount = amount * weight;
                        erosionMap[y, x] += weightedAmount;
                        
                        if (erosionMap[y, x] > maxErosion)
                        {
                            maxErosion = erosionMap[y, x];
                        }
                    }
                }
            }
        }
    }

    private void SaveErosionFlowMapToFile()
    {
        int width = heightmapResolution;
        int height = heightmapResolution;
        
        Texture2D texture = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float erosionValue = (maxErosion > 0) ? erosionMap[y, x] / maxErosion : 0f;
                texture.SetPixel(x, y, new Color(erosionValue, erosionValue, erosionValue, 1));
            }
        }
        
        texture.Apply();
        SaveTexture(texture, flowMapExportFileName);
    }
    
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

    private struct GradientInfo
    {
        public Vector2 gradient;
        public float heightDifference;
    }
}