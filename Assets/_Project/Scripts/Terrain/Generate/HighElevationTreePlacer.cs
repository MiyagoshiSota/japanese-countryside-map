using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Terrain))]
public class HighElevationTreePlacer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    
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

    // ★ treePrefabs の項目は削除

    [Header("ランダム設定")]
    public int seed = 0;

    [ContextMenu("高所に木をまばらに配置する")]
    public void PlaceTrees()
    {
        if (!Initialize()) return;

        TerrainData terrainData = terrain.terrainData;
        int prototypeCount = terrainData.treePrototypes.Length;
        if (prototypeCount == 0)
        {
            Debug.LogError("Terrainに木が登録されていません！ Step 1の手順で木を登録してください。");
            return;
        }

        List<TreeInstance> treeInstances = new List<TreeInstance>();
        
        for (float y = 0; y < terrainData.size.z; y += 5) // 5mグリッド
        {
            for (float x = 0; x < terrainData.size.x; x += 5)
            {
                float normalizedX = x / terrainData.size.x;
                float normalizedZ = y / terrainData.size.z;

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
                    treeInstance.prototypeIndex = Random.Range(0, prototypeCount); // ★ 登録済みの木からランダムに選ぶ
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

    // ★ Initialize関数をシンプルに変更
    bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
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