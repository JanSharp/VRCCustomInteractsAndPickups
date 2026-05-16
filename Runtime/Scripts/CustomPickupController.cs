using UdonSharp;

namespace JanSharp
{
    public abstract class CustomPickupController : UdonSharpBehaviour
    {
        public CustomPickupController chainedController;

        public virtual void HandlePickingUp(CustomPickupPickingUpState state)
        {
            if (chainedController != null)
                chainedController.HandlePickingUp(state);
        }

        public virtual void HandlePrimaryPickingUp(CustomPickupState state)
        {
            if (chainedController != null)
                chainedController.HandlePrimaryPickingUp(state);
        }

        public virtual void HandleSecondaryPickingUp(CustomPickupState state)
        {
            if (chainedController != null)
                chainedController.HandleSecondaryPickingUp(state);
        }

        public virtual void HandlePrimaryDropping(CustomPickupState state)
        {
            if (chainedController != null)
                chainedController.HandlePrimaryDropping(state);
        }

        public virtual void HandleSecondaryDropping(CustomPickupState state)
        {
            if (chainedController != null)
                chainedController.HandleSecondaryDropping(state);
        }

        /// <summary>
        /// <para>Modify the <see cref="CustomPickupState.pickupTransform"/> in this function.</para>
        /// </summary>
        public virtual void MovePickup(CustomPickupState state)
        {
            if (chainedController != null)
                chainedController.MovePickup(state);
        }
    }
}
