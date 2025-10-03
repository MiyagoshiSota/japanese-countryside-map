using UnityEngine;
using System.IO;

/// <summary>
/// 画像ファイルを読み込み、コントラスト調整としきい値処理を行って、
/// 新しいリバーマスク画像を生成するスクリプト。
/// </summary>
public class RiverFinalizer : MonoBehaviour
{
    [Header("File Settings")]
    [Tooltip("加工する元のフローマップ名")]
    [SerializeField] private string sourceFlowMapName = "ErosionFlowMap.png";

    [Tooltip("出力する最終的なリバーマスク名")]
    [SerializeField] private string finalRiverMaskName = "FinalRiverMask.png";

    [Header("Processing Settings")]
    [Tooltip("川として認識させる明るさの最低値（これ以下のピクセルは黒になる）")]
    [SerializeField, Range(0f, 1f)] private float riverThreshold = 0.1f;

    [Tooltip("コントラストの強さ。値を上げると太い川が強調される")]
    [SerializeField, Range(1f, 8f)] private float riverContrastExponent = 2.5f;

    /// <summary>
    /// フローマップを読み込み、加工してリバーマスクを生成する
    /// </summary>
    [ContextMenu("Finalize River Network from Flow Map")]
    public void FinalizeRiverNetwork()
    {
        string sourcePath = Path.Combine(Application.dataPath, sourceFlowMapName);

        if (!File.Exists(sourcePath))
        {
            Debug.LogError($"ソースファイルが見つかりません: {sourcePath}。先にフローマップを生成してください。");
            return;
        }
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayProgressBar("Processing Image", "Loading source texture...", 0.1f);
        #endif

        // ソース画像を読み込む
        byte[] fileData = File.ReadAllBytes(sourcePath);
        Texture2D sourceTexture = new Texture2D(2, 2);
        sourceTexture.LoadImage(fileData); // サイズは自動的に調整される

        int width = sourceTexture.width;
        int height = sourceTexture.height;

        // 出力用の新しいテクスチャを作成
        Texture2D finalTexture = new Texture2D(width, height);
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayProgressBar("Processing Image", "Applying filters to pixels...", 0.3f);
        #endif

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 元のピクセルの明るさを取得 (グレースケールなのでr,g,bは同じ値)
                float originalValue = sourceTexture.GetPixel(x, y).r;
                
                // 1. コントラストを強調
                float contrastedValue = Mathf.Pow(originalValue, riverContrastExponent);
                
                // 2. しきい値で2値化
                float finalValue = (contrastedValue > riverThreshold) ? 1.0f : 0.0f;
                
                // 新しいテクスチャにピクセルを設定
                finalTexture.SetPixel(x, y, new Color(finalValue, finalValue, finalValue, 1));
            }
        }
        
        finalTexture.Apply();

        #if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayProgressBar("Processing Image", "Saving final texture...", 0.9f);
        #endif

        // 新しいリバーマスクをファイルとして保存
        byte[] bytes = finalTexture.EncodeToPNG();
        string finalPath = Path.Combine(Application.dataPath, finalRiverMaskName);
        File.WriteAllBytes(finalPath, bytes);

        Debug.Log($"Final River Mask saved to: {finalPath}");
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.ClearProgressBar();
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
}