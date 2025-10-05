using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 田んぼ適性マップを元に、シーンに田んぼオブジェクトを配置するスクリプト。
/// </summary>
public class PaddyFieldPlacer : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("田んぼ配置の基準となる田んぼ適性マップ")]
    [SerializeField] private string paddySuitabilityMapName = "PaddySuitabilityMap.png";
    [Tooltip("オブジェクトを配置するTerrain")]
    [SerializeField] private Terrain targetTerrain;

    [Header("Placement Settings")]
    [Tooltip("配置する田んぼのプレハブ")]
    [SerializeField] private GameObject paddyPrefab;
    [Tooltip("配置する田んぼの総数")]
    [SerializeField] private int numberOfPaddyFields = 200;
    [Tooltip("田んぼとして認識する最低スコア（0~1）")]
    [SerializeField, Range(0f, 1f)] private float scoreThreshold = 0.5f;
    [Tooltip("田んぼ同士の最低距離（小さくすると密集する）")]
    [SerializeField] private float minDistanceBetweenPaddies = 5f;
    [Tooltip("配置する田んぼのX方向のスケールランダム幅")]
    [SerializeField] private float scaleXRandomness = 0.2f;
    [Tooltip("配置する田んぼのZ方向のスケールランダム幅")]
    [SerializeField] private float scaleZRandomness = 0.2f;
    [Tooltip("田んぼのY座標のオフセット（地形から少し浮かせたい場合）")]
    [SerializeField] private float yOffset = 0.1f;


    private class CandidatePoint { public Vector2Int position; public float score; }
    private List<Vector3> placedPaddyPositions = new List<Vector3>();

    [ContextMenu("Place Paddy Fields")]
    public void PlacePaddyFields()
    {
        // 既存オブジェクトの全削除
        while (transform.childCount > 0) { DestroyImmediate(transform.GetChild(0).gameObject); }
        placedPaddyPositions.Clear();

        if (targetTerrain == null || paddyPrefab == null)
        {
            Debug.LogError("TerrainまたはPaddy Prefabが設定されていません。");
            return;
        }

        // 1. 田んぼ適性マップを読み込み
        Texture2D paddyMap = LoadTexture(paddySuitabilityMapName);
        if (paddyMap == null) return;

        // 2. 候補地をリストアップし、スコアが高い順にソート
        List<CandidatePoint> candidates = GetSortedCandidates(paddyMap, scoreThreshold);

        // 3. 田んぼを配置
        int placedCount = 0;
        while (placedCount < numberOfPaddyFields && candidates.Count > 0)
        {
            CandidatePoint bestCandidate = candidates[0];
            
            if (TryPlacePaddyField(bestCandidate.position, minDistanceBetweenPaddies))
            {
                placedCount++;
            }
            candidates.RemoveAt(0); // 処理した候補地はリストから削除
        }
        
        Debug.Log($"{placedCount} paddy fields placed.");
    }

    /// <summary>
    /// 指定された位置に田んぼを配置する試み
    /// </summary>
    private bool TryPlacePaddyField(Vector2Int mapPosition, float minDistance)
    {
        Vector3 worldPos = GetWorldPosition(mapPosition);
        
        // 既に配置済みの田んぼとの距離をチェック
        foreach (var placedPos in placedPaddyPositions)
        {
            if (Vector3.Distance(worldPos, placedPos) < minDistance)
            {
                return false; // 近すぎるので配置しない
            }
        }

        // 田んぼをインスタンス化
        GameObject newPaddy = Instantiate(paddyPrefab, worldPos + Vector3.up * yOffset, paddyPrefab.transform.rotation, this.transform);
        
        // ランダムなスケールを適用
        Vector3 originalScale = newPaddy.transform.localScale;
        newPaddy.transform.localScale = new Vector3(
            originalScale.x * (1f + Random.Range(-scaleXRandomness, scaleXRandomness)),
            originalScale.y, // Yスケールは通常固定
            originalScale.z * (1f + Random.Range(-scaleZRandomness, scaleZRandomness))
        );

        placedPaddyPositions.Add(worldPos);
        return true;
    }

    // --- 以下、各種ヘルパー関数 (AdvancedObjectPlacer.csからコピー) ---
    private List<CandidatePoint> GetSortedCandidates(Texture2D map, float threshold)
    {
        List<CandidatePoint> candidates = new List<CandidatePoint>();
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                float score = map.GetPixel(x, y).r;
                if (score >= threshold)
                {
                    candidates.Add(new CandidatePoint { position = new Vector2Int(x, y), score = score });
                }
            }
        }
        return candidates.OrderByDescending(p => p.score).ToList();
    }

    private Vector3 GetWorldPosition(Vector2Int mapPos)
    {
        TerrainData td = targetTerrain.terrainData;
        float normX = mapPos.x / (float)td.heightmapResolution;
        float normZ = mapPos.y / (float)td.heightmapResolution;
        float y = td.GetHeight((int)(normX * td.heightmapResolution), (int)(normZ * td.heightmapResolution));
        Vector3 worldPos = new Vector3(normX * td.size.x, y, normZ * td.size.z);
        return worldPos + targetTerrain.transform.position;
    }
    
    // このスクリプトでは使わないが、共通関数として残しておく
    private Vector2Int GetMapPosition(Vector3 worldPos)
    {
        TerrainData td = targetTerrain.terrainData;
        Vector3 localPos = worldPos - targetTerrain.transform.position;
        int mapX = (int)((localPos.x / td.size.x) * td.heightmapResolution);
        int mapY = (int)((localPos.z / td.size.z) * td.heightmapResolution);
        return new Vector2Int(mapX, mapY);
    }
    
    private Texture2D LoadTexture(string fileName)
    {
        string path = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }
        Debug.LogError($"File not found: {path}");
        return null;
    }
}