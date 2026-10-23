using UdonSharp;

namespace JanSharp
{
    public abstract class CustomPickupController : UdonSharpBehaviour
    {
        public abstract void HandlePickingUp(CustomPickupPickingUpState state);
        public abstract void HandlePrimaryPickingUp(CustomPickupState state);
        public abstract void HandleSecondaryPickingUp(CustomPickupState state);
        public abstract void HandlePrimaryDropping(CustomPickupState state);
        public abstract void HandleSecondaryDropping(CustomPickupState state);
        /// <summary>
        /// <para>Modify the <see cref="CustomPickupState.pickupTransform"/> in this function.</para>
        /// </summary>
        public abstract void MovePickup(CustomPickupState state);

        /// <summary>
        /// <para>Must set <see cref="CustomPickup.attachedOffsetVector"/> and
        /// <see cref="CustomPickup.attachedOffsetRotation"/>.</para>
        /// </summary>
        /// <param name="state"></param>
        public abstract void HandleAttaching(CustomPickupAttachedState state);
        public abstract void HandleForceAttaching(CustomPickupAttachedState state);
        public abstract void HandleDetaching(CustomPickupAttachedState state);
        /// <summary>
        /// <para>Modify the <see cref="CustomPickupAttachedState.pickupTransform"/> in this function.</para>
        /// </summary>
        public abstract void MoveAttachedPickup(CustomPickupAttachedState state);
    }
}
