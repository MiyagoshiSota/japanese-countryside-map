using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")] public float heightMultiplier = 30f;

    [Header("Material & Instancing")] 
    public Material terrainMaterial;
    public FieldPlacer fieldPlacer;
    public GrassInstancer grassInstancer;

    private Mesh mesh;

    public void GenerateTerrain(float[,] heightMap, Texture2D roadMap, List<Vector2Int> roadPath)
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        Renderer renderer = GetComponent<Renderer>();
        if (terrainMaterial != null)
        {
            Material instancedMaterial = new Material(terrainMaterial);
            if (roadMap != null)
            {
                instancedMaterial.SetTexture("_RoadSplatMap", roadMap);
            }

            renderer.material = instancedMaterial;
        }
        else
        {
            Debug.LogError("Terrain Materialがアサインされていません。");
            return;
        }

        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        // --- メッシュデータ作成 ---
        Vector3[] vertices = new Vector3[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];
        Vector2[] uvs = new Vector2[width * height]; // UVを追加

        int triangleIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int vertexIndex = y * width + x;
                float vertexHeight = heightMap[x, y];
                vertices[vertexIndex] = new Vector3(x, vertexHeight * heightMultiplier, y);
                uvs[y * width + x] = new Vector2((float)x / width, (float)y / height); // UVを設定

                if (x < width - 1 && y < height - 1)
                {
                    triangles[triangleIndex] = vertexIndex;
                    triangles[triangleIndex + 1] = vertexIndex + width;
                    triangles[triangleIndex + 2] = vertexIndex + 1;
                    triangles[triangleIndex + 3] = vertexIndex + 1;
                    triangles[triangleIndex + 4] = vertexIndex + width;
                    triangles[triangleIndex + 5] = vertexIndex + width + 1;
                    triangleIndex += 6;
                }
            }
        }

        // --- メッシュ適用 ---
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs; // UVを適用
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // --- コライダーとインスタンサーの更新 ---
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        // GrassInstancerの呼び出し
        if (grassInstancer != null)
        {
            grassInstancer.GeneratePlacement(mesh, roadMap);
        }
    
        // ★FieldPlacerの呼び出し
        if (fieldPlacer != null)
        {
            fieldPlacer.PlaceFields(mesh, roadPath);
        }
    }
}