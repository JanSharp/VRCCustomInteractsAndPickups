using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractHighlightPart : UdonSharpBehaviour
    {
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        [System.NonSerialized] public MeshFilter originalMeshFilter;
        [System.NonSerialized] public MeshRenderer originalMeshRenderer;
    }
}
