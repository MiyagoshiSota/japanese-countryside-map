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
    [Tooltip("田んぼ1区画のサイズ（プレハブの大きさに合わせる）")]
    public float paddyPlotSize = 10f;
    [Tooltip("家から最低限離す距離")]
    public float exclusionRadiusHouses = 15f;
    [Tooltip("道路から最低限離す距離")]
    public float exclusionRadiusRoads = 5f;

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
        List<GameObject> newPaddies = new List<GameObject>();

        // 田んぼの区画サイズに合わせてグリッド状にチェック
        for (float y = 0; y < terrainData.size.z; y += paddyPlotSize)
        {
            for (float x = 0; x < terrainData.size.x; x += paddyPlotSize)
            {
                Vector3 plotCenter = new Vector3(x + paddyPlotSize / 2, 0, y + paddyPlotSize / 2);

                // --- 1. 配置可能かどうかのチェック ---
                
                // a) 田んぼマスクの範囲内か？
                Vector2 normalizedPos = new Vector2(plotCenter.x / terrainData.size.x, plotCenter.z / terrainData.size.z);
                if (riceFieldMask.GetPixelBilinear(normalizedPos.x, normalizedPos.y).r < 0.5f)
                {
                    continue;
                }

                // b) 家に近すぎないか？
                if (IsTooClose(plotCenter, housePositions, exclusionRadiusHouses))
                {
                    continue;
                }

                // c) 道路に近すぎないか？ (正規化座標で比較)
                if (IsTooClose(normalizedPos, roadPointsNormalized, exclusionRadiusRoads / terrainData.size.x))
                {
                    continue;
                }
                
                // --- 2. 配置処理 ---
                // 地面の正確な高さと傾斜を取得
                RaycastHit hit;
                if (Physics.Raycast(plotCenter + Vector3.up * 100, Vector3.down, out hit, 200f))
                {
                    GameObject prefab = paddyPrefabs[Random.Range(0, paddyPrefabs.Length)];
                    
                    // 90度単位でランダムな回転
                    Quaternion randomRotation = Quaternion.Euler(0, 90 * Random.Range(0, 4), 0);
                    // 地面の傾斜に合わせる回転
                    Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                    GameObject newPaddy = Instantiate(prefab, hit.point, slopeRotation * randomRotation);
                    if (paddiesParent != null) newPaddy.transform.SetParent(paddiesParent);
                }
            }
        }
        
        Debug.Log("田んぼの配置が完了しました。");
    }

    // --- 初期化とヘルパー関数 ---
    bool Initialize()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (riceFieldMask == null || roadMask == null) { Debug.LogError("マスクが設定されていません！"); return false; }
        if (paddyPrefabs.Length == 0) { Debug.LogWarning("田んぼのプレハブが設定されていません。"); return false; }

        // 家の座標リストを作成
        housePositions = new List<Vector3>();
        if (housesParent != null)
        {
            foreach (Transform child in housesParent)
            {
                housePositions.Add(child.position);
            }
        }

        // 道路の正規化座標リストを作成
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
    
    // 距離チェック用のジェネリックな関数
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