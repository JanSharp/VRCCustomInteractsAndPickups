﻿using UdonSharp;
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

        private int interactLayerNumber = 8;
        private LayerMask interactLayer = (LayerMask)(1 << 8);
        private LayerMask pickupLayer = (LayerMask)(1 << 13);
        private Vector3 desktopOffsetVectorShift = new Vector3(0.4f, -0.2f, 0.5f);

        private VRCPlayerApi localPlayer;
        private VRCPlayerApi.TrackingData head;
        private Vector3 headPosition;
        private Quaternion headRotation;
        private Vector3 headForward;

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

        private void Start()
        {
            localPlayer = Networking.LocalPlayer;
        }

        private void Update()
        {
            if (isHolding)
            {
                UpdateHeldPickup();
                return;
            }

            FetchHeadValues();

            CustomInteractiveBase newActiveScript = TryGetInteractive(out bool isInteract);
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
            activeTransform.position = headPosition + headRotation * heldOffsetVector;
            activeTransform.rotation = headRotation * heldOffsetRotation;
        }

        private void FetchHeadValues()
        {
            head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            headPosition = head.position;
            headRotation = head.rotation;
            headForward = headRotation * Vector3.forward;
        }

        private CustomInteractiveBase TryGetInteractive(out bool isInteract)
        {
            isInteract = false;
            if (!Physics.Raycast(headPosition, headForward, out RaycastHit hit, 100f, interactLayer | pickupLayer, QueryTriggerInteraction.Collide))
                return null;
            Transform hitTransform = hit.transform;
            if (hitTransform == null) // Some VRC internal that we're not allowed to access so we get null instead,
                return null; // even though in normal Unity if we have a hit... this is not possible to be null.
            hitPoint = hit.point;
            isInteract = hitTransform.gameObject.layer == interactLayerNumber;
            return isInteract
                ? (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomInteract>()
                : (CustomInteractiveBase)hitTransform.GetComponentInParent<CustomPickup>();
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
            activeScript.manager = this;
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
            highlightTextRoot.rotation = headRotation;
            highlightTextTransform.localScale = Vector3.one * Vector3.Distance(headPosition, interactPosition);
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (!value || !hasActiveInteract)
                return;
            activeInteract.DispatchOnInteract();
        }

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if (!hasActivePickup)
                return;
            if (!value && isHolding && !activePickup.autoHold)
            {
                DropActivePickup();
                return;
            }
            if (!value)
                return;

            isHolding = true;
            Quaternion inverseHeadRotation = Quaternion.Inverse(headRotation);
            Vector3 distanceFromHead = inverseHeadRotation * (hitPoint - headPosition);
            heldOffsetVector = inverseHeadRotation * (activeTransform.position - headPosition);
            heldOffsetVector = heldOffsetVector - distanceFromHead + desktopOffsetVectorShift;
            heldOffsetRotation = inverseHeadRotation * activeTransform.rotation;
            activePickup.HideHighlight();
            HideHighlightText();
        }

        public override void InputDrop(bool value, UdonInputEventArgs args)
        {
            // Maybe use on release instead.
            if (!value || !isHolding)
                return;
            DropActivePickup();
        }

        private void DropActivePickup()
        {
            isHolding = false;
            ClearActiveScript();
        }
    }
}
