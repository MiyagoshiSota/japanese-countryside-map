using UnityEngine;
using System.IO;

public class MaskCombiner : MonoBehaviour
{
    [Header("入力マスク")]
    [Tooltip("合成したいマスク1 (例: TownMask)")]
    public Texture2D maskA;
    [Tooltip("合成したいマスク2 (例: RiceFieldMask)")]
    public Texture2D maskB;

    [Header("出力設定")]
    [Tooltip("出力するファイル名")]
    public string outputFilename = "FlatAreaMask.png";

    [ContextMenu("マスクを合成する")]
    public void CombineMasks()
    {
        if (maskA == null || maskB == null)
        {
            Debug.LogError("入力マスクが両方設定されていません！");
            return;
        }
        if (maskA.width != maskB.width || maskA.height != maskB.height)
        {
            Debug.LogError("マスクの解像度が一致していません！");
            return;
        }

        // Read/Write Enabledのチェック
        try
        {
            maskA.GetPixel(0, 0);
            maskB.GetPixel(0, 0);
        }
        catch (UnityException e)
        {
            Debug.LogError($"マスクのインポート設定で「Read/Write Enabled」を有効にしてください。エラー: {e.Message}");
            return;
        }

        int width = maskA.width;
        int height = maskA.height;
        Texture2D outputMask = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // どちらかのマスクのピクセルが白（r > 0.5）なら、出力も白にする
                if (maskA.GetPixel(x, y).r > 0.5f || maskB.GetPixel(x, y).r > 0.5f)
                {
                    outputMask.SetPixel(x, y, Color.white);
                }
                else
                {
                    outputMask.SetPixel(x, y, Color.black);
                }
            }
        }

        outputMask.Apply();
        SaveTextureAsPNG(outputMask, outputFilename);
        Debug.Log($"マスクの合成が完了しました。プロジェクトのルートフォルダに {outputFilename} が保存されました。");
    }

    private void SaveTextureAsPNG(Texture2D texture, string filename)
    {
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, filename), bytes);
    }
}