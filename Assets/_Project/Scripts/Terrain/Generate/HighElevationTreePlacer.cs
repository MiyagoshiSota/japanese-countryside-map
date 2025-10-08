using System.Collections.Generic;
using UnityEngine;

// File I/Oのために追加

namespace _Project.Scripts.Terrain.Generate
{
    [RequireComponent(typeof(UnityEngine.Terrain))]
    public class HighElevationTreePlacer : MonoBehaviour
    {
        [Header("参照")]
        public UnityEngine.Terrain terrain;
    
        // --- ▼ マスク設定の項目を更新 ▼ ---
        [Header("マスク設定")]
        [Tooltip("木を配置したくない畑エリアを指定するマスク")]
        public Texture2D paddyMask;
        [Tooltip("木を配置したくない道路エリアを指定するマスク")]
        public Texture2D roadMask;
        [Tooltip("道路の周囲、何ピクセルに木を生成しないか")]
        [Range(0, 20)]
        public int roadProximityBuffer = 5;
        // --- ▲ ここまで ▲ ---
    
        [Header("配置設定")]
        [Tooltip("木を配置し始める最低標高 (0.0 = 海面, 1.0 = 最高峰)")]
        [Range(0f, 1f)]
        public float minPlacementHeight = 0.4f;

        [Tooltip("木を配置する最大傾斜（これ以上急な崖には生えない）")]
        [Range(0f, 90f)]
        public float maxPlacementSlope = 40f;
    
        [Tooltip("木の密度（値が小さいほど、まばらになります）")]
        [Range(0f, 1f)]
        public float density = 0.05f;

        [Header("ランダム設定")]
        public int seed = 0;

        [ContextMenu("高所に木をまばらに配置する")]
        public void PlaceTrees()
        {
            if (!Initialize()) return;

            // --- ▼ マスクの事前処理をここで行う ▼ ---
            if (paddyMask == null || roadMask == null) { Debug.LogError("Paddy Mask または Road Mask が設定されていません！"); return; }
            if (paddyMask.width != roadMask.width || paddyMask.height != roadMask.height) { Debug.LogError("両方のマスクの解像度を一致させてください！"); return; }

            int maskWidth = roadMask.width;
            int maskHeight = roadMask.height;
        
            // 道路マスクを読み込み、指定されたピクセル数だけ膨張（バッファーを作成）させる
            bool[] dilatedRoadMask = DilateRoadMask(roadMask, roadProximityBuffer);
        
            Color[] paddyMaskPixels = paddyMask.GetPixels();
            // --- ▲ ここまで ▲ ---

            TerrainData terrainData = terrain.terrainData;
            int prototypeCount = terrainData.treePrototypes.Length;
            if (prototypeCount == 0) { Debug.LogError("Terrainに木が登録されていません！"); return; }

            List<TreeInstance> treeInstances = new List<TreeInstance>();
        
            for (float y = 0; y < terrainData.size.z; y += 5)
            {
                for (float x = 0; x < terrainData.size.x; x += 5)
                {
                    float normalizedX = x / terrainData.size.x;
                    float normalizedZ = y / terrainData.size.z;

                    // --- ▼ マスクのチェック処理を更新 ▼ ---
                    int maskX = (int)(normalizedX * maskWidth);
                    int maskY = (int)(normalizedZ * maskHeight);
                    int pixelIndex = maskY * maskWidth + maskX;

                    // 畑マスクのチェック
                    Color paddyPixel = paddyMaskPixels[pixelIndex];
                    bool isPaddyArea = paddyPixel.r > 0.5f || paddyPixel.g > 0.5f;
                
                    // 膨張させた道路マスクのチェック
                    bool isNearRoad = dilatedRoadMask[pixelIndex];

                    if (isPaddyArea || isNearRoad) // 畑エリア、または道路の近くだったらスキップ
                    {
                        continue;
                    }
                    // --- ▲ ここまで ▲ ---

                    float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) / terrainData.size.y;
                    if (height < minPlacementHeight) continue;

                    float slope = terrainData.GetSteepness(normalizedX, normalizedZ);
                    if (slope > maxPlacementSlope) continue;

                    if (Random.value < density)
                    {
                        float jitterX = (x + Random.Range(-2.5f, 2.5f)) / terrainData.size.x;
                        float jitterZ = (y + Random.Range(-2.5f, 2.5f)) / terrainData.size.z;

                        TreeInstance treeInstance = new TreeInstance();
                        treeInstance.position = new Vector3(jitterX, 0, jitterZ);
                        treeInstance.prototypeIndex = Random.Range(0, prototypeCount);
                        treeInstance.widthScale = Random.Range(0.8f, 1.5f);
                        treeInstance.heightScale = Random.Range(0.8f, 1.5f);
                        treeInstance.color = Color.white;
                        treeInstance.lightmapColor = Color.white;
                    
                        treeInstances.Add(treeInstance);
                    }
                }
            }
        
            terrainData.SetTreeInstances(treeInstances.ToArray(), true);
            Debug.Log($"{treeInstances.Count}本の木を高所に配置しました。");
        }

        // --- ▼▼▼ 新しく追加したメソッド ▼▼▼ ---
        /// <summary>
        /// 道路マスクを読み込み、指定された半径（radius）ピクセル分だけ膨張させたマスクを生成する
        /// </summary>
        private bool[] DilateRoadMask(Texture2D roadMask, int radius)
        {
            int width = roadMask.width;
            int height = roadMask.height;
            Color[] roadPixels = roadMask.GetPixels();
            bool[] initialRoadMask = new bool[width * height];

            // まず、元の道路（白い部分）をbool配列に変換
            for (int i = 0; i < initialRoadMask.Length; i++)
            {
                if (roadPixels[i].r > 0.5f)
                {
                    initialRoadMask[i] = true;
                }
            }

            if (radius == 0) return initialRoadMask;

            bool[] dilatedMask = new bool[width * height];
            // 次に、元の道路ピクセルの周囲を指定された半径分だけ true で埋める
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 自身のピクセルか、周囲のピクセルをチェック
                    for (int j = -radius; j <= radius; j++)
                    {
                        for (int i = -radius; i <= radius; i++)
                        {
                            int nx = x + i;
                            int ny = y + j;

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                if (initialRoadMask[ny * width + nx])
                                {
                                    dilatedMask[y * width + x] = true;
                                    goto nextPixel; // このピクセルは確定したので次のピクセルへ
                                }
                            }
                        }
                    }
                    nextPixel:;
                }
            }
            return dilatedMask;
        }
    
        // --- 以下のメソッドは変更ありません ---
        bool Initialize()
        {
            if (terrain == null) terrain = GetComponent<UnityEngine.Terrain>();
            if (seed != 0) Random.InitState(seed);
            return true;
        }

        [ContextMenu("配置した木を全て削除")]
        void ClearTrees()
        {
            if (terrain != null)
            {
                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
            }
        }
    }
}