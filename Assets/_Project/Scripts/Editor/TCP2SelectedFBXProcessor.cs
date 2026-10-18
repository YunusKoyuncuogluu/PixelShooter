using ToonyColorsPro.Utilities;
using UnityEditor;
using UnityEngine;

public class TCP2SelectedFBXProcessor : AssetPostprocessor
{
    private const string ProcessFlag = "TCP2_PROCESS_NORMALS";

    [MenuItem("Assets/TCP2/Apply Smoothed Normals to Selected FBX", true)]
    private static bool ValidateProcessSelectedFBX()
    {
        return Selection.activeObject != null && Selection.activeObject is GameObject;
    }

    [MenuItem("Assets/TCP2/Apply Smoothed Normals to Selected FBX")]
    private static void ProcessSelectedFBX()
    {
        foreach (UnityEngine.Object selectedObj in Selection.objects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObj);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (importer == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(importer.userData))
            {
                importer.userData = ProcessFlag;
            }
            else if (!importer.userData.Contains(ProcessFlag))
            {
                importer.userData += "," + ProcessFlag;
            }

            importer.SaveAndReimport();
            Debug.Log($"[TCP2] Smoothed normals applied and embedded into {selectedObj.name}");
        }
    }

    private void OnPostprocessModel(GameObject processedObject)
    {
        ModelImporter importer = assetImporter as ModelImporter;

        if (importer == null || string.IsNullOrEmpty(importer.userData) || !importer.userData.Contains(ProcessFlag))
        {
            return;
        }

        MeshFilter[] meshFilters = processedObject.GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            ApplySmoothedNormals(meshFilter.sharedMesh);
        }

        SkinnedMeshRenderer[] skinnedMeshes = processedObject.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshes)
        {
            ApplySmoothedNormals(skinnedMeshRenderer.sharedMesh);
        }
    }

    private static void ApplySmoothedNormals(Mesh mesh)
    {
        if (mesh == null)
        {
            return;
        }

        Utils.CreateSmoothedMesh(
            mesh,
            "XYZ",
            Utils.SmoothedNormalsChannel.VertexColors,
            Utils.SmoothedNormalsUVType.FullXYZ,
            true);
    }
}
