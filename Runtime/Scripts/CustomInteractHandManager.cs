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

        [SerializeField] private Transform debugRaycast;
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
        public GameObject useTextRootDesktop;
        public TextMeshProUGUI useTextElemDesktop;

        private Vector3 raycastOrigin;
        private Quaternion raycastRotation;
        private Vector3 raycastForward;

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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  Initialize");
            #endif
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
            #if CustomInteractsAndPickupsDebug
            debugSphere.gameObject.SetActive(false);
            debugLine.gameObject.SetActive(false);
            #endif

            if (isHolding)
            {
                UpdateHeldPickup();
                return;
            }

            FetchRaycastCoordinateSystem();

            CustomInteractiveBase newActiveScript;
            bool isInteract;
            if (isInVR)
            {
                newActiveScript = TryGetNearInteractive(out isInteract);
                if (newActiveScript != null)
                {
                    if (newActiveScript == activeScript)
                        UpdateInteractText();
                    else if (isInteract)
                        SetActiveInteract((CustomInteract)newActiveScript);
                    else
                        SetActivePickup((CustomPickup)newActiveScript);
                    return;
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
            FetchRaycastCoordinateSystem();
            activeTransform.position = raycastOrigin + raycastRotation * heldOffsetVector;
            activeTransform.rotation = raycastRotation * heldOffsetRotation;
            UpdateUseText();
        }

        private void FetchRaycastCoordinateSystem()
        {
            VRCPlayerApi.TrackingData hand = localPlayer.GetTrackingData(trackingHandType);
            raycastOrigin = hand.position;
            raycastRotation = hand.rotation * rotationNormalization;
            raycastForward = raycastRotation * Vector3.forward;
            #if CustomInteractsAndPickupsDebug
            debugRaycast.SetPositionAndRotation(raycastOrigin, raycastRotation);
            #endif
        }

        private CustomInteractiveBase TryGetInteractive(out bool isInteract)
        {
            float maxDistance = 10f // Max proximity.
                * ((5f - 2f) / 2f + 1f) // Max eyeHeightScale.
                * raycastProximityMultiplier;

            #if CustomInteractsAndPickupsDebug
            debugLine.gameObject.SetActive(true);
            debugLine.position = raycastOrigin;
            debugLine.rotation = raycastRotation;
            debugLine.localScale = new Vector3(1f, 1f, maxDistance);
            #endif

            isInteract = false;
            if (!Physics.Raycast(raycastOrigin, raycastForward, out RaycastHit hit, maxDistance, interactLayer | pickupLayer, QueryTriggerInteraction.Collide))
                return null;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return null; // even though in normal Unity if we have a hit... this is not possible to be null.
            #if CustomInteractsAndPickupsDebug
            debugLine.localScale = new Vector3(1f, 1f, Vector3.Distance(raycastOrigin, hit.point));
            #endif
            isInteract = hitTransform.gameObject.layer == interactLayerNumber;
            CustomInteractiveBase interactive = isInteract
                ? (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomInteract>()
                : (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomPickup>();
            if (interactive == null)
                return null;
            hitPoint = hit.point;
            if (Vector3.Distance(raycastOrigin, hitPoint) > interactive.proximity * eyeHeightScale * raycastProximityMultiplier)
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

            Collider[] colliders = Physics.OverlapSphere(raycastOrigin, maxRadius, interactLayer | pickupLayer, QueryTriggerInteraction.Collide);
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
                Vector3 closestPoint = collider.ClosestPoint(raycastOrigin);
                float distance = Vector3.Distance(raycastOrigin, closestPoint);
                if (distance > interactive.proximity * eyeHeightScale)
                    continue;
                if (distance >= closestDistance)
                    continue;
                closestIsInteract = currentIsInteract;
                closestInteractive = interactive;
                closestDistance = distance;
                closestHitPoint = closestPoint;
            }

            #if CustomInteractsAndPickupsDebug
            if (closestInteractive != null)
            {
                debugSphere.gameObject.SetActive(true);
                debugSphere.position = closestHitPoint;
                debugSphere.localScale = Vector3.one * (closestInteractive.proximity * eyeHeightScale * 2f);
            }
            #endif

            isInteract = closestIsInteract;
            hitPoint = closestHitPoint;
            return closestInteractive;
        }

        private void ClearActiveScript()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ClearActiveScript");
            #endif
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
            #if CustomInteractsAndPickupsDebug
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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  SetActivePickup");
            #endif
            ClearActiveScript();
            if (newPickup == null)
                return;
            hasActivePickup = true;
            activePickup = newPickup;
            SetActiveScriptGeneric(newPickup);
        }

        private void SetActiveScriptGeneric(CustomInteractiveBase newActiveScript)
        {
            #if CustomInteractsAndPickupsDebug
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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ShowInteractText");
            #endif
            interactTextRoot.gameObject.SetActive(true);
        }

        private void HideInteractText()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  HideInteractText");
            #endif
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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputUse - value: {value}, args.handType == handType: {args.handType == handType}, lastInputUse == Time.time: {lastInputUse == Time.time}");
            #endif
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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputGrab - value: {value}, args.handType == handType: {args.handType == handType}");
            #endif
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
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  InputDrop - value: {value}, args.handType == handType: {args.handType == handType}");
            #endif
            if ((isInVR && args.handType != handType) || value || !isHolding)
                return;
            // Dropped on InputDropUp, matching VRCHat's behaviour.
            DropActivePickup();
        }

        private void CalculateActivePickupOffsets()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  CalculateActivePickupOffsets");
            #endif
            Transform exactGrip = activePickup.exactGrip;
            if (exactGrip == null)
            {
                // Move to hand.
                // TODO: add interpolation
                Quaternion inverseHandRotation = Quaternion.Inverse(raycastRotation);
                Vector3 distanceFromHand = inverseHandRotation * (hitPoint - raycastOrigin);
                heldOffsetVector = inverseHandRotation * (activeTransform.position - raycastOrigin);
                heldOffsetVector = heldOffsetVector - distanceFromHand + offsetVectorShift;
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
        }

        private void PickupActivePickup(bool skipOffsetCalculation = false)
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  PickupActivePickup");
            #endif
            isHolding = true;
            pickedUpAt = Time.time;

            if (!skipOffsetCalculation)
                CalculateActivePickupOffsets();

            activePickup.HideHighlight();
            HideInteractText();
            UpdateUseText();

            activePickup.isHeld = true;
            activePickup.heldTrackingType = trackingHandType;
            activePickup.heldOffsetVector = heldOffsetVector;
            activePickup.heldOffsetRotation = heldOffsetRotation;
            activePickup.DispatchOnPickup();
        }

        private Vector3 GetClosestPoint(CustomPickup pickup)
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  GetClosestPoint");
            #endif
            float closestDistance = float.PositiveInfinity;
            Vector3 closestHitPoint = pickup.transform.position; // Default for when there are 0 colliders.
            foreach (Collider collider in pickup.GetComponentsInChildren<Collider>())
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity... this is not possible to be null.
                Vector3 closestPoint = collider.ClosestPoint(raycastOrigin);
                float distance = Vector3.Distance(raycastOrigin, closestPoint);
                if (distance >= closestDistance)
                    continue;
                closestDistance = distance;
                closestHitPoint = closestPoint;
            }
            return closestHitPoint;
        }

        public void ForcePickup(CustomPickup pickup)
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ForcePickup");
            #endif
            FetchRaycastCoordinateSystem();
            if (pickup.exactGrip == null)
                hitPoint = GetClosestPoint(pickup);
            // TODO: remove pointless enabling and disabling of the highlight
            SetActivePickup(pickup);
            PickupActivePickup();
        }

        public void ForcePickupUsingExistingOffset(CustomPickup pickup)
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  ForcePickupUsingExistingOffset");
            #endif
            FetchRaycastCoordinateSystem();
            if (pickup.exactGrip == null)
                hitPoint = GetClosestPoint(pickup);
            // TODO: remove pointless enabling and disabling of the highlight
            SetActivePickup(pickup);
            heldOffsetVector = pickup.heldOffsetVector;
            heldOffsetRotation = pickup.heldOffsetRotation;
            PickupActivePickup(skipOffsetCalculation: true);
        }

        public void DropActivePickup()
        {
            #if CustomInteractsAndPickupsDebug
            Debug.Log($"[CustomInteractsAndPickupsDebug] HandManager {this.name}  DropActivePickup");
            #endif
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
