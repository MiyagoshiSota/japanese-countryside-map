using UnityEngine;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(Terrain))]
public class RoadGenerator : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    public Texture2D flatAreaMask; // TownMaskとRiceFieldMaskを合成したもの

    [Header("道路設定")]
    [Tooltip("道路網の主要な経由点の数")]
    public int numberOfNodes = 10;
    [Tooltip("道路本体の幅")]
    public float roadWidth = 6f;
    [Tooltip("道路脇の、地形を滑らかにするための追加の幅")]
    public float smoothingWidth = 10f;
    [Tooltip("道路が周囲の地形より高くなる絶対的な高さ (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float roadElevationHeight = 0.05f;

    [Header("ランダム設定")]
    [Tooltip("経由点の配置を変えるためのシード値")]
    public int seed = 0;

    [ContextMenu("高台の道路を生成し地形を調整する")]
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

        if (seed != 0) Random.InitState(seed);
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int i = 0; i < numberOfNodes; i++)
        {
            int x, y;
            do {
                x = Random.Range(0, resolution);
                y = Random.Range(0, resolution);
            } while (flatAreaMask.GetPixel(x, y).r < 0.5f);
            nodes.Add(new Vector2Int(x, y));
        }

        // --- ★ここから修正 ---
        Texture2D roadVisualizer = new Texture2D(resolution, resolution);
        // 全ピクセルを一度に黒く塗りつぶす
        Color[] blackPixels = new Color[resolution * resolution];
        for (int i = 0; i < blackPixels.Length; i++)
        {
            blackPixels[i] = Color.black;
        }
        roadVisualizer.SetPixels(blackPixels);
        // --- ★ここまで修正 ---
        
        // --- 最小全域木でノードを結ぶロジック ---
        List<Edge> edges = new List<Edge>();
        for (int i = 0; i < nodes.Count; i++) {
            for (int j = i + 1; j < nodes.Count; j++) {
                float distance = Vector2.Distance(nodes[i], nodes[j]);
                edges.Add(new Edge(i, j, distance));
            }
        }
        edges.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        int[] parent = new int[nodes.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int find(int i) => (parent[i] == i) ? i : (parent[i] = find(parent[i]));
        void unite(int i, int j) {
            int rootI = find(i);
            int rootJ = find(j);
            if(rootI != rootJ) parent[rootI] = rootJ;
        }

        foreach (var edge in edges)
        {
            if (find(edge.u) != find(edge.v))
            {
                unite(edge.u, edge.v);
                DrawElevatedRoadAndSmooth(nodes[edge.u], nodes[edge.v], terrainData, modifiedHeights, originalHeights, roadVisualizer);
            }
        }

        terrainData.SetHeights(0, 0, modifiedHeights);
        
        roadVisualizer.Apply();
        SaveTextureAsPNG(roadVisualizer, "RoadVisualizer.png");
        Debug.Log("高台道路の生成と地形調整が完了しました。");
    }

    void DrawElevatedRoadAndSmooth(Vector2Int start, Vector2Int end, TerrainData terrainData, float[,] modifiedHeights, float[,] originalHeights, Texture2D roadVisualizer)
    {
        int resolution = terrainData.heightmapResolution;
        int pointsCount = (int)Vector2.Distance(start, end);

        for (int k = 0; k <= pointsCount; k++)
        {
            float t = (float)k / pointsCount;
            int cx = (int)Mathf.Lerp(start.x, end.x, t);
            int cy = (int)Mathf.Lerp(start.y, end.y, t);

            float targetHeightAtCenter = originalHeights[cy, cx] + roadElevationHeight; 
            targetHeightAtCenter = Mathf.Clamp01(targetHeightAtCenter);

            float totalWidth = roadWidth + smoothingWidth;

            for (int y = -(int)totalWidth; y <= (int)totalWidth; y++)
            {
                for (int x = -(int)totalWidth; x <= (int)totalWidth; x++)
                {
                    int px = cx + x;
                    int py = cy + y;
                    float dist = Mathf.Sqrt(x * x + y * y);

                    if (px >= 0 && px < resolution && py >= 0 && py < resolution)
                    {
                        if (dist <= roadWidth / 2)
                        {
                            modifiedHeights[py, px] = targetHeightAtCenter;
                            // 道路部分だけ白く上書きする
                            roadVisualizer.SetPixel(px, py, Color.white);
                        }
                        else if (dist <= totalWidth / 2)
                        {
                            float blendFactor = 1.0f - (dist - (roadWidth / 2f)) / smoothingWidth;
                            modifiedHeights[py, px] = Mathf.Lerp(originalHeights[py, px], targetHeightAtCenter, blendFactor);
                        }
                    }
                }
            }
        }
    }

    private void SaveTextureAsPNG(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        // 保存先をプロジェクトのルートフォルダに変更
        File.WriteAllBytes(Path.Combine(Application.dataPath, filename), bytes);
    }
    
    private class Edge {
        public int u, v; public float distance;
        public Edge(int u, int v, float distance) { this.u = u; this.v = v; this.distance = distance; }
    }
}