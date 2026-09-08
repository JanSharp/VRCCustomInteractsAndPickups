using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomInteractHandManager : UdonSharpBehaviour
    {
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
        [HideInInspector][SerializeField][SingletonReference] private QuickDebugUI qd;
#endif
        public CustomInteractablesManager manager;
        public CustomInteractHandManager otherHandManager;
        public CustomAttachedPickupsManager attachedManager;
        public CustomPickupPickingUpState pickingUpStateForController;
        public CustomPickupState stateForController;
        public CustomPickupController fallbackPickupController;

        [System.NonSerialized] public VRCPlayerApi.TrackingDataType handTrackingType;
        [System.NonSerialized] public VRC_Pickup.PickupHand pickupHandType;
        [System.NonSerialized] public HandType handType;
        [System.NonSerialized] public DroppingHandType droppingHandType;
        [System.NonSerialized] public Quaternion rotationNormalization;
        [System.NonSerialized] public Vector3 offsetVectorShift;
        [System.NonSerialized] public Vector3 palmDirection;
        [System.NonSerialized] public Vector3 coneDirection;

        // DEBUG
        [SerializeField] private Transform debugSphere;
        [SerializeField] private Transform debugLine;

        public Transform interactTextRoot;
        public Transform interactTextTransform;
        public TextMeshPro interactTextElem;
        [Space]
        public Transform useTextRoot;
        public Transform useTextTransform;
        public TextMeshPro useTextElem;
        [Space]
        public GameObject textRootDesktop;
        public TextMeshProUGUI interactTextElemDesktop;
        public TextMeshProUGUI useTextElemDesktop;

        private bool hasActiveInteract;
        private bool hasActivePickup;
        private bool isControllingActivePickup;
        private CustomInteract activeInteract;
        [System.NonSerialized] public CustomPickup activePickup;
        private CustomInteractableBase activeScript;
        private Transform activeTransform;
        private Vector3 hitPoint;
        private VRCPlayerApi.TrackingData trackingDataForHitPoint;
        private Vector3 interactablePositionForHitPoint;
        private Quaternion interactableRotationForHitPoint;

        private CustomInteractableBase lastHapticsState = null;
        private bool lastHapticsHoldingState = false;
        private bool pickupWasAttachedForHaptics = false;
        private bool dropResultedInAttachForHaptics = false;

        private bool isHolding;
        private bool isAutoHolding;
        private bool isPrimaryHoldingHand;
        private float pickedUpAt = -1;
        private Vector3 heldOffsetVector;
        private Quaternion heldOffsetRotation;
        private bool isHoldingUseButton;

        private float lastInputUseEventTime = -1f;
        private float lastInputUseDownTime = -1f;
        private float inputGrabDownAt = -1f;
        private float useConeModeUntilTime = -1f;
        /// <summary>
        /// <para>Always <see langword="true"/> for desktop.</para>
        /// <para>Set to <see langword="true"/> for VR if and when we get a
        /// <see cref="InputDrop(bool, UdonInputEventArgs)"/> event.</para>
        /// </summary>
        private bool hasDropKeyBind = false;
        private const float MaxClickDurationSecondsDesktop = 0.2f;
        private const float MaxClickDurationSecondsVR = 0.4f; // The grab motion is less common and slower than a button click.
        private float maxClickDurationSeconds;
        [System.NonSerialized] public CustomPickupsAutoHoldMode autoHoldMode;
        private const float SimultaneousInputSeconds = 0.2f;
        private const float ConeModeDurationSeconds = 2f;

        private int interactLayerNumber;
        private int pickupLayerNumber;
        private LayerMask interactLayer;
        private LayerMask pickupLayer;
        private const float InteractAndUseTextScale = 0.5f;

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;
        private float eyeHeightScale = 1f;

#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
        private System.Diagnostics.Stopwatch updateSw = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch fixedUpdateSw = new System.Diagnostics.Stopwatch();
        private object[] updateContainer;
        private object[] fixedUpdateContainer;
#endif

        public void Initialize()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  Initialize");
#endif
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            hasDropKeyBind = !isInVR;
            maxClickDurationSeconds = isInVR ? MaxClickDurationSecondsVR : MaxClickDurationSecondsDesktop;
            interactLayerNumber = manager.InteractLayerNumber;
            pickupLayerNumber = manager.PickupLayerNumber;
            interactLayer = manager.InteractLayer;
            pickupLayer = manager.PickupLayer;
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            updateContainer = StopwatchUtil.CreateDataContainer();
            fixedUpdateContainer = StopwatchUtil.CreateDataContainer();
#endif
        }

        public void SetEyeHeightScale(float eyeHeightScale)
        {
            this.eyeHeightScale = eyeHeightScale;
            useTextTransform.localScale = Vector3.one * eyeHeightScale * InteractAndUseTextScale;
            interactTextTransform.localScale = Vector3.one * eyeHeightScale * InteractAndUseTextScale;
        }

        public void UpdateHand()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            qd.ShowForOneFrame(this, "Fixed Update MS", StopwatchUtil.FormatAvgMinMax(fixedUpdateSw, fixedUpdateContainer));
            fixedUpdateSw.Reset();
            updateSw.Reset();
            updateSw.Start();
