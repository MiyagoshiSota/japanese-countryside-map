using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Terrain))]
public class DebugVisualizer : MonoBehaviour
{
    [Header("参照")]
    public Terrain terrain;
    [Tooltip("確認したいマスク画像 (RoadVisualizer.png)")]
    public Texture2D maskToVisualize;
    [Tooltip("目印として置くプレハブ（赤いキューブなど）")]
    public GameObject markerPrefab;

    private List<GameObject> markers = new List<GameObject>();

    [ContextMenu("マスクの座標を可視化する")]
    public void VisualizeMaskPoints()
    {
        // 前回のマーカーを削除
        foreach (var marker in markers) { DestroyImmediate(marker); }
        markers.Clear();
        
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (maskToVisualize == null || markerPrefab == null)
        {
            Debug.LogError("マスクまたはマーカーのプレハブが設定されていません。");
            return;
        }

        // --- マスクを読み取り、マーカーを配置 ---
        TerrainData td = terrain.terrainData;
        for (int y = 0; y < maskToVisualize.height; y += 10) // 10ピクセルごとに間引いてチェック
        {
            for (int x = 0; x < maskToVisualize.width; x += 10)
            {
                if (maskToVisualize.GetPixel(x, y).r > 0.5f) // 白いピクセルか？
                {
                    // ワールド座標に変換
                    Vector3 worldPos = new Vector3(
                        x / (float)maskToVisualize.width * td.size.x,
                        0,
                        y / (float)maskToVisualize.height * td.size.z
                    );
                    worldPos.y = terrain.SampleHeight(worldPos) + 1f; // 地面に埋まらないように少し浮かせる

                    // マーカーを配置
                    markers.Add(Instantiate(markerPrefab, worldPos, Quaternion.identity));
                }
            }
        }
        Debug.Log($"{markers.Count}個のマーカーを配置しました。道路の形に沿っていますか？");
    }
}