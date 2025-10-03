using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// コストマップの生成と、それに基づいた道路網の生成を個別に行うスクリプト。
/// 始点/終点をコストが低い平坦な「エリア」から柔軟に選択する。
/// </summary>
public class RoadGenerator : MonoBehaviour
{
    [Header("Input Files")]
    [Tooltip("元になるハイトマップ画像（侵食後など）")]
    [SerializeField] private string heightmapFileName = "ErodedHeightmap.png";
    [Tooltip("川の位置を示すリバーマスク画像")]
    [SerializeField] private string riverMaskFileName = "FinalRiverMask.png";

    [Header("Output Files")]
    [Tooltip("出力するコストマップ画像")]
    [SerializeField] private string costMapFileName = "CostMap.png";
    [Tooltip("出力する道路マップ画像")]
    [SerializeField] private string roadMapFileName = "RoadMap.png";

    [Header("Cost Parameters")]
    [Tooltip("傾斜コストの計算に乗算する指数。大きいほど急斜面を避ける")]
    [SerializeField, Range(1f, 4f)] private float slopeExponent = 2f;
    [Tooltip("川を横断する際のペナルティコスト。大きいほど川を避ける")]
    [SerializeField] private float riverPenalty = 1000f;

    [Header("Road Network Parameters")]
    [Tooltip("始点/終点として許容するエリア内の最大コスト")]
    [SerializeField] private float maxStartEndCost = 20f;
    [Tooltip("始点/終点を探す際にチェックする周辺エリアの半径。2にすると5x5マスをチェックする")]
    [SerializeField] private int startEndCheckRadius = 2;
    [Tooltip("幹線道路の始点と終点の最低距離（ダウンサンプル後の座標）")]
    [SerializeField] private float minMainRoadDistance = 64f;
    [Tooltip("生成する幹線道路の数")]
    [SerializeField] private int numMainRoads = 5;
    [Tooltip("生成する枝道の数")]
    [SerializeField] private int numBranchRoads = 50;
    [Tooltip("枝道の最大長")]
    [SerializeField] private int branchLength = 100;

    [Header("Performance Optimization")]
    [Tooltip("探索に使用する解像度のスケール。4にすると1/4の解像度(16倍高速)で探索します。")]
    [SerializeField, Range(1, 16)] private int downsampleScale = 4;

    private int width;
    private int height;

    private class Node
    {
        public Vector2Int position;
        public float gCost; public float hCost; public Node parent;
        public float fCost => gCost + hCost;
        public Node(Vector2Int pos) { position = pos; }
    }

    [ContextMenu("Step 1: Generate Cost Map")]
    public void GenerateCostMap()
    {
        Texture2D heightmapTexture = LoadTexture(heightmapFileName);
        Texture2D riverMaskTexture = LoadTexture(riverMaskFileName);
        if (heightmapTexture == null || riverMaskTexture == null) return;
        width = heightmapTexture.width; height = heightmapTexture.height;
        Debug.Log("Creating cost map...");
        float[,] costMap = CreateCostMap(heightmapTexture, riverMaskTexture);
        Debug.Log("Saving cost map texture...");
        SaveCostMapTexture(costMap, costMapFileName);
        Debug.Log("Cost Map generation complete!");
    }
    
