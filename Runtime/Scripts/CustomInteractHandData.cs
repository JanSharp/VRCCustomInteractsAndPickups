using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using TMPro;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractHandData : UdonSharpBehaviour
    {
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType trackingHandType;
        [System.NonSerialized] public HandType handType;
        [System.NonSerialized] public Quaternion rotationNormalization;
        [System.NonSerialized] public Vector3 offsetVectorShift;
        [System.NonSerialized] public CustomInteractsAndPickupsManager manager;

        public Transform highlightTextRoot;
        public Transform highlightTextTransform;
        public TextMeshPro highlightText;

        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 handForward;

        private bool hasActiveInteract;
        private bool hasActivePickup;
        private CustomInteract activeInteract;
        private CustomPickup activePickup;
        private CustomInteractiveBase activeScript;
        private Transform activeTransform;
        private Vector3 hitPoint;

        private bool isHolding;
        private Vector3 heldOffsetVector;
        private Quaternion heldOffsetRotation;

        private int interactLayerNumber = 8;
        private LayerMask interactLayer = (LayerMask)(1 << 8);
        private LayerMask pickupLayer = (LayerMask)(1 << 13);
        private const float HighlightTextScale = 0.5f;
        // TODO: adjust based on feedback, also update CustomInteractBase proximity tooltip.
        private const float RaycastProximityMultiplierVR = 5f;
        private const float RaycastProximityMultiplierDesktop = 5f;

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;
        private float raycastProximityMultiplier = RaycastProximityMultiplierVR;
        [System.NonSerialized] public float eyeHeightScale = 1f;

        public void Initialize()
        {
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            raycastProximityMultiplier = isInVR ? RaycastProximityMultiplierVR : RaycastProximityMultiplierDesktop;
        }

        public void SetEyeHeightScale(float eyeHeightScale)
        {
            if (this.eyeHeightScale == eyeHeightScale)
                return;
            this.eyeHeightScale = eyeHeightScale;
        }

        public void UpdateHand()
        {
            if (isHolding)
            {
                UpdateHeldPickup();
                return;
            }

            FetchHeadValues(); // TODO: fix

            CustomInteractiveBase newActiveScript;
            bool isInteract;
            if (isInVR)
            {
                newActiveScript = TryGetNearInteractive(out isInteract);
                if (newActiveScript == activeScript)
                {
                    UpdateHighlightText();
                    return;
                }

                if (newActiveScript != null)
                {
                    if (isInteract)
                        SetActiveInteract((CustomInteract)newActiveScript);
                    else
                        SetActivePickup((CustomPickup)newActiveScript);
                }
            }

            newActiveScript = TryGetInteractive(out isInteract);
            if (newActiveScript == activeScript)
            {
                UpdateHighlightText();
                return;
            }

            if (isInteract)
                SetActiveInteract((CustomInteract)newActiveScript);
            else
                SetActivePickup((CustomPickup)newActiveScript);
        }

        private void UpdateHeldPickup()
        {
            FetchHeadValues();
            activeTransform.position = handPosition + handRotation * heldOffsetVector;
            activeTransform.rotation = handRotation * heldOffsetRotation;
        }

        private void FetchHeadValues()
        {
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            handPosition = head.position;
            handRotation = head.rotation;
            handForward = handRotation * Vector3.forward;
        }

        private void FetchHandValues(CustomInteractHandData data)
        {
            VRCPlayerApi.TrackingData hand = localPlayer.GetTrackingData(data.trackingHandType);
            handPosition = hand.position;
            handRotation = hand.rotation * data.rotationNormalization;
            handForward = handRotation * Vector3.forward;
        }

        private CustomInteractiveBase TryGetInteractive(out bool isInteract)
        {
            float maxDistance = 10f // Max proximity.
                * ((5f - 2f) / 2f + 1f) // Max eyeHeightScale.
                * raycastProximityMultiplier;

            isInteract = false;
            if (!Physics.Raycast(handPosition, handForward, out RaycastHit hit, maxDistance, interactLayer | pickupLayer, QueryTriggerInteraction.Collide))
                return null;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return null; // even though in normal Unity if we have a hit... this is not possible to be null.
            isInteract = hitTransform.gameObject.layer == interactLayerNumber;
            CustomInteractiveBase interactive = isInteract
                ? (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomInteract>()
                : (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomPickup>();
            if (interactive == null)
                return null;
            hitPoint = hit.point;
            if (Vector3.Distance(handPosition, hitPoint) > interactive.proximity * eyeHeightScale * raycastProximityMultiplier)
                return null;
            return interactive;
        }

        private CustomInteractiveBase TryGetNearInteractive(out bool isInteract)
        {
            float maxRadius = 10f // Max proximity.
                * ((5f - 2f) / 2f + 1f); // Max eyeHeightScale.

            bool closestIsInteract = false;
            CustomInteractiveBase closestInteractive = null;
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = Vector3.zero;

            Collider[] colliders = Physics.OverlapSphere(handPosition, maxRadius, interactLayer | pickupLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in colliders)
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity if we have a hit... this is not possible to be null.
                Transform hitTransform = collider.transform;
                bool currentIsInteract = hitTransform.gameObject.layer == interactLayerNumber;
                CustomInteractiveBase interactive = currentIsInteract
                    ? (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomInteract>()
                    : (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomPickup>();
                if (interactive == null)
                    continue;
                Vector3 closestPoint = collider.ClosestPoint(handPosition);
                float distance = Vector3.Distance(handPosition, closestPoint);
                if (distance > interactive.proximity * eyeHeightScale)
                    continue;
                if (distance >= closestDistance)
                    continue;
                closestIsInteract = currentIsInteract;
                closestInteractive = interactive;
                closestDistance = distance;
                closestHitPoint = closestPoint;
            }

            isInteract = closestIsInteract;
            hitPoint = closestHitPoint;
            return closestInteractive;
        }

        private void ClearActiveScript()
        {
            if (activeScript == null)
                return;
            activeScript.HideHighlight();
            HideHighlightText();
            hasActiveInteract = false;
            hasActivePickup = false;
            activeInteract = null;
            activePickup = null;
            activeScript = null;
            activeTransform = null;
        }

        private void SetActiveInteract(CustomInteract newInteract)
        {
            ClearActiveScript();
            if (newInteract == null)
                return;
            hasActiveInteract = true;
            activeInteract = newInteract;
            SetActiveScriptGeneric(newInteract);
        }

        private void SetActivePickup(CustomPickup newPickup)
        {
            ClearActiveScript();
            if (newPickup == null)
                return;
            hasActivePickup = true;
            activePickup = newPickup;
            SetActiveScriptGeneric(newPickup);
        }

        private void SetActiveScriptGeneric(CustomInteractiveBase newActiveScript)
        {
            activeScript = newActiveScript;
            activeTransform = activeScript.transform;
            activeScript.manager = manager;
            activeScript.ShowHighlight();
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
            if (activeScript == null)
                return;
            highlightText.text = activeScript.interactText;
            // TODO: Maybe calculate the total bounds of all renderers and use the center of the bounds instead.
            Vector3 interactPosition = activeTransform.position;
            highlightTextRoot.position = interactPosition;
            highlightTextRoot.rotation = handRotation;
            float scale = Vector3.Distance(handPosition, interactPosition) * HighlightTextScale;
            highlightTextTransform.localScale = Vector3.one * scale;
        }

        private float lastInputUse = -1;
        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if ((isInVR && args.handType != handType) || !value || !hasActiveInteract || lastInputUse == Time.time)
                return;
            // Ignore multiple InputUse events in the same frame... because for some unexplainable reason
            // VRChat is raising the InputUse event twice when I click the mouse button once.
            lastInputUse = Time.time;
            activeInteract.DispatchOnInteract();
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if ((isInVR && args.handType != handType) || !hasActivePickup)
                return;
            if (!value && isHolding && !activePickup.autoHold)
            {
                DropActivePickup();
                return;
            }
            if (!value || isHolding)
                return;

            isHolding = true;

            Transform exactGrip = activePickup.exactGrip;
            if (exactGrip == null)
            {
                // Desktop, move to hand.
                Quaternion inverseHandRotation = Quaternion.Inverse(handRotation);
                Vector3 distanceFromHead = inverseHandRotation * (hitPoint - handPosition);
                heldOffsetVector = inverseHandRotation * (activeTransform.position - handPosition);
                heldOffsetVector = heldOffsetVector - distanceFromHead + offsetVectorShift;
                heldOffsetRotation = inverseHandRotation * activeTransform.rotation;
            }
            else
            {
                // Desktop, exact grip.
                Quaternion activeRotation = activeTransform.rotation;
                Vector3 offsetVector = Quaternion.Inverse(activeRotation) * (activeTransform.position - exactGrip.position);
                heldOffsetRotation = Quaternion.Inverse(exactGrip.rotation) * activeRotation;
                heldOffsetVector = heldOffsetRotation * offsetVector + offsetVectorShift;
            }

            activePickup.HideHighlight();
            HideHighlightText();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            if ((isInVR && args.handType != handType) || value || !isHolding)
                return;
            // Dropped on InputDropUp, matching VRCHat's behaviour.
            DropActivePickup();
        }

        private void DropActivePickup()
        {
            isHolding = false;
            ClearActiveScript();
        }
    }
}
