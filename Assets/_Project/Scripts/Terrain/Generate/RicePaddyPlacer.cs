using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Terrain))]
public class RicePaddyPlacer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    [Tooltip("階層を整理するための親オブジェクト")]
    public Transform paddiesParent;
    [Tooltip("家の親オブジェクト（この周りを避ける）")]
    public Transform housesParent;

    [Header("マスク")]
    [Tooltip("田んぼを配置するエリアを示すマスク")]
    public Texture2D riceFieldMask;
    [Tooltip("道路のマスク（この周りを避ける）")]
    public Texture2D roadMask;
    
    [Header("配置設定")]
    [Tooltip("配置する田んぼのプレハブ（正方形を想定）")]
    public GameObject[] paddyPrefabs;
    [Tooltip("田んぼ1区画のサイズ（プレハブの大きさに合わせる）※プレハブより少し大きめにすると重なりません")]
    public float paddyPlotSize = 10f;
    [Tooltip("家から最低限離す距離")]
    public float exclusionRadiusHouses = 15f;
    [Tooltip("道路から最低限離す距離")]
    public float exclusionRadiusRoads = 5f;
    [Tooltip("配置を許可する地面の最大傾斜角度")]
    public float maxSlopeAngle = 30f;
    [Tooltip("回転の揃い具合。値が小さいほど広い範囲で向きが揃います。0.05 ~ 0.2くらいがおすすめです。")]
    public float rotationCoherence = 0.1f;

    // --- プライベート変数 ---
    private List<Vector3> housePositions;
    private List<Vector2> roadPointsNormalized;


    private void Start()
    {
        GeneratePaddies();
    }

    [ContextMenu("田んぼを生成する")]
    public void GeneratePaddies()
    {
        if (!Initialize()) return;
        ClearPreviousPaddies();

        TerrainData terrainData = terrain.terrainData;
        
        for (float y = 0; y < terrainData.size.z; y += paddyPlotSize)
        {
            for (float x = 0; x < terrainData.size.x; x += paddyPlotSize)
            {
                Vector3 plotCenter = new Vector3(x + paddyPlotSize / 2, 0, y + paddyPlotSize / 2);

                Vector2 normalizedPos = new Vector2(plotCenter.x / terrainData.size.x, plotCenter.z / terrainData.size.z);
                if (riceFieldMask.GetPixelBilinear(normalizedPos.x, normalizedPos.y).r < 0.5f)
                {
                    continue;
                }

                if (IsTooClose(plotCenter, housePositions, exclusionRadiusHouses))
                {
                    continue;
                }

                if (IsTooClose(normalizedPos, roadPointsNormalized, exclusionRadiusRoads / terrainData.size.x))
                {
                    continue;
                }
                
                RaycastHit hit;
                if (Physics.Raycast(plotCenter + Vector3.up * 100, Vector3.down, out hit, 200f))
                {
                    float slope = Vector3.Angle(Vector3.up, hit.normal);
                    if (slope > maxSlopeAngle)
                    {
                        continue;
                    }

                    GameObject prefab = paddyPrefabs[Random.Range(0, paddyPrefabs.Length)];
                    
                    // パーリンノイズを使って、座標に応じた連続的なノイズ値(0.0~1.0)を取得
                    float noise = Mathf.PerlinNoise(x * rotationCoherence, y * rotationCoherence);
                    // ノイズ値を0~3の整数に変換し、90度単位の角度を決定
                    int angleStep = Mathf.FloorToInt(noise * 4); 
                    float targetAngle = angleStep * 90f;
                    
                    Quaternion randomRotation = Quaternion.Euler(0, targetAngle, 0);
                    
                    Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    Quaternion finalRotation = slopeRotation * randomRotation * prefab.transform.rotation;

                    GameObject newPaddy = Instantiate(prefab, hit.point, finalRotation);
                    if (paddiesParent != null) newPaddy.transform.SetParent(paddiesParent);
                }
            }
        }
        
        Debug.Log("田んぼの配置が完了しました。");
    }

    bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (riceFieldMask == null || roadMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
        if (paddyPrefabs.Length == 0) { Debug.LogWarning("田んぼのプレハブが設定されていません。"); return false; }

        housePositions = new List<Vector3>();
        if (housesParent != null)
        {
            foreach (Transform child in housesParent)
            {
                housePositions.Add(child.position);
            }
        }

        roadPointsNormalized = new List<Vector2>();
        for (int y = 0; y < roadMask.height; y++)
        {
            for (int x = 0; x < roadMask.width; x++)
            {
                if (roadMask.GetPixel(x, y).r > 0.5f)
                {
                    roadPointsNormalized.Add(new Vector2(x / (float)roadMask.width, y / (float)roadMask.height));
                }
            }
        }
        return true;
    }
    
    bool IsTooClose<T>(T point, List<T> targets, float radius) where T : struct
    {
        System.Func<T, T, float> distanceFunc;
        if (typeof(T) == typeof(Vector3))
            distanceFunc = (a, b) => Vector3.Distance((Vector3)(object)a, (Vector3)(object)b);
        else if (typeof(T) == typeof(Vector2))
            distanceFunc = (a, b) => Vector2.Distance((Vector2)(object)a, (Vector2)(object)b);
        else return false;

        foreach (var target in targets)
        {
            if (distanceFunc(point, target) < radius)
            {
                return true;
            }
        }
        return false;
    }

    [ContextMenu("配置した田んぼを削除")]
    void ClearPreviousPaddies()
    {
        if (paddiesParent != null)
        {
            for (int i = paddiesParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(paddiesParent.GetChild(i).gameObject);
        }
    }
}