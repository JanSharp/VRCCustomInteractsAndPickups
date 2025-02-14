using JanSharp.Internal;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    public abstract class CustomInteractableBase : UdonSharpBehaviour
    {
        [Tooltip("At an eye height of 2 meters, this proximity defines the exact distance from hands to "
            + "objects at which point they are within reach. Imagine a sphere around the player hands with "
            + "a radius of the defined Proximity value. Scaling eye height up or down affects proximity 2 to "
            + "1, which is to say being 2x tall only multiplies proximity by 1.5x.\n"
            + "Objects can also be reachable when pointing at them. Imagine finger guns, the index finger is "
            + "the pointing direction. When pointing at objects, the distance at which they are reachable "
            + "is 5x the Proximity while in VR, 5x on desktop.")]
        [Range(0, 10)]
        public float proximity = 0.5f;
        public string interactText;
        [System.NonSerialized] public CustomInteractablesManager manager;
        protected bool initialized;
        protected CustomInteractHighlightPart[] highlightParts;
        private int shownCount = 0;

        protected void EnsureHasManagerRef()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  EnsureHasManagerRef");
            #endif
            if (manager != null)
                return;
            manager = SingletonsUtil.GetSingleton<CustomInteractablesManager>(nameof(CustomInteractablesManager));
        }

        private void Initialize()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  Initialize");
            #endif
            if (initialized)
                return;
            initialized = true;
            GenerateHighlight();
        }

        public void ShowHighlight()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  ShowHighlight - shownCount: {shownCount}");
            #endif
            if ((++shownCount) != 1)
                return;
            Initialize();
            foreach (CustomInteractHighlightPart part in highlightParts)
                part.gameObject.SetActive(true);
        }

        public void HideHighlight()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  HideHighlight - shownCount: {shownCount}");
            #endif
            if ((--shownCount) != 0)
                return;
            Initialize();
            foreach (CustomInteractHighlightPart part in highlightParts)
                part.gameObject.SetActive(false);
        }

        protected void GenerateHighlight()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  GenerateHighlight");
            #endif
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
        }
    }
}
