using UnityEngine;
using UnityEditor;

/// <summary>
/// Helper that creates a vertex-color material for the sculpted mountain.
/// Requires the included LowPolyMountain.shader.
/// Run via: Tools > Low-Poly Mountain Sculptor > Setup Vertex Color Material
/// </summary>
public static class MountainMaterialSetup
{
    [MenuItem("Tools/Low-Poly Mountain Sculptor/Setup Vertex Color Material")]
    static void CreateMaterial()
    {
        // Try to find or create the shader
        Shader shader = Shader.Find("Custom/LowPolyVertexColor");
        if (shader == null)
        {
            Debug.LogWarning("[Mountain Sculptor] Custom/LowPolyVertexColor shader not found. " +
                             "Create LowPolyVertexColor.shader in your project (see README), " +
                             "or assign a vertex-color shader manually.");
            return;
        }

        string path = "Assets/LowPolyMountain_Mat.mat";
        var mat = new Material(shader);
        mat.name = "LowPolyMountain_Mat";

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();

        // Auto-assign to selected object
        var sel = Selection.activeGameObject;
        if (sel != null)
        {
            var mr = sel.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                Debug.Log("[Mountain Sculptor] Material assigned to " + sel.name);
            }
        }

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = mat;
        Debug.Log("[Mountain Sculptor] Material created at " + path);
    }
}
