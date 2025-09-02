using UnityEngine;
using System.Collections.Generic;

public class GrassInstancer : MonoBehaviour
{
    [Header("Setup")]
    public Mesh grassMesh;
    public Material grassMaterial;

    [Header("Placement Settings")]
    public int density = 10;
    public float minHeight = 5f;
    public float maxHeight = 25f;
    [Range(0, 90)]
    public float maxSteepness = 45f;

    [Header("Randomization")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    private List<Matrix4x4> matrices = new List<Matrix4x4>();

    public void GeneratePlacement(Mesh terrainMesh, Texture2D roadMap)
    {
        matrices.Clear();
        if (grassMesh == null || grassMaterial == null) return;

        Bounds bounds = terrainMesh.bounds;
        int totalToPlace = (int)(bounds.size.x * bounds.size.z * density);

        for (int i = 0; i < totalToPlace; i++)
        {
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 rayStart = new Vector3(randomX, bounds.max.y + 10f, randomZ);

            RaycastHit hit;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, bounds.size.y + 20f))
            {
                bool heightCondition = hit.point.y >= minHeight && hit.point.y <= maxHeight;
                float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                bool slopeCondition = slopeAngle <= maxSteepness;
                
                // 道の上でないかチェック
                bool onRoad = false;
                if (roadMap != null)
                {
                    // ワールド座標からUV座標を計算
                    float uvX = (hit.point.x - bounds.min.x) / bounds.size.x;
                    float uvY = (hit.point.z - bounds.min.z) / bounds.size.z;
                    // roadMapの色が黒(0)に近いかチェック
                    if (roadMap.GetPixelBilinear(uvX, uvY).r > 0.1f)
                    {
                        onRoad = true;
                    }
                }

                if (heightCondition && slopeCondition && !onRoad)
                {
                    Vector3 position = hit.point;
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, Random.Range(0, 360), 0);
                    Vector3 scale = Vector3.one * Random.Range(minScale, maxScale);
                    matrices.Add(Matrix4x4.TRS(position, rotation, scale));
                }
            }
        }
    }

    void Update()
    {
        if (matrices.Count == 0) return;
        int batchSize = 1023;
        for (int i = 0; i < matrices.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, matrices.Count - i);
            Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrices.GetRange(i, count));
        }
    }
}