#endif
            if (activeScript != null)
            {
                if (isControllingActivePickup)
                    UpdateControlledPickup();
                if (isHolding)
                    UpdateUseText();
                else
                    UpdateInteractText();
            }
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            updateSw.Stop();
            qd.ShowForOneFrame(this, "Update MS", StopwatchUtil.FormatAvgMinMax(updateSw, updateContainer));
#endif
        }

        public void FixedUpdateHand()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugSphere.gameObject.SetActive(false);
            debugLine.gameObject.SetActive(false);
#endif
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            fixedUpdateSw.Start();
#endif
            if (isHolding)
            {
                if (activeScript != null)
                {
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
                    fixedUpdateSw.Stop();
#endif
                    return;
                }
                DropActivePickup(preventAttachment: true);
            }
            else if ((hasActiveInteract || hasActivePickup) && activeScript == null)
                ClearActiveScriptVariables();

            bool isInteract;
            CustomInteractableBase script;
            if (!isInVR)
                script = TryGetInteractable(out isInteract);
            else if (Time.time <= useConeModeUntilTime)
                script = TryGetInteractableInCone(includeInteracts: false, out isInteract);
            else
                script = TryGetNearInteractable(out isInteract);

            if (script == null)
                ClearActiveScript();
            else if (script != activeScript)
            {
                if (isInteract)
                    SetActiveInteract((CustomInteract)script);
                else
                    SetActivePickup((CustomPickup)script);
            }

#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            fixedUpdateSw.Stop();
#endif
        }

        private void PlayHaptics(string variableName)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PlayHaptics - variableName: {variableName}");
#endif
            Vector3 config = (Vector3)manager.GetProgramVariable(variableName);
            localPlayer.PlayHapticEventInHand(pickupHandType, config.x, config.y, config.z);
        }

        /// <summary>
        /// <para>Update haptics 1 frame delayed in order to deduplicate haptics calls, as well as to be able
        /// to detect selection changes rather than having 2 separate calls. One for selection lost and one
        /// for selection gained.</para>
        /// </summary>
        public void UpdateHaptics()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  UpdateHaptics");
