using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteract : UdonSharpBehaviour
    {
        public string interactText;
        public UdonSharpBehaviour[] listeners;
        [System.NonSerialized] public CustomInteractsAndPickupsManager manager;
        private bool initialized;
        private CustomInteractHighlightPart[] highlightParts;

        private void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            GenerateHighlight();
        }

        public void ShowHighlight()
        {
            Initialize();
            foreach (CustomInteractHighlightPart part in highlightParts)
                part.gameObject.SetActive(true);
        }

        public void HideHighlight()
        {
            Initialize();
            foreach (CustomInteractHighlightPart part in highlightParts)
                part.gameObject.SetActive(false);
        }

        private void GenerateHighlight()
        {
            MeshRenderer[] renderers = this.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            highlightParts = new CustomInteractHighlightPart[renderers.Length];
            int partsCount = 0;
            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null)
                    continue;
                GameObject clone = Instantiate(manager.highlightPartPrefab);
                clone.transform.SetParent(renderer.transform, worldPositionStays: false);
                CustomInteractHighlightPart part = clone.GetComponent<CustomInteractHighlightPart>();
                part.originalMeshFilter = filter;
                part.originalMeshRenderer = renderer;
                part.meshRenderer.enabled = renderer.enabled;
                part.meshFilter.mesh = filter.mesh;
                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                    materials[j] = manager.highlightMat;
                part.meshRenderer.sharedMaterials = materials;
                highlightParts[partsCount++] = part;
            }

            // DataDictionary dict = new DataDictionary();
            // GameObject highlightObj = Instantiate(manager.emptyPrefab);
            // highlight = highlightObj.transform;
            // Transform rootTransform = this.transform;
            // highlight.SetParent(rootTransform, worldPositionStays: false);
            // dict.Add(rootTransform, highlight);
            // MeshRenderer[] renderers = this.GetComponentsInChildren<MeshRenderer>();
            // Transform[] toClone = new Transform[ArrList.MinCapacity];
            // int toCloneCount = 0;
            // foreach (MeshRenderer renderer in renderers)
            // {
            //     Transform current = renderer.transform;
            //     Transform parent = rootTransform;
            //     while (current != rootTransform)
            //     {
            //         if (dict.TryGetValue(current, out DataToken existingCloneToken))
            //         {
            //             parent = (Transform)existingCloneToken.Reference;
            //             break;
            //         }
            //         ArrList.Add(ref toClone, ref toCloneCount, current);
            //     }
            //     for (int i = toCloneCount - 1; i >= 0 ; i--)
            //     {
            //         Transform original = toClone[i];
            //         Transform clone = Instantiate(manager.emptyPrefab, original.position, original.rotation, parent).transform;
            //         clone.localScale = original.localScale;
            //         dict.Add(original, clone);
            //         parent = clone;
            //     }
            //     MeshFilter filterClone = parent.gameObject.AddComponent<MeshFilter>();
            //     MeshRenderer rendererClone = parent.gameObject.AddComponent<MeshRenderer>();
            // }
        }

        public void DispatchOnInteract()
        {
            foreach (UdonSharpBehaviour listener in listeners)
                listener.SendCustomEvent("_interact");
        }
    }
}
