using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 地形データ（傾斜、川、道路）を元に、集落の建設適性マップを生成するスクリプト。
/// 通常の適性マップと、道路沿いを優先するマップの2種類を生成可能。
/// </summary>
public class PlacementGenerator : MonoBehaviour
{
    [Header("Input Files")]
    [Tooltip("傾斜マップ画像")]
    [SerializeField] private string slopeMapFileName = "SlopeMap.png";
    [Tooltip("道路マップ画像")]
    [SerializeField] private string roadMapFileName = "RoadMap.png";
    [Tooltip("リバーマスク画像")]
    [SerializeField] private string riverMaskFileName = "FinalRiverMask.png";

    [Header("Output Files")]
    [Tooltip("出力する集落適性マップ画像")]
    [SerializeField] private string settlementSuitabilityMapName = "SettlementSuitabilityMap.png";
    [Tooltip("出力する道路沿い適性マップ画像")]
    [SerializeField] private string roadsideSuitabilityMapName = "RoadsideSuitabilityMap.png";

    [Header("Suitability Parameters")]
    [Tooltip("集落を建設できる最大傾斜。これより急な場所は0点になる")]
    [SerializeField, Range(0f, 1f)] private float maxSlope = 0.1f;
    [Tooltip("道路からこの距離以内にあるとボーナス点")]
    [SerializeField] private int roadProximity = 20;
    [Tooltip("川からこの距離以内にあるとボーナス点")]
    [SerializeField] private int riverProximity = 30;

    [Tooltip("道路に近いことの重要度（ボーナス点）")]
    [SerializeField] private float roadBonus = 1.5f;
    [Tooltip("川に近いことの重要度（ボーナス点）")]
    [SerializeField] private float riverBonus = 1.0f;

    private int width;
    private int height;

    [ContextMenu("Generate Settlement Suitability Map")]
    public void GenerateSettlementMap()
    {
        GenerateMap(false);
    }

    [ContextMenu("Generate Roadside Suitability Map")]
    public void GenerateRoadsideMap()
    {
        GenerateMap(true);
    }
    
    private void GenerateMap(bool roadsidePriority)
    {
        Texture2D slopeMap = LoadTexture(slopeMapFileName);
        Texture2D roadMap = LoadTexture(roadMapFileName);
        Texture2D riverMask = LoadTexture(riverMaskFileName);
        if (slopeMap == null || roadMap == null || riverMask == null) return;

        width = slopeMap.width; height = slopeMap.height;

        Debug.Log("Generating distance fields...");
        float[,] roadDistanceMap = GenerateDistanceField(roadMap);
        float[,] riverDistanceMap = GenerateDistanceField(riverMask);

        Debug.Log("Calculating suitability scores...");
        float[,] suitabilityMap = new float[width, height];
        float maxScore = 0f;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (slopeMap.GetPixel(x, y).r > maxSlope || riverMask.GetPixel(x, y).r > 0.5f) {
                    suitabilityMap[x, y] = 0;
                    continue;
                }
                float score = 1.0f;

                float roadDist = roadDistanceMap[x, y];
                if (roadsidePriority) {
                    if (roadDist <= 1) { score += roadBonus * 10f; } // 道路沿いを最優先
                } else {
                    if (roadDist <= roadProximity) { score += (1.0f - (roadDist / roadProximity)) * roadBonus; }
                }

                float riverDist = riverDistanceMap[x, y];
                if (riverDist <= riverProximity) { score += (1.0f - (riverDist / riverProximity)) * riverBonus; }

                suitabilityMap[x, y] = score;
                if (score > maxScore) maxScore = score;
            }
        }

        string fileName = roadsidePriority ? roadsideSuitabilityMapName : settlementSuitabilityMapName;
        Debug.Log($"Saving suitability map to {fileName}...");
        SaveMapTexture(suitabilityMap, maxScore, fileName);
        Debug.Log("Suitability map generation complete!");
    }

    private float[,] GenerateDistanceField(Texture2D source)
    {
        float[,] distanceMap = new float[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (source.GetPixel(x, y).r > 0.5f) {
                    distanceMap[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                } else {
                    distanceMap[x, y] = float.MaxValue;
                }
            }
        }
        
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        while (queue.Count > 0) {
            Vector2Int p = queue.Dequeue();
            for (int i = 0; i < 4; i++) {
                int nx = p.x + dx[i];
                int ny = p.y + dy[i];
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && distanceMap[nx, ny] == float.MaxValue) {
                    distanceMap[nx, ny] = distanceMap[p.x, p.y] + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
        return distanceMap;
    }

    private void SaveMapTexture(float[,] map, float maxVal, string fileName)
    {
        Texture2D texture = new Texture2D(width, height);
        if (maxVal == 0) maxVal = 1;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float normalizedValue = map[x, y] / maxVal;
                texture.SetPixel(x, y, new Color(normalizedValue, normalizedValue, normalizedValue, 1));
            }
        }
        texture.Apply();
        SaveTexture(texture, fileName);
    }
    
    private Texture2D LoadTexture(string fileName)
    {
        string path = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(path)) {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }
        Debug.LogError($"File not found: {path}");
        return null;
    }

    private void SaveTexture(Texture2D texture, string fileName)
    {
        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllBytes(path, bytes);
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
}