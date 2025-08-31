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
        [HideInInspector][SerializeField][SingletonReference] private BoneAttachmentManager boneAttachment;
        [HideInInspector][SerializeField][SingletonReference] private InterpolationManager interpolation;
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
        [HideInInspector][SerializeField][SingletonReference] private QuickDebugUI qd;
#endif

        public const float PickupInterpolationDuration = 0.1f;

        [System.NonSerialized] public VRCPlayerApi.TrackingDataType trackingHandType;
        [System.NonSerialized] public VRC_Pickup.PickupHand pickupHandType;
        [System.NonSerialized] public HandType handType;
        [System.NonSerialized] public Quaternion rotationNormalization;
        [System.NonSerialized] public Vector3 offsetVectorShift;
        [System.NonSerialized] public Vector3 palmDirection;
        [System.NonSerialized] public CustomInteractablesManager manager;

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

        // private Vector3 trackingDataOrigin;
        // private Quaternion trackingDataRotation;

        // private Vector3 raycastOrigin;
        // private Quaternion raycastRotation;
        // private Vector3 raycastForward;

        private bool hasActiveInteract;
        private bool hasActivePickup;
        private CustomInteract activeInteract;
        [System.NonSerialized] public CustomPickup activePickup;
        private CustomInteractableBase activeScript;
        private Transform activeTransform;
        private Vector3 hitPoint;
        private VRCPlayerApi.TrackingData trackingDataForHitPoint;
        private Vector3 interactablePositionForHitPoint;
        private Quaternion interactableRotationForHitPoint;

        private bool nextIsInteract;
        private CustomInteractableBase nextScript;
        private Vector3 nextHitPoint;
        private VRCPlayerApi.TrackingData nextTrackingDataForHitPoint;
        private Vector3 nextInteractablePositionForHitPoint;
        private Quaternion nextInteractableRotationForHitPoint;

        private CustomInteractableBase lastHapticsState = null;
        private bool lastHapticsHoldingState = false;

        private bool isHolding;
        private float pickedUpAt = -1;
        private Vector3 heldOffsetVector;
        private Quaternion heldOffsetRotation;
        private bool isHoldingUseButton;

        private float lastInputUseTime = -1f;
        private float inputGrabDownAt = -1f;
        /// <summary>
        /// <para>Always <see langword="true"/> for desktop.</para>
        /// <para>Set to <see langword="true"/> for VR if and when we get a
        /// <see cref="InputDrop(bool, UdonInputEventArgs)"/> event.</para>
        /// </summary>
        private bool hasDropKeyBind = false;
        private const float MaxClickDurationSeconds = 0.2f;

        private int interactLayerNumber = 8;
        private LayerMask interactLayer = (LayerMask)(1 << 8);
        private LayerMask pickupLayer = (LayerMask)(1 << 13);
        private const float InteractAndUseTextScale = 0.5f;

        private VRCPlayerApi localPlayer;
        private bool isInVR = true;
        private float eyeHeightScale = 1f;

#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
        private System.Diagnostics.Stopwatch updateSw = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch fixedUpdateSw = new System.Diagnostics.Stopwatch();
        private object[] updateContainer;
        private object[] fixedUpdateContainer;
        private string fixedUpdateFormatted = "";
#endif

        public void Initialize()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  Initialize");
#endif
            localPlayer = Networking.LocalPlayer;
            isInVR = localPlayer.IsUserInVR();
            hasDropKeyBind = !isInVR;
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            updateContainer = StopwatchUtil.CreateDataContainer();
            fixedUpdateContainer = StopwatchUtil.CreateDataContainer();
#endif
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugRaycast.gameObject.SetActive(true);
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
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugSphere.gameObject.SetActive(false);
            debugLine.gameObject.SetActive(false);
#endif
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            qd.ShowForOneFrame(this, "Fixed Update MS", fixedUpdateFormatted);
            updateSw.Reset();
            updateSw.Start();