    [ContextMenu("Step 2: Generate Road Network")]
    public void GenerateRoadNetwork()
    {
        Texture2D heightmapTexture = LoadTexture(heightmapFileName);
        Texture2D riverMaskTexture = LoadTexture(riverMaskFileName);
        if (heightmapTexture == null || riverMaskTexture == null) return;
        width = heightmapTexture.width; height = heightmapTexture.height;

        Debug.Log("Creating cost map for pathfinding...");
        float[,] costMap = CreateCostMap(heightmapTexture, riverMaskTexture);

        Debug.Log("Downsampling cost map for performance...");
        int smallWidth = width / downsampleScale;
        int smallHeight = height / downsampleScale;
        float[,] smallCostMap = DownsampleCostMap(costMap, smallWidth, smallHeight);

        Texture2D roadMap = new Texture2D(width, height);
        for(int y=0; y<height; y++) for(int x=0; x<width; x++) roadMap.SetPixel(x,y,Color.black);
        
        Debug.Log("Generating main roads with flexible start/end points...");
        List<Vector2Int> allRoadPoints = new List<Vector2Int>();

        // --- ★★★ ここからが変更されたロジック ★★★ ---
        List<Vector2Int> allGoodPoints = new List<Vector2Int>();
        for (int y = 0; y < smallHeight; y++) {
            for (int x = 0; x < smallWidth; x++) {
                
                float maxCostInNeighborhood = 0f;
                bool isGood = true;
                // 周辺エリアをチェック
                for (int ny = -startEndCheckRadius; ny <= startEndCheckRadius; ny++) {
                    for (int nx = -startEndCheckRadius; nx <= startEndCheckRadius; nx++) {
                        int checkX = x + nx;
                        int checkY = y + ny;

                        if (checkX >= 0 && checkX < smallWidth && checkY >= 0 && checkY < smallHeight) {
                            if (smallCostMap[checkX, checkY] > maxCostInNeighborhood) {
                                maxCostInNeighborhood = smallCostMap[checkX, checkY];
                            }
                        } else {
                            // 範囲外は崖とみなす
                            maxCostInNeighborhood = float.MaxValue;
                            isGood = false;
                            break;
                        }
                    }
                    if (!isGood) break;
                }

                // エリア全体の最大コストが上限以下なら候補に追加
                if (maxCostInNeighborhood <= maxStartEndCost) {
                    allGoodPoints.Add(new Vector2Int(x, y));
                }
            }
        }
        // --- ★★★ 変更ロジックここまで ★★★ ---
        
        if (allGoodPoints.Count < 2) {
            Debug.LogError("Not enough suitable points found to generate roads. Try increasing maxStartEndCost or decreasing startEndCheckRadius.");
            return;
        }

        for (int i = 0; i < numMainRoads; i++)
        {
            Vector2Int start = allGoodPoints[Random.Range(0, allGoodPoints.Count)];
            List<Vector2Int> distantPoints = allGoodPoints.Where(p => Vector2.Distance(p, start) > minMainRoadDistance).ToList();
            
            if (distantPoints.Count > 0)
            {
                Vector2Int end = distantPoints[Random.Range(0, distantPoints.Count)];
                List<Vector2Int> smallPath = A_Star_FindPath(start, end, smallCostMap, smallWidth, smallHeight);
                if (smallPath != null)
                {
                    List<Vector2Int> fullPath = UpsamplePath(smallPath);
                    DrawPath(roadMap, fullPath, downsampleScale);
                    allRoadPoints.AddRange(fullPath);
                }
            }
            else { Debug.LogWarning($"Could not find a distant end point from {start}. Skipping one main road."); }
        }

        Debug.Log("Generating branch roads...");
        List<Vector2Int> allSmallRoadPoints = new List<Vector2Int>();
        foreach (var p in allRoadPoints) { allSmallRoadPoints.Add(new Vector2Int(p.x / downsampleScale, p.y / downsampleScale)); }

        for (int i = 0; i < numBranchRoads; i++)
        {
            if (allSmallRoadPoints.Count == 0) break;
            
            Vector2Int start = allSmallRoadPoints[Random.Range(0, allSmallRoadPoints.Count)];
            float angle = Random.Range(0, 2 * Mathf.PI);
            Vector2Int end = new Vector2Int(
                Mathf.Clamp(start.x + (int)(Mathf.Cos(angle) * (branchLength / downsampleScale)), 0, smallWidth - 1),
                Mathf.Clamp(start.y + (int)(Mathf.Sin(angle) * (branchLength / downsampleScale)), 0, smallHeight - 1)
            );

            List<Vector2Int> smallPath = A_Star_FindPath(start, end, smallCostMap, smallWidth, smallHeight);
            if (smallPath != null)
            {
                List<Vector2Int> fullPath = UpsamplePath(smallPath);
                DrawPath(roadMap, fullPath, downsampleScale);
                foreach (var p in fullPath) { allSmallRoadPoints.Add(new Vector2Int(p.x / downsampleScale, p.y / downsampleScale)); }
            }
        }
        
        Debug.Log("Saving road map...");
        roadMap.Apply();
        SaveTexture(roadMap, roadMapFileName);
        Debug.Log("Road network generation complete!");
    }

