using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public class DefaultPickupControllerLogic : UdonSharpBehaviour
    {
        /// <summary>
        /// <para>Using a field rather than an out parameter for micro optimization reasons.</para>
        /// </summary>
        private Vector3 targetPosition;
        /// <inheritdoc cref="targetPosition"/>
        private Quaternion targetRotation;

        private const float ShortDistanceNotNeedingInterpolation = 0.02f;

        // Public so other custom pickup controllers can use this function if they wish.
        public void CalculatePickupOffsets(CustomPickupPickingUpState state, Transform exactGrip)
        {
            if (exactGrip == null)
            {
                // Move to hand.
                state.EnsureHasClosestPoint();
                Quaternion inverseHandRotation = Quaternion.Inverse(state.handRotationForClosestPoint);
                state.heldOffsetRotation = inverseHandRotation * state.pickupRotationForClosestPoint;
                state.heldOffsetVector = inverseHandRotation * (state.pickupPositionForClosestPoint - state.closestPoint);
            }
            else
            {
                // Exact grip.
                Transform pickupTransform = state.pickupTransform;
                Quaternion pickupRotation = pickupTransform.rotation;
                Vector3 offsetVector = Quaternion.Inverse(pickupRotation) * (pickupTransform.position - exactGrip.position);
                Quaternion heldOffsetRotation = Quaternion.Inverse(exactGrip.rotation) * pickupRotation;
                state.heldOffsetRotation = heldOffsetRotation;
                state.heldOffsetVector = heldOffsetRotation * offsetVector;
            }
        }

        public void HandlePickingUp(CustomPickupPickingUpState state)
        {
            CustomPickup pickup = state.pickup;
            bool shouldBecomeSecondaryHand = pickup.isHeldByPrimaryHand;
            state.shouldBecomeSecondaryHand = shouldBecomeSecondaryHand;
            if (shouldBecomeSecondaryHand)
                CalculatePickupOffsets(state, pickup.secondaryExactGrip);
            else
                CalculatePickupOffsets(state, pickup.primaryExactGrip);

            float distanceToMove = Mathf.Abs((state.pickupTransform.position - state.handPosition).magnitude - state.heldOffsetVector.magnitude);
            if (!pickup.isHeld && distanceToMove > ShortDistanceNotNeedingInterpolation)
                pickup.StartInterpolation();
        }

        public void HandlePrimaryPickingUp(CustomPickupState state)
        {
        }

        public void HandleSecondaryPickingUp(CustomPickupState state)
        {
        }

        public void HandlePrimaryDropping(CustomPickupState state)
        {
            CustomPickup pickup = state.pickup;
            if (!pickup.isHeldBySecondaryHand)
                return;
            MakeSecondaryOffsetsMatchCurrentLocation(state);
            if (pickup.droppingTransfersPrimaryHand)
                pickup.MakeSecondaryHandPrimary();
        }

        public void HandleSecondaryDropping(CustomPickupState state)
        {
            if (state.pickup.isHeldByPrimaryHand)
                MakePrimaryOffsetsMatchCurrentLocation(state);
        }

        public void MakePrimaryOffsetsMatchCurrentLocation(CustomPickupState state)
        {
            CustomPickup pickup = state.pickup;
            Transform pickupTransform = state.pickupTransform;
            Quaternion inverseHandRotation = Quaternion.Inverse(state.primaryHandRotation);
            Vector3 offsetVector = inverseHandRotation * (pickupTransform.position - state.primaryHandPosition);
            float desiredMagnitude = pickup.primaryOffsetVector.magnitude;
            float tooLongBy = offsetVector.magnitude - desiredMagnitude;
            if (tooLongBy > 0f)
            {
                offsetVector = offsetVector.normalized * desiredMagnitude;
                if (tooLongBy > ShortDistanceNotNeedingInterpolation)
                    pickup.StartInterpolation();
            }
            pickup.primaryOffsetVector = offsetVector;
            pickup.primaryOffsetRotation = inverseHandRotation * pickupTransform.rotation;
        }

        public void MakeSecondaryOffsetsMatchCurrentLocation(CustomPickupState state)
        {
            // 100% copy paste, with "primary" replaced with "secondary".
            CustomPickup pickup = state.pickup;
            Transform pickupTransform = state.pickupTransform;
            Quaternion inverseHandRotation = Quaternion.Inverse(state.secondaryHandRotation);
            Vector3 offsetVector = inverseHandRotation * (pickupTransform.position - state.secondaryHandPosition);
            float desiredMagnitude = pickup.secondaryOffsetVector.magnitude;
            float tooLongBy = offsetVector.magnitude - desiredMagnitude;
            if (tooLongBy > 0f)
            {
                offsetVector = offsetVector.normalized * desiredMagnitude;
                if (tooLongBy > ShortDistanceNotNeedingInterpolation)
                    pickup.StartInterpolation();
            }
            pickup.secondaryOffsetVector = offsetVector;
            pickup.secondaryOffsetRotation = inverseHandRotation * pickupTransform.rotation;
        }

        public void MovePickup(CustomPickupState state)
        {
            GetLocationToMoveTo(state);

            CustomPickup pickup = state.pickup;
            float progress = pickup.interpolationProgress;
            if (progress >= 1f)
            {
                state.pickupTransform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            float toAdd = Time.deltaTime / CustomPickup.InterpolationDuration;
            // If progress is 0.6f and toAdd is 0.1f then currentStep is 0.25f.
            // Effectively one quarter of the way from current position (0.6f) to target position (1.0f).
            float currentStep = toAdd / (1f - progress);
            progress += toAdd;
            if (progress >= 1f)
            {
                pickup.interpolationProgress = 1f;
                state.pickupTransform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            pickup.interpolationProgress = progress;
            Transform pickupTransform = state.pickupTransform;
            pickupTransform.SetPositionAndRotation(
                Vector3.Lerp(pickupTransform.position, targetPosition, currentStep),
                Quaternion.Lerp(pickupTransform.rotation, targetRotation, currentStep));
        }

        public void GetLocationToMoveTo(CustomPickupState state, out Vector3 position, out Quaternion rotation)
        {
            GetLocationToMoveTo(state);
            position = targetPosition;
            rotation = targetRotation;
        }

        private void GetLocationToMoveTo(CustomPickupState state)
        {
            CustomPickup pickup = state.pickup;
            Quaternion primaryHandRotation = state.primaryHandRotation;

            if (pickup.isHeldBySecondaryHand)
            {
                if (!pickup.isHeldByPrimaryHand)
                {
                    targetPosition = state.secondaryHandPosition + state.secondaryHandRotation * pickup.secondaryOffsetVector;
                    targetRotation = state.secondaryHandRotation * pickup.secondaryOffsetRotation;
                    return;
                }

                // Held by both, make pickup point towards secondary hand, anchored on primary hand.
                // The rotation getting "added" here is in primary hand local space. The primaryHandRotation
                // is the rotation effectively defining going from world space to local space,
                // any further rotation should be relative to that first rotation.
                primaryHandRotation *= Quaternion.FromToRotation(
                    // Primary anchor to secondary anchor (effectively expected secondary hand position) in primary hand local space.
                    // Primary offset vector is already in the correct local space.
                    // Secondary offset is in secondary hand local space.
                    // First it gets converted to pickup local space then to primary hand local space,
                    // though it points the opposite direction therefore gets subtracted.
                    pickup.primaryOffsetVector
                        - pickup.primaryOffsetRotation * (Quaternion.Inverse(pickup.secondaryOffsetRotation) * pickup.secondaryOffsetVector),
                    // Primary anchor to secondary actual hand position, converted from world to primary hand local space.
                    Quaternion.Inverse(primaryHandRotation) * (state.secondaryHandPosition - state.primaryHandPosition));
            }

            targetPosition = state.primaryHandPosition + primaryHandRotation * pickup.primaryOffsetVector;
            targetRotation = primaryHandRotation * pickup.primaryOffsetRotation;
        }
    }
}