#endif

            if (isHolding)
            {
                if (activeScript == null)
                {
                    DropActivePickup();
                    FixedUpdateHand(); // Immediately search for the next interactable.
                }
                else
                {
                    UpdateUseText();
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
                    updateSw.Stop();
                    qd.ShowForOneFrame(this, "Update MS", StopwatchUtil.FormatAvgMinMax(updateSw, updateContainer));
#endif
                    return;
                }
            }
            else if ((hasActiveInteract || hasActivePickup) && activeScript == null)
                ClearActiveScriptVariables();

            // These values need to be updated in case we reach the branches (nextScript == activeScript) or SetActivePickup.
            hitPoint = nextHitPoint;
            trackingDataForHitPoint = nextTrackingDataForHitPoint;
            interactablePositionForHitPoint = nextInteractablePositionForHitPoint;
            interactableRotationForHitPoint = nextInteractableRotationForHitPoint;

            if (nextScript == null)
                ClearActiveScript();
            else if (nextScript == activeScript)
                UpdateInteractText();
            else if (nextIsInteract)
                SetActiveInteract((CustomInteract)nextScript);
            else
                SetActivePickup((CustomPickup)nextScript);

#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            updateSw.Stop();
            qd.ShowForOneFrame(this, "Update MS", StopwatchUtil.FormatAvgMinMax(updateSw, updateContainer));
#endif
        }

        public void FixedUpdateHand()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            fixedUpdateSw.Reset();
            fixedUpdateSw.Start();
#endif
            if (!isHolding)
                if (isInVR)
                    TryGetNearInteractable();
                else
                    TryGetInteractable();
#if CUSTOM_INTERACTS_AND_PICKUPS_STOPWATCH
            fixedUpdateSw.Stop();
            fixedUpdateFormatted = StopwatchUtil.FormatAvgMinMax(fixedUpdateSw, fixedUpdateContainer);
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
                PlayHaptics(nameof(manager.onDropHaptics));
                lastHapticsHoldingState = false;
                return;
            }
            if (isHolding)
            {
                lastHapticsState = activeScript;
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

        private void TryGetInteractable()
        {
            float maxDistance = 25f * eyeHeightScale;

            nextTrackingDataForHitPoint = localPlayer.GetTrackingData(trackingHandType);
            Vector3 raycastOrigin = nextTrackingDataForHitPoint.position;
            Vector3 raycastForward = nextTrackingDataForHitPoint.rotation * rotationNormalization * Vector3.forward;

#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.gameObject.SetActive(true);
            debugLine.position = raycastOrigin;
            debugLine.rotation = raycastRotation;
            debugLine.localScale = new Vector3(1f, 1f, maxDistance);
#endif

            nextScript = null;
            if (!Physics.Raycast(raycastOrigin, raycastForward, out RaycastHit hit, maxDistance, interactLayer | pickupLayer, QueryTriggerInteraction.Collide))
                return;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return; // even though in normal Unity if we have a hit... this is not possible to be null.
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugLine.localScale = new Vector3(1f, 1f, Vector3.Distance(raycastOrigin, hit.point));
#endif
            nextIsInteract = hitTransform.gameObject.layer == interactLayerNumber;
            CustomInteractableBase interactable = nextIsInteract
                ? (CustomInteractableBase)hitTransform.GetComponentInParent<CustomInteract>() // Does not need to include inactive, as the child is active.
                : (CustomInteractableBase)hitTransform.GetComponentInParent<CustomPickup>();
            if (interactable == null || !interactable.CanInteract())
                return;
            nextHitPoint = hit.point;
            Transform t = interactable.transform;
            nextInteractablePositionForHitPoint = t.position;
            nextInteractableRotationForHitPoint = t.rotation;
            if (Vector3.Distance(raycastOrigin, nextHitPoint) > interactable.desktopReach * eyeHeightScale)
                return;
            nextScript = interactable;
        }

        private void TryGetNearInteractable()
        {
            // Max proximityReach is 1.
            // Divide by 2 because the definition is a diameter.
            float maxRadius = /* 1f * */ eyeHeightScale / 2f;

            nextTrackingDataForHitPoint = localPlayer.GetTrackingData(trackingHandType);
            Vector3 handPosition = nextTrackingDataForHitPoint.position;

            bool closestIsInteract = false;
            CustomInteractableBase closestInteractable = null;
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = Vector3.zero;

            Vector3 maxSphereOffset = nextTrackingDataForHitPoint.rotation * palmDirection * maxRadius;
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
                float vrReach = interactable.vRReach;
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
                debugSphere.position = raycastOrigin + trackingDataRotation * palmDirection * closestInteractable.vRReach * maxRadius;
                debugSphere.localScale = Vector3.one * (closestInteractable.vRReach * maxRadius * 2f);
                debugLine.gameObject.SetActive(true);
                debugLine.position = raycastOrigin;
                debugLine.rotation = Quaternion.LookRotation(closestHitPoint - raycastOrigin);
                debugLine.localScale = new Vector3(1f, 1f, closestDistance);
            }
#endif

            nextIsInteract = closestIsInteract;
            nextScript = closestInteractable;
            nextHitPoint = closestHitPoint;
            if (closestInteractable != null)
            {
                Transform t = closestInteractable.transform;
                nextInteractablePositionForHitPoint = t.position;
                nextInteractableRotationForHitPoint = t.rotation;
            }
        }

        private void ClearActiveScript()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ClearActiveScript");
