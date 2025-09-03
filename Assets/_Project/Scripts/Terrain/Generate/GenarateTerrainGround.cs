using System.Collections.Generic;
using UnityEngine;

public class GenarateTerrainGround : MonoBehaviour
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Terrain terrain;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void ResetTerrainData()
    {
        terrain.terrainData.SetHeights(0, 0, new float[width, height]);
    }

    [ContextMenu("Generate Map")]
    void GenerateTerrain()
    {
        // ResetTerrainData();
        // C#スクリプトのロジック（疑似コード）
        float[,] heights = new float[width, height];
        float centerX = width / 2f;
        float centerY = height / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 1. パーリンノイズで山の起伏を計算
                float mountainNoise = Mathf.PerlinNoise(x * 0.01f, y * 0.01f);

                // 2. 中央からの距離を計算（0.0～1.0の範囲）
                float distFromCenter =
                    Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY)) / (width / 2f);

                // 3. 距離を使って高さを調整（これが盆地マスクになる）
                // 中央(dist=0)はそのまま、外側(dist=1)にいくほど高さを強調
                float finalHeight = mountainNoise * distFromCenter;

                heights[x, y] = finalHeight;
            }
        }

        terrain.terrainData.SetHeights(0, 0, heights);
    }
    
    [Header("Mountain")]
    [Range(0f, 1f)]
    public float baseHeight = 0.1f;

    [Header("Mountain")]
    [Tooltip("生成する山の数")]
    public int numberOfMountains = 10;

    [Header("Mountain")]
    [Range(0f, 1f)]
    public float maxMountainHeight = 0.5f;
    
    [Header("Mountain")]
    [Range(10f, 500f)]
    public float mountainRadius = 100f;

    [Tooltip("生成のたびに結果を変えるためのシード値。0の場合は実行ごとにランダム。")]
    public int seed = 0;

    [ContextMenu("Generate Mountain")]
    void GenerateMountain()
    {
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;

        // シード値に基づいてランダム状態を初期化
        if (seed != 0)
        {
            Random.InitState(seed);
        }

        // 1. 山頂の中心座標をランダムに決める
        List<Vector2> peakCenters = new List<Vector2>();
        for (int i = 0; i < numberOfMountains; i++)
        {
            float x = Random.Range(0, resolution);
            float y = Random.Range(0, resolution);
            peakCenters.Add(new Vector2(x, y));
        }

        // 2. ハイトマップのデータを保持する2次元配列を初期化
        float[,] heights = new float[resolution, resolution];

        // 3. 地形のすべてのピクセルをループ処理
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float currentHeight = baseHeight;

                // 各山頂からの影響を計算
                foreach (Vector2 peak in peakCenters)
                {
                    // 現在のピクセルと山頂との距離を計算
                    float distance = Vector2.Distance(new Vector2(x, y), peak);

                    // 山の半径内にいるか？
                    if (distance < mountainRadius)
                    {
                        // 距離に基づいて山の高さを計算（コサインを使ったなめらかなカーブ）
                        // 中心(距離0)で1、端(距離=半径)で0になる
                        float heightMultiplier = (Mathf.Cos(distance / mountainRadius * Mathf.PI) + 1) / 2f;

                        // 基準の高さに、計算した山の高さを加える
                        float mountainInfluence = maxMountainHeight * heightMultiplier;

                        // 複数の山が重なった場合、最も高い値を採用する
                        if (baseHeight + mountainInfluence > currentHeight)
                        {
                            currentHeight = baseHeight + mountainInfluence;
                        }
                    }
                }

                // 配列に高さを格納 (Unityのハイトマップは Y, X の順)
                heights[y, x] = currentHeight;
            }
        }

        // 4. 計算したハイトマップデータを実際の地形に適用
        terrainData.SetHeights(0, 0, heights);
        Debug.Log("地形の生成が完了しました。");
    }
}