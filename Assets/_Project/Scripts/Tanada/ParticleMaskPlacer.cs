using UnityEngine;

namespace _Project.Scripts.Tanada
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleMaskPlacer : MonoBehaviour
    {
        [Header("配置設定")]
        public UnityEngine.Terrain targetTerrain;
        public Texture2D placementMask;

        [Header("パーティクル設定")]
        public int maxParticles = 10000;
        public float yOffset = 0.1f;

        [ContextMenu("マスクに従って稲パーティクルを配置 (Emit方式)")]
        public void PlaceParticles()
        {
            ParticleSystem riceParticleSystem = GetComponent<ParticleSystem>();
            if (riceParticleSystem == null || targetTerrain == null || placementMask == null)
            {
                Debug.LogError("コンポーネント、テレイン、マスクのいずれかが設定されていません。");
                return;
            }

            // Textureの読み書き設定をチェック
            try
            {
                placementMask.GetPixel(0, 0);
            }
            catch (UnityException)
            {
                Debug.LogError($"マスクテクスチャ '{placementMask.name}' の 'Read/Write Enabled' を有効にしてください。");
                return;
            }

            // 既存のパーティクルをクリア
            riceParticleSystem.Clear();

            // ★★★ 違いはここから ★★★
            // 1. パーティクルをシステムの現在の設定で生成させる
            riceParticleSystem.Emit(maxParticles);

            // 2. 生成されたパーティクルを配列に取得
            var particles = new ParticleSystem.Particle[maxParticles];
            int particleCount = riceParticleSystem.GetParticles(particles);

            int placedParticleCount = 0;
            TerrainData terrainData = targetTerrain.terrainData;
            Vector3 terrainPosition = targetTerrain.transform.position;
            Vector3 terrainSize = terrainData.size;
        
            // 3. 取得したパーティクルの位置を書き換えていく
            for (int i = 0; i < particleCount; i++)
            {
                float randomX = Random.Range(0f, 1f);
                float randomZ = Random.Range(0f, 1f);

                Color maskColor = placementMask.GetPixelBilinear(randomX, randomZ);

                if (maskColor.grayscale > 0.5f)
                {
                    float worldX = terrainPosition.x + randomX * terrainSize.x;
                    float worldZ = terrainPosition.z + randomZ * terrainSize.z;
                    float worldY = targetTerrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + terrainPosition.y;

                    // 位置情報だけを上書き
                    particles[placedParticleCount].position = new Vector3(worldX, worldY + yOffset, worldZ);
                    // 寿命を無限にして、消えないようにする
                    particles[placedParticleCount].remainingLifetime = float.MaxValue;
                    // 動きを止める
                    particles[placedParticleCount].velocity = Vector3.zero;

                    placedParticleCount++;
                }
            }
        
            // 4. マスクの黒い部分に配置されてしまった余分なパーティクルを消す
            //    (placedParticleCountより後ろのパーティクルの寿命を0にする)
            for (int i = placedParticleCount; i < particleCount; i++)
            {
                particles[i].remainingLifetime = 0;
            }

            // 5. 変更をシステムに適用
            riceParticleSystem.SetParticles(particles, particleCount);
        
            // 念のためシミュレーションを停止
            riceParticleSystem.Pause();

            Debug.Log(placedParticleCount + "個のパーティクルを配置しました。");
        }
    }
}