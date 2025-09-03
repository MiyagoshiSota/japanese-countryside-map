using UnityEngine;
using System.IO;

[RequireComponent(typeof(Terrain))]
public class IrregularZoneAnalyzer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;

    [Header("ゾーン設定")]
    [Tooltip("この標高'より高い'場所を「森林ゾーン」とします。この値は下の範囲内でランダムに決まります。")]
    [Range(0f, 1f)]
    public float minMountainHeight = 0.25f;
    [Range(0f, 1f)]
    public float maxMountainHeight = 0.45f;

    [Tooltip("平地における田んぼの割合（0.0～1.0）")]
    [Range(0f, 1f)]
    public float riceFieldRatio = 0.5f;

    [Header("ランダム設定")]
    [Tooltip("分析結果を変えるためのシード値。0の場合は実行ごとにランダム。")]
    public int seed = 0;
    
    [ContextMenu("不規則なゾーンマスクを生成する")]
    public void AnalyzeAndGenerateMasks()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;

        // --- 1. 閾値をランダムに決定 ---
        if (seed != 0) Random.InitState(seed);
        else Random.InitState((int)System.DateTime.Now.Ticks);
        
        float mountainHeightThreshold = Random.Range(minMountainHeight, maxMountainHeight);

        Debug.Log($"今回のランダム閾値 -> 森林の標高: {mountainHeightThreshold:F2}");

        // --- テクスチャを初期化 ---
        Texture2D townMask = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        Texture2D riceFieldMask = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        Texture2D forestMask = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);

        // --- 地形の全ピクセルをループして分析 ---
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = terrainData.GetHeight(x, y) / terrainData.size.y;

                bool isTownArea = false;
                bool isRiceFieldArea = false;
                bool isForestArea = false;

                // 条件：標高が「森林」の基準より高いか？
                if (height > mountainHeightThreshold)
                {
                    isForestArea = true; // 森林ゾーン
                }
                else
                {
                    // 平地の場合、ランダムで町か田んぼかを決める
                    if (Random.Range(0f, 1.0f) < riceFieldRatio)
                    {
                        isRiceFieldArea = true; // 田んぼゾーン
                    }
                    else
                    {
                        isTownArea = true; // 町ゾーン
                    }
                }
                
                townMask.SetPixel(x, y, isTownArea ? Color.white : Color.black);
                riceFieldMask.SetPixel(x, y, isRiceFieldArea ? Color.white : Color.black);
                forestMask.SetPixel(x, y, isForestArea ? Color.white : Color.black);
            }
        }

        // --- テクスチャを適用してPNGファイルとして保存 ---
        townMask.Apply();
        riceFieldMask.Apply();
        forestMask.Apply();
        
        SaveTextureAsPNG(townMask, "TownMask.png");
        SaveTextureAsPNG(riceFieldMask, "RiceFieldMask.png");
        SaveTextureAsPNG(forestMask, "ForestMask.png");
        
        Debug.Log("不規則なゾーンマスクの生成が完了しました。");
    }

    private void SaveTextureAsPNG(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, filename), bytes);
    }
}