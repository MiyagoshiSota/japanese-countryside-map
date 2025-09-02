using UnityEngine;
using System.Collections.Generic; // Listを使うために必要

public class FieldPlacer : MonoBehaviour
{
    [Header("Object Settings")]
    public GameObject fieldPrefab;
    public int placementAttempts = 500; // 試行回数を増やして平地を見つけやすくする

    [Header("Placement Rules (Filters)")]
    [Tooltip("配置可能な最大傾斜角度。非常に小さい値に設定する")]
    [Range(0, 10)]
    public float maxSlope = 2f; // ★1や2のような非常に小さい値に！

    [Tooltip("道からこの距離『以内』である必要がある")]
    public float maxDistanceFromRoad = 50f;

    // ★ heightMapを受け取る必要がなくなった
    public void PlaceFields(Mesh terrainMesh, List<Vector2Int> roadPath)
    {
        if (fieldPrefab == null) return;

        Bounds bounds = terrainMesh.bounds;
        Transform container = new GameObject(fieldPrefab.name + " Container").transform;

        for (int i = 0; i < placementAttempts; i++)
        {
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 rayStart = new Vector3(randomX, bounds.max.y + 10f, randomZ);

            RaycastHit hit;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, bounds.size.y + 20f))
            {
                // フィルター1：傾斜チェック (ほぼ平坦か？)
                float slope = Vector3.Angle(Vector3.up, hit.normal);
                if (slope > maxSlope)
                {
                    continue; // 平坦ではないのでスキップ
                }

                // フィルター2：道路との距離チェック (道の近くか？)
                if (roadPath != null && roadPath.Count > 0)
                {
                    float closestDistanceToRoad = GetClosestDistanceToRoad(hit.point, roadPath);
                    if (closestDistanceToRoad > maxDistanceFromRoad)
                    {
                        continue; // 道から遠すぎるのでスキップ
                    }
                }

                // --- 全てのフィルターを通過 ---
                Vector3 position = hit.point;
                Quaternion rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                
                GameObject newObj = Instantiate(fieldPrefab, position, rotation);
                newObj.transform.SetParent(container);
            }
        }
    }

    private float GetClosestDistanceToRoad(Vector3 point, List<Vector2Int> roadPath)
    {
        float minDistance = float.MaxValue;
        foreach (Vector2Int roadPoint in roadPath)
        {
            float dist = Vector2.Distance(new Vector2(point.x, point.z), roadPoint);
            if (dist < minDistance) minDistance = dist;
        }
        return minDistance;
    }
}