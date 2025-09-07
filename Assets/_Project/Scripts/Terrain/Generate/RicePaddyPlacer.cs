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
    public Transform paddiesParent;
    public Transform housesParent;

    [Header("マスク")]
    public Texture2D riceFieldMask;
    public Texture2D roadMask;
    public Texture2D riverMask; 

    [Header("配置設定")]
    public GameObject[] paddyPrefabs;
    public float paddyPlotSize = 10f;
    public Vector3 paddyDimensions = new Vector3(10, 1, 20);
    public LayerMask paddyLayer;
    public float exclusionRadiusHouses = 15f;
    public float exclusionRadiusRoads = 5f;
    // ▼▼▼ 追加 ▼▼▼
    [Tooltip("川から最低限離す距離")]
    public float exclusionRadiusRiver = 8f;
    // ▲▲▲ ▲▲▲
    public float maxSlopeAngle = 30f;
    public float rotationCoherence = 0.1f;
    public float sinkAmount = 0.5f;
    
    [Header("川周辺への配置設定")]
    public float nearRiverDistance = 50f; 
    public float farRiverDistance = 200f;

    [Header("地形変形設定")]
    public bool modifyTerrainHeight = true;
    public float terrainLowerAmount = 1.0f;

    private List<Vector3> housePositions;
    private List<Vector2> roadPointsNormalized;
    private List<Vector2> riverPointsNormalized;

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
                    
                    if(!ShouldPlaceByRiverDistance(plotCenter)) continue;

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
        
        float diagonal = Mathf.Sqrt(Mathf.Pow(paddyDimensions.x, 2) + Mathf.Pow(paddyDimensions.z, 2));
        float worldRadius = diagonal * 0.5f;
        int searchRadius = Mathf.CeilToInt(worldRadius / terrainSize.x * (heightmapResolution - 1));

        int centerX = (int)((hit.point.x - terrainPos.x) / terrainSize.x * (heightmapResolution - 1));
        int centerY = (int)((hit.point.z - terrainPos.z) / terrainSize.z * (heightmapResolution - 1));

        int startX = Mathf.Clamp(centerX - searchRadius, 0, heightmapResolution - 1);
        int startY = Mathf.Clamp(centerY - searchRadius, 0, heightmapResolution - 1);
        int width = Mathf.Min(searchRadius * 2, heightmapResolution - startX);
        int height = Mathf.Min(searchRadius * 2, heightmapResolution - startY);

        float[,] heights = terrainData.GetHeights(startX, startY, width, height);
        float targetHeight = (hit.point.y - terrainLowerAmount) / terrainSize.y;
        Quaternion inverseRotation = Quaternion.Inverse(finalRotation);

        for (int j = 0; j < height; j++) {
            for (int i = 0; i < width; i++) {
                int hmX = startX + i;
                int hmY = startY + j;
                
                float worldX = (hmX / (float)(heightmapResolution - 1)) * terrainSize.x + terrainPos.x;
                float worldZ = (hmY / (float)(heightmapResolution - 1)) * terrainSize.z + terrainPos.z;
                Vector3 worldPos = new Vector3(worldX, hit.point.y, worldZ);
                
                Vector3 localPos = inverseRotation * (worldPos - hit.point);
                
                if (Mathf.Abs(localPos.x) < paddyDimensions.x / 2f && Mathf.Abs(localPos.z) < paddyDimensions.z / 2f)
                {
                    heights[j, i] = targetHeight;
                }
            }
        }
        terrainData.SetHeights(startX, startY, heights);
    }
    
    private bool ShouldPlaceByRiverDistance(Vector3 plotCenter)
    {
        if (riverPointsNormalized == null || riverPointsNormalized.Count == 0) return true;

        TerrainData terrainData = terrain.terrainData;
        Vector2 normalizedPos = new Vector2(plotCenter.x / terrainData.size.x, plotCenter.z / terrainData.size.z);
        
        float minSqrDist = riverPointsNormalized.Min(riverPoint => (normalizedPos - riverPoint).sqrMagnitude);
        float dist = Mathf.Sqrt(minSqrDist) * terrainData.size.x;

        if (dist <= nearRiverDistance) return true;
        if (dist > farRiverDistance) return false;

        float placementChance = 1.0f - (dist - nearRiverDistance) / (farRiverDistance - nearRiverDistance);
        return Random.value < placementChance;
    }

    private Quaternion CalculateFinalRotation(float x, float y, RaycastHit hit, GameObject prefab)
    {
        float noise = Mathf.PerlinNoise(x * rotationCoherence, y * rotationCoherence);
        float targetAngle = Mathf.FloorToInt(noise * 4) * 90f;
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
        
        // ▼▼▼ 追加 ▼▼▼
        if (IsTooClose(normalizedPos, riverPointsNormalized, exclusionRadiusRiver / terrainData.size.x)) return false;
        // ▲▲▲ ▲▲▲

        return true;
    }

    private bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (riceFieldMask == null || roadMask == null || riverMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
        if (paddyPrefabs.Length == 0) { Debug.LogWarning("田んぼのプレハブが設定されていません。"); return false; }

        housePositions = new List<Vector3>();
        if (housesParent != null)
        {
            foreach (Transform child in housesParent) housePositions.Add(child.position);
        }

        // --- 川、道路の情報をキャッシュ ---
        roadPointsNormalized = GetPointsFromMask(roadMask);
        riverPointsNormalized = GetPointsFromMask(riverMask);
        
        return true;
    }

    // ▼▼▼ マスクから座標リストを生成するヘルパー関数を追加 ▼▼▼
    private List<Vector2> GetPointsFromMask(Texture2D mask)
    {
        List<Vector2> points = new List<Vector2>();
        for (int y = 0; y < mask.height; y++)
        {
            for (int x = 0; x < mask.width; x++)
            {
                if (mask.GetPixel(x, y).r > 0.5f)
                {
                    points.Add(new Vector2(x / (float)mask.width, y / (float)mask.height));
                }
            }
        }
        return points;
    }
    // ▲▲▲ ▲▲▲

    private bool IsTooClose<T>(T point, List<T> targets, float radius) where T : struct
    {
        if(targets == null || targets.Count == 0) return false;
        
        // LinqのMin()を使うと重いので、手動で最小距離を探す
        float minSqrDist = float.MaxValue;
        if (typeof(T) == typeof(Vector3))
        {
            Vector3 p = (Vector3)(object)point;
            foreach (var t in targets) {
                float sqrDist = Vector3.SqrMagnitude(p - (Vector3)(object)t);
                if (sqrDist < minSqrDist) minSqrDist = sqrDist;
            }
        }
        else if (typeof(T) == typeof(Vector2))
        {
            Vector2 p = (Vector2)(object)point;
            foreach (var t in targets) {
                float sqrDist = Vector2.SqrMagnitude(p - (Vector2)(object)t);
                if (sqrDist < minSqrDist) minSqrDist = sqrDist;
            }
        }
        return minSqrDist < radius * radius;
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