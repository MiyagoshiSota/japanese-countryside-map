// ElevatedRoadGenerator.cs

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Terrainの指定した標高の範囲に木を自動配置するジェネレーター
/// </summary>
[AddComponentMenu("Terrain/Elevated Tree Generator")]
public class ElevatedRoadGenerator : MonoBehaviour
{
    [Header("基本設定")]
    [Tooltip("木を配置する対象のTerrainオブジェクト")]
    public Terrain targetTerrain;

    [Tooltip("配置する木のプレハブ（複数設定可）")]
    public GameObject[] treePrefabs;

    [Header("配置パラメータ")]
    [Tooltip("配置する木のおおよその本数")]
    [Range(0, 5000)]
    public int treeDensity = 500;

    [Tooltip("木を配置し始める最低標高（ワールド座標）")]
    public float minPlacementHeight = 50f;

    [Tooltip("木を配置する上限の標高（ワールド座標）")]
    public float maxPlacementHeight = 200f;

    [Header("木のスケール設定")]
    [Tooltip("木の最小スケール")]
    [Range(0.1f, 3.0f)]
    public float minTreeScale = 0.8f;

    [Tooltip("木の最大スケール")]
    [Range(0.1f, 3.0f)]
    public float maxTreeScale = 1.5f;


    /// <summary>
    /// インスペクターのコンテキストメニューから木を生成します。
    /// </summary>
    [ContextMenu("木を生成 (Generate Trees)")]
    public void GenerateTrees()
    {
        // --- 事前チェック ---
        if (targetTerrain == null)
        {
            Debug.LogError("対象のTerrainが設定されていません！");
            return;
        }

        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("木のプレハブが設定されていません！");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;

        // --- 木のプロトタイプ（種類）をTerrainに登録 ---
        var treePrototypes = new List<TreePrototype>();
        foreach (var prefab in treePrefabs)
        {
            treePrototypes.Add(new TreePrototype { prefab = prefab });
        }
        terrainData.treePrototypes = treePrototypes.ToArray();

        // --- 新しい木のインスタンスリストを作成 ---
        var newTreeInstances = new List<TreeInstance>();

        for (int i = 0; i < treeDensity; i++)
        {
            // Terrain上のランダムな位置を決定 (0.0 ~ 1.0の正規化された座標)
            float randomX = Random.Range(0f, 1f);
            float randomZ = Random.Range(0f, 1f);

            // その地点の標高を取得
            float normalizedHeight = terrainData.GetHeight(
                (int)(randomX * terrainData.heightmapResolution),
                (int)(randomZ * terrainData.heightmapResolution)
            ) / terrainData.size.y;

            // ワールド座標での高さを計算
            float worldY = (normalizedHeight * terrainData.size.y) + targetTerrain.transform.position.y;

            // 高さが指定範囲内かチェック
            if (worldY >= minPlacementHeight && worldY <= maxPlacementHeight)
            {
                // 木のインスタンスを作成
                var treeInstance = new TreeInstance();
                
                // 位置を設定
                treeInstance.position = new Vector3(randomX, normalizedHeight, randomZ);
                
                // 木の種類をランダムに選択
                treeInstance.prototypeIndex = Random.Range(0, treePrototypes.Count);

                // スケールをランダムに設定
                float randomScale = Random.Range(minTreeScale, maxTreeScale);
                treeInstance.widthScale = randomScale;
                treeInstance.heightScale = randomScale;
                
                // 色とライトマップ色を設定
                treeInstance.color = Color.white;
                treeInstance.lightmapColor = Color.white;

                newTreeInstances.Add(treeInstance);
            }
        }

        // --- Terrainに木の情報を設定 ---
        // 既存の木はすべてクリアされ、新しい木で上書きされます
        terrainData.SetTreeInstances(newTreeInstances.ToArray(), true);

        Debug.Log($"<color=green>{newTreeInstances.Count} 本の木を配置しました。</color>");
    }

    /// <summary>
    /// インスペクターのコンテキストメニューからすべての木を削除します。
    /// </summary>
    [ContextMenu("すべての木を削除 (Clear Trees)")]
    public void ClearTrees()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("対象のTerrainが設定されていません！");
            return;
        }

        targetTerrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
        Debug.Log("<color=orange>Terrain上のすべての木を削除しました。</color>");
    }
}