#endif
            if (lastHapticsHoldingState)
            {
                lastHapticsState = activeScript;
                if (isHolding)
                    return;
                if (dropResultedInAttachForHaptics)
                    PlayHaptics(nameof(manager.onDropAndAttachHaptics));
                else
                    PlayHaptics(nameof(manager.onDropHaptics));
                lastHapticsHoldingState = false;
                return;
            }
            if (isHolding)
            {
                lastHapticsState = activeScript;
                if (pickupWasAttachedForHaptics)
                    PlayHaptics(nameof(manager.onPickupAndDetachHaptics));
                else
                    PlayHaptics(nameof(manager.onPickupHaptics));
                lastHapticsHoldingState = true;
                return;
            }
            if (lastHapticsState == activeScript)
                return;
            if (lastHapticsState == null)
            {
                PlayHaptics(nameof(manager.onSelectionGainedHaptics));
                lastHapticsState = activeScript;
                return;
            }
            if (activeScript == null)
            {
                PlayHaptics(nameof(manager.onSelectionLostHaptics));
                lastHapticsState = null;
                return;
            }
            PlayHaptics(nameof(manager.onSelectionChangedHaptics));
            lastHapticsState = activeScript;
        }

        private CustomInteractableBase TryGetInteractable(out bool isInteract)
        {
            float maxDistance = 25f * eyeHeightScale;

            trackingDataForHitPoint = localPlayer.GetTrackingData(handTrackingType);
            Vector3 raycastOrigin = trackingDataForHitPoint.position;
            Vector3 raycastForward = trackingDataForHitPoint.rotation * rotationNormalization * Vector3.forward;

#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.gameObject.SetActive(true);
            debugLine.position = raycastOrigin;
            debugLine.rotation = Quaternion.LookRotation(raycastForward);
            debugLine.localScale = new Vector3(1f, 1f, maxDistance);
#endif

            isInteract = false;
            if (!Physics.Raycast(raycastOrigin, raycastForward, out RaycastHit hit, maxDistance, interactLayer | pickupLayer, QueryTriggerInteraction.Collide))
                return null;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return null; // even though in normal Unity if we have a hit... this is not possible to be null.
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.localScale = new Vector3(1f, 1f, Vector3.Distance(raycastOrigin, hit.point));
#endif
            isInteract = hitTransform.gameObject.layer == interactLayerNumber;
            CustomInteractableBase interactable = isInteract
                ? (CustomInteractableBase)hitTransform.GetComponentInParent<CustomInteract>() // Does not need to include inactive, as the child is active.
                : (CustomInteractableBase)hitTransform.GetComponentInParent<CustomPickup>();
            if (interactable == null || !interactable.CanInteract())
                return null;
            hitPoint = hit.point;
            Transform t = interactable.transform;
            interactablePositionForHitPoint = t.position;
            interactableRotationForHitPoint = t.rotation;
            if (Vector3.Distance(raycastOrigin, hitPoint) > interactable.desktopReach * eyeHeightScale)
                return null;
            return interactable;
        }

        private CustomInteractableBase TryGetNearInteractable(out bool isInteract)
        {
            // Max proximityReach is 1.
            // Divide by 2 because the definition is a diameter.
            float maxRadius = /* 1f * */ eyeHeightScale / 2f;

            trackingDataForHitPoint = localPlayer.GetTrackingData(handTrackingType);
            Vector3 handPosition = trackingDataForHitPoint.position;

            bool closestIsInteract = false;
            CustomInteractableBase closestInteractable = null;
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = Vector3.zero;

            Vector3 maxSphereOffset = trackingDataForHitPoint.rotation * palmDirection * maxRadius;
            Collider[] colliders = Physics.OverlapSphere(handPosition + maxSphereOffset, maxRadius, interactLayer | pickupLayer, QueryTriggerInteraction.Collide);
            foreach (Collider collider in colliders)
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity if we have a hit... this is not possible to be null.
                Transform hitTransform = collider.transform;
                bool currentIsInteract = hitTransform.gameObject.layer == interactLayerNumber;
                CustomInteractableBase interactable = currentIsInteract
                    ? (CustomInteractableBase)hitTransform.GetComponentInParent<CustomInteract>() // Does not need to include inactive, as the child is active.
                    : (CustomInteractableBase)hitTransform.GetComponentInParent<CustomPickup>();
                if (interactable == null || !interactable.CanInteract())
                    continue;
                Vector3 closestPoint = collider.ClosestPoint(handPosition);
                float distanceFromHand = Vector3.Distance(handPosition, closestPoint);
                float vrReach = interactable.GetEffectiveVRReach();
                float scaledReach = vrReach * eyeHeightScale;
                if (distanceFromHand > scaledReach || distanceFromHand >= closestDistance)
                    continue;
                Vector3 scaledSphereOrigin = handPosition + maxSphereOffset * vrReach;
                float distanceFromScaledSphereOrigin = Vector3.Distance(scaledSphereOrigin, collider.ClosestPoint(scaledSphereOrigin));
                if (distanceFromScaledSphereOrigin > scaledReach / 2f)
                    continue;
                closestIsInteract = currentIsInteract;
                closestInteractable = interactable;
                closestDistance = distanceFromHand;
                closestHitPoint = closestPoint;
            }

