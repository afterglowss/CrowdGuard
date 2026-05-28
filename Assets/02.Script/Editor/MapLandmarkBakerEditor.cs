using CrowdGuard.Climbing.Tools.Map;
using UnityEditor;
using UnityEngine;

namespace CrowdGuard.Editor.Map
{
    /// <summary>
    /// MapLandmarkBaker inspector에서 bake를 직접 실행할 수 있게 합니다.
    /// </summary>
    [CustomEditor(typeof(MapLandmarkBaker))]
    public class MapLandmarkBakerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Bake Landmarks"))
                {
                    MapLandmarkBakeUtility.Bake((MapLandmarkBaker)target, out _);
                }
            }
        }
    }
}
