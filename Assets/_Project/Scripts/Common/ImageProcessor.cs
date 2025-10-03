using UnityEngine;
using System.IO;

/// <summary>
/// 指定された画像を回転・反転させて保存するユーティリティスクリプト
/// </summary>
public class ImageProcessor : MonoBehaviour
{
    [Header("File Settings")]
    [Tooltip("処理する元の画像ファイル名")]
    [SerializeField] private string sourceFileName = "RoadMap.png";
    [Tooltip("処理後に出力する画像ファイル名")]
    [SerializeField] private string outputFileName = "RoadMap_Processed.png";

    [Header("Processing Settings")]
    [Tooltip("適用する処理の種類を選択")]
    [SerializeField] private ProcessType processType = ProcessType.Rotate90CounterClockwise;

    public enum ProcessType
    {
        None,
        Rotate90Clockwise,          // 時計回りに90度回転
        Rotate90CounterClockwise,   // 反時計回りに90度回転
        Rotate180,                  // 180度回転
        FlipHorizontal,             // 水平反転
        FlipVertical                // 垂直反転
    }

    [ContextMenu("Process Image")]
    public void ProcessImage()
    {
        // 1. ソース画像を読み込み
        Texture2D sourceTexture = LoadTexture(sourceFileName);
        if (sourceTexture == null) return;

        Texture2D processedTexture = null;

        // 2. 選択された処理を適用
        switch (processType)
        {
            case ProcessType.Rotate90Clockwise:
                processedTexture = RotateTexture(sourceTexture, true);
                break;
            case ProcessType.Rotate90CounterClockwise:
                processedTexture = RotateTexture(sourceTexture, false);
                break;
            case ProcessType.Rotate180:
                // 90度回転を2回実行
                Texture2D temp = RotateTexture(sourceTexture, true);
                processedTexture = RotateTexture(temp, true);
                DestroyImmediate(temp);
                break;
            case ProcessType.FlipHorizontal:
                processedTexture = FlipTexture(sourceTexture, true);
                break;
            case ProcessType.FlipVertical:
                processedTexture = FlipTexture(sourceTexture, false);
                break;
            case ProcessType.None:
            default:
                processedTexture = sourceTexture;
                break;
        }

        if (processedTexture != null)
        {
            // 3. 結果を保存
            Debug.Log($"Saving processed image to {outputFileName}...");
            SaveTexture(processedTexture, outputFileName);
            Debug.Log("Processing complete!");
            if(processedTexture != sourceTexture) DestroyImmediate(processedTexture);
        }
    }
    
    /// <summary>
    /// テクスチャを90度回転させる
    /// </summary>
    private Texture2D RotateTexture(Texture2D originalTexture, bool clockwise)
    {
        int width = originalTexture.width;
        int height = originalTexture.height;
        
        // 回転後は幅と高さが入れ替わる
        Texture2D rotatedTexture = new Texture2D(height, width);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (clockwise)
                {
                    // 時計回り: (x,y) -> (y, width-1-x)
                    rotatedTexture.SetPixel(y, width - 1 - x, originalTexture.GetPixel(x, y));
                }
                else
                {
                    // 反時計回り: (x,y) -> (height-1-y, x)
                    rotatedTexture.SetPixel(height - 1 - y, x, originalTexture.GetPixel(x, y));
                }
            }
        }
        rotatedTexture.Apply();
        return rotatedTexture;
    }

    /// <summary>
    /// テクスチャを反転させる
    /// </summary>
    private Texture2D FlipTexture(Texture2D originalTexture, bool horizontal)
    {
        int width = originalTexture.width;
        int height = originalTexture.height;
        Texture2D flippedTexture = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (horizontal)
                {
                    flippedTexture.SetPixel(width - 1 - x, y, originalTexture.GetPixel(x, y));
                }
                else // Vertical
                {
                    flippedTexture.SetPixel(x, height - 1 - y, originalTexture.GetPixel(x, y));
                }
            }
        }
        flippedTexture.Apply();
        return flippedTexture;
    }
    
    // --- ファイル操作用のヘルパー関数 ---
    private Texture2D LoadTexture(string fileName)
    {
        string path = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(path)) {
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