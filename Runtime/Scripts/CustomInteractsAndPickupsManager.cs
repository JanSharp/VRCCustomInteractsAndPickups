using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using TMPro;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractsAndPickupsManager : UdonSharpBehaviour
    {
        public Material highlightMat;
        public GameObject highlightPartPrefab;
        public Transform highlightTextRoot;
        public Transform highlightTextTransform;
        public TextMeshPro highlightText;

        private LayerMask interactLayer = (LayerMask)(1 << 8);
        private LayerMask pickupLayer = (LayerMask)(1 << 13);

        private VRCPlayerApi localPlayer;
        private VRCPlayerApi.TrackingData head;
        private Vector3 headPosition;
        private Quaternion headRotation;
        private Vector3 headForward;
        private CustomInteract interact;
        private Transform interactTransform;

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
        }

        private void Update()
        {
            if (interact != null)
            {
                interact.HideHighlight();
                interact = null;
                highlightTextRoot.gameObject.SetActive(false);
            }
            FetchHeadValues();
            CustomInteract newInteract = TryGetInteract();
            if (newInteract == interact)
            {
                UpdateHighlightText();
                return;
            }
            SetInteract(newInteract);
        }

        private void FetchHeadValues()
        {
            head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            headPosition = head.position;
            headRotation = head.rotation;
            headForward = headRotation * Vector3.forward;
        }

        private CustomInteract TryGetInteract()
        {
            if (!Physics.Raycast(headPosition, headForward, out RaycastHit hit, 100f, interactLayer, QueryTriggerInteraction.Collide))
                return null;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return null; // even though in normal Unity if we have a hit... this is not possible to be null.
            return hitTransform.GetComponentInParent<CustomInteract>();
        }

        private void SetInteract(CustomInteract newInteract)
        {
            if (newInteract == null)
            {
                if (interact == null)
                    return;
                interact.HideHighlight();
                interact = null;
                interactTransform = null;
                HideHighlightText();
                return;
            }
            interact = newInteract;
            interactTransform = newInteract.transform;
            interact.manager = this;
            interact.ShowHighlight();
            UpdateHighlightText();
            ShowHighlightText();
        }

        private void ShowHighlightText()
        {
            highlightTextRoot.gameObject.SetActive(true);
        }

        private void HideHighlightText()
        {
            highlightTextRoot.gameObject.SetActive(false);
        }

        private void UpdateHighlightText()
        {
            if (interact == null)
                return;
            highlightText.text = interact.interactText;
            // TODO: Maybe calculate the total bounds of all renderers and use the center of the bounds instead.
            Vector3 interactPosition = interactTransform.position;
            highlightTextRoot.position = interactPosition;
            highlightTextRoot.rotation = headRotation;
            highlightTextTransform.localScale = Vector3.one * Vector3.Distance(headPosition, interactPosition);
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || interact == null)
                return;
            interact.DispatchOnInteract();
        }
    }
}
