using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using TMPro;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractHandManager : UdonSharpBehaviour
    {
        [System.NonSerialized] public VRCPlayerApi.TrackingDataType trackingHandType;
        [System.NonSerialized] public HandType handType;
        [System.NonSerialized] public Quaternion rotationNormalization;
        [System.NonSerialized] public Vector3 offsetVectorShift;
        [System.NonSerialized] public CustomInteractsAndPickupsManager manager;

        public Transform interactTextRoot;
        public Transform interactTextTransform;
        public TextMeshPro interactTextElem;
        [Space]
        public Transform useTextRoot;
        public Transform useTextTransform;
        public TextMeshPro useTextElem;
        [Space]
        public GameObject useTextRootDesktop;
        public TextMeshProUGUI useTextElemDesktop;

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
        private float pickedUpAt = -1;
        private Vector3 heldOffsetVector;
        private Quaternion heldOffsetRotation;
        private bool isHoldingUseButton;

        private int interactLayerNumber = 8;
        private LayerMask interactLayer = (LayerMask)(1 << 8);
        private LayerMask pickupLayer = (LayerMask)(1 << 13);
        private const float InteractAndUseTextScale = 0.5f;
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

            FetchHandValues();

            CustomInteractiveBase newActiveScript;
            bool isInteract;
            if (isInVR)
            {
                newActiveScript = TryGetNearInteractive(out isInteract);
                if (newActiveScript == activeScript)
                {
                    UpdateInteractText();
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
                UpdateInteractText();
                return;
            }

            if (isInteract)
                SetActiveInteract((CustomInteract)newActiveScript);
            else
                SetActivePickup((CustomPickup)newActiveScript);
        }

        private void UpdateHeldPickup()
        {
            FetchHandValues();
            activeTransform.position = handPosition + handRotation * heldOffsetVector;
            activeTransform.rotation = handRotation * heldOffsetRotation;
            UpdateUseText();
        }

        private void FetchHandValues()
        {
            VRCPlayerApi.TrackingData hand = localPlayer.GetTrackingData(trackingHandType);
            handPosition = hand.position;
            handRotation = hand.rotation * rotationNormalization;
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
            HideInteractText();
            hasActiveInteract = false;
            hasActivePickup = false;
            activeInteract = null;
            activePickup = null;
            activeScript = null;
            activeTransform = null;
            UpdateUseText();
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
            UpdateInteractText();
            ShowInteractText();
        }

        private void ShowInteractText()
        {
            interactTextRoot.gameObject.SetActive(true);
        }

        private void HideInteractText()
        {
            interactTextRoot.gameObject.SetActive(false);
        }

        private void UpdateInteractText()
        {
            if (activeScript == null)
                return;
            interactTextElem.text = activeScript.interactText;
            // TODO: Maybe calculate the total bounds of all renderers and use the center of the bounds instead.
            Vector3 interactPosition = activeTransform.position;
            interactTextRoot.position = interactPosition;
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            interactTextRoot.rotation = head.rotation;
            float scale = Vector3.Distance(head.position, interactPosition) * InteractAndUseTextScale;
            interactTextTransform.localScale = Vector3.one * scale;
        }

        private void UpdateUseText()
        {
            if (!isHolding)
            {
                if (!isInVR)
                    useTextRootDesktop.SetActive(false);
                return;
            }

            string useText = activePickup.useText;
            if (!isInVR)
            {
                bool hasText = useText != "";
                useTextRootDesktop.SetActive(hasText);
                if (!hasText)
                    return;
                useTextElemDesktop.text = useText;
                return;
            }

            useTextElem.text = useText;
            // TODO: Maybe calculate the total bounds of all renderers and use the center of the bounds instead.
            Vector3 pickupPosition = activeTransform.position;
            useTextRoot.position = pickupPosition;
            VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            useTextRoot.rotation = head.rotation;
            float scale = Vector3.Distance(head.position, pickupPosition) * InteractAndUseTextScale;
            useTextTransform.localScale = Vector3.one * scale;
        }

        private float lastInputUse = -1;
        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if ((isInVR && args.handType != handType) || lastInputUse == Time.time)
                return;
            // Ignore multiple InputUse events in the same frame... because for some unexplainable reason
            // VRChat is raising the InputUse event twice when I click the mouse button once.
            lastInputUse = Time.time;
            if (hasActiveInteract)
            {
                if (value)
                    activeInteract.DispatchOnInteract();
            }
            if (hasActivePickup && isHolding && Time.time != pickedUpAt)
            {
                if (value)
                {
                    if (!isHoldingUseButton)
                    {
                        isHoldingUseButton = true;
                        activePickup.DispatchOnPickupUseDown();
                    }
                }
                else
                {
                    if (isHoldingUseButton)
                    {
                        isHoldingUseButton = false;
                        activePickup.DispatchOnPickupUseUp();
                    }
                }
            }
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
            PickupActivePickup();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            if ((isInVR && args.handType != handType) || value || !isHolding)
                return;
            // Dropped on InputDropUp, matching VRCHat's behaviour.
            DropActivePickup();
        }

        private void PickupActivePickup()
        {
            isHolding = true;
            pickedUpAt = Time.time;

            Transform exactGrip = activePickup.exactGrip;
            if (exactGrip == null)
            {
                // Move to hand.
                // TODO: add interpolation
                Quaternion inverseHandRotation = Quaternion.Inverse(handRotation);
                Vector3 distanceFromHead = inverseHandRotation * (hitPoint - handPosition);
                heldOffsetVector = inverseHandRotation * (activeTransform.position - handPosition);
                heldOffsetVector = heldOffsetVector - distanceFromHead + offsetVectorShift;
                heldOffsetRotation = inverseHandRotation * activeTransform.rotation;
            }
            else
            {
                // Exact grip.
                Quaternion activeRotation = activeTransform.rotation;
                Vector3 offsetVector = Quaternion.Inverse(activeRotation) * (activeTransform.position - exactGrip.position);
                heldOffsetRotation = Quaternion.Inverse(exactGrip.rotation) * activeRotation;
                heldOffsetVector = heldOffsetRotation * offsetVector + offsetVectorShift;
            }

            activePickup.HideHighlight();
            HideInteractText();
            UpdateUseText();

            activePickup.isHeld = true;
            activePickup.heldTrackingData = trackingHandType;
            activePickup.heldOffsetVector = heldOffsetVector;
            activePickup.heldOffsetRotation = heldOffsetRotation;
            activePickup.DispatchOnPickup();
        }

        private void DropActivePickup()
        {
            isHolding = false;
            CustomPickup prevActivePickup = activePickup;
            ClearActiveScript();
            if (isHoldingUseButton)
            {
                isHoldingUseButton = false;
                prevActivePickup.DispatchOnPickupUseUp();
                // Once the api has been implemented the state of this script could have changed completely...
                // and this function should therefore probably be marked as recursive, because it could be called
                // recursively... but so could every calling function so uhhhhh idk typical Udon moment I guess.
            }
            prevActivePickup.isHeld = false;
            prevActivePickup.DispatchOnDrop();
        }
    }
}
