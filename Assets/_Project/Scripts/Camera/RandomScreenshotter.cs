using UnityEngine;
using System.IO;

public class PanoramicScreenshotter : MonoBehaviour
{
    [Header("必須設定")]
    [Tooltip("基準となる地形")]
    public Terrain targetTerrain;
    [Tooltip("撮影に使うカメラ")]
    public Camera panoramaCamera;
    [Tooltip("中心点を計算するための棚田マスク")]
    public Texture2D paddyMask;
    [Tooltip("シーン上にある稲の描画スクリプト")]
    public _Project.Scripts.Tanada.DrawInstancedRice riceDrawer;

    [Header("撮影設定")]
    [Tooltip("旋回する中心点からの距離（半径）")]
    public float orbitRadius = 150f;
    [Tooltip("中心点の地形からのカメラの高さ")]
    public float cameraHeightAboveCenter = 50f;

    [Header("出力画像設定")]
    [Tooltip("出力するパノラマ画像の横幅（撮影するスライス数）")]
    public int outputImageWidth = 2048;
    [Tooltip("出力するパノラマ画像の高さ")]
    public int outputImageHeight = 512;
    [Tooltip("保存するファイル名")]
    public string outputFileName = "panorama_shot.png";

    [ContextMenu("パノラマ写真を撮影する")]
    public void GeneratePanoramicShot()
    {
        if (targetTerrain == null || panoramaCamera == null || paddyMask == null)
        {
            Debug.LogError("必須設定が不足しています。Terrain, Camera, Maskを全て設定してください。");
            return;
        }
        if (riceDrawer == null)
        {
            Debug.LogError("Rice Drawerが設定されていません！稲の描画スクリプトをアタッチしてください。");
            return;
        }

        Debug.Log("マスクの中心点を計算しています...");
        Vector2 centerUV = CalculateMaskCenter(paddyMask);
        if (centerUV == Vector2.zero)
        {
            Debug.LogError("マスクから有効なエリア（赤または緑のピクセル）が見つかりませんでした。");
            return;
        }

        TerrainData td = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;
        float centerX = centerUV.x * td.size.x + terrainPos.x;
        float centerZ = centerUV.y * td.size.z + terrainPos.z;
        float centerTerrainHeight = targetTerrain.SampleHeight(new Vector3(centerX, 0, centerZ));
        Vector3 centerPoint = new Vector3(centerX, centerTerrainHeight, centerZ);

        Debug.Log($"中心点を計算しました: {centerPoint}");
        
        Texture2D outputTexture = new Texture2D(outputImageWidth, outputImageHeight, TextureFormat.RGB24, false);
        RenderTexture renderTexture = new RenderTexture(outputImageHeight, outputImageHeight, 24); 
        
        panoramaCamera.targetTexture = renderTexture;

        Debug.Log("パノラマ撮影を開始します...");
        
        for (int i = 0; i < outputImageWidth; i++)
        {
            float angle = (float)i / outputImageWidth * 360f * Mathf.Deg2Rad;
            
            float camX = centerPoint.x + orbitRadius * Mathf.Cos(angle);
            float camZ = centerPoint.z + orbitRadius * Mathf.Sin(angle);
            float camY = centerPoint.y + cameraHeightAboveCenter;
            Vector3 camPos = new Vector3(camX, camY, camZ);
            
            panoramaCamera.transform.position = camPos;
            panoramaCamera.transform.LookAt(centerPoint);
            
            riceDrawer.DrawNow();
            
            panoramaCamera.Render();
            
            RenderTexture.active = renderTexture;
            Rect sourceRect = new Rect(renderTexture.width / 2f, 0, 1, renderTexture.height);
            outputTexture.ReadPixels(sourceRect, i, 0);
        }
        
        RenderTexture.active = null;
        panoramaCamera.targetTexture = null;
        DestroyImmediate(renderTexture);
        
        outputTexture.Apply();

        byte[] bytes = outputTexture.EncodeToPNG();
        string path = Path.Combine(Application.dataPath, "..", outputFileName);
        File.WriteAllBytes(path, bytes);

        Debug.Log($"パノラマ撮影が完了しました！ {path} に保存しました。");
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
    
    private Vector2 CalculateMaskCenter(Texture2D mask)
    {
        Color[] pixels = mask.GetPixels();
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        bool found = false;

        for (int y = 0; y < mask.height; y++)
        {
            for (int x = 0; x < mask.width; x++)
            {
                Color p = pixels[y * mask.width + x];
                if (p.r > 0.5f || p.g > 0.5f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    found = true;
                }
            }
        }

        if (!found) return Vector2.zero;
        
        return new Vector2((minX + maxX) / 2f / mask.width, (minY + maxY) / 2f / mask.height);
    }
}