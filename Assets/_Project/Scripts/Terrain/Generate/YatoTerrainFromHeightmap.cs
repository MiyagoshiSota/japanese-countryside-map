using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class LeafVeinMaskGenerator : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("地形全体の高さ参照用のハイトマップ")]
    public Texture2D heightmapTexture;
    [Tooltip("川を生成したいエリアを示すマスク画像（白が平地）")]
    public Texture2D flatAreaMask;

    [Header("河川ネットワーク設定")]
    [Tooltip("本流の源流以外の、枝分かれする支流の源流の数")]
    public int numberOfTributaries = 50; // 本流の始点は1つに固定するため、支流の数のみ設定

    [Tooltip("最も細い源流部の川幅")]
    public float minRiverWidth = 1.5f;
    [Tooltip("合流を重ねた最も太い下流部の川幅")]
    public float maxRiverWidth = 12f;
    [Tooltip("川岸を滑らかにするための追加の幅")]
    public float smoothingWidth = 10f;

    [Header("ランダム設定")]
    public int seed = 0;

    private class Edge
    {
        public int u, v; public float distance;
        public Edge(int u, int v, float distance) { this.u = u; this.v = v; this.distance = distance; }
    }
    
    // C# 7.0 Tuple for convenience
    private struct PointData
    {
        public Vector2Int position;
        public float height;
        public PointData(Vector2Int pos, float h) { position = pos; height = h; }
    }

    [ContextMenu("川のマスク画像（ハイトマップ）を生成する")]
    public void GenerateRiverMask()
    {
        if (heightmapTexture == null || flatAreaMask == null) {
            Debug.LogError("ハイトマップまたは平地マスクが設定されていません！");
            return;
        }
        if (!heightmapTexture.isReadable || !flatAreaMask.isReadable) {
            Debug.LogError("使用するテクスチャのRead/Write設定を有効にしてください。");
            return;
        }

        int resolution = heightmapTexture.width;
        
        float[,] originalHeights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++) {
            for (int x = 0; x < resolution; x++) {
                originalHeights[y, x] = heightmapTexture.GetPixelBilinear((float)x / resolution, (float)y / resolution).r;
            }
        }
        
        if (seed != 0) Random.InitState(seed);
        
        // --- ▼▼▼ ノード配置ロジックを再々度改良 ▼▼▼ ---
        
        // 1. マスク内の有効な全地点をリストアップ
        List<PointData> validPoints = new List<PointData>();
        for (int y = 0; y < resolution; y++) {
            for (int x = 0; x < resolution; x++) {
                if (flatAreaMask.GetPixel(x,y).r > 0.5f) {
                    validPoints.Add(new PointData(new Vector2Int(x,y), originalHeights[y,x]));
                }
            }
        }
        
        if(validPoints.Count < 1 + numberOfTributaries) // 最低1つの本流始点とTributaries分のノードが必要
        {
            Debug.LogWarning("マスクエリアが狭すぎるため、指定された数のノードを配置できません。");
            return;
        }
        
        // 2. ノードリストを初期化
        List<Vector2Int> nodes = new List<Vector2Int>();
        
        // 3. 最も標高が低い地点を、本流の「始まり」として追加（ルートノード）
        PointData lowestPoint = validPoints.OrderBy(p => p.height).First();
        nodes.Add(lowestPoint.position);
        
        // 4. 残りの地点から、標高が高い順にソートし、ランダムに支流ノードを選択
        List<PointData> tributaryCandidates = validPoints
                                            .Where(p => p.position != lowestPoint.position) // 最低地点は除く
                                            .OrderByDescending(p => p.height) // 高い場所から選択されやすくする
                                            .ToList();

        for (int i = 0; i < numberOfTributaries; i++) {
            if (tributaryCandidates.Count == 0) break;

            // 残りの候補からランダムに選択
            int randIndex = Random.Range(0, tributaryCandidates.Count);
            nodes.Add(tributaryCandidates[randIndex].position);
            tributaryCandidates.RemoveAt(randIndex);
        }
        
        // --- ▲▲▲ 改良箇所 おわり ▲▲▲ ---

        if (nodes.Count < 2) {
            Debug.LogWarning("川を生成するにはノードが2つ以上必要です。");
            return;
        }
        
        // 最小全域木(MST)でノードを接続
        List<Edge> edges = new List<Edge>();
        for (int i = 0; i < nodes.Count; i++) {
            for (int j = i + 1; j < nodes.Count; j++) {
                edges.Add(new Edge(i, j, Vector2.Distance(nodes[i], nodes[j])));
            }
        }
        edges.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        List<Edge> mstEdges = new List<Edge>();
        int[] parent = new int[nodes.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int find(int i) => (parent[i] == i) ? i : (parent[i] = find(parent[i]));
        void unite(int i, int j) {
            int rootI = find(i);
            int rootJ = find(j);
            if(rootI != rootJ) parent[rootI] = rootJ;
        }
        foreach (var edge in edges) {
            if (find(edge.u) != find(edge.v)) {
                unite(edge.u, edge.v);
                mstEdges.Add(edge);
            }
        }

        // 流量を計算 (MSTのルートノードは、ここで選択されたlowestPointのindexになる)
        // lowestPointがnodesリストの最初の要素として追加されているため、rootNodeIndexは0
        int rootNodeIndex = 0; // 最も標高の低い地点がnodes[0]になるようにした

        Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
        for (int i = 0; i < nodes.Count; i++) adjacencyList[i] = new List<int>();
        foreach (var edge in mstEdges) {
            adjacencyList[edge.u].Add(edge.v);
            adjacencyList[edge.v].Add(edge.u);
        }
        
        int[] flow = new int[nodes.Count];
        int[] parentMap = new int[nodes.Count];
        for(int i = 0; i < nodes.Count; i++) parentMap[i] = -1;

        Queue<int> queue = new Queue<int>();
        queue.Enqueue(rootNodeIndex);
        
        List<int> bfsOrder = new List<int>();
        bfsOrder.Add(rootNodeIndex);
        parentMap[rootNodeIndex] = -2;

        while(queue.Count > 0) {
            int u = queue.Dequeue();
            foreach(var v in adjacencyList[u]) {
                if(parentMap[v] == -1) {
                    parentMap[v] = u;
                    queue.Enqueue(v);
                    bfsOrder.Add(v);
                }
            }
        }

        for(int i = bfsOrder.Count - 1; i >= 0; i--) {
            int u = bfsOrder[i];
            flow[u] = 1;
            foreach(var v in adjacencyList[u]) {
                if(parentMap[u] != v) {
                    flow[u] += flow[v];
                }
            }
        }
        
        float[,] riverMap = new float[resolution, resolution];
        
        foreach (var edge in mstEdges) {
            int downstreamNode = (parentMap[edge.u] == edge.v) ? edge.v : edge.u;
            float flowRatio = (float)flow[downstreamNode] / nodes.Count;
            float currentWidth = Mathf.Lerp(minRiverWidth, maxRiverWidth, flowRatio);
            
            DrawRiverOnMap(nodes[edge.u], nodes[edge.v], resolution, riverMap, currentWidth);
        }
        
        Debug.Log("マスク画像を生成して保存します...");
        Texture2D riverMaskTexture = CreateMaskTexture(riverMap, resolution);
        SaveTextureAsPNG(riverMaskTexture, "GeneratedRiverMask.png");
    }

    private void DrawRiverOnMap(Vector2Int start, Vector2Int end, int resolution, float[,] riverMap, float riverWidth)
    {
        int pointsCount = (int)Vector2.Distance(start, end);
        float totalWidth = riverWidth + smoothingWidth;

        for (int k = 0; k <= pointsCount; k++) {
            float t = (float)k / pointsCount;
            int cx = (int)Mathf.Lerp(start.x, end.x, t);
            int cy = (int)Mathf.Lerp(start.y, end.y, t);

            for (int y = -(int)Mathf.CeilToInt(totalWidth); y <= (int)Mathf.CeilToInt(totalWidth); y++) {
                for (int x = -(int)Mathf.CeilToInt(totalWidth); x <= (int)Mathf.CeilToInt(totalWidth); x++) {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < resolution && py >= 0 && py < resolution) {
                        float dist = Mathf.Sqrt(x * x + y * y);
                        
                        if (dist <= riverWidth / 2) {
                            riverMap[py, px] = 1.0f;
                        } else if (dist <= totalWidth / 2) {
                            float blendFactor = 1.0f - (dist - (riverWidth / 2f)) / smoothingWidth;
                            riverMap[py, px] = Mathf.Max(riverMap[py, px], blendFactor);
                        }
                    }
                }
            }
        }
    }

    private Texture2D CreateMaskTexture(float[,] riverMap, int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++) {
            for (int x = 0; x < resolution; x++) {
                float value = riverMap[y, x];
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
        if (!Directory.Exists(dirPath)) {
            Directory.CreateDirectory(dirPath);
        }
        File.WriteAllBytes(Path.Combine(dirPath, filename), bytes);
        Debug.Log($"<color=green>{filename} を {dirPath} に保存しました。</color>");
    }
}