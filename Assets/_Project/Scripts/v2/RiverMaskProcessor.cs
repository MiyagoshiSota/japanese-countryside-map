using UnityEngine;
using System.IO;

public class RiverMaskProcessor : MonoBehaviour
{
    [Header("入力テクスチャ")]
    [Tooltip("加工したい元の川マップ (ErosionFlowMap)")]
    public Texture2D riverMap;

    [Tooltip("川をせき止めるための棚田のマスク (PaddyFieldMask)")]
    public Texture2D paddyMask;

    [Header("出力設定")]
    [Tooltip("保存する新しいファイル名")]
    public string outputFileName = "RiverMap_Masked.png";

    /// <summary>
    /// Inspectorからこのメニューを実行すると、マスク処理を開始します。
    /// </summary>
    [ContextMenu("マスク処理を実行して新しい川マップを保存")]
    public void ProcessAndSaveMaskedRiverMap()
    {
        // --- エラーチェック ---
        if (riverMap == null || paddyMask == null)
        {
            Debug.LogError("川マップと棚田マスクの両方のテクスチャを設定してください。");
            return;
        }
        if (riverMap.width != paddyMask.width || riverMap.height != paddyMask.height)
        {
            Debug.LogError("入力するテクスチャの解像度（幅と高さ）は同じにしてください。");
            return;
        }

        Debug.Log("マスク処理を開始します...");

        // --- ピクセルデータを配列として取得 ---
        Color[] riverPixels = riverMap.GetPixels();
        Color[] paddyPixels = paddyMask.GetPixels();
        Color[] outputPixels = new Color[riverPixels.Length];

        // --- 全てのピクセルをループして処理 ---
        for (int i = 0; i < riverPixels.Length; i++)
        {
            // 棚田マスクのピクセルが赤色または緑色かを確認
            bool isPaddyArea = paddyPixels[i].r > 0.5f || paddyPixels[i].g > 0.5f;

            if (isPaddyArea)
            {
                // 棚田のエリアの場合、川マップを黒く塗りつぶす
                outputPixels[i] = Color.black;
            }
            else
            {
                // 棚田のエリアでない場合、元の川マップのピクセルをそのまま使う
                outputPixels[i] = riverPixels[i];
            }
        }

        // --- 新しいテクスチャを作成して保存 ---
        Texture2D resultTexture = new Texture2D(riverMap.width, riverMap.height);
        resultTexture.SetPixels(outputPixels);
        resultTexture.Apply();

        byte[] pngData = resultTexture.EncodeToPNG();
        if (pngData != null)
        {
            string path = Path.Combine(Application.dataPath, outputFileName);
            File.WriteAllBytes(path, pngData);
            Debug.Log($"処理が完了しました！新しい川マップを {path} に保存しました。");
        }
        else
        {
            Debug.LogError("PNGへのエンコードに失敗しました。");
        }

        // Unityエディタに新しいアセットを認識させる
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
}