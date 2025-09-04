using System.Collections.Generic;
using UnityEngine;

public class ObjectGenerator : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    [Tooltip("階層を整理するための親オブジェクト")]
    public Transform objectsParent;

    [Header("マスク")]
    public Texture2D forestMask;
    public Texture2D townMask;
    
    [Header("森林の配置設定")]
    [Tooltip("配置する木のプレハブ（TerrainのTreesに要登録）")]
    public GameObject[] treePrefabs;
    [Range(0f, 1f)]
    public float treePlacementThreshold = 0.5f;
    [Tooltip("木の密度")]
    [Range(0f, 0.1f)]
    public float treeDensity = 0.02f;

    [Header("町の配置設定")]
    [Tooltip("配置する家のプレハブ")]
    public GameObject[] housePrefabs;
    [Range(0f, 1f)]
    public float housePlacementThreshold = 0.5f;
    [Tooltip("家の配置を試みる間隔（小さいほど密になる）")]
    public float houseGridSize = 20f;

    [Header("ランダム設定")]
    public int seed = 0;

    [ContextMenu("大きなものを配置する")]
    public void PlaceObjects()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (seed != 0) Random.InitState(seed);

        ClearPreviousObjects();
        PlaceTrees();
        PlaceHouses();
    }

    void PlaceTrees()
    {
        TerrainData terrainData = terrain.terrainData;
        List<TreeInstance> treeInstances = new List<TreeInstance>();
        
        // TerrainにTree Prototypeを登録する
        if (treePrefabs.Length > 0)
        {
            TreePrototype[] treePrototypes = new TreePrototype[treePrefabs.Length];
            for (int i = 0; i < treePrefabs.Length; i++)
            {
                treePrototypes[i] = new TreePrototype();
                treePrototypes[i].prefab = treePrefabs[i];
            }
            terrainData.treePrototypes = treePrototypes;
        }

        for (float y = 0; y < terrainData.size.z; y += 5) // 5mごとにチェック
        {
            for (float x = 0; x < terrainData.size.x; x += 5)
            {
                float normalizedX = x / terrainData.size.x;
                float normalizedY = y / terrainData.size.z;
                
                if (forestMask.GetPixelBilinear(normalizedX, normalizedY).r > treePlacementThreshold)
                {
                    if (Random.value < treeDensity)
                    {
                        TreeInstance treeInstance = new TreeInstance();
                        treeInstance.position = new Vector3(normalizedX, 0, normalizedY);
                        treeInstance.prototypeIndex = Random.Range(0, treePrefabs.Length);
                        treeInstance.widthScale = Random.Range(0.8f, 1.2f);
                        treeInstance.heightScale = Random.Range(0.8f, 1.2f);
                        treeInstance.color = Color.white;
                        treeInstance.lightmapColor = Color.white;
                        
                        treeInstances.Add(treeInstance);
                    }
                }
            }
        }
        terrainData.SetTreeInstances(treeInstances.ToArray(), true);
        Debug.Log($"{treeInstances.Count}本の木を配置しました。");
    }

    void PlaceHouses()
    {
        TerrainData terrainData = terrain.terrainData;
        if (housePrefabs.Length == 0) return;

        for (float y = 0; y < terrainData.size.z; y += houseGridSize)
        {
            for (float x = 0; x < terrainData.size.x; x += houseGridSize)
            {
                float normalizedX = x / terrainData.size.x;
                float normalizedY = y / terrainData.size.z;

                if (townMask.GetPixelBilinear(normalizedX, normalizedY).r > housePlacementThreshold)
                {
                    float jitterX = x + Random.Range(-houseGridSize / 2, houseGridSize / 2);
                    float jitterY = y + Random.Range(-houseGridSize / 2, houseGridSize / 2);

                    Vector3 position = new Vector3(jitterX, 0, jitterY);
                    position.y = terrain.SampleHeight(position);

                    GameObject prefabToPlace = housePrefabs[Random.Range(0, housePrefabs.Length)];
                    Quaternion rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

                    GameObject newHouse = Instantiate(prefabToPlace, position, rotation);
                    if (objectsParent != null) newHouse.transform.SetParent(objectsParent);
                }
            }
        }
        Debug.Log("家を配置しました。");
    }

    [ContextMenu("配置したものを削除")]
    void ClearPreviousObjects()
    {
        if (terrain != null)
        {
            terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
        }
        if (objectsParent != null)
        {
            for (int i = objectsParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(objectsParent.GetChild(i).gameObject);
            }
        }
    }
}