#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            if (closestInteractable != null)
            {
                debugSphere.gameObject.SetActive(true);
                debugSphere.position = handPosition + maxSphereOffset * closestInteractable.vRReach;
                debugSphere.localScale = Vector3.one * (closestInteractable.vRReach * maxRadius * 2f);
                debugLine.gameObject.SetActive(true);
                debugLine.position = handPosition;
                debugLine.rotation = Quaternion.LookRotation(closestHitPoint - handPosition);
                debugLine.localScale = new Vector3(1f, 1f, closestDistance);
            }
#endif

            isInteract = closestIsInteract;
            hitPoint = closestHitPoint;
            if (closestInteractable != null)
            {
                Transform t = closestInteractable.transform;
                interactablePositionForHitPoint = t.position;
                interactableRotationForHitPoint = t.rotation;
            }
            return closestInteractable;
        }

        private const int ConeIterations = 4;
        private const float ConeStartDistance = 0.5f;
        private const float ConeRadiusPerDistance = 0.4f;
        private const float ConeOverlap = 0.6f;

        private CustomInteractableBase TryGetInteractableInCone(bool includeInteracts, out bool isInteract)
        {
            int layerMask = includeInteracts ? interactLayer | pickupLayer : (int)pickupLayer;

            trackingDataForHitPoint = localPlayer.GetTrackingData(handTrackingType);
            Vector3 handPosition = trackingDataForHitPoint.position;

            bool closestIsInteract = false;
            CustomInteractableBase closestInteractable = null;
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = Vector3.zero;

            Vector3 direction = trackingDataForHitPoint.rotation * rotationNormalization * coneDirection;
            float distance = ConeStartDistance * eyeHeightScale;
            float distanceOffset = ConeRadiusPerDistance * distance - distance; // Make the first sphere tangential to the palm.
            for (int i = 0; i < ConeIterations; i++)
            {
                float radius = ConeRadiusPerDistance * distance;
                Collider[] colliders = Physics.OverlapSphere(
                    handPosition + direction * (distance + distanceOffset),
                    radius,
                    layerMask,
                    QueryTriggerInteraction.Collide);
                distance += radius / ConeOverlap;
                foreach (Collider collider in colliders)
                {
                    if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                        continue; // even though in normal Unity if we have a hit... this is not possible to be null.
                    Transform hitTransform = collider.transform;
                    bool currentIsInteract = hitTransform.gameObject.layer == interactLayerNumber;
                    CustomInteractableBase interactable = currentIsInteract
                        ? (CustomInteractableBase)hitTransform.GetComponentInParent<CustomInteract>() // Does not need to include inactive, as the child is active.
                        : (CustomInteractableBase)hitTransform.GetComponentInParent<CustomPickup>();
                    if (interactable == null || !interactable.CanInteract())
                        continue;
                    Vector3 closestPoint = collider.ClosestPoint(handPosition);
                    float distanceFromHand = Vector3.Distance(handPosition, closestPoint);
                    if (distanceFromHand >= closestDistance) // This bypasses reach checks even for already held pickups. But it's probably acceptable.
                        continue;
                    closestIsInteract = currentIsInteract;
                    closestInteractable = interactable;
                    closestDistance = distanceFromHand;
                    closestHitPoint = closestPoint;
                }
                if (closestInteractable != null)
                    break; // Anything colliding with a smaller sphere wins. They do not overlap all that much.
            }

#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.gameObject.SetActive(true);
            debugLine.position = handPosition;
            debugLine.rotation = Quaternion.LookRotation(direction);
            debugLine.localScale = new Vector3(1f, 1f, 3f * eyeHeightScale);
#endif

            isInteract = closestIsInteract;
            hitPoint = closestHitPoint;
            if (closestInteractable != null)
            {
                Transform t = closestInteractable.transform;
                interactablePositionForHitPoint = t.position;
                interactableRotationForHitPoint = t.rotation;
            }
            return closestInteractable;
        }

        private void ClearActiveScript()
        {
            if (activeScript == null)
                return;
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name} (inner)  ClearActiveScript");
#endif
            activeScript.HideHighlight();
            ClearActiveScriptVariables();
        }

        private void ClearActiveScriptVariables()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ClearActiveScriptVariables");
