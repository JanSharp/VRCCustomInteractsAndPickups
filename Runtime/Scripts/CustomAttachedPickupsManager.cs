using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomAttachedPickupsManager : UdonSharpBehaviour
    {
        public CustomInteractablesManager manager;
        [HideInInspector][SerializeField][SingletonReference] private BoneAttachmentManager boneAttachment;
        private VRCPlayerApi localPlayer;
        private int localPlayerId;

        private int[] attachableBoneValues = new int[]
        {
            (int)HumanBodyBones.Head,
            (int)HumanBodyBones.Chest,
            (int)HumanBodyBones.Hips,
            // (int)HumanBodyBones.LeftUpperArm,
            // (int)HumanBodyBones.LeftLowerArm,
            (int)HumanBodyBones.LeftUpperLeg,
            (int)HumanBodyBones.LeftLowerLeg,
            (int)HumanBodyBones.LeftFoot,
            // (int)HumanBodyBones.RightUpperArm,
            // (int)HumanBodyBones.RightLowerArm,
            (int)HumanBodyBones.RightUpperLeg,
            (int)HumanBodyBones.RightLowerLeg,
            (int)HumanBodyBones.RightFoot,
        };

        /// <summary>
        /// <para><see cref="CustomPickup"/> pickup => <see cref="int"/> (<see cref="HumanBodyBones"/>) bone</para>
        /// </summary>
        private DataDictionary attachedPickups = new DataDictionary();

        public void Start()
        {
            localPlayer = Networking.LocalPlayer;
            localPlayerId = localPlayer.playerId;
        }

        public void DetachIfAttached(CustomPickup pickup)
        {
            if (!attachedPickups.Remove(pickup, out DataToken bone))
                return;
            pickup.BeginStateModification();
            boneAttachment.DetachFromBone(localPlayerId, (HumanBodyBones)bone.Int, pickup.transform);
            pickup.isAttached = false;
            // Keep the attachedToBone value untouched such that scripts can continue to read what the last
            // attached bone was.
            pickup.DispatchOnPickupDetach();
            pickup.FinishStateModification();
        }

        public void AttachToNearestBone(CustomPickup pickup)
        {
            // Using a colliders closest point rather than the pickup position would yield more
            // predictable results... however since I allowed having multiple colliders on a pickup this
            // kind of becomes really annoying and similarly im-performant.
            Vector3 pickupPosition = pickup.transform.position;
            int foundBoneValue = -1;
            float foundDistance = float.PositiveInfinity;
            foreach (int boneValue in attachableBoneValues)
            {
                Vector3 bonePosition = localPlayer.GetBonePosition((HumanBodyBones)boneValue);
                if (bonePosition == Vector3.zero)
                    continue;
                float distance = Vector3.Distance(pickupPosition, bonePosition);
                if (distance >= foundDistance)
                    continue;
                foundBoneValue = boneValue;
                foundDistance = distance;
            }
            if (float.IsInfinity(foundDistance))
                return;
            AttachToBone(pickup, (HumanBodyBones)foundBoneValue);
        }

        public void AttachToBone(CustomPickup pickup, HumanBodyBones attachedToBone)
        {
            pickup.BeginStateModification();
            boneAttachment.AttachToBone(localPlayer, attachedToBone, pickup.transform);
            attachedPickups.Add(pickup, (int)attachedToBone);
            pickup.manager = manager;
            pickup.isAttached = true;
            pickup.attachedToBone = attachedToBone;
            pickup.DispatchOnPickupAttach();
            pickup.FinishStateModification();
        }
    }
}
