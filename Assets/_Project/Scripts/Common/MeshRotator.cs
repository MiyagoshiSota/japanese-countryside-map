#if UNITY_EDITOR // この行以下のコードはUnityエディタ内でのみ有効になります
using UnityEngine;
using UnityEditor; // Editor機能を使うために必要
using System.IO;   // ファイルパスを操作するために必要

[RequireComponent(typeof(MeshFilter))]
public class MeshSaver : MonoBehaviour
{
    [Header("メッシュ回転設定")]
    [Tooltip("この角度でメッシュデータを回転させます。")]
    public Vector3 rotationAngles = new Vector3(180, 0, 0);

    [Header("メッシュ保存設定")]
    [Tooltip("新しいメッシュアセットを保存するプロジェクト内のパス")]
    public string saveFolderPath = "Assets/SavedMeshes";
    [Tooltip("保存する新しいメッシュのファイル名")]
    public string newMeshFileName = "RotatedRice.asset";

    [ContextMenu("回転を適用し、新しいメッシュとして保存")]
    public void RotateAndSaveMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshFilterまたは元のメッシュが見つかりません。");
            return;
        }

        // 1. メッシュを複製して回転処理を行う（前回と同じロジック）
        Mesh rotatedMesh = Instantiate(meshFilter.sharedMesh);
        
        Vector3[] vertices = rotatedMesh.vertices;
        Vector3[] normals = rotatedMesh.normals;
        Quaternion rotation = Quaternion.Euler(rotationAngles);

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = rotation * vertices[i];
        }
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = rotation * normals[i];
        }

        rotatedMesh.vertices = vertices;
        rotatedMesh.normals = normals;
        rotatedMesh.RecalculateBounds();

        // 2. 保存パスが存在するか確認し、なければ作成する
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }

        // 3. メッシュをアセットファイルとしてプロジェクトに保存する
        string fullPath = Path.Combine(saveFolderPath, newMeshFileName);
        // パスが重複しないように、ユニークなパスを生成する
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
        
        AssetDatabase.CreateAsset(rotatedMesh, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"回転したメッシュを新しいアセットとして '{fullPath}' に保存しました。");
        
        // 4. (推奨) このオブジェクトのメッシュを、新しく保存したアセットに差し替える
        meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(fullPath);
    }
}
#endif // この行でエディタ専用コードは終わり