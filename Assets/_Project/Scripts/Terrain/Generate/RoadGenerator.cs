using UnityEngine;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(Terrain))]
public class RoadGenerator : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    public Texture2D flatAreaMask; // TownMask.png または RiceFieldMask.png を合成したマスク

    [Header("道路設定")]
    [Tooltip("道路網の主要な経由点の数")]
    public int numberOfNodes = 15;
    [Tooltip("道路の幅")]
    public float roadWidth = 8f;
    [Tooltip("地形を平坦化する強さ (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float flattenStrength = 0.5f;

    [Header("ランダム設定")]
    [Tooltip("経由点の配置を変えるためのシード値")]
    public int seed = 0;

    [ContextMenu("道路を生成し地形を調整する")]
    public void GenerateRoads()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (flatAreaMask == null)
        {
            Debug.LogError("平地エリアのマスク(flatAreaMask)が設定されていません！");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] originalHeights = terrainData.GetHeights(0, 0, resolution, resolution);
        float[,] modifiedHeights = (float[,])originalHeights.Clone();

        // --- 1. 平地エリアにランダムな経由点（ノード）を配置 ---
        if (seed != 0) Random.InitState(seed);
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int i = 0; i < numberOfNodes; i++)
        {
            int x, y;
            // マスクの白い部分（平地）に当たるまでランダムな座標を探す
            do
            {
                x = Random.Range(0, resolution);
                y = Random.Range(0, resolution);
            } while (flatAreaMask.GetPixel(x, y).r < 0.5f); // r<0.5f は黒い部分
            nodes.Add(new Vector2Int(x, y));
        }

        // --- 2. ノード間を線で結び、地形を平坦化 ---
        Texture2D roadVisualizer = new Texture2D(resolution, resolution); // 可視化用
        // 全てのノードのペアに対して処理
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                // 簡単な直線でノード間を結ぶ
                Vector2Int start = nodes[i];
                Vector2Int end = nodes[j];
                int pointsCount = (int)Vector2.Distance(start, end);

                for (int k = 0; k <= pointsCount; k++)
                {
                    float t = (float)k / pointsCount;
                    int currentX = (int)Mathf.Lerp(start.x, end.x, t);
                    int currentY = (int)Mathf.Lerp(start.y, end.y, t);

                    // 道路の幅に影響する範囲をループ
                    for (int offsetY = -(int)roadWidth / 2; offsetY <= (int)roadWidth / 2; offsetY++)
                    {
                        for (int offsetX = -(int)roadWidth / 2; offsetX <= (int)roadWidth / 2; offsetX++)
                        {
                            int px = currentX + offsetX;
                            int py = currentY + offsetY;

                            if (px >= 0 && px < resolution && py >= 0 && py < resolution)
                            {
                                // 現在の高さと目標の高さをブレンドして、なだらかにする
                                float originalHeight = originalHeights[py, px];
                                float targetHeight = originalHeights[currentY, currentX]; // 道路の中心の高さに合わせる
                                modifiedHeights[py, px] = Mathf.Lerp(originalHeight, targetHeight, flattenStrength);
                                
                                // 可視化テクスチャを白く塗る
                                roadVisualizer.SetPixel(px, py, Color.white);
                            }
                        }
                    }
                }
            }
        }
        
        // 変更したハイトマップを地形に適用
        terrainData.SetHeights(0, 0, modifiedHeights);
        
        // 可視化テクスチャを保存
        roadVisualizer.Apply();
        SaveTextureAsPNG(roadVisualizer, "RoadVisualizer.png");

        Debug.Log("道路の生成と地形の調整が完了しました。");
    }

    private void SaveTextureAsPNG(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", filename), bytes);
    }
}