#endif
            if (activeScript == null)
                return;
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
            if (activeScript == null)
                return;

            if (!isInVR)
            {
                interactTextElemDesktop.text = activeScript.interactText;
                return;
            }

            interactTextElem.text = activeScript.interactText;
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

            useTextElem.text = activePickup.useText;
            MoveTextToHand(useTextTransform);
        }

        private void MoveTextToHand(Transform textTransform)
        {
            Quaternion headRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
            var projected = Vector3.ProjectOnPlane(headRotation * Vector3.forward, Vector3.up);
            Quaternion yRotation = Quaternion.LookRotation(projected);
            projected = Vector3.ProjectOnPlane((Quaternion.Inverse(yRotation) * headRotation) * Vector3.forward, Vector3.right);
            Quaternion tiltRotation = Quaternion.LookRotation(projected);

            textTransform.position = localPlayer.GetTrackingData(trackingHandType).position;
            textTransform.rotation = yRotation * tiltRotation;
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputUse - value: {value}, args.handType == handType: {args.handType == handType}, lastInputUse == Time.time: {lastInputUseTime == Time.time}");
#endif
            if ((isInVR && args.handType != handType) || lastInputUseTime == Time.time)
                return;
            // Ignore multiple InputUse events in the same frame... because for some unexplainable reason
            // VRChat is raising the InputUse event twice when I click the mouse button once.
            lastInputUseTime = Time.time;
            if (activeScript == null) // UpdateHand will handle cleanup if the active script got destroyed.
                return;
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
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputGrab - value: {value}, args.handType == handType: {args.handType == handType}");
#endif
            if (!hasActivePickup
                || (isInVR && args.handType != handType)
                || activeScript == null)  // UpdateHand will handle cleanup if the active script got destroyed.
            {
                return;
            }

            if (value)
            {
                inputGrabDownAt = Time.time;
                if (!isHolding)
                    PickupActivePickup();
                return;
            }

            if (!isHolding)
                return;

            if (inputGrabDownAt == pickedUpAt) // Is the same button press as the one that picked up the pickup.
            {
                if (!activePickup.autoHold || Time.time - inputGrabDownAt > MaxClickDurationSeconds)
                    DropActivePickup();
                return;
            }

            if (!hasDropKeyBind)
                DropActivePickup();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputDrop - value: {value}, args.handType == handType: {args.handType == handType}");
#endif
            hasDropKeyBind = true;
            if ((isInVR && args.handType != handType) || value || !isHolding)
                return;
            // Dropped on InputDropUp, matching VRCHat's behaviour.
            DropActivePickup();
        }

        private void CalculateActivePickupOffsets()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  CalculateActivePickupOffsets");
#endif
            Transform exactGrip = activePickup.exactGrip;
            if (exactGrip == null)
            {
                // Move to hand.
                Vector3 handPosition = trackingDataForHitPoint.position;
                Quaternion inverseTrackingDataRotation = Quaternion.Inverse(trackingDataForHitPoint.rotation);
                Vector3 distanceFromTrackingData = inverseTrackingDataRotation * (hitPoint - handPosition);
                heldOffsetRotation = inverseTrackingDataRotation * interactableRotationForHitPoint;
                heldOffsetVector = inverseTrackingDataRotation * (interactablePositionForHitPoint - handPosition)
                    - distanceFromTrackingData + offsetVectorShift;
            }
            else
            {
                // Exact grip.
                Quaternion activeRotation = activeTransform.rotation;
                Vector3 offsetVector = Quaternion.Inverse(activeRotation) * (activeTransform.position - exactGrip.position);
                heldOffsetRotation = rotationNormalization * Quaternion.Inverse(exactGrip.rotation) * activeRotation;
                heldOffsetVector = heldOffsetRotation * offsetVector + offsetVectorShift;
            }
        }

        private void PickupActivePickup(bool skipOffsetCalculation = false)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PickupActivePickup");
