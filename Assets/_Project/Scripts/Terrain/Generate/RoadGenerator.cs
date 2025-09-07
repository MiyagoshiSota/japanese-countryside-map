using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class RoadAndBridgeMaskGenerator : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("道路を生成したいエリアを示すマスク画像（白が有効エリア）")]
    public Texture2D flatAreaMask;
    [Tooltip("川の位置を示すマスク画像（白が川）")]
    public Texture2D riverMask;

    [Header("道路ネットワーク設定")]
    [Tooltip("道路網の主要な経由点の数")]
    public int numberOfNodes = 20;
    [Tooltip("道路本体の幅")]
    public float roadWidth = 6f;
    [Tooltip("道路脇を滑らかにするための追加の幅")]
    public float smoothingWidth = 10f;

    [Header("ランダム設定")]
    public int seed = 0;

    private class Edge
    {
        public int u, v; public float distance;
        public Edge(int u, int v, float distance) { this.u = u; this.v = v; this.distance = distance; }
    }

    [ContextMenu("川を考慮した道路マスク画像を生成する")]
    public void GenerateRoadMask()
    {
        if (flatAreaMask == null || riverMask == null)
        {
            Debug.LogError("マスク画像(flatAreaMask or riverMask)が設定されていません！");
            return;
        }
        if (!flatAreaMask.isReadable || !riverMask.isReadable)
        {
            Debug.LogError("マスク画像のRead/Write設定を有効にしてください。");
            return;
        }

        int resolution = flatAreaMask.width;
        
        if (seed != 0) Random.InitState(seed);
        
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int i = 0; i < numberOfNodes; i++)
        {
            int x, y;
            int attempts = 0;
            do {
                x = Random.Range(0, resolution);
                y = Random.Range(0, resolution);
                attempts++;
                if (attempts > 1000) {
                    Debug.LogWarning("ノードを配置できる場所が見つかりませんでした。");
                    break;
                }
            } while (flatAreaMask.GetPixel(x, y).r < 0.5f || riverMask.GetPixel(x,y).r > 0.1f);

            if(attempts <= 1000) nodes.Add(new Vector2Int(x, y));
        }

        if (nodes.Count < 2)
        {
            Debug.LogWarning("道路を生成するにはノードが2つ以上必要です。");
            return;
        }

        List<Edge> edges = new List<Edge>();
        for (int i = 0; i < nodes.Count; i++) {
            for (int j = i + 1; j < nodes.Count; j++) {
                edges.Add(new Edge(i, j, Vector2.Distance(nodes[i], nodes[j])));
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

        // 道路と橋の形状を書き込むための一時的なマップ
        float[,] roadMap = new float[resolution, resolution];

        foreach (var edge in edges)
        {
            if (find(edge.u) != find(edge.v))
            {
                unite(edge.u, edge.v);
                DrawPathOnMap(nodes[edge.u], nodes[edge.v], resolution, roadMap);
            }
        }

        Debug.Log("マスク画像を生成して保存します...");
        Texture2D roadMaskTexture = CreateMaskTexture(roadMap, resolution);
        SaveTextureAsPNG(roadMaskTexture, "GeneratedRoadAndBridgeMask.png");
    }

    void DrawPathOnMap(Vector2Int start, Vector2Int end, int resolution, float[,] roadMap)
    {
        int pointsCount = (int)Vector2.Distance(start, end);
        float totalWidth = roadWidth + smoothingWidth;

        for (int k = 0; k <= pointsCount; k++)
        {
            float t = (float)k / pointsCount;
            int cx = (int)Mathf.Lerp(start.x, end.x, t);
            int cy = (int)Mathf.Lerp(start.y, end.y, t);

            for (int y = -(int)Mathf.CeilToInt(totalWidth); y <= (int)Mathf.CeilToInt(totalWidth); y++) {
                for (int x = -(int)Mathf.CeilToInt(totalWidth); x <= (int)Mathf.CeilToInt(totalWidth); x++) {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < resolution && py >= 0 && py < resolution)
                    {
                        float dist = Mathf.Sqrt(x * x + y * y);
                        
                        if (dist <= roadWidth / 2) {
                            roadMap[py, px] = 1.0f; // 道の中心は白
                        } else if (dist <= totalWidth / 2) {
                            // 道の脇は滑らかに減衰
                            float blendFactor = 1.0f - (dist - (roadWidth / 2f)) / smoothingWidth;
                            roadMap[py, px] = Mathf.Max(roadMap[py, px], blendFactor);
                        }
                    }
                }
            }
        }
    }
    
    private Texture2D CreateMaskTexture(float[,] map, int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++) {
            for (int x = 0; x < resolution; x++) {
                float value = map[y, x];
                pixels[y * resolution + x] = new Color(value, value, value);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void SaveTextureAsPNG(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        var dirPath = Path.Combine(Application.dataPath, "GeneratedMaps");
        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
        File.WriteAllBytes(Path.Combine(dirPath, filename), bytes);
        Debug.Log($"<color=green>{filename} を {dirPath} に保存しました。</color>");
    }
}