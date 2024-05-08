using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshPartsSeparation.Analyze;
using UnityEditor;
using UnityEngine;

namespace MeshPartsSeparation
{
    public class MeshPartsSeparaterWindow : EditorWindow
    {
        private const float InspectorWidth = 300f;

        private enum Category
        {
            Source,
            MeshGroup,
            SeparatedMesh,
        }

        private MeshPartsSeparaterSettings _settings;
        private Vector2 _inspectorScrollPosition;
        private bool _toggleAnalyze = true;
        private PreviewRenderUtility _previewRender;
        private Category _selectedCategory = Category.Source;
        private int _selectedCategoryElement;
        private List<int> _meshGroupIdBuffer = new List<int>();
        private Vector2 _linksScrollPosition;
        private Vector2 _meshGroupScrollPosition;
        private Mesh _previewMesh;
        private bool _updatePreviewMesh = true;
        private List<int> _previewTriBuffer = new List<int>();
        private Vector3 _camPivot = Vector3.zero;
        private Vector3 _camAngles = Vector3.zero;
        private float _camDistance = 5;

        public static void Open(MeshPartsSeparaterSettings settings)
        {
            var window = GetWindow<MeshPartsSeparaterWindow>();
            window.titleContent = new GUIContent("Mesh Parts Separater");
            window.Show();
            window.SetSettings(settings);
        }

        private void OnEnable()
        {
            _previewRender = new PreviewRenderUtility();
        }

        private void OnDisable()
        {
            _previewRender.Cleanup();
            DestroyImmediate(_previewMesh);
            _previewMesh = null;
        }

        public void SetSettings(MeshPartsSeparaterSettings settings)
        {
            _settings = settings;
        }

        private void TryCreatePreviewMesh()
        {
            if (_previewMesh == null && _settings != null && _settings.SourceMesh != null)
            {
                _previewMesh = Instantiate(_settings.SourceMesh);
            }
        }

        private void OnGUI()
        {
            var uvViewRect = new Rect(0, 0, position.width - InspectorWidth, position.height);
            var inspectorRect = new Rect(position.width - InspectorWidth, 0, InspectorWidth, position.height);

            // Inspector
            using (new GUILayout.AreaScope(inspectorRect))
            using (var scrollView = new GUILayout.ScrollViewScope(_inspectorScrollPosition))
            {
                _inspectorScrollPosition = scrollView.scrollPosition;
                DrawInspectorGUI();
            }

            // 3D View
            TryCreatePreviewMesh();
            if (_updatePreviewMesh)
            {
                _updatePreviewMesh = false;
                UpdatePreviewMesh();
            }
            UpdateCamera();
            DrawPreview(uvViewRect);
        }

        private void DrawPreview(Rect rect)
        {
            _previewRender.BeginPreview(rect, GUIStyle.none);
            _previewRender.camera.cameraType = CameraType.Preview;
            _previewRender.camera.orthographic = false;
            _previewRender.camera.fieldOfView = 60.0f;
            _previewRender.camera.nearClipPlane = 0.1f;
            _previewRender.camera.farClipPlane = 100.0f;
            var rot = Quaternion.AngleAxis(_camAngles.y, Vector3.up) * Quaternion.AngleAxis(_camAngles.x, Vector3.right);
            _previewRender.camera.transform.position = _camPivot + rot * Vector3.back * _camDistance;
            _previewRender.camera.transform.rotation = rot;
            var light = _previewRender.lights.FirstOrDefault();
            if (light != null)
            {
                light.transform.rotation = rot;
            }

            // Draw mesh
            _previewRender.DrawMesh(_previewMesh, Vector3.zero, Quaternion.identity, _settings.PreviewMaterial, 0);

            _previewRender.camera.Render();
            Handles.SetCamera(_previewRender.camera);

            var tex = _previewRender.EndPreview();
            GUI.DrawTexture(rect, tex);
        }

        private void DrawInspectorGUI()
        {
            var serializedObject = new SerializedObject(_settings);
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(_settings, typeof(MeshPartsSeparaterSettings), false);
            }

