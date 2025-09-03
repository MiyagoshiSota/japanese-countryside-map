using UnityEngine;

namespace _Project.Scripts.Terrain.Generate
{
    [RequireComponent(typeof(UnityEngine.Terrain))]
    public class RandomUndulationMountain : MonoBehaviour
    {
        [Header("地形設定")]
        public UnityEngine.Terrain terrain;
    
        [Header("ベースの山の設定")]
        [Tooltip("山の半径。山の大きさを決めます。")]
        public float mountainRadius = 900f;

        [Tooltip("山の頂上の最大高 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float maxHeight = 0.7f;

        [Tooltip("山全体の斜面の滑らかさ。大きいほど麓が広がります。")]
        [Range(0.1f, 5f)]
        public float smoothness = 2.0f;

        [Header("起伏のノイズ設定")]
        [Tooltip("起伏のスケール感。値を大きくすると、より細かく複雑な起伏になります。")]
        public float noiseScale = 60f;

        [Tooltip("ノイズを重ねる回数（ディテールの量）")]
        [Range(1, 10)]
        public int octaves = 7;
    
        [Tooltip("起伏の強さ。0にすると完全に滑らか、1にすると起伏が激しくなります。")]
        [Range(0f, 1f)]
        public float noiseStrength = 0.6f;

        [Header("ランダム設定")]
        [Tooltip("生成のたびに結果を変えるためのシード値。0の場合は実行ごとにランダム。")]
        public int seed = 0;

        [ContextMenu("ランダムな起伏のある山を生成する")]
        public void Generate()
        {
            if (terrain == null) terrain = GetComponent<UnityEngine.Terrain>();
            TerrainData terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;

            // シード値に基づいてランダム状態を初期化
            if (seed != 0) Random.InitState(seed);
            else Random.InitState((int)System.DateTime.Now.Ticks);

            // --- 1. 山の中心点をランダムに決定 ---
            Vector2 mountainCenter = Vector2.zero;
            float offsetFromEdge = -300f; // 地形の外側にどれだけ離すか
            int edge = Random.Range(0, 4); // 0:上, 1:右, 2:下, 3:左

            switch (edge)
            {
                case 0: // 上
                    mountainCenter = new Vector2(Random.Range(0, resolution), resolution - offsetFromEdge);
                    break;
                case 1: // 右
                    mountainCenter = new Vector2(resolution - offsetFromEdge, Random.Range(0, resolution));
                    break;
                case 2: // 下
                    mountainCenter = new Vector2(Random.Range(0, resolution), offsetFromEdge);
                    break;
                case 3: // 左
                    mountainCenter = new Vector2(offsetFromEdge, Random.Range(0, resolution));
                    break;
            }

            // シード値が変わっても同じノイズパターンになるようにオフセットをランダム化
            Vector2 noiseOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));

            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // --- 2. 滑らかなベースの山の形状を計算 ---
                    float distance = Vector2.Distance(new Vector2(x, y), mountainCenter);
                    float smoothMountainMask = Mathf.Clamp01(1.0f - (distance / mountainRadius));
                    smoothMountainMask = Mathf.Pow(smoothMountainMask, smoothness);

                    // --- 3. 起伏となるフラクタルノイズを計算 ---
                    float noiseHeight = 0f;
                    float amplitude = 1f;
                    float frequency = 1f;
                    for (int i = 0; i < octaves; i++)
                    {
                        float sampleX = (x + noiseOffset.x) / resolution * noiseScale * frequency;
                        float sampleY = (y + noiseOffset.y) / resolution * noiseScale * frequency;
                        // ノイズの値を-0.5～0.5の範囲にして、地形を削る方向にも変化させる
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) - 0.5f;
                        noiseHeight += perlinValue * amplitude;
                        amplitude *= 0.5f; // persistence
                        frequency *= 2.0f; // lacunarity
                    }

                    // --- 4. ベースの山と起伏を合成 ---
                    // (滑らかな山の高さ) + (ノイズの起伏)
                    // ノイズの影響は、ベースの山の高さに応じて強くなるようにする
                    float finalHeight = smoothMountainMask + (noiseHeight * noiseStrength * smoothMountainMask);

                    heights[y, x] = finalHeight * maxHeight;
                }
            }
        
            terrainData.SetHeights(0, 0, heights);
            Debug.Log($"山の生成が完了しました。中心座標: {mountainCenter}");
        }
    }
}