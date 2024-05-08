using System.Collections.Generic;
using MeshPartsSeparation.Analyze;
using UnityEngine;

namespace MeshPartsSeparation
{
    [CreateAssetMenu(fileName = "MeshPartsSeparaterSettings", menuName = "MeshPartsSeparation/Settings")]
    public class MeshPartsSeparaterSettings : ScriptableObject
    {
        [SerializeField]
        public Mesh SourceMesh = null;

        [SerializeField]
        public Material PreviewMaterial = null;

        [SerializeField]
        public MeshGroupAnalyzer.Settings AnalyzerSettings = default;

        [SerializeField]
        public List<GroupResult> GroupResults = default;

        [SerializeField]
        public List<MeshGroupLink> MeshGroupLinks = default;
    }
}