    private float[,] CreateCostMap(Texture2D heightmap, Texture2D riverMask)
    {
        float[,] costMap = new float[width, height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float h = heightmap.GetPixel(x, y).r;
                float hx = (x + 1 < width) ? heightmap.GetPixel(x + 1, y).r : h;
                float hy = (y + 1 < height) ? heightmap.GetPixel(x, y + 1).r : h;
                float slope = Mathf.Max(Mathf.Abs(h - hx), Mathf.Abs(h - hy));
                float cost = Mathf.Pow(slope * 100, slopeExponent);
                if (riverMask.GetPixel(x, y).r > 0.5f) { cost += riverPenalty; }
                costMap[x, y] = cost;
            }
        }
        return costMap;
    }

    private void SaveCostMapTexture(float[,] costMap, string fileName)
    {
        Texture2D texture = new Texture2D(width, height);
        float maxCost = 0f;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (costMap[x, y] < riverPenalty * 0.9f && costMap[x, y] > maxCost) { maxCost = costMap[x, y]; }
            }
        }
        if (maxCost == 0) maxCost = 1;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float cost = costMap[x, y];
                Color pixelColor;
                if (cost >= riverPenalty * 0.9f) { pixelColor = Color.cyan; }
                else {
                    float normalizedCost = Mathf.Clamp01(cost / maxCost);
                    pixelColor = new Color(normalizedCost, normalizedCost, normalizedCost, 1);
                }
                texture.SetPixel(x, y, pixelColor);
            }
        }
        texture.Apply();
        SaveTexture(texture, fileName);
    }
    
    private float[,] DownsampleCostMap(float[,] originalMap, int newWidth, int newHeight)
    {
        float[,] smallCostMap = new float[newWidth, newHeight];
        for (int y = 0; y < newHeight; y++) {
            for (int x = 0; x < newWidth; x++) {
                float totalCost = 0;
                for (int sy = 0; sy < downsampleScale; sy++) {
                    for (int sx = 0; sx < downsampleScale; sx++) {
                        totalCost += originalMap[x * downsampleScale + sx, y * downsampleScale + sy];
                    }
                }
                smallCostMap[x, y] = totalCost / (downsampleScale * downsampleScale);
            }
        }
        return smallCostMap;
    }
    
    private List<Vector2Int> A_Star_FindPath(Vector2Int startPos, Vector2Int endPos, float[,] costMap, int mapWidth, int mapHeight)
    {
        Node startNode = new Node(startPos);
        Dictionary<Vector2Int, Node> openSet = new Dictionary<Vector2Int, Node>();
        openSet.Add(startPos, startNode);
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        startNode.gCost = 0;
        startNode.hCost = Vector2Int.Distance(startPos, endPos);
        List<Node> openListForSort = new List<Node> { startNode };
        while (openListForSort.Count > 0) {
            Node currentNode = openListForSort[0];
            openListForSort.RemoveAt(0);
            openSet.Remove(currentNode.position);
            closedSet.Add(currentNode.position);
            if (currentNode.position == endPos) { return RetracePath(startNode, currentNode); }
            for (int y = -1; y <= 1; y++) {
                for (int x = -1; x <= 1; x++) {
                    if (x == 0 && y == 0) continue;
                    Vector2Int neighbourPos = new Vector2Int(currentNode.position.x + x, currentNode.position.y + y);
                    if (neighbourPos.x < 0 || neighbourPos.x >= mapWidth || neighbourPos.y < 0 || neighbourPos.y >= mapHeight || closedSet.Contains(neighbourPos)) continue;
                    float moveCost = currentNode.gCost + Vector2Int.Distance(currentNode.position, neighbourPos) + costMap[neighbourPos.x, neighbourPos.y];
                    if (!openSet.TryGetValue(neighbourPos, out Node neighbourNode) || moveCost < neighbourNode.gCost) {
                        if (neighbourNode == null) {
                            neighbourNode = new Node(neighbourPos);
                            neighbourNode.hCost = Vector2Int.Distance(neighbourPos, endPos);
                            openSet.Add(neighbourPos, neighbourNode);
                            openListForSort.Add(neighbourNode);
                        }
                        neighbourNode.gCost = moveCost;
                        neighbourNode.parent = currentNode;
                    }
                }
            }
            openListForSort.Sort((a, b) => a.fCost.CompareTo(b.fCost));
        }
        return null;
    }

    private List<Vector2Int> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node currentNode = endNode;
        while(currentNode != null && currentNode != startNode) {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }
    
    private List<Vector2Int> UpsamplePath(List<Vector2Int> smallPath)
    {
        List<Vector2Int> fullPath = new List<Vector2Int>();
        foreach (var p in smallPath) { fullPath.Add(new Vector2Int(p.x * downsampleScale, p.y * downsampleScale)); }
        return fullPath;
    }

    private void DrawPath(Texture2D texture, List<Vector2Int> path, int thickness)
    {
        if (path == null || path.Count == 0) return;
        for(int i = 0; i < path.Count -1; i++) { DrawLine(texture, path[i], path[i+1], thickness/2); }
        DrawCircle(texture, path[0], thickness/2);
        DrawCircle(texture, path[path.Count - 1], thickness/2);
    }

    private void DrawLine(Texture2D tex, Vector2Int p1, Vector2Int p2, int thickness)
    {
        int x0 = p1.x; int y0 = p1.y; int x1 = p2.x; int y1 = p2.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy, e2;
        while (true) {
            DrawCircle(tex, new Vector2Int(x0, y0), thickness);
            if (x0 == x1 && y0 == y1) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
    
    private void DrawCircle(Texture2D tex, Vector2Int center, int radius)
    {
        for (int y = -radius; y <= radius; y++) {
            for (int x = -radius; x <= radius; x++) {
                if (x * x + y * y <= radius * radius) {
                    int drawX = center.x + x;
                    int drawY = center.y + y;
                    if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height) { tex.SetPixel(drawX, drawY, Color.white); }
                }
            }
        }
    }
    
    private Texture2D LoadTexture(string fileName)
    {
        string path = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(path)) {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }
        Debug.LogError($"File not found: {path}");
        return null;
    }

    private void SaveTexture(Texture2D texture, string fileName)
    {
        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllBytes(path, bytes);
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
}