            using (var changeCheck = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MeshPartsSeparaterSettings.SourceMesh)));
                if (changeCheck.changed)
                {
                    DestroyImmediate(_previewMesh);
                    _previewMesh = null;
                    _updatePreviewMesh = true;
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MeshPartsSeparaterSettings.PreviewMaterial)));

            _toggleAnalyze = EditorGUILayout.BeginFoldoutHeaderGroup(_toggleAnalyze, "Analyze");
            if (_toggleAnalyze)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MeshPartsSeparaterSettings.AnalyzerSettings)));
                    if (GUILayout.Button("Analyze"))
                    {
                        var analyzer = new MeshGroupAnalyzer(_settings.SourceMesh, _settings.AnalyzerSettings);
                        var result = new List<GroupResult>();
                        analyzer.Analyze(result);
                        var groupResultsProperty = serializedObject.FindProperty(nameof(MeshPartsSeparaterSettings.GroupResults));
                        groupResultsProperty.ClearArray();
                        for (int i = 0; i < result.Count; i++)
                        {
                            groupResultsProperty.InsertArrayElementAtIndex(i);
                            var groupResultProperty = groupResultsProperty.GetArrayElementAtIndex(i);
                            groupResultProperty.FindPropertyRelative(nameof(GroupResult.Triangles)).arraySize = result[i].Triangles.Count;
                            for (int j = 0; j < result[i].Triangles.Count; j++)
                            {
                                groupResultProperty.FindPropertyRelative(nameof(GroupResult.Triangles)).GetArrayElementAtIndex(j).intValue = result[i].Triangles[j];
                            }
                        }
                        _updatePreviewMesh = true;
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Common");
            using (new GUILayout.HorizontalScope())
            {
                var isSelected = _selectedCategory == Category.Source;
                if (GUILayout.Toggle(isSelected, GUIContent.none))
                {
                    _selectedCategory = Category.Source;
                    _selectedCategoryElement = 0;
                    _updatePreviewMesh = true;
                }
                EditorGUILayout.LabelField("View source");
            }

            EditorGUILayout.LabelField("Mesh Group Links");
            using (var scroll = new GUILayout.ScrollViewScope(_linksScrollPosition))
            {
                _linksScrollPosition = scroll.scrollPosition;
                var groupResults = _settings.GroupResults;
                var meshGroupLinksProperty = serializedObject.FindProperty(nameof(MeshPartsSeparaterSettings.MeshGroupLinks));
                for (int i = 0; i < groupResults.Count; i++)
                {
                    var linkIndex = TryGetMeshGroupLinkProperty(meshGroupLinksProperty, i, out var meshGroupLinkProperty);
                    var linkedGroupId = linkIndex >= 0 ? meshGroupLinkProperty.FindPropertyRelative(nameof(MeshGroupLink.MeshGroupId)).intValue : 0;
                    var isSelected = _selectedCategory == Category.SeparatedMesh && i == _selectedCategoryElement;
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(isSelected, GUIContent.none))
                        {
                            _selectedCategory = Category.SeparatedMesh;
                            _selectedCategoryElement = i;
                            _updatePreviewMesh = true;
                        }
                        EditorGUILayout.LabelField(i.ToString());
                        using (var changeCheck = new EditorGUI.ChangeCheckScope())
                        {
                            linkedGroupId = EditorGUILayout.IntField(linkedGroupId);
                            if (changeCheck.changed)
                            {
                                if (linkIndex >= 0)
                                {
                                    if (linkedGroupId == 0)
                                    {
                                        meshGroupLinksProperty.DeleteArrayElementAtIndex(linkIndex);
                                    }
                                    else
                                    {
                                        meshGroupLinkProperty.FindPropertyRelative(nameof(MeshGroupLink.MeshGroupId)).intValue = linkedGroupId;
                                    }
                                }
                                else if (linkedGroupId != 0)
                                {
                                    meshGroupLinksProperty.InsertArrayElementAtIndex(meshGroupLinksProperty.arraySize);
                                    var newLinkProperty = meshGroupLinksProperty.GetArrayElementAtIndex(meshGroupLinksProperty.arraySize - 1);
                                    newLinkProperty.FindPropertyRelative(nameof(MeshGroupLink.GroupResultIndex)).intValue = i;
                                    newLinkProperty.FindPropertyRelative(nameof(MeshGroupLink.MeshGroupId)).intValue = linkedGroupId;
                                }
                                _updatePreviewMesh = true;
                            }
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Mesh Groups");
            using (var scroll = new GUILayout.ScrollViewScope(_meshGroupScrollPosition))
            {
                _meshGroupScrollPosition = scroll.scrollPosition;
                _meshGroupIdBuffer.Clear();
                CollectMeshGroupIds(_settings.MeshGroupLinks, _meshGroupIdBuffer);
                _meshGroupIdBuffer.Sort();

                for (int i = 0; i < _meshGroupIdBuffer.Count; i++)
                {
                    var id = _meshGroupIdBuffer[i];
                    using (new GUILayout.HorizontalScope())
                    {
                        var isSelected = _selectedCategory == Category.MeshGroup && _selectedCategoryElement == id;
                        if (GUILayout.Toggle(isSelected, GUIContent.none))
                        {
                            _selectedCategory = Category.MeshGroup;
                            _selectedCategoryElement = id;
                            _updatePreviewMesh = true;
                        }

                        EditorGUILayout.LabelField($"Group {id}");
                    }
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate"))
            {
                Generate();
            }
        }

        private static void CollectMeshGroupIds(IReadOnlyList<MeshGroupLink> meshGroupLinks, List<int> results)
        {
            var links = meshGroupLinks;
            results.Add(0);
            for (int i = 0; i < links.Count; i++)
            {
                var id = links[i].MeshGroupId;
                if (!results.Contains(id))
                {
                    results.Add(id);
                }
            }
        }

        private int TryGetMeshGroupLinkProperty(SerializedProperty meshGroupLinksProperty, int groupResultIndex, out SerializedProperty meshGroupLinkProperty)
        {
            for (int i = 0; i < meshGroupLinksProperty.arraySize; i++)
            {
                var property = meshGroupLinksProperty.GetArrayElementAtIndex(i);
                var meshGroupIdProperty = property.FindPropertyRelative(nameof(MeshGroupLink.GroupResultIndex));
                if (meshGroupIdProperty.intValue == groupResultIndex)
                {
                    meshGroupLinkProperty = property;
                    return i;
                }
            }
            meshGroupLinkProperty = null;
            return -1;
        }

        private void UpdatePreviewMesh()
        {
            _previewTriBuffer.Clear();
            if (_selectedCategory == Category.Source)
            {
                _previewTriBuffer.AddRange(_settings.SourceMesh.triangles);
            }
            else if (_selectedCategory == Category.SeparatedMesh)
            {
                var groupResult = _settings.GroupResults[_selectedCategoryElement];
                _previewTriBuffer.AddRange(groupResult.Triangles);
            }
            else if (_selectedCategory == Category.MeshGroup)
            {
                CollectMeshGroupTriangles(_selectedCategoryElement, _previewTriBuffer);
            }

            _previewMesh.SetTriangles(_previewTriBuffer, 0);
        }

        private void CollectMeshGroupTriangles(int meshGroupId, List<int> result)
        {
            if (meshGroupId == 0)
            {
                var groupResults = _settings.GroupResults;
                for (int i = 0; i < groupResults.Count; i++)
                {
                    if (_settings.MeshGroupLinks.FindIndex(v => v.GroupResultIndex == i) == -1)
                    {
                        result.AddRange(groupResults[i].Triangles);
                    }
                }
            }
            else
            {
                var links = _settings.MeshGroupLinks;
                for (int i = 0; i < links.Count; i++)
                {
                    if (links[i].MeshGroupId == meshGroupId)
                    {
                        var groupResult = _settings.GroupResults[links[i].GroupResultIndex];
                        result.AddRange(groupResult.Triangles);
                    }
                }
            }
        }

        private void UpdateCamera()
        {
            var e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                _camDistance = Mathf.Max(0.1f, _camDistance + e.delta.y * 0.1f);
                Repaint();
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (e.button == 0)
                {
                    var rot = Quaternion.AngleAxis(_camAngles.y, Vector3.up) * Quaternion.AngleAxis(_camAngles.x, Vector3.right);
                    _camPivot += rot * new Vector3(-e.delta.x, e.delta.y, 0) * 0.01f;
                    Repaint();
                }
                else if (e.button == 1)
                {
                    _camAngles.x += e.delta.y * 0.5f;
                    _camAngles.y += e.delta.x * 0.5f;
                    Repaint();
                }
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.R)
                {
                    _camPivot = Vector3.zero;
                    _camAngles = Vector3.zero;
                    _camDistance = 5;
                    Repaint();
                }
            }
        }

        private void Generate()
        {
            var meshGroupIds = new List<int>();
            CollectMeshGroupIds(_settings.MeshGroupLinks, meshGroupIds);
            meshGroupIds.Sort();
            var sourceVerts = new List<Vector3>();
            _settings.SourceMesh.GetVertices(sourceVerts);
            var sourceUVs = new List<Vector2>();
            _settings.SourceMesh.GetUVs(0, sourceUVs);
            var verticesBuffer = new List<Vector3>();
            var uvsBuffer = new List<Vector2>();
            var trianglesBuffer1 = new List<int>();
            var trianglesBuffer2 = new List<int>();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < meshGroupIds.Count; i++)
                {
                    var meshGroupId = meshGroupIds[i];

                    // Get or create mesh asset
                    var settingsPath = AssetDatabase.GetAssetPath(_settings);
                    var dir = Path.GetDirectoryName(settingsPath);
                    var meshAssetName = $"{_settings.SourceMesh.name}_Separated_{meshGroupId}";
                    var meshAssetPath = Path.Combine(dir, $"{meshAssetName}.asset");
                    var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
                    if (meshAsset == null)
                    {
                        meshAsset = Instantiate(_settings.SourceMesh);
                        meshAsset.name = meshAssetName;
                        AssetDatabase.CreateAsset(meshAsset, meshAssetPath);
                        AssetDatabase.SaveAssetIfDirty(meshAsset);
                        Debug.Log($"Mesh asset created. {meshAssetPath}");
                    }

                    trianglesBuffer1.Clear();
                    CollectMeshGroupTriangles(meshGroupId, trianglesBuffer1);

                    verticesBuffer.Clear();
                    uvsBuffer.Clear();
                    trianglesBuffer2.Clear();
                    OptimizeVertices(sourceVerts, sourceUVs, trianglesBuffer1, verticesBuffer, uvsBuffer, trianglesBuffer2);
                    meshAsset.SetTriangles(trianglesBuffer2, 0);
                    meshAsset.SetVertices(verticesBuffer);
                    meshAsset.SetUVs(0, uvsBuffer);
                    meshAsset.RecalculateBounds();
                    meshAsset.RecalculateNormals();
                    meshAsset.RecalculateTangents();

                    // Save mesh
                    EditorUtility.SetDirty(meshAsset);
                    AssetDatabase.SaveAssetIfDirty(meshAsset);

                    EditorUtility.DisplayProgressBar("Generate separated mesh", "Generating...", 1.0f - (float)meshGroupIds.Count / i);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }
        }

        private void OptimizeVertices(
            IReadOnlyList<Vector3> sourceVerts,
            IReadOnlyList<Vector2> sourceUVs,
            IReadOnlyList<int> sourceTris,
            List<Vector3> resultVerts,
            List<Vector2> resultUVs,
            List<int> resultTris)
        {
            var indexMap = new Dictionary<int, int>();
            for (int i = 0; i < sourceTris.Count; i++)
            {
                var index = sourceTris[i];
                if (indexMap.TryGetValue(index, out var changedIndex))
                {
                    resultTris.Add(changedIndex);
                }
                else
                {
                    var vert = sourceVerts[index];
                    var uv = sourceUVs[index];
                    resultVerts.Add(vert);
                    resultUVs.Add(uv);
                    var insertedIndex = resultVerts.Count - 1;
                    resultTris.Add(insertedIndex);
                    indexMap.Add(index, insertedIndex);
                }
            }
        }
    }
}
