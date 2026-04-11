using JanSharp.Internal;
using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    public abstract class CustomInteractableBase : UdonSharpBehaviour
    {
        [Tooltip("Imagine a sphere tangential to the palm with this diameter.\n"
            + "To support tiny and huge avatars, the actual range is "
            + "'VR Reach * Clamp(EyeHeight / 2, 0.25, 4)'.")]
        [Range(0f, 1f)]
        public float vRReach = 0.1f; // Stupid capitalization to make it display as VR Reach in the inspector.
        [Tooltip("Imagine a laser from the center of the screen.\n"
            + "To support tiny and huge avatars, the actual range is "
            + "'DesktopReach * Clamp(EyeHeight / 2, 0.25, 4)'.")]
        [Range(0f, 25f)]
        public float desktopReach = 2.5f;
        public string interactText;
        [HideInInspector][SingletonReference] public CustomInteractablesManager manager;
        protected bool initialized;
        protected GameObject[] highlightParts;
        private int shownCount = 0;
        private uint preventInteraction = 0u;

        public bool PreventInteraction => preventInteraction != 0u;
        public void IncrementPreventInteraction() => preventInteraction++;
        public void DecrementPreventInteraction() => preventInteraction--;

        public abstract bool CanInteract();

        protected void EnsureHasManagerRef()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  EnsureHasManagerRef");
#endif
            if (manager != null) // Null if this object got instantiated where a prefab was passed to the instantiate call.
                return;
            manager = SingletonsUtil.GetSingleton<CustomInteractablesManager>(nameof(CustomInteractablesManager));
        }

        private void Initialize()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  Initialize");
#endif
            if (initialized)
                return;
            initialized = true;
            GenerateHighlight();
        }

        public void ShowHighlight()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  ShowHighlight - shownCount: {shownCount}");
#endif
            if ((++shownCount) != 1)
                return;
            ActivateHighlight();
        }

        private void ActivateHighlight()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  ActivateHighlight");
#endif
            Initialize();
            foreach (GameObject part in highlightParts)
                if (part != null)
                    part.SetActive(true);
        }

        public void HideHighlight()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  HideHighlight - shownCount: {shownCount}");
#endif
            if ((--shownCount) != 0)
                return;
            foreach (GameObject part in highlightParts)
                if (part != null)
                    part.SetActive(false);
        }

        public void InvalidateHighlight()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  InvalidateHighlight - shownCount: {shownCount}");
#endif
            if (!initialized)
                return;
            foreach (GameObject part in highlightParts)
                if (part != null)
                    Destroy(part);
            highlightParts = null;
            initialized = false;
            // In case the highlight is currently being shown.
            if (shownCount > 0)
                ActivateHighlight();
        }

        protected void GenerateHighlight()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] InteractableBase {this.name}  GenerateHighlight");
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            MeshRenderer[] renderers = this.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            highlightParts = new GameObject[renderers.Length];
            int partsCount = 0;
            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || renderer.GetComponent<ExcludeFromInteractableHighlight>() != null)
                    continue;
                GameObject part = Instantiate(manager.highlightPartPrefab);
                part.transform.SetParent(renderer.transform, worldPositionStays: false);
                MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
                partRenderer.enabled = renderer.enabled;
                part.GetComponent<MeshFilter>().mesh = filter.mesh;
                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                    materials[j] = manager.highlightMat;
                partRenderer.sharedMaterials = materials;
                highlightParts[partsCount++] = part;
            }
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] [sw] InteractableBase {this.name}  GenerateHighlight (inner) - ms: {sw.Elapsed.TotalMilliseconds}, partsCount: {partsCount}");
#endif
        }
    }
}
