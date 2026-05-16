using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DefaultPickupController : CustomPickupController
    {
        [HideInInspector][SerializeField][SingletonReference] protected DefaultPickupControllerLogic defaultLogic;

        public override void HandlePickingUp(CustomPickupPickingUpState state)
        {
            defaultLogic.HandlePickingUp(state);
            if (chainedController != null)
                chainedController.HandlePickingUp(state);
        }

        public override void HandlePrimaryPickingUp(CustomPickupState state)
        {
            defaultLogic.HandlePrimaryPickingUp(state);
            if (chainedController != null)
                chainedController.HandlePrimaryPickingUp(state);
        }

        public override void HandleSecondaryPickingUp(CustomPickupState state)
        {
            defaultLogic.HandleSecondaryPickingUp(state);
            if (chainedController != null)
                chainedController.HandleSecondaryPickingUp(state);
        }

        public override void HandlePrimaryDropping(CustomPickupState state)
        {
            defaultLogic.HandlePrimaryDropping(state);
            if (chainedController != null)
                chainedController.HandlePrimaryDropping(state);
        }

        public override void HandleSecondaryDropping(CustomPickupState state)
        {
            defaultLogic.HandleSecondaryDropping(state);
            if (chainedController != null)
                chainedController.HandleSecondaryDropping(state);
        }

        public override void MovePickup(CustomPickupState state)
        {
            defaultLogic.MovePickup(state);
            if (chainedController != null)
                chainedController.MovePickup(state);
        }
    }
}