#endif
            HideInteractText();
            hasActiveInteract = false;
            hasActivePickup = false;
            activeInteract = null;
            activePickup = null;
            activeScript = null;
            activeTransform = null;
            EnableDisableUseText();
        }

        private void SetActiveInteract(CustomInteract newInteract)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  SetActiveInteract");
#endif
            ClearActiveScript();
            if (newInteract == null)
                return;
            hasActiveInteract = true;
            activeInteract = newInteract;
            SetActiveScriptGeneric(newInteract);
        }

        private void SetActivePickup(CustomPickup newPickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  SetActivePickup");
#endif
            ClearActiveScript();
            if (newPickup == null)
                return;
            hasActivePickup = true;
            activePickup = newPickup;
            SetActiveScriptGeneric(newPickup);
        }

        private void SetActiveScriptGeneric(CustomInteractableBase newActiveScript)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  SetActiveScriptGeneric");
#endif
            activeScript = newActiveScript;
            activeTransform = activeScript.transform;
            activeScript.manager = manager;
            activeScript.ShowHighlight();
            UpdateInteractText();
            ShowInteractText();
        }

        private void ShowInteractText()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ShowInteractText");
#endif
            if (isInVR)
            {
                interactTextRoot.gameObject.SetActive(true);
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);
            }
            else
                textRootDesktop.SetActive(true);
        }

        private void HideInteractText()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  HideInteractText");
#endif
            if (isInVR)
            {
                interactTextRoot.gameObject.SetActive(false);
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);
            }
            else
            {
                textRootDesktop.SetActive(false);
                interactTextElemDesktop.text = "";
            }
        }

        private void UpdateInteractText()
        {
            string interactText = (hasActivePickup && activePickup.isHeld)
                ? "" // No text when it is a pickup and said pickup is held by the other hand.
                : activeScript.interactText;

            if (!isInVR)
            {
                interactTextElemDesktop.text = interactText;
                return;
            }

            interactTextElem.text = interactText;
            if (!string.IsNullOrWhiteSpace(interactText)) // Optimization.
                MoveTextToHand(interactTextRoot);
        }

        private void EnableDisableUseText()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  EnableDisableUseText");
