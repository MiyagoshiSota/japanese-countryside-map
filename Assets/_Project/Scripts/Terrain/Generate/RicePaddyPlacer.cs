using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Terrain))]
public class RicePaddyPlacer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    [Tooltip("階層を整理するための親オブジェクト")]
    public Transform paddiesParent;
    [Tooltip("家の親オブジェクト（この周りを避ける）")]
    public Transform housesParent;

    [Header("マスク")]
    [Tooltip("田んぼを配置するエリアを示すマスク")]
    public Texture2D riceFieldMask;
    [Tooltip("道路のマスク（この周りを避ける）")]
    public Texture2D roadMask;
    
    [Header("配置設定")]
    [Tooltip("配置する田んぼのプレハブ")]
    public GameObject[] paddyPrefabs;
    [Tooltip("田んぼをチェックする間隔")]
    public float paddyPlotSize = 10f;
    [Tooltip("配置する田んぼの実際のサイズ（X, Y, Z）")]
    public Vector3 paddyDimensions = new Vector3(10, 1, 20);
    [Tooltip("配置された田んぼが属するレイヤー")]
    public LayerMask paddyLayer;
    [Tooltip("家から最低限離す距離")]
    public float exclusionRadiusHouses = 15f;
    [Tooltip("道路から最低限離す距離")]
    public float exclusionRadiusRoads = 5f;
    [Tooltip("配置を許可する地面の最大傾斜角度")]
    public float maxSlopeAngle = 30f;
    [Tooltip("回転の揃い具合。値が小さいほど広い範囲で向きが揃います。")]
    public float rotationCoherence = 0.1f;
    [Tooltip("田んぼを地面に埋め込む（沈める）深さ")]
    public float sinkAmount = 0.5f;
    [Tooltip("地形自体を平坦に凹ませる機能を有効にするか")]
    public bool modifyTerrainHeight = true;
    [Tooltip("地形を凹ませる深さ（ワールド単位）。テスト用に5などの大きな値も試してみてください。")]
    public float terrainLowerAmount = 1.0f;

    private List<Vector3> housePositions;
    private List<Vector2> roadPointsNormalized;

    private void Start()
    {
        GeneratePaddies();
    }

    [ContextMenu("田んぼを生成する")]
    public void GeneratePaddies()
    {
        if (!Initialize()) return;
        ClearPreviousPaddies();

        TerrainData currentTerrainData = terrain.terrainData;
        
        for (float y = 0; y < currentTerrainData.size.z; y += paddyPlotSize)
        {
            for (float x = 0; x < currentTerrainData.size.x; x += paddyPlotSize)
            {
                Vector3 plotCenter = new Vector3(x + paddyPlotSize / 2, 0, y + paddyPlotSize / 2);

                if (!IsPlacementAllowed(plotCenter)) continue;
                
                RaycastHit hit;
                if (Physics.Raycast(plotCenter + Vector3.up * 100, Vector3.down, out hit, 200f, ~paddyLayer))
                {
                    if (Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle) continue;

                    GameObject prefab = paddyPrefabs[Random.Range(0, paddyPrefabs.Length)];
                    Quaternion finalRotation = CalculateFinalRotation(x, y, hit, prefab);
                    
                    if (IsOverlapping(hit, finalRotation)) continue;

                    if (modifyTerrainHeight)
                    {
                        FlattenTerrainUnderPaddy(hit, finalRotation, currentTerrainData);
                    }

                    Vector3 finalPosition = hit.point - Vector3.up * sinkAmount;
                    GameObject newPaddy = Instantiate(prefab, finalPosition, finalRotation);
                    if (paddiesParent != null) newPaddy.transform.SetParent(paddiesParent);
                }
            }
        }
        
        Debug.Log("田んぼの配置が完了しました。");
    }

    private void FlattenTerrainUnderPaddy(RaycastHit hit, Quaternion finalRotation, TerrainData terrainData)
    {
        int heightmapResolution = terrainData.heightmapResolution;
        Vector3 terrainSize = terrainData.size;
        Vector3 terrainPos = terrain.transform.position;

        // ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
        // 【修正点】田んぼの対角線の長さを基に、より正確な検索範囲を計算する
        float diagonal = Mathf.Sqrt(Mathf.Pow(paddyDimensions.x, 2) + Mathf.Pow(paddyDimensions.z, 2));
        float worldRadius = diagonal * 0.5f; // ワールド座標での半径
        // ワールド座標の半径をハイトマップのピクセル数に変換
        int searchRadius = Mathf.CeilToInt(worldRadius / terrainSize.x * (heightmapResolution - 1));
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        int centerX = (int)((hit.point.x - terrainPos.x) / terrainSize.x * (heightmapResolution - 1));
        int centerY = (int)((hit.point.z - terrainPos.z) / terrainSize.z * (heightmapResolution - 1));

        int startX = Mathf.Clamp(centerX - searchRadius, 0, heightmapResolution - 1);
        int startY = Mathf.Clamp(centerY - searchRadius, 0, heightmapResolution - 1);
        int width = Mathf.Min(searchRadius * 2, heightmapResolution - startX);
        int height = Mathf.Min(searchRadius * 2, heightmapResolution - startY);

        float[,] heights = terrainData.GetHeights(startX, startY, width, height);
        // 凹ませて平らにする目標の高さを計算（0~1の正規化された値）
        float targetHeight = (hit.point.y - terrainLowerAmount) / terrainSize.y;
        Quaternion inverseRotation = Quaternion.Inverse(finalRotation);

        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                int hmX = startX + i;
                int hmY = startY + j;

                // ハイトマップ座標をワールド座標に変換
                float worldX = (hmX / (float)(heightmapResolution - 1)) * terrainSize.x + terrainPos.x;
                float worldZ = (hmY / (float)(heightmapResolution - 1)) * terrainSize.z + terrainPos.z;
                Vector3 worldPos = new Vector3(worldX, hit.point.y, worldZ);

                // ワールド座標を田んぼのローカル座標に変換
                Vector3 localPos = inverseRotation * (worldPos - hit.point);

                // 田んぼの範囲内かチェック
                if (Mathf.Abs(localPos.x) < paddyDimensions.x / 2f && Mathf.Abs(localPos.z) < paddyDimensions.z / 2f)
                {
                    heights[j, i] = targetHeight;
                }
            }
        }
        terrainData.SetHeights(startX, startY, heights);
    }

    private Quaternion CalculateFinalRotation(float x, float y, RaycastHit hit, GameObject prefab)
    {
        float noise = Mathf.PerlinNoise(x * rotationCoherence, y * rotationCoherence);
        int angleStep = Mathf.FloorToInt(noise * 4);
        float targetAngle = angleStep * 90f;
        Quaternion randomRotation = Quaternion.Euler(0, targetAngle, 0);
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        return slopeRotation * randomRotation * prefab.transform.rotation;
    }

    private bool IsOverlapping(RaycastHit hit, Quaternion finalRotation)
    {
        Vector3 boxCenter = hit.point + hit.normal * (paddyDimensions.y * 0.5f);
        Vector3 halfExtents = paddyDimensions * 0.5f;
        return Physics.OverlapBox(boxCenter, halfExtents, finalRotation, paddyLayer).Length > 0;
    }

    private bool IsPlacementAllowed(Vector3 plotCenter)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector2 normalizedPos = new Vector2(plotCenter.x / terrainData.size.x, plotCenter.z / terrainData.size.z);
        if (riceFieldMask.GetPixelBilinear(normalizedPos.x, normalizedPos.y).r < 0.5f) return false;
        if (IsTooClose(plotCenter, housePositions, exclusionRadiusHouses)) return false;
        if (IsTooClose(normalizedPos, roadPointsNormalized, exclusionRadiusRoads / terrainData.size.x)) return false;
        return true;
    }

    private bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (riceFieldMask == null || roadMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
        if (paddyPrefabs.Length == 0) { Debug.LogWarning("田んぼのプレハブが設定されていません。"); return false; }

        housePositions = new List<Vector3>();
        if (housesParent != null)
        {
            foreach (Transform child in housesParent) housePositions.Add(child.position);
        }

        roadPointsNormalized = new List<Vector2>();
        for (int y = 0; y < roadMask.height; y++)
        {
            for (int x = 0; x < roadMask.width; x++)
            {
                if (roadMask.GetPixel(x, y).r > 0.5f)
                {
                    roadPointsNormalized.Add(new Vector2(x / (float)roadMask.width, y / (float)roadMask.height));
                }
            }
        }
        return true;
    }

    private bool IsTooClose<T>(T point, List<T> targets, float radius) where T : struct
    {
        if(targets == null) return false;
        Func<T, T, float> distanceFunc;
        if (typeof(T) == typeof(Vector3)) distanceFunc = (a, b) => Vector3.Distance((Vector3)(object)a, (Vector3)(object)b);
        else if (typeof(T) == typeof(Vector2)) distanceFunc = (a, b) => Vector2.Distance((Vector2)(object)a, (Vector2)(object)b);
        else return false;

        foreach (var target in targets)
        {
            if (distanceFunc(point, target) < radius) return true;
        }
        return false;
    }

    [ContextMenu("配置した田んぼを削除")]
    private void ClearPreviousPaddies()
    {
        if (paddiesParent != null)
        {
            for (int i = paddiesParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(paddiesParent.GetChild(i).gameObject);
        }
    }
}