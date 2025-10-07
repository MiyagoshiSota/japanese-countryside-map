using UnityEngine;
using System.IO;

/// <summary>
/// 複数のマップ（コストマップ、リバーマスク、ロードマップ）を読み込み、
/// 1枚のカラーマップに合成して出力するスクリプト。
/// </summary>
public class MapCombiner : MonoBehaviour
{
    [Header("Input Files")]
    [Tooltip("等高線情報が含まれるコストマップ")]
    [SerializeField] private string costMapFileName = "CostMap.png";
    [Tooltip("川の位置を示すリバーマスク")]
    [SerializeField] private string riverMaskFileName = "FinalRiverMask.png";
    [Tooltip("道路の位置を示すロードマップ")]
    [SerializeField] private string roadMapFileName = "RoadMap.png";

    [Header("Output File")]
    [Tooltip("出力する合成マップのファイル名")]
    [SerializeField] private string combinedMapFileName = "CombinedMap.png";

    [Header("Element Colors")]
    [Tooltip("等高線の色")]
    [SerializeField] private Color contourLineColor = Color.white;
    [Tooltip("川の色")]
    [SerializeField] private Color riverColor = Color.cyan;
    [Tooltip("道路の色")]
    [SerializeField] private Color roadColor = Color.yellow;
    
    [Header("Settings")]
    [Tooltip("等高線として認識するコストマップの明るさのしきい値")]
    [SerializeField, Range(0f, 1f)] private float contourThreshold = 0.1f;


    [ContextMenu("Generate Combined Map")]
    public void CombineMaps()
    {
        // 1. 全ての入力画像を読み込む
        Texture2D costMap = LoadTexture(costMapFileName);
        Texture2D riverMask = LoadTexture(riverMaskFileName);
        Texture2D roadMap = LoadTexture(roadMapFileName);

        if (costMap == null || riverMask == null || roadMap == null)
        {
            Debug.LogError("入力ファイルが不足しています。全てのファイル名が正しいか確認してください。");
            return;
        }

        int width = costMap.width;
        int height = costMap.height;

        // 2. 出力用の新しいテクスチャを作成
        Texture2D outputTexture = new Texture2D(width, height);

        // 3. 1ピクセルずつ色を決定し、合成していく
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixelColor = Color.black; // デフォルトは黒

                // 等高線の描画 (優先度: 低)
                if (costMap.GetPixel(x, y).grayscale > contourThreshold)
                {
                    pixelColor = contourLineColor;
                }

                // 川の描画 (優先度: 中)
                if (riverMask.GetPixel(x, y).r > 0.5f)
                {
                    pixelColor = riverColor;
                }

                // 道路の描画 (優先度: 高)
                if (roadMap.GetPixel(x, y).r > 0.5f)
                {
                    pixelColor = roadColor;
                }

                outputTexture.SetPixel(x, y, pixelColor);
            }
        }
        
        outputTexture.Apply();

        // 4. 合成した画像を保存
        SaveTexture(outputTexture, combinedMapFileName);
        Debug.Log("Combined map generation complete!");
    }

    // --- ファイル操作用のヘルパー関数 ---
    private Texture2D LoadTexture(string fileName)
    {
        string path = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(path))
        {
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