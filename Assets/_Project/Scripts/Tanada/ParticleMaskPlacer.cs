using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Tanada
{
    public class DrawInstancedRice : MonoBehaviour
    {
        [Header("必須設定")]
        [Tooltip("配置先のテレイン")]
        public UnityEngine.Terrain terrain;
        [Tooltip("配置の基準となるマスクテクスチャ")]
        public Texture2D mask;
        [Tooltip("インスタンス描画する稲のメッシュ")]
        public Mesh riceMesh;

        // ★★★ 変更点1：単一のマテリアルからマテリアルの配列に変更 ★★★
        [Tooltip("稲のメッシュに適用するマテリアルのリスト。メッシュのサブメッシュの順番と一致させること")]
        public Material[] riceMaterials;

        [Header("配置設定")]
        [Tooltip("配置を試みるインスタンスの最大数")]
        public int instanceCount = 100000;
        [Tooltip("マスク画像の白として判定する色の閾値")]
        [Range(0f, 1f)]
        public float maskThreshold = 0.5f;

        [Header("見た目の調整")]
        [Tooltip("Y軸の回転をランダムにするか")]
        public bool randomizeRotation = true;
        [Tooltip("スケールのランダム範囲 (X=Min, Y=Max)")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    
        private List<Matrix4x4> matrices;
        private const int BATCH_SIZE = 1023;

        [ContextMenu("1. 配置データを生成する")]
        private void GeneratePlacementData()
        {
            if (!IsValid(true)) return;

            Debug.Log("配置データの生成を開始します...");
            matrices = new List<Matrix4x4>(instanceCount);
            var terrainData = terrain.terrainData;
            var terrainPos = terrain.transform.position;

            int placedCount = 0;
            for (int i = 0; i < instanceCount; i++)
            {
                float u = Random.value;
                float v = Random.value;
                if (mask.GetPixelBilinear(u, v).grayscale > maskThreshold)
                {
                    float x = u * terrainData.size.x;
                    float z = v * terrainData.size.z;
                    float y = terrain.SampleHeight(new Vector3(x, 0, z)); 
                    Vector3 pos = new Vector3(x, y, z) + terrainPos;
                
                    Quaternion rot = randomizeRotation ? 
                        Quaternion.Euler(0, Random.Range(0, 360f), 0) : 
                        Quaternion.identity;
                
                    float scale = Random.Range(scaleRange.x, scaleRange.y);
                
                    matrices.Add(Matrix4x4.TRS(pos, rot, Vector3.one * scale));
                    placedCount++;
                }
            }
            Debug.Log($"{placedCount}個の配置データを生成しました。");
        }

        [ContextMenu("2. 配置データをクリアする")]
        private void ClearPlacementData()
        {
            if (matrices != null)
            {
                matrices.Clear();
                Debug.Log("配置データをクリアしました。");
            }
        }
    
        private void Update()
        {
            if (matrices == null || matrices.Count == 0 || !IsValid(false))
            {
                return;
            }

            // ★★★ 変更点2：マテリアルごとに描画処理をループする ★★★
            // メッシュが持つサブメッシュの数だけ描画命令を出す
            for (int submeshIndex = 0; submeshIndex < riceMesh.subMeshCount; submeshIndex++)
            {
                // このサブメッシュに対応するマテリアルが設定されていなければスキップ
                if (submeshIndex >= riceMaterials.Length) continue;

                // 全てのインスタンスをバッチに分けて、現在のサブメッシュを描画
                for (int i = 0; i < matrices.Count; i += BATCH_SIZE)
                {
                    int count = Mathf.Min(BATCH_SIZE, matrices.Count - i);
                    Graphics.DrawMeshInstanced(
                        riceMesh, 
                        submeshIndex, // 0番目のサブメッシュ、1番目のサブメッシュ...
                        riceMaterials[submeshIndex], // 0番目のマテリアル、1番目のマテリアル...
                        matrices.GetRange(i, count)
                    );
                }
            }
        }
    
        private bool IsValid(bool logErrors)
        {
            if (terrain == null || mask == null || riceMesh == null || riceMaterials == null || riceMaterials.Length == 0)
            {
                if(logErrors) Debug.LogError("必須設定が不足しています。");
                return false;
            }
            if (logErrors && riceMaterials.Length != riceMesh.subMeshCount)
            {
                Debug.LogWarning($"メッシュは {riceMesh.subMeshCount} 個のサブメッシュを持っていますが、マテリアルは {riceMaterials.Length} 個しか設定されていません。数が一致しないと正しく表示されません。");
            }
            return true;
        }
    }
}