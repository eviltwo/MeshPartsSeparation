using System.Collections.Generic;
using UnityEngine;

namespace MeshPartsSeparation.Analyze
{
    [System.Serializable]
    public class GroupResult
    {
        [SerializeField]
        public List<int> Triangles = default;
    }
}
