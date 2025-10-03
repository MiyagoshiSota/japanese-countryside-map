using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace  v2
{

/// <summary>
/// 「道路沿い」と「点在」の2つのパターンを組み合わせてオブジェクトを配置するスクリプト。
/// </summary>
public class ObjectPlacer : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("通常の集落適性マップ")]
    [SerializeField] private string settlementSuitabilityMapName = "SettlementSuitabilityMap.png";
    [Tooltip("道路沿い優先の適性マップ")]
    [SerializeField] private string roadsideSuitabilityMapName = "RoadsideSuitabilityMap.png";
    [Tooltip("オブジェクトを配置するTerrain")]
    [SerializeField] private Terrain targetTerrain;
    [Tooltip("配置するオブジェクトのプレハブ")]
    [SerializeField] private GameObject objectPrefab;

    [Header("Pass 1: Main Village Settings")]
    [SerializeField] private int numMainVillages = 1;
    [SerializeField] private int objectsPerMainVillage = 40;
    [SerializeField] private float mainVillageRadius = 80f;
    [SerializeField] private float mainVillageMinDistance = 10f;
    [SerializeField, Range(0f, 1f)] private float mainVillageScoreThreshold = 0.8f;

    [Header("Pass 2: Roadside Hamlet Settings")]
    [SerializeField] private int numRoadsideHamlets = 4;
    [SerializeField] private int objectsPerRoadsideHamlet = 8;
    [SerializeField] private float roadsideHamletRadius = 60f;
    [SerializeField] private float roadsideMinDistance = 15f;
    [SerializeField, Range(0f, 1f)] private float roadsideScoreThreshold = 0.5f;

    [Header("Pass 3: Scattered House Settings")]
    [SerializeField] private int numScatteredHouses = 20;
    [SerializeField] private float scatteredMinDistance = 100f;
    [SerializeField, Range(0f, 1f)] private float scatteredScoreThreshold = 0.3f;

    private class CandidatePoint { public Vector2Int position; public float score; }
    private List<Vector3> allPlacedPositions = new List<Vector3>();

    [ContextMenu("Place All Objects Hierarchically")]
    public void PlaceAllObjects()
    {
        while (transform.childCount > 0) { DestroyImmediate(transform.GetChild(0).gameObject); }
        allPlacedPositions.Clear();

        if (targetTerrain == null || objectPrefab == null) { Debug.LogError("Terrain or Prefab is not set."); return; }

        // --- パス1: 中核集落の配置 ---
        Debug.Log("--- PASS 1: Placing Main Villages ---");
        Texture2D settlementMap = LoadTexture(settlementSuitabilityMapName);
        if(settlementMap == null) return;
        List<CandidatePoint> mainCandidates = GetSortedCandidates(settlementMap, mainVillageScoreThreshold);
        PlaceVillages(mainCandidates, numMainVillages, objectsPerMainVillage, mainVillageRadius, mainVillageMinDistance);

        // --- パス2: 道路沿い集落の配置 ---
        Debug.Log("--- PASS 2: Placing Roadside Hamlets ---");
        Texture2D roadsideMap = LoadTexture(roadsideSuitabilityMapName);
        if(roadsideMap == null) return;
        List<CandidatePoint> roadsideCandidates = GetSortedCandidates(roadsideMap, roadsideScoreThreshold);
        RemoveCandidatesNearPlacedObjects(roadsideCandidates, roadsideHamletRadius);
        PlaceVillages(roadsideCandidates, numRoadsideHamlets, objectsPerRoadsideHamlet, roadsideHamletRadius, roadsideMinDistance);

        // --- パス3: 孤立家屋の配置 ---
        Debug.Log("--- PASS 3: Placing Scattered Houses ---");
        List<CandidatePoint> scatteredCandidates = GetSortedCandidates(settlementMap, scatteredScoreThreshold);
        RemoveCandidatesNearPlacedObjects(scatteredCandidates, scatteredMinDistance);
        PlaceScattered(scatteredCandidates, numScatteredHouses, scatteredMinDistance);
        
        Debug.Log($"Process finished. Total {allPlacedPositions.Count} objects placed.");
    }

    private void PlaceVillages(List<CandidatePoint> candidates, int numVillages, int objectsPerVillage, float villageRadius, float minDistance)
    {
        for (int i = 0; i < numVillages; i++)
        {
            if (candidates.Count == 0) break;
            CandidatePoint villageCenter = candidates[0];

            List<CandidatePoint> villageCandidates = candidates
                .Where(p => Vector2.Distance(p.position, villageCenter.position) < villageRadius)
                .OrderByDescending(p => p.score)
                .ToList();
            
            int placedInThisVillage = 0;
            while (placedInThisVillage < objectsPerVillage && villageCandidates.Count > 0)
            {
                CandidatePoint bestCandidate = villageCandidates[0];
                if(PlaceObjectAt(bestCandidate.position, minDistance))
                {
                    placedInThisVillage++;
                }
                villageCandidates.RemoveAt(0);
            }
            candidates.RemoveAll(p => Vector2.Distance(p.position, villageCenter.position) < villageRadius);
        }
    }

    private void PlaceScattered(List<CandidatePoint> candidates, int numToPlace, float minDistance)
    {
        int placedCount = 0;
        while(placedCount < numToPlace && candidates.Count > 0)
        {
            CandidatePoint bestCandidate = candidates[0];
            if(PlaceObjectAt(bestCandidate.position, minDistance))
            {
                placedCount++;
            }
            candidates.RemoveAt(0);
        }
    }
    
    private void RemoveCandidatesNearPlacedObjects(List<CandidatePoint> candidates, float clearance)
    {
        foreach (var pos in allPlacedPositions)
        {
            Vector2Int mapPos = GetMapPosition(pos);
            candidates.RemoveAll(p => Vector2.Distance(p.position, mapPos) < clearance);
        }
    }
    
    private bool PlaceObjectAt(Vector2Int mapPosition, float minDistance)
    {
        Vector3 worldPos = GetWorldPosition(mapPosition);
        foreach (var placedPos in allPlacedPositions)
        {
            if (Vector3.Distance(worldPos, placedPos) < minDistance)
            {
                return false;
            }
        }
        Instantiate(objectPrefab, worldPos, Quaternion.identity, this.transform);
        allPlacedPositions.Add(worldPos);
        return true;
    }

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
}