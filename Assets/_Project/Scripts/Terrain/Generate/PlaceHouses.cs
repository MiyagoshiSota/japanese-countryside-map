using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ファイル名が長すぎるため、クラス名を短く変更
public class SettlementPlacer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    public Transform objectsParent;
    [Tooltip("Step 3で生成した道路のマスク (RoadVisualizer.png)")]
    public Texture2D roadMask;
    [Tooltip("平地エリアのマスク（家を建てる候補地）")]
    public Texture2D flatAreaMask;

    [Header("集落の設定")]
    [Tooltip("生成する集落（町の中心）の数")]
    public int numberOfSettlements = 3;
    [Tooltip("配置する家の総数")]
    public int totalHouses = 150;
    [Tooltip("集落の中心部の半径。この範囲内は家が密集します。")]
    public float coreRadius = 100f;
    [Tooltip("集落の最大半径。この範囲外には家は建ちません。")]
    public float maxRadius = 400f;

    [Header("家の詳細設定")]
    [Tooltip("配置する家のプレハブ")]
    public GameObject[] housePrefabs;
    [Tooltip("道路から家を離す最小距離")]
    public float minOffsetFromRoad = 8f;
    [Tooltip("道路から家を離す最大距離")]
    public float maxOffsetFromRoad = 12f;
    [Tooltip("家同士が最低限離れる距離")]
    public float minDistanceBetweenHouses = 15f;
    [Tooltip("★モデルの向きがズレている場合の補正値 (Y軸を90や-90に設定)")]
    public Vector3 rotationOffset; // ★追加パラメータ

    [Header("ランダム設定")]
    public int seed = 0;

    // --- プライベート変数 ---
    private List<Vector2> roadPointsWorld;
    private List<Vector3> placedHousePositions;

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

        // ★追加：最終座標が道路上にないかチェック
        float nearestRoadDist = roadPointsWorld.Min(p => Vector2.Distance(p, placementPos2D));
        if (nearestRoadDist < minOffsetFromRoad / 2f)
        {
            return null; // 道路に近すぎるので配置しない
        }

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
        // ★修正：回転の補正を追加
        Vector3 directionToRoad = (new Vector3(roadPoint.x, position.y, roadPoint.y) - position).normalized;
        Quaternion baseRotation = Quaternion.LookRotation(directionToRoad);
        Quaternion finalRotation = baseRotation * Quaternion.Euler(rotationOffset);

        GameObject prefab = housePrefabs[Random.Range(0, housePrefabs.Length)];
        GameObject newHouse = Instantiate(prefab, position, finalRotation);
        if (objectsParent != null) newHouse.transform.SetParent(objectsParent);
    }
    
    // (Initialize, IsInFlatArea, ClearPreviousHouses はほぼ変更なし)
    #region Initialization and Helpers
    bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (roadMask == null || flatAreaMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
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