using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Terrain))]
public class OrganicSettlementPlacer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    public Transform objectsParent;
    [Tooltip("Step 3で生成した道路のマスク (RoadVisualizer.png)")]
    public Texture2D roadMask;

    [Header("集落の設定")]
    [Tooltip("配置する家の総数")]
    public int totalHouses = 200;
    [Tooltip("家の密度のムラを表現するノイズのスケール。小さいほど大きな塊になります。")]
    public float densityNoiseScale = 10f;

    [Header("家の詳細設定")]
    [Tooltip("配置する家のプレハブ")]
    public GameObject[] housePrefabs;
    [Tooltip("道路から家を離す距離")]
    public float offsetFromRoad = 8f;
    [Tooltip("家同士が最低限離れる距離")]
    public float minDistanceBetweenHouses = 12f;

    [Header("ランダム設定")]
    public int seed = 0;

    // --- プライベート変数 ---
    private List<Vector2> roadPointsWorld;
    private List<Vector3> placedHousePositions = new List<Vector3>();

    [ContextMenu("有機的な集落を生成する")]
    public void PlaceSettlements()
    {
        if (!Initialize()) return;
        ClearPreviousHouses();

        int placedHouses = 0;
        int attempts = 0; // 無限ループを避けるためのカウンター

        // --- 家の総数に達するまで配置を試みる ---
        while (placedHouses < totalHouses && attempts < totalHouses * 10)
        {
            // ランダムな道路上の点を取得
            Vector2 roadPoint = roadPointsWorld[Random.Range(0, roadPointsWorld.Count)];

            // --- 1. パーリンノイズで配置確率を計算 ---
            float noiseValue = Mathf.PerlinNoise(
                roadPoint.x / terrain.terrainData.size.x * densityNoiseScale,
                roadPoint.y / terrain.terrainData.size.z * densityNoiseScale
            );

            // ノイズの値が低い場所は家が建ちにくい（0.5を基準に確率を調整）
            if (Random.value > noiseValue * 1.5f) 
            {
                attempts++;
                continue;
            }

            // --- 2. 配置座標を決定 ---
            Vector2 placementPos2D = CalculatePlacementPosition(roadPoint);
            Vector3 placementPos3D = new Vector3(placementPos2D.x, 0, placementPos2D.y);
            placementPos3D.y = terrain.SampleHeight(placementPos3D);

            // --- 3. 他の家と近すぎないかチェック ---
            if (IsTooCloseToOtherHouses(placementPos3D))
            {
                attempts++;
                continue;
            }

            // --- 4. 配置処理 ---
            PlaceSingleHouse(placementPos3D, roadPoint);
            placedHousePositions.Add(placementPos3D);
            placedHouses++;
            attempts++;
        }
        
        Debug.Log($"{placedHouses}軒の家を配置しました。");
    }
    
    // --- 配置と計算の関数 ---
    void PlaceSingleHouse(Vector3 position, Vector2 roadPoint)
    {
        // 近くの道路の点から向きを決定
        Vector2 neighborPoint = roadPointsWorld.OrderBy(p => Vector2.Distance(roadPoint, p)).Skip(1).First();
        Vector2 roadDirection = (neighborPoint - roadPoint).normalized;
        Vector2 perpendicularDir = new Vector2(roadDirection.y, -roadDirection.x);
        
        // 配置座標から道路の中心を向くように回転を計算
        Vector3 directionToRoad = (new Vector3(roadPoint.x, position.y, roadPoint.y) - position).normalized;
        Quaternion rotation = Quaternion.LookRotation(directionToRoad);
        
        GameObject prefab = housePrefabs[Random.Range(0, housePrefabs.Length)];
        GameObject newHouse = Instantiate(prefab, position, rotation);
        if (objectsParent != null) newHouse.transform.SetParent(objectsParent);
    }
    
    Vector2 CalculatePlacementPosition(Vector2 roadPoint)
    {
        Vector2 neighborPoint = roadPointsWorld.OrderBy(p => Vector2.Distance(roadPoint, p)).Skip(1).First();
        Vector2 roadDirection = (neighborPoint - roadPoint).normalized;
        Vector2 perpendicularDir = new Vector2(roadDirection.y, -roadDirection.x);
        if (Random.value < 0.5f) perpendicularDir *= -1;
        
        return roadPoint + perpendicularDir * (offsetFromRoad + Random.Range(0f, 5f)); // 少しだけ距離をランダムにする
    }
    
    bool IsTooCloseToOtherHouses(Vector3 position)
    {
        foreach (var placedPos in placedHousePositions)
        {
            if (Vector3.Distance(position, placedPos) < minDistanceBetweenHouses)
            {
                return true;
            }
        }
        return false;
    }

    // --- 初期化とクリア ---
    bool Initialize()
    {
        // ... (以前のスクリプトから変更なし)
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (roadMask == null) { Debug.LogError("道路マスクが設定されていません！"); return false; }
        if (housePrefabs.Length == 0) { Debug.LogWarning("家のプレハブが設定されていません。"); return false; }
        if (seed != 0) Random.InitState(seed);
        else Random.InitState((int)System.DateTime.Now.Ticks);
        
        roadPointsWorld = new List<Vector2>();
        placedHousePositions.Clear();
        TerrainData td = terrain.terrainData;
        for (int y = 0; y < roadMask.height; y++) {
            for (int x = 0; x < roadMask.width; x++) {
                if (roadMask.GetPixel(x, y).r > 0.5f) {
                    roadPointsWorld.Add(new Vector2(x / (float)roadMask.width * td.size.x, y / (float)roadMask.height * td.size.z));
                }
            }
        }
        return roadPointsWorld.Count > 0;
    }

    [ContextMenu("配置した家を削除")]
    void ClearPreviousHouses()
    {
        // ... (以前のスクリプトから変更なし)
        if (objectsParent != null) {
            for (int i = objectsParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(objectsParent.GetChild(i).gameObject);
        }
    }
}