#endif
            isHolding = true;
            pickedUpAt = Time.time;

            if (!skipOffsetCalculation)
                CalculateActivePickupOffsets();

            boneAttachment.AttachToLocalTrackingData(trackingHandType, activeTransform);
            // TODO: Test and see how it feels to have interpolation enabled for pickups with exact grip.
            interpolation.InterpolateLocalPosition(activeTransform, heldOffsetVector, PickupInterpolationDuration, this, nameof(PickupPositionInterpolationCallback), null);
            interpolation.InterpolateLocalRotation(activeTransform, heldOffsetRotation, PickupInterpolationDuration, this, nameof(PickupRotationInterpolationCallback), null);
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugRaycast.gameObject.SetActive(false);
#endif
            if (isInVR)
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);

            activePickup.HideHighlight();
            HideInteractText();
            EnableDisableUseText();

            activePickup.isHeld = true;
            activePickup.heldTrackingType = trackingHandType;
            activePickup.heldOffsetVector = heldOffsetVector;
            activePickup.heldOffsetRotation = heldOffsetRotation;
            activePickup.DispatchOnPickup();
        }

        public void PickupPositionInterpolationCallback()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PickupPositionInterpolationCallback - isHolding: {isHolding}");
#endif
            if (isHolding && activeTransform != null)
                activeTransform.localPosition = heldOffsetVector;
        }

        public void PickupRotationInterpolationCallback()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PickupRotationInterpolationCallback - isHolding: {isHolding}");
#endif
            if (isHolding && activeTransform != null)
                activeTransform.localRotation = heldOffsetRotation;
        }

        private Vector3 GetClosestPoint(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  GetClosestPoint");
#endif
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = pickup.transform.position; // Default for when there are 0 colliders.
            foreach (Collider collider in pickup.GetComponentsInChildren<Collider>())
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity... this is not possible to be null.
                VRCPlayerApi.TrackingData trackingData = localPlayer.GetTrackingData(trackingHandType);
                Vector3 trackingDataPosition = trackingData.position;
                Vector3 closestPoint = collider.ClosestPoint(trackingDataPosition);
                float distance = Vector3.Distance(trackingDataPosition, closestPoint);
                if (distance >= closestDistance)
                    continue;
                closestDistance = distance;
                closestHitPoint = closestPoint;
            }
            return closestHitPoint;
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
                DropActivePickup();
            }
            if (pickup.exactGrip == null)
                hitPoint = GetClosestPoint(pickup);
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
            PickupActivePickup();
        }

        public void ForcePickupUsingExistingOffset(CustomPickup pickup)
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ForcePickupUsingExistingOffset");
#endif
            bool alreadyHoldingThisPickup = !PrepareForcePickup(pickup);
            heldOffsetVector = pickup.heldOffsetVector;
            heldOffsetRotation = pickup.heldOffsetRotation;
            if (alreadyHoldingThisPickup)
                return;
            PickupActivePickup(skipOffsetCalculation: true);
        }

        public void DropActivePickup()
        {
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  DropActivePickup");
            if (!isHolding)
            {
                Debug.LogError($"[CustomInteractsAndPickupsDebug] Attempt to DropActivePickup while isHolding is false.");
                return;
            }
#endif
            boneAttachment.DetachFromLocalTrackingData(trackingHandType, activeTransform);
#if CUSTOM_INTERACTS_AND_PICKUPS_DEBUG
            debugRaycast.gameObject.SetActive(true);
#endif
            isHolding = false;
            if (activeTransform != null)
            {
                interpolation.CancelLocalPositionInterpolation(activeTransform);
                interpolation.CancelLocalRotationInterpolation(activeTransform);
            }
            if (isInVR)
                SendCustomEventDelayedFrames(nameof(UpdateHaptics), 1);

            CustomPickup prevActivePickup = activePickup;
            ClearActiveScriptVariables();
            if (prevActivePickup == null) // Got destroyed.
            {
                isHoldingUseButton = false;
                return;
            }

            if (isHoldingUseButton)
            {
                isHoldingUseButton = false;
                prevActivePickup.DispatchOnPickupUseUp();
                // Once the api has been implemented the state of this script could have changed completely...
                // and this function should therefore probably be marked as recursive, because it could be called
                // recursively... but so could every calling function so uhh idk typical Udon moment I guess.
            }
            prevActivePickup.isHeld = false;
            prevActivePickup.DispatchOnDrop();
        }
    }
}
