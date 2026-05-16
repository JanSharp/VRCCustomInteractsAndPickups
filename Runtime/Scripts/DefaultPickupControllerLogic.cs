using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonScript("bb7ec25f46ae4ab699263323ebfb58ec")] // Runtime/Prefabs/CustomInteractablesManager.prefab
    public class DefaultPickupControllerLogic : UdonSharpBehaviour
    {
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
            pickup.primaryOffsetVector = offsetVector.normalized * pickup.primaryOffsetVector.magnitude;
            pickup.primaryOffsetRotation = inverseHandRotation * pickupTransform.rotation;
        }

        public void MakeSecondaryOffsetsMatchCurrentLocation(CustomPickupState state)
        {
            // 100% copy paste, with "primary" replaced with "secondary".
            CustomPickup pickup = state.pickup;
            Transform pickupTransform = state.pickupTransform;
            Quaternion inverseHandRotation = Quaternion.Inverse(state.secondaryHandRotation);
            Vector3 offsetVector = inverseHandRotation * (pickupTransform.position - state.secondaryHandPosition);
            pickup.secondaryOffsetVector = offsetVector.normalized * pickup.secondaryOffsetVector.magnitude;
            pickup.secondaryOffsetRotation = inverseHandRotation * pickupTransform.rotation;
        }

        public void MovePickup(CustomPickupState state)
        {
            CustomPickup pickup = state.pickup;
            Quaternion primaryHandRotation = state.primaryHandRotation;

            if (pickup.isHeldBySecondaryHand)
            {
                if (!pickup.isHeldByPrimaryHand)
                {
                    state.pickupTransform.SetPositionAndRotation(
                        state.secondaryHandPosition + state.secondaryHandRotation * pickup.secondaryOffsetVector,
                        state.secondaryHandRotation * pickup.secondaryOffsetRotation);
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

            state.pickupTransform.SetPositionAndRotation(
                state.primaryHandPosition + primaryHandRotation * pickup.primaryOffsetVector,
                primaryHandRotation * pickup.primaryOffsetRotation);
        }
    }
}
