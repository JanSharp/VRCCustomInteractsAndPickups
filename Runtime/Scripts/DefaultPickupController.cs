using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DefaultPickupController : CustomPickupController
    {
        [HideInInspector][SerializeField][SingletonReference] protected DefaultPickupControllerLogic defaultLogic;

        public override void HandlePickingUp(CustomPickupPickingUpState state) => defaultLogic.HandlePickingUp(state);
        public override void HandlePrimaryPickingUp(CustomPickupState state) => defaultLogic.HandlePrimaryPickingUp(state);
        public override void HandleSecondaryPickingUp(CustomPickupState state) => defaultLogic.HandleSecondaryPickingUp(state);
        public override void HandlePrimaryDropping(CustomPickupState state) => defaultLogic.HandlePrimaryDropping(state);
        public override void HandleSecondaryDropping(CustomPickupState state) => defaultLogic.HandleSecondaryDropping(state);
        public override void MovePickup(CustomPickupState state) => defaultLogic.MovePickup(state);

        public override void HandleAttaching(CustomPickupAttachedState state) => defaultLogic.HandleAttaching(state);
        public override void HandleDetaching(CustomPickupAttachedState state) => defaultLogic.HandleDetaching(state);
        public override void MoveAttachedPickup(CustomPickupAttachedState state) => defaultLogic.MoveAttachedPickup(state);
    }
}
