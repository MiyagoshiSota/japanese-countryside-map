using UnityEngine;
using System.Collections.Generic;

public static class Pathfinder
{
    private class PathNode
    {
        public int x, y;
        public float gCost, hCost, fCost; // ★コストを全てfloatに変更
        public PathNode parentNode;
        public PathNode(int x, int y) { this.x = x; this.y = y; }
        public void CalculateFCost() { fCost = gCost + hCost; }
    }

    // slopePenaltyMultiplier を引数に追加
    public static List<Vector2Int> FindPath(float[,] heightMap, Vector2Int startCoords, Vector2Int endCoords, float slopePenaltyMultiplier)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        PathNode[,] grid = new PathNode[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) grid[x, y] = new PathNode(x, y);

        PathNode startNode = grid[startCoords.x, startCoords.y];
        PathNode endNode = grid[endCoords.x, endCoords.y];

        List<PathNode> openList = new List<PathNode> { startNode };
        HashSet<PathNode> closedSet = new HashSet<PathNode>();

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, endNode);
        startNode.CalculateFCost();

        while (openList.Count > 0)
        {
            PathNode currentNode = GetLowestFCostNode(openList);
            if (currentNode == endNode) return RetracePath(endNode);

            openList.Remove(currentNode);
            closedSet.Add(currentNode);

            foreach (PathNode neighbourNode in GetNeighbours(currentNode, grid, width, height))
            {
                if (closedSet.Contains(neighbourNode)) continue;

                float tentativeGCost = currentNode.gCost + CalculateMovementCost(currentNode, neighbourNode, heightMap, slopePenaltyMultiplier);

                if (tentativeGCost < neighbourNode.gCost || !openList.Contains(neighbourNode))
                {
                    neighbourNode.parentNode = currentNode;
                    neighbourNode.gCost = tentativeGCost;
                    neighbourNode.hCost = GetDistance(neighbourNode, endNode);
                    neighbourNode.CalculateFCost();

                    if (!openList.Contains(neighbourNode)) openList.Add(neighbourNode);
                }
            }
        }
        return null;
    }
    
    // ★より安定した傾斜コスト計算
    private static float CalculateMovementCost(PathNode from, PathNode to, float[,] heightMap, float slopePenaltyMultiplier)
    {
        float heightFrom = heightMap[from.x, from.y];
        float heightTo = heightMap[to.x, to.y];
        
        float heightDifference = Mathf.Abs(heightFrom - heightTo);
        // ★単純な乗算に変更し、外部から係数を調整できるように
        float slopePenalty = heightDifference * slopePenaltyMultiplier; 

        return GetDistance(from, to) + slopePenalty;
    }
    
    // 他のメソッドもfloatを返すように調整
    private static float GetDistance(PathNode a, PathNode b)
    {
        float dstX = Mathf.Abs(a.x - b.x);
        float dstY = Mathf.Abs(a.y - b.y);
        return 14f * Mathf.Min(dstX, dstY) + 10f * Mathf.Abs(dstX - dstY);
    }
    
    private static List<Vector2Int> RetracePath(PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode currentNode = endNode;
        while (currentNode != null)
        {
            path.Add(new Vector2Int(currentNode.x, currentNode.y));
            currentNode = currentNode.parentNode;
        }
        path.Reverse();
        return path;
    }
    
    private static PathNode GetLowestFCostNode(List<PathNode> pathNodeList)
    {
        PathNode lowestFCostNode = pathNodeList[0];
        for (int i = 1; i < pathNodeList.Count; i++) if (pathNodeList[i].fCost < lowestFCostNode.fCost) lowestFCostNode = pathNodeList[i];
        return lowestFCostNode;
    }

    private static List<PathNode> GetNeighbours(PathNode node, PathNode[,] grid, int width, int height)
    {
        List<PathNode> neighbours = new List<PathNode>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                int checkX = node.x + x;
                int checkY = node.y + y;
                if (checkX >= 0 && checkX < width && checkY >= 0 && checkY < height) neighbours.Add(grid[checkX, checkY]);
            }
        }
        return neighbours;
    }
}