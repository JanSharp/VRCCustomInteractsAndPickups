using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomPickupPickingUpState : UdonSharpBehaviour
    {
        private int pickupLayerNumber;
        public const string PickupLayerNumberFieldName = nameof(pickupLayerNumber);

        #region Input

        [System.NonSerialized] public CustomPickup pickup;
        [System.NonSerialized] public Transform pickupTransform;

        [System.NonSerialized] public Vector3 handPosition;
        /// <inheritdoc cref="CustomPickupState.primaryHandRotation"/>
        [System.NonSerialized] public Quaternion handRotation;

        [System.NonSerialized] public bool hasClosestPoint;
        [System.NonSerialized] public Vector3 closestPoint;
        [System.NonSerialized] public Vector3 pickupPositionForClosestPoint;
        [System.NonSerialized] public Quaternion pickupRotationForClosestPoint;
        [System.NonSerialized] public Vector3 handPositionForClosestPoint;
        /// <inheritdoc cref="CustomPickupState.primaryHandRotation"/>
        [System.NonSerialized] public Quaternion handRotationForClosestPoint;

        public void EnsureHasClosestPoint()
        {
            if (hasClosestPoint)
                return;
            hasClosestPoint = true;
            pickupPositionForClosestPoint = pickupTransform.position;
            pickupRotationForClosestPoint = pickupTransform.rotation;
            handPositionForClosestPoint = handPosition;
            handRotationForClosestPoint = handRotation;

            float closestDistance = float.PositiveInfinity;
            closestPoint = pickupPositionForClosestPoint; // Default for when there are 0 colliders.
            foreach (Collider collider in pickupTransform.GetComponentsInChildren<Collider>())
            {
                if (collider == null) // Some VRC internal that we're not allowed to access so we get null instead,
                    continue; // even though in normal Unity... this is not possible to be null.
                if (collider.gameObject.layer != pickupLayerNumber)
                    continue;
                Vector3 point = collider.ClosestPoint(handPosition);
                float distance = Vector3.Distance(handPosition, point);
                if (distance >= closestDistance)
                    continue;
                closestDistance = distance;
                closestPoint = point;
            }
        }

        #endregion

        #region Output

        /// <summary>
        /// <para>Must write to this variable during
        /// <see cref="CustomPickupController.HandlePickingUp(CustomPickupPickingUpState)"/>. The value of
        /// this is undefined at the time of entering said function.</para>
        /// </summary>
        [System.NonSerialized] public bool shouldBecomeSecondaryHand;
        /// <inheritdoc cref="shouldBecomeSecondaryHand"/>
        [System.NonSerialized] public Vector3 heldOffsetVector;
        /// <inheritdoc cref="shouldBecomeSecondaryHand"/>
        [System.NonSerialized] public Quaternion heldOffsetRotation;

        #endregion
    }
}
