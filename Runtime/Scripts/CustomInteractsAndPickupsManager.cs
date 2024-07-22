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
        public LayerMask pickupLayer;
        public GameObject highlightPartPrefab;
        public Transform highlightTextRoot;
        public Transform highlightTextTransform;
        public TextMeshPro highlightText;

        private VRCPlayerApi localPlayer;
        private CustomInteract interact;

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
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 headPosition = head.position;
            Quaternion headRotation = head.rotation;
            Vector3 headForward = headRotation * Vector3.forward;
            if (!Physics.Raycast(headPosition, headForward, out RaycastHit hit, 100f, pickupLayer, QueryTriggerInteraction.Collide))
                return;
            interact = hit.transform.GetComponentInParent<CustomInteract>();
            if (interact == null)
                return;
            if (interact.interactText != "")
            {
                highlightText.text = interact.interactText;
                highlightTextRoot.position = interact.transform.position;
                highlightTextRoot.rotation = headRotation;
                highlightTextTransform.localScale = Vector3.one * Vector3.Distance(headPosition, interact.transform.position);
                highlightTextRoot.gameObject.SetActive(true);
            }
            interact.manager = this;
            interact.ShowHighlight();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || interact == null)
                return;
            interact.DispatchOnInteract();
        }
    }
}
