using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SettlementPlacerWithRiver : MonoBehaviour // クラス名を変更
{
    [Header("参照")]
    public Terrain terrain;
    public Transform objectsParent;
    [Tooltip("道路のマスク (RoadVisualizer.png)")]
    public Texture2D roadMask;
    [Tooltip("平地エリアのマスク（家を建てる候補地）")]
    public Texture2D flatAreaMask;
    
    // ▼▼▼ 追加 ▼▼▼
    [Tooltip("川の位置を示すマスク画像（白が川）")]
    public Texture2D riverMask;
    // ▲▲▲ ▲▲▲

    [Header("集落の設定")]
    public int numberOfSettlements = 3;
    public int totalHouses = 150;
    public float coreRadius = 100f;
    public float maxRadius = 400f;

    [Header("家の詳細設定")]
    public GameObject[] housePrefabs;
    public float minOffsetFromRoad = 8f;
    public float maxOffsetFromRoad = 12f;
    public float minDistanceBetweenHouses = 15f;
    
    // ▼▼▼ 追加 ▼▼▼
    [Tooltip("川から家を離す最小距離")]
    public float minOffsetFromRiver = 10f;
    // ▲▲▲ ▲▲▲
    
    public Vector3 rotationOffset;

    [Header("ランダム設定")]
    public int seed = 0;

    private List<Vector2> roadPointsWorld;
    private List<Vector3> placedHousePositions;
    
    // ▼▼▼ 追加 ▼▼▼
    private float[,] riverMaskCache; // 川マスクの情報を高速に参照するためのキャッシュ
    private int maskResolution;
    // ▲▲▲ ▲▲▲

    [ContextMenu("現実的な集落を生成する")]
    public void PlaceSettlements()
    {
        if (!Initialize()) return;
        ClearPreviousHouses();

        List<Vector2> settlementCenters = DetermineSettlementCenters();
        if (settlementCenters.Count == 0) return;

        int placedHousesCount = 0;
        int attempts = 0;

        while (placedHousesCount < totalHouses && attempts < totalHouses * 20)
        {
            attempts++;
            Vector2 roadPoint = roadPointsWorld[Random.Range(0, roadPointsWorld.Count)];
            
            if (!ShouldPlaceHouseAt(roadPoint, settlementCenters)) continue;

            Vector3? placementPos3D = CalculatePlacementPosition(roadPoint);
            
            if (placementPos3D.HasValue && !IsTooCloseToOtherHouses(placementPos3D.Value))
            {
                PlaceSingleHouse(placementPos3D.Value, roadPoint);
                placedHousePositions.Add(placementPos3D.Value);
                placedHousesCount++;
            }
        }
    
        Debug.Log($"{placedHousesCount}軒の家を配置しました。");
    }

    bool ShouldPlaceHouseAt(Vector2 roadPoint, List<Vector2> centers)
    {
        Vector2 nearestCenter = centers.OrderBy(c => Vector2.Distance(roadPoint, c)).First();
        float distanceToCenter = Vector2.Distance(roadPoint, nearestCenter);
        if (distanceToCenter > maxRadius) return false;
        
        float placementChance = 1.0f;
        if (distanceToCenter > coreRadius)
        {
            placementChance = 1.0f - (distanceToCenter - coreRadius) / (maxRadius - coreRadius);
        }
        return Random.value < placementChance;
    }

    Vector3? CalculatePlacementPosition(Vector2 roadPoint)
    {
        Vector2 neighborPoint = roadPointsWorld.OrderBy(p => Vector2.Distance(roadPoint, p)).Skip(1).First();
        Vector2 roadDirection = (neighborPoint - roadPoint).normalized;
        Vector2 perpendicularDir = new Vector2(roadDirection.y, -roadDirection.x);
        if (Random.value < 0.5f) perpendicularDir *= -1;

        float offset = Random.Range(minOffsetFromRoad, maxOffsetFromRoad);
        Vector2 placementPos2D = roadPoint + perpendicularDir * offset;
        
        float nearestRoadDist = roadPointsWorld.Min(p => Vector2.Distance(p, placementPos2D));
        if (nearestRoadDist < minOffsetFromRoad / 2f)
        {
            return null;
        }

        // ▼▼▼ 川との距離をチェックする処理を追加 ▼▼▼
        if (IsOnRiver(placementPos2D))
        {
            return null; // 川の上、または川に近すぎるので配置しない
        }
        // ▲▲▲ ▲▲▲

        Vector3 placementPos3D = new Vector3(placementPos2D.x, 0, placementPos2D.y);
        placementPos3D.y = terrain.SampleHeight(placementPos3D);
        return placementPos3D;
    }

    bool IsTooCloseToOtherHouses(Vector3 position)
    {
        return placedHousePositions.Any(p => Vector3.Distance(p, position) < minDistanceBetweenHouses);
    }

    void PlaceSingleHouse(Vector3 position, Vector2 roadPoint)
    {
        Vector3 directionToRoad = (new Vector3(roadPoint.x, position.y, roadPoint.y) - position).normalized;
        Quaternion baseRotation = Quaternion.LookRotation(directionToRoad);
        Quaternion finalRotation = baseRotation * Quaternion.Euler(rotationOffset);

        GameObject prefab = housePrefabs[Random.Range(0, housePrefabs.Length)];
        GameObject newHouse = Instantiate(prefab, position, finalRotation);
        if (objectsParent != null) newHouse.transform.SetParent(objectsParent);
    }
    
    #region Initialization and Helpers
    bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        // ▼▼▼ riverMaskのチェックを追加 ▼▼▼
        if (roadMask == null || flatAreaMask == null || riverMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
        if (!riverMask.isReadable) { Debug.LogError("riverMaskのRead/Write設定を有効にしてください。"); return false; }
        // ▲▲▲ ▲▲▲
        if (housePrefabs.Length == 0) { Debug.LogWarning("家のプレハブが設定されていません。"); return false; }
        if (seed != 0) Random.InitState(seed);

        placedHousePositions = new List<Vector3>();
        roadPointsWorld = new List<Vector2>();
        TerrainData terrainData = terrain.terrainData;
        for (int y = 0; y < roadMask.height; y++) {
            for (int x = 0; x < roadMask.width; x++) {
                if (roadMask.GetPixel(x, y).r > 0.5f) {
                    roadPointsWorld.Add(new Vector2(x / (float)roadMask.width * terrainData.size.x, y / (float)roadMask.height * terrainData.size.z));
                }
            }
        }
        
        // ▼▼▼ 川マスクの情報をキャッシュする処理を追加 ▼▼▼
        maskResolution = riverMask.width;
        riverMaskCache = new float[maskResolution, maskResolution];
        for (int y = 0; y < maskResolution; y++)
        {
            for (int x = 0; x < maskResolution; x++)
            {
                riverMaskCache[x, y] = riverMask.GetPixel(x, y).r;
            }
        }
        // ▲▲▲ ▲▲▲

        return roadPointsWorld.Count > 0;
    }

    List<Vector2> DetermineSettlementCenters()
    {
        List<Vector2> centers = new List<Vector2>();
        List<Vector2> validCenterCandidates = roadPointsWorld.Where(p => IsInFlatArea(p)).ToList();
        if (validCenterCandidates.Count == 0) { Debug.LogError("平地に道路がありません。"); return centers; }

        for (int i = 0; i < numberOfSettlements; i++)
        {
            centers.Add(validCenterCandidates[Random.Range(0, validCenterCandidates.Count)]);
        }
        return centers;
    }

    bool IsInFlatArea(Vector2 worldPos)
    {
        TerrainData td = terrain.terrainData;
        int px = (int)(worldPos.x / td.size.x * flatAreaMask.width);
        int py = (int)(worldPos.y / td.size.z * flatAreaMask.height);
        return flatAreaMask.GetPixel(px, py).r > 0.5f;
    }

    // ▼▼▼ 川との距離をチェックするヘルパー関数を追加 ▼▼▼
    bool IsOnRiver(Vector2 worldPos)
    {
        TerrainData td = terrain.terrainData;
        int searchRadius = Mathf.CeilToInt(minOffsetFromRiver / td.size.x * maskResolution);
        int centerX = (int)(worldPos.x / td.size.x * maskResolution);
        int centerY = (int)(worldPos.y / td.size.z * maskResolution);
        
        for (int y = -searchRadius; y <= searchRadius; y++)
        {
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                int px = centerX + x;
                int py = centerY + y;
                
                if (px >= 0 && px < maskResolution && py >= 0 && py < maskResolution)
                {
                    if (riverMaskCache[px, py] > 0.1f) // 川(白)かチェック
                    {
                        return true; //範囲内に川が見つかった
                    }
                }
            }
        }
        return false; //範囲内に川はなかった
    }
    // ▲▲▲ ▲▲▲

    [ContextMenu("配置した家を削除")]
    void ClearPreviousHouses()
    {
        if (objectsParent != null)
        {
            for (int i = objectsParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(objectsParent.GetChild(i).gameObject);
        }
    }
    #endregion
}