#endif
            if (isInVR)
                useTextRoot.gameObject.SetActive(isHolding);
            else
                textRootDesktop.SetActive(isHolding);

            if (isHolding)
                UpdateUseText();
            else if (!isInVR)
                useTextElemDesktop.text = "";
        }

        private void UpdateUseText()
        {
            if (!isInVR)
            {
                useTextElemDesktop.text = activePickup.useText;
                return;
            }

            string useText = activePickup.useText;
            useTextElem.text = useText;
            if (!string.IsNullOrWhiteSpace(useText)) // Optimization.
                MoveTextToHand(useTextTransform);
        }

        private void MoveTextToHand(Transform textTransform)
        {
            Quaternion headRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
            var projected = Vector3.ProjectOnPlane(headRotation * Vector3.forward, Vector3.up);
            Quaternion yRotation = Quaternion.LookRotation(projected);
            projected = Vector3.ProjectOnPlane((Quaternion.Inverse(yRotation) * headRotation) * Vector3.forward, Vector3.right);
            Quaternion tiltRotation = Quaternion.LookRotation(projected);

            textTransform.SetPositionAndRotation(
                localPlayer.GetTrackingData(handTrackingType).position,
                yRotation * tiltRotation);
        }

        private void PopulateStateForController()
        {
            stateForController.pickup = activePickup;
            stateForController.pickupTransform = activeTransform;
            if (activePickup.isHeldByPrimaryHand)
            {
                var hand = localPlayer.GetTrackingData(activePickup.primaryHeldTrackingType);
                Quaternion rotation = hand.rotation * rotationNormalization;
                stateForController.primaryHandPosition = hand.position + rotation * offsetVectorShift;
                stateForController.primaryHandRotation = rotation;
                // TODO: Only define rotationNormalization once if this ends up being the way this goes.
                // The same value gets used for both hands.
            }
            if (activePickup.isHeldBySecondaryHand)
            {
                var hand = localPlayer.GetTrackingData(activePickup.secondaryHeldTrackingType);
                Quaternion rotation = hand.rotation * rotationNormalization;
                stateForController.secondaryHandPosition = hand.position + rotation * offsetVectorShift;
                stateForController.secondaryHandRotation = rotation;
            }
        }

        private void UpdateControlledPickup()
        {
            PopulateStateForController();
            (activePickup.pickupController ?? fallbackPickupController).MovePickup(stateForController);
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputUse - value: {value}, args.handType == handType: {args.handType == handType}, lastInputUseEventTime == Time.time: {lastInputUseEventTime == Time.time}");
#endif
            // Ignore multiple InputUse events in the same frame... because for some unexplainable reason
            // VRChat is raising the InputUse event twice when I click the mouse button once.
            float timeTime = Time.time;
            if ((isInVR && args.handType != handType) || lastInputUseEventTime == timeTime)
                return;
            lastInputUseEventTime = timeTime;
            if (value)
                lastInputUseDownTime = timeTime;
            if (activeScript == null) // Update logic will handle cleanup if the active script got destroyed.
                return;

            if (hasActiveInteract)
            {
                if (value)
                    activeInteract.DispatchOnInteract();
                return;
            }

            if (!hasActivePickup || !isHolding)
                return;
            // Logic for when grab and use are the same physical input.
            if (autoHoldMode != CustomPickupsAutoHoldMode.SimultaneousGrabAndUse && timeTime == pickedUpAt)
                return;

            if (value)
            {
                if (!isAutoHolding // A theoretical optimization. Premature, probably.
                    && timeTime <= pickedUpAt + SimultaneousInputSeconds
                    && autoHoldMode == CustomPickupsAutoHoldMode.SimultaneousGrabAndUse)
                {
                    isAutoHolding = true;
                    return;
                }

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

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputGrab - value: {value}, args.handType == handType: {args.handType == handType}");
#endif
            if (isInVR && args.handType != handType)
                return;
            if (!hasActivePickup)
            {
                InputGrabWithoutActiveScript(value);
                return;
            }
            if (activeScript == null) // Update logic will handle cleanup if the active script got destroyed.
                return;

            float timeTime = Time.time;
            if (value)
            {
                inputGrabDownAt = timeTime;
                if (isHolding)
                    return;
                if (autoHoldMode == CustomPickupsAutoHoldMode.AnyDurationGrab
                    || (autoHoldMode == CustomPickupsAutoHoldMode.SimultaneousGrabAndUse
                        && timeTime <= lastInputUseDownTime + SimultaneousInputSeconds))
                {
                    isAutoHolding = true;
                }
                PickupActivePickup(hasHitPoint: true);
                return;
            }

            if (!isHolding)
                return;

            if (inputGrabDownAt == pickedUpAt) // Is the same button press as the one that picked up the pickup.
            {
                if (autoHoldMode == CustomPickupsAutoHoldMode.ShortGrab)
                    isAutoHolding = timeTime - inputGrabDownAt <= maxClickDurationSeconds;
                if (!isAutoHolding)
                    DropActivePickup();
                return;
            }

            if (!hasDropKeyBind)
                DropActivePickup();
        }

        private void InputGrabWithoutActiveScript(bool value)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputGrabWithoutActiveScript - value: {value}");
#endif
            float timeTime = Time.time;
            if (value)
            {
                inputGrabDownAt = timeTime;
                return;
            }
            if (timeTime - inputGrabDownAt <= maxClickDurationSeconds)
                useConeModeUntilTime = timeTime + ConeModeDurationSeconds;
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputDrop - value: {value}, args.handType == handType: {args.handType == handType}");
#endif
            hasDropKeyBind = true;
            if ((isInVR && args.handType != handType) || value || !isHolding)
                return;
            // Dropped on InputDropUp, matching VRChat's behaviour.
            DropActivePickup();
        }

        private void PopulatePickingUpStateForController(bool hasHitPoint)
        {
            pickingUpStateForController.pickup = activePickup;
            pickingUpStateForController.pickupTransform = activeTransform;

            var hand = localPlayer.GetTrackingData(handTrackingType);
            Quaternion handRotation = hand.rotation * rotationNormalization;
            pickingUpStateForController.handPosition = hand.position + handRotation * offsetVectorShift;
            pickingUpStateForController.handRotation = handRotation;

            pickingUpStateForController.hasClosestPoint = hasHitPoint;
            if (!hasHitPoint)
                return;
            pickingUpStateForController.closestPoint = hitPoint;
            Quaternion rotation = trackingDataForHitPoint.rotation * rotationNormalization;
            pickingUpStateForController.handPositionForClosestPoint = trackingDataForHitPoint.position + rotation * offsetVectorShift;
            pickingUpStateForController.handRotationForClosestPoint = rotation;
            pickingUpStateForController.pickupPositionForClosestPoint = interactablePositionForHitPoint;
            pickingUpStateForController.pickupRotationForClosestPoint = interactableRotationForHitPoint;
        }

        private void PickupActivePickup(bool hasHitPoint)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PickupActivePickup");
#endif
            if (activePickup.receivedOnDestroy)
                return;
            activePickup.BeginStateModification();
            pickupWasAttachedForHaptics = activePickup.isAttached;
            attachedManager.DetachIfAttached(activePickup);

            isHolding = true;
            pickedUpAt = Time.time;
            useConeModeUntilTime = -1f;

            CustomPickupController pickupController = activePickup.pickupController ?? fallbackPickupController;
            PopulatePickingUpStateForController(hasHitPoint);
            pickupController.HandlePickingUp(pickingUpStateForController);

#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.gameObject.SetActive(false);
#endif
            if (isInVR)
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);

            activePickup.HideHighlight();
            HideInteractText();
            EnableDisableUseText();

            bool doRaiseOnPickup = !activePickup.isHeld;

            if (pickingUpStateForController.shouldBecomeSecondaryHand)
            {
                if (activePickup.isHeldBySecondaryHand)
                {
                    otherHandManager.DropActivePickup();
                    doRaiseOnPickup = true;
                }
                isControllingActivePickup = !activePickup.isHeldByPrimaryHand; // Only control if the other hand is not controlling it.
                isPrimaryHoldingHand = false;
                activePickup.SetControlState(CustomPickupControlState.Held);
                activePickup.SetControllingPlayer(localPlayer);
                activePickup.isHeldBySecondaryHand = true;
                activePickup.secondaryHeldTrackingType = handTrackingType;
                activePickup.secondaryOffsetVector = pickingUpStateForController.heldOffsetVector;
                activePickup.secondaryOffsetRotation = pickingUpStateForController.heldOffsetRotation;
                PopulateStateForController();
                pickupController.HandleSecondaryPickingUp(stateForController);
            }
            else
            {
                if (activePickup.isHeldByPrimaryHand)
                {
                    otherHandManager.DropActivePickup();
                    doRaiseOnPickup = true;
                }
                if (activePickup.isHeldBySecondaryHand)
                    otherHandManager.isControllingActivePickup = false;
                isControllingActivePickup = true; // The primary hand is always the one in control, if there is primary one.
                isPrimaryHoldingHand = true;
                activePickup.SetControlState(CustomPickupControlState.Held);
                activePickup.SetControllingPlayer(localPlayer);
                activePickup.isHeldByPrimaryHand = true;
                activePickup.primaryHeldTrackingType = handTrackingType;
                activePickup.primaryOffsetVector = pickingUpStateForController.heldOffsetVector;
                activePickup.primaryOffsetRotation = pickingUpStateForController.heldOffsetRotation;
                PopulateStateForController();
                pickupController.HandlePrimaryPickingUp(stateForController);
            }

            if (doRaiseOnPickup)
                activePickup.DispatchOnPickup();
            activePickup.FinishStateModification();
        }

        private bool PrepareForcePickup(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PrepareForcePickup");
#endif
            if (isHolding)
            {
                if (activePickup == pickup)
                    return false;
                DropActivePickup(preventAttachment: true);
            }
            // TODO: remove pointless enabling and disabling of the highlight
            SetActivePickup(pickup);
            return true;
        }

        public void ForcePickup(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ForcePickup");
#endif
            if (!PrepareForcePickup(pickup))
                return;
            PickupActivePickup(hasHitPoint: false);
        }

        public void DropPickupIfHeld(CustomPickup pickup, bool preventAttachment = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  DropPickupIfHeld");
#endif
            // When isHolding is false, activePickup is always null. However the given pickup could be null,
            // which is arguably invalid use of the API, but this way it'll simply ignore the invalid drop call.
            if (isHolding && activePickup == pickup)
                DropActivePickup(preventAttachment);
        }

        public void DropActivePickup(bool preventAttachment = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  DropActivePickup");
            if (!isHolding)
            {
                Debug.LogError($"[CustomInteractsAndPickupsDebug] Attempt to DropActivePickup while isHolding is false.");
                return;
            }
#endif
            isHolding = false;
            isAutoHolding = false;
            if (isInVR)
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);

            if (activePickup == null) // Got destroyed.
            {
                ClearActiveScriptVariables();
                isHoldingUseButton = false;
                dropResultedInAttachForHaptics = false;
                return;
            }

            CustomPickup prevActivePickup = activePickup;
            CustomPickupController prevPickupController = activePickup.pickupController ?? fallbackPickupController;
            PopulateStateForController();

            ClearActiveScriptVariables();

            prevActivePickup.BeginStateModification();

            if (isHoldingUseButton)
            {
                isHoldingUseButton = false;
                prevActivePickup.DispatchOnPickupUseUp();
                // Once the api has been implemented the state of this script could have changed completely...
                // and this function should therefore probably be marked as recursive, because it could be called
                // recursively... but so could every calling function so uhh idk typical Udon moment I guess.
            }

            isControllingActivePickup = false;

            if (isPrimaryHoldingHand)
            {
                isPrimaryHoldingHand = false;
                prevActivePickup.isHeldByPrimaryHand = false;
                if (!prevActivePickup.isHeldBySecondaryHand)
                    prevActivePickup.SetControlState(CustomPickupControlState.None);
                prevPickupController.HandlePrimaryDropping(stateForController);
            }
            else
            {
                prevActivePickup.isHeldBySecondaryHand = false;
                if (!prevActivePickup.isHeldByPrimaryHand)
                    prevActivePickup.SetControlState(CustomPickupControlState.None);
                prevPickupController.HandleSecondaryDropping(stateForController);
            }

            if (isHolding)
            {
                Debug.LogError($"{nameof(CustomPickupController.HandlePrimaryDropping)} and "
                    + $"{nameof(CustomPickupController.HandleSecondaryDropping)} must not pick up the dropped "
                    + $"pickup by the same hand again.");
            }

            if (prevActivePickup.isHeld && !isHolding)
                otherHandManager.isControllingActivePickup = true;
            else
            {
                prevActivePickup.DispatchOnDrop();
                // Vertical mouse movement counts as lookVerticalInput on desktop. Ignore desktop entirely,
                // the user would not be able to pick up an attached item anymore.
                if (isInVR
                    && !preventAttachment
                    && manager.lookVerticalInput <= CustomInteractablesManager.VerticalLookDownThreshold
                    && prevActivePickup.CanAttach)
                {
                    attachedManager.AttachToNearestBone(prevActivePickup, droppingHandType);
                    dropResultedInAttachForHaptics = prevActivePickup.isAttached;
                }
            }

            prevActivePickup.FinishStateModification();
        }

        public void BecomePrimaryHand(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  BecomePrimaryHand");
#endif
            if (!isHolding // This hand is not holding anything.
                || isPrimaryHoldingHand // Holding but already primary.
                || pickup != activePickup // Holding a different pickup.
                || pickup.isHeldByPrimaryHand) // The other hand is holding it. This method refuses to swap hands.
            {
                return;
            }
            isPrimaryHoldingHand = true;
            isControllingActivePickup = true;
            pickup.isHeldByPrimaryHand = true;
            pickup.isHeldBySecondaryHand = false;
            pickup.primaryHeldTrackingType = handTrackingType;
            pickup.primaryOffsetVector = pickup.secondaryOffsetVector;
            pickup.primaryOffsetRotation = pickup.secondaryOffsetRotation;
        }
    }
}
