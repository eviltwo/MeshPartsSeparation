using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeshPartsSeparation.Analyze
{
    public class MeshGroupAnalyzer
    {
        [System.Serializable]
        public class Settings
        {
            [SerializeField]
            public bool SeparateByVertexDistance = true;

            [SerializeField]
            public float VertexDistance = 0.01f;
        }

        private readonly Mesh _mesh;
        private readonly Settings _settings;

        public MeshGroupAnalyzer(Mesh mesh, Settings settings)
        {
            _mesh = mesh;
            _settings = settings;
        }

        public void Analyze(List<GroupResult> groupResults)
        {
            groupResults.Clear();

            var vertices = new List<Vector3>();
            _mesh.GetVertices(vertices);
            var uvs = new List<Vector2>();
            _mesh.GetUVs(0, uvs);
            var remainTriangles = new List<int>();
            _mesh.GetTriangles(remainTriangles, 0);
            var maxTriangleCount = remainTriangles.Count;
            try
            {
                while (remainTriangles.Count > 0)
                {
                    var groupResult = new GroupResult();
                    groupResult.Triangles = new List<int>();

                    // Add first triangle
                    {
                        var tri0 = remainTriangles[0];
                        var tri1 = remainTriangles[1];
                        var tri2 = remainTriangles[2];
                        remainTriangles.RemoveRange(0, 3);
                        groupResult.Triangles.Add(tri0);
                        groupResult.Triangles.Add(tri1);
                        groupResult.Triangles.Add(tri2);
                    }

                    var base_i = 0;
                    while (base_i < groupResult.Triangles.Count)
                    {
                        for (var base_j = 0; base_j < 3; base_j++)
                        {
                            var base_tri = groupResult.Triangles[base_i + base_j];
                            var base_v = vertices[base_tri];
                            var rmn_i = 0;
                            while (rmn_i < remainTriangles.Count)
                            {
                                var isGroup = false;
                                for (var rmn_j = 0; rmn_j < 3; rmn_j++)
                                {
                                    var rmn_tri = remainTriangles[rmn_i + rmn_j];
                                    var rmn_v = vertices[rmn_tri];
                                    if (_settings.SeparateByVertexDistance && IsGroupVertex(base_v, rmn_v, _settings.VertexDistance))
                                    {
                                        isGroup = true;
                                        break;
                                    }
                                }
                                if (isGroup)
                                {
                                    var tri0 = remainTriangles[rmn_i];
                                    var tri1 = remainTriangles[rmn_i + 1];
                                    var tri2 = remainTriangles[rmn_i + 2];
                                    remainTriangles.RemoveRange(rmn_i, 3);
                                    groupResult.Triangles.Add(tri0);
                                    groupResult.Triangles.Add(tri1);
                                    groupResult.Triangles.Add(tri2);
                                }
                                else
                                {
                                    rmn_i += 3;
                                }
                            }
                        }

                        base_i += 3;
                    }

                    groupResults.Add(groupResult);

                    EditorUtility.DisplayProgressBar("Analyze", "Analyzing...", 1.0f - (float)remainTriangles.Count / maxTriangleCount);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool IsGroupVertex(Vector3 v1, Vector3 v2, float threshold)
        {
            return Vector3.SqrMagnitude(v1 - v2) < threshold * threshold;
        }
    }
}
