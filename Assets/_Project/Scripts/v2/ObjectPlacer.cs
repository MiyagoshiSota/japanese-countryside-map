using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace v2
{
    public class ObjectPlacer : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("通常の集落適性マップ")]
        [SerializeField] private string settlementSuitabilityMapName = "SettlementSuitabilityMap.png";
        [Tooltip("道路沿い優先の適性マップ")]
        [SerializeField] private string roadsideSuitabilityMapName = "RoadsideSuitabilityMap.png";
        [Tooltip("田んぼのマスク画像（赤/緑の領域を除外）")]
        [SerializeField] private string paddyFieldMaskName = "PaddyFieldMask.png";
        [Tooltip("道路マップ画像（白色の領域を除外）")]
        [SerializeField] private string roadMapName = "RoadMap.png";
        [Tooltip("オブジェクトを配置するTerrain")]
        [SerializeField] private Terrain targetTerrain;
        [Tooltip("配置するオブジェクトのプレハブ")]
        [SerializeField] private GameObject objectPrefab;

        // --- ▼ここに追加▼ ---
        [Header("Mask Settings")]
        [Tooltip("建築不可マスクを太らせる量(ピクセル数)。線の細さが問題な場合に大きくします。")]
        [SerializeField, Range(0, 10)] private int maskDilation = 2;
        // --- ▲ここまで▲ ---

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

            Texture2D paddyMaskTex = LoadTexture(paddyFieldMaskName);
            Texture2D roadMapTex = LoadTexture(roadMapName);
            if (paddyMaskTex == null || roadMapTex == null) { return; }

            // --- ▼ 2つのマスクを合成し、太らせる処理 ▼ ---
            bool[] combinedMask = CreateCombinedMask(paddyMaskTex, roadMapTex, maskDilation);
            // --- ▲ ここまで ▲ ---

            Debug.Log("--- PASS 1: Placing Main Villages ---");
            Texture2D settlementMap = LoadTexture(settlementSuitabilityMapName);
            if(settlementMap == null) return;
            List<CandidatePoint> mainCandidates = GetSortedCandidates(settlementMap, mainVillageScoreThreshold, combinedMask);
            PlaceVillages(mainCandidates, numMainVillages, objectsPerMainVillage, mainVillageRadius, mainVillageMinDistance);

            Debug.Log("--- PASS 2: Placing Roadside Hamlets ---");
            Texture2D roadsideMap = LoadTexture(roadsideSuitabilityMapName);
            if(roadsideMap == null) return;
            List<CandidatePoint> roadsideCandidates = GetSortedCandidates(roadsideMap, roadsideScoreThreshold, combinedMask);
            RemoveCandidatesNearPlacedObjects(roadsideCandidates, roadsideHamletRadius);
            PlaceVillages(roadsideCandidates, numRoadsideHamlets, objectsPerRoadsideHamlet, roadsideHamletRadius, roadsideMinDistance);

            Debug.Log("--- PASS 3: Placing Scattered Houses ---");
            List<CandidatePoint> scatteredCandidates = GetSortedCandidates(settlementMap, scatteredScoreThreshold, combinedMask);
            RemoveCandidatesNearPlacedObjects(scatteredCandidates, scatteredMinDistance);
            PlaceScattered(scatteredCandidates, numScatteredHouses, scatteredMinDistance);
            
            Debug.Log($"Process finished. Total {allPlacedPositions.Count} objects placed.");
        }

        // --- ▼▼▼ 新しく追加したメソッド ▼▼▼ ---
        /// <summary>
        /// 畑マスクと道路マスクを合成し、指定された量だけ膨張（Dilation）させる
        /// </summary>
        private bool[] CreateCombinedMask(Texture2D paddyMask, Texture2D roadMask, int dilation)
        {
            int width = paddyMask.width;
            int height = paddyMask.height;
            
            Color[] paddyPixels = paddyMask.GetPixels();
            Color[] roadPixels = roadMask.GetPixels();

            bool[] initialMask = new bool[width * height];
            for (int i = 0; i < initialMask.Length; i++)
            {
                bool isPaddy = paddyPixels[i].r > 0.5f || paddyPixels[i].g > 0.5f;
                bool isRoad = roadPixels[i].r > 0.5f;
                if (isPaddy || isRoad)
                {
                    initialMask[i] = true; // true = 建築不可
                }
            }

            if (dilation == 0) return initialMask;

            bool[] dilatedMask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // もし現在のピクセルが建築不可なら、最終マスクも建築不可
                    if (initialMask[y * width + x])
                    {
                        dilatedMask[y * width + x] = true;
                        continue;
                    }

                    // 周囲のピクセルをチェックして、建築不可エリアを膨張させる
                    for (int dy = -dilation; dy <= dilation; dy++)
                    {
                        for (int dx = -dilation; dx <= dilation; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                if (initialMask[ny * width + nx])
                                {
                                    dilatedMask[y * width + x] = true;
                                    goto nextPixel; // このピクセルは建築不可に確定したので次のピクセルへ
                                }
                            }
                        }
                    }
                    nextPixel:;
                }
            }
            return dilatedMask;
        }
        
        // --- ▼▼▼ GetSortedCandidatesをシンプルに変更 ▼▼▼ ---
        private List<CandidatePoint> GetSortedCandidates(Texture2D map, float threshold, bool[] exclusionMask)
        {
            List<CandidatePoint> candidates = new List<CandidatePoint>();
            int width = map.width;
            int height = map.height;
            Color[] mapPixels = map.GetPixels();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    
                    // 合成されたマスクをチェック
                    if (exclusionMask[index])
                    {
                        continue; // 建築不可エリアなのでスキップ
                    }
                    
                    float score = mapPixels[index].r;
                    if (score >= threshold)
                    {
                        candidates.Add(new CandidatePoint { position = new Vector2Int(x, y), score = score });
                    }
                }
            }
            return candidates.OrderByDescending(p => p.score).ToList();
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

        // --- ▼このメソッドを大幅に修正▼ ---
        private List<CandidatePoint> GetSortedCandidates(Texture2D map, float threshold, Texture2D paddyMask, Texture2D roadMap)
        {
            List<CandidatePoint> candidates = new List<CandidatePoint>();

            if (map.width != paddyMask.width || map.height != paddyMask.height || 
                map.width != roadMap.width || map.height != roadMap.height)
            {
                Debug.LogError("All input textures must have the same dimensions.");
                return candidates;
            }

            int width = map.width;
            int height = map.height;

            Color[] mapPixels = map.GetPixels();
            Color[] paddyPixels = paddyMask.GetPixels();
            Color[] roadPixels = roadMap.GetPixels();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    // ▼▼▼ この行を修正 ▼▼▼
                    // 田んぼマスクのチェック (赤色 "または" 緑色を避ける)
                    if (paddyPixels[index].r > 0.5f || paddyPixels[index].g > 0.5f)
                    {
                        continue; // この地点は田んぼエリアなので候補から除外
                    }
                    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

                    // 道路マップのチェック (白色を避ける)
                    if (roadPixels[index].r > 0.5f)
                    {
                        continue; 
                    }

                    float score = mapPixels[index].r;
                    if (score >= threshold)
                    {
                        candidates.Add(new CandidatePoint { position = new Vector2Int(x, y), score = score });
                    }
                }
            }
            return candidates.OrderByDescending(p => p.score).ToList();
        }
        // --- ▲ここまで▲ ---

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