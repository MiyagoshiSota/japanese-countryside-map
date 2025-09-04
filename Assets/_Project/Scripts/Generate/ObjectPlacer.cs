using UnityEngine;

public partial class ObjectPlacer : MonoBehaviour
{
    [Header("Object Settings")]
    public GameObject objectToPlacePrefab; // 配置するオブジェクトのプレハブ
    public int numberOfObjects = 500;      // 配置する数

    [Header("Placement Conditions")]
    [Tooltip("オブジェクトを配置する最低標高")]
    public float minHeight = 5f;
    [Tooltip("オブジェクトを配置する最高標高")]
    public float maxHeight = 25f;
    [Tooltip("配置可能な最大傾斜角度")]
    [Range(0, 90)]
    public float maxSteepness = 45f;
    
    [Header("Randomization")]
    [Tooltip("Y軸回転をランダムにするか")]
    public bool randomizeRotation = true;
    [Tooltip("スケールの最小値")]
    public float minScale = 0.8f;
    [Tooltip("スケールの最大値")]
    public float maxScale = 1.2f;

    // このメソッドを外部から呼び出して実行する
    public void PlaceObjects(Mesh terrainMesh)
    {
        if (objectToPlacePrefab == null)
        {
            Debug.LogError("配置するオブジェクトのプレハブが設定されていません。");
            return;
        }

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogError("MeshColliderが必要です。TerrainGeneratorに追加してください。");
            return;
        }

        Bounds bounds = terrainMesh.bounds;
        Transform container = new GameObject(objectToPlacePrefab.name + " Container").transform;

        for (int i = 0; i < numberOfObjects; i++)
        {
            // 1. ランダムなXZ座標を決定
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 raycastStartPos = new Vector3(randomX, bounds.max.y + 10f, randomZ);

            RaycastHit hit;
            // 2. 地面に向かってRayを飛ばす
            if (Physics.Raycast(raycastStartPos, Vector3.down, out hit, bounds.size.y + 20f))
            {
                // 3. 配置条件をチェック
                // 高さ条件
                bool heightCondition = hit.point.y >= minHeight && hit.point.y <= maxHeight;

                // 傾斜条件
                float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                bool slopeCondition = slopeAngle <= maxSteepness;

                // 4. 条件をすべて満たしたらオブジェクトを生成
                if (heightCondition && slopeCondition)
                {
                    Vector3 position = hit.point;
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    
                    if(randomizeRotation)
                    {
                        rotation *= Quaternion.Euler(0, Random.Range(0, 360), 0);
                    }

                    GameObject newObj = Instantiate(objectToPlacePrefab, position, rotation);
                    newObj.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
                    newObj.transform.SetParent(container); // 生成したオブジェクトを整理
                }
            }
        }
    }
}