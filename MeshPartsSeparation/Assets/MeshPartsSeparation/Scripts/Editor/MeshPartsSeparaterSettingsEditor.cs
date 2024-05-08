using UnityEditor;
using UnityEngine;

namespace MeshPartsSeparation
{
    [CustomEditor(typeof(MeshPartsSeparaterSettings))]
    public class MeshPartsSeparaterSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Mesh Parts Separater Window"))
            {
                MeshPartsSeparaterWindow.Open((MeshPartsSeparaterSettings)target);
            }
            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
