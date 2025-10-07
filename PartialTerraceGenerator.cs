using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 適性マップと傾斜を基に、等高線に沿って棚田を配置するスクリプト
/// </summary>
public class ContourPaddyPlacer : MonoBehaviour
{
    [Header("必須設定")]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private GameObject paddyPrefab;
    [Tooltip("配置エリアを指定するマップ（黒い部分に配置）")]
    [SerializeField] private Texture2D suitabilityMap;

    [Header("配置条件")]
    [Tooltip("この傾斜角度『以上』の場所に配置を試みる")]
    [SerializeField, Range(0f, 90f)] private float minPlacementSlope = 20f;
    [Tooltip("マップの黒色として認識する色のしきい値（0に近いほど黒）")]
    [SerializeField, Range(0f, 1f)] private float mapThreshold = 0.1f;
    [SerializeField] private int numberOfPaddies = 300;
    [SerializeField] private float minDistanceBetweenPaddies = 5f;

    [Header("ランダム化設定")]
    [SerializeField] private float minScaleX = 0.5f;
    [SerializeField] private float maxScaleX = 1.2f;
    [SerializeField] private float minScaleZ = 2.0f;
    [SerializeField] private float maxScaleZ = 4.0f;
    
    private List<Vector3> placedPaddyPositions = new List<Vector3>();

    [ContextMenu("Generate Paddies along Contours")]
    public void GeneratePaddies()
    {
        // 既存の田んぼをクリア
        while (transform.childCount > 0) { DestroyImmediate(transform.GetChild(0).gameObject); }
        placedPaddyPositions.Clear();

        if (!IsReady()) return;

        int placedCount = 0;
        int attempts = 0;
        int maxAttempts = numberOfPaddies * 20;

        while (placedCount < numberOfPaddies && attempts < maxAttempts)
        {
            attempts++;

            Vector3 candidatePos = GetRandomPositionOnTerrain();
            
            // 1. 適性マップのチェック
            if (!IsInSuitableArea(candidatePos)) continue;

            // 2. 傾斜のチェック
            Vector3 normal = GetTerrainNormal(candidatePos);
            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope < minPlacementSlope) continue;

            // 3. 距離のチェック
            if (IsTooClose(candidatePos)) continue;
            
            // 4. 配置
            // 等高線に沿った向きを計算
            Vector3 forward = Vector3.Cross(normal, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward; // 平地の場合のフォールバック
            Quaternion rotation = Quaternion.LookRotation(forward, normal);

            // ランダムなスケールを適用（Z方向を長くして棚田っぽくする）
            Vector3 randomScale = new Vector3(
                Random.Range(minScaleX, maxScaleX),
                1f,
                Random.Range(minScaleZ, maxScaleZ)
            );

            GameObject newPaddy = Instantiate(paddyPrefab, candidatePos, rotation, this.transform);
            newPaddy.transform.localScale = randomScale;

            placedPaddyPositions.Add(candidatePos);
            placedCount++;
        }
        
        Debug.Log($"{placedCount}個の田んぼを配置しました。");
    }

    #region Helper Functions
    private bool IsInSuitableArea(Vector3 worldPos)
    {
        Vector2Int mapCoord = WorldToMapCoordinates(worldPos);
        Color mapColor = suitabilityMap.GetPixel(mapCoord.x, mapCoord.y);
        return mapColor.r < mapThreshold; // 赤チャンネルの値で判定
    }
    
    private Vector3 GetRandomPositionOnTerrain()
    {
        Vector3 terrainSize = targetTerrain.terrainData.size;
        float randomX = Random.Range(0, terrainSize.x);
        float randomZ = Random.Range(0, terrainSize.z);
        Vector3 pos = new Vector3(randomX, 0, randomZ) + targetTerrain.transform.position;
        pos.y = targetTerrain.SampleHeight(pos) + targetTerrain.transform.position.y;
        return pos;
    }

    private Vector3 GetTerrainNormal(Vector3 worldPos)
    {
        TerrainData td = targetTerrain.terrainData;
        Vector3 localPos = worldPos - targetTerrain.transform.position;
        Vector2 normalizedPos = new Vector2(localPos.x / td.size.x, localPos.z / td.size.z);
        return td.GetInterpolatedNormal(normalizedPos.x, normalizedPos.y);
    }
    
    private Vector2Int WorldToMapCoordinates(Vector3 worldPos)
    {
        TerrainData td = targetTerrain.terrainData;
        Vector3 localPos = worldPos - targetTerrain.transform.position;
        int mapX = (int)((localPos.x / td.size.x) * suitabilityMap.width);
        int mapY = (int)((localPos.z / td.size.z) * suitabilityMap.height);
        return new Vector2Int(mapX, mapY);
    }

    private bool IsTooClose(Vector3 pos)
    {
        foreach (var placedPos in placedPaddyPositions)
        {
            if (Vector3.Distance(pos, placedPos) < minDistanceBetweenPaddies) return true;
        }
        return false;
    }

    private bool IsReady()
    {
        if (targetTerrain == null || paddyPrefab == null || suitabilityMap == null)
        {
            Debug.LogError("必須項目（Terrain, Prefab, Suitability Map）が設定されていません。");
            return false;
        }
        // スクリプトからテクスチャを読み取るために必要
        if (!suitabilityMap.isReadable)
        {
            Debug.LogError("Suitability Mapのインポート設定で 'Read/Write Enabled' をオンにしてください。");
            return false;
        }
        return true;
    }
    #endregion
}