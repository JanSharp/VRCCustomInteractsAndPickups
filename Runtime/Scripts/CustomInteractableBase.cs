using JanSharp.Internal;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JanSharp
{
    public abstract class CustomInteractableBase : UdonSharpBehaviour
    {
        [Tooltip("Imagine a sphere centered on the palm with this radius.\n"
            + "To support tiny and huge avatars, the actual range is "
            + "'ProximityReach * Clamp(EyeHeight / 2, 0.25, 4)'.")]
        [Range(0f, 1f)]
        public float proximityReach = 0.05f;
        [Tooltip("Imagine a laser from the center of the palm following the direction of finger guns.\n"
            + "To support tiny and huge avatars, the actual range is "
            + "'PointerReach * Clamp(EyeHeight / 2, 0.25, 4)'.")]
        [Range(0f, 25f)]
        public float pointerReach = 0.3f;
        [Tooltip("Imagine a laser from the center of the screen.\n"
            + "To support tiny and huge avatars, the actual range is "
            + "'DesktopReach * Clamp(EyeHeight / 2, 0.25, 4)'.")]
        [Range(0f, 25f)]
        public float desktopReach = 2.5f;
        public string interactText;
        [System.NonSerialized] public CustomInteractablesManager manager;
        protected bool initialized;
        protected CustomInteractHighlightPart[] highlightParts;
        private int shownCount = 0;
        private uint preventInteraction = 0u;

        public bool PreventInteraction => preventInteraction != 0u;
        public void IncrementPreventInteraction() => preventInteraction++;
        public void DecrementPreventInteraction() => preventInteraction--;

        public abstract bool CanInteract();

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
