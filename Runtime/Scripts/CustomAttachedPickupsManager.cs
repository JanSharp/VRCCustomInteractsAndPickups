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

        public CustomPickup[] GetAllAttachedPickups()
        {
            int count = attachedPickups.Count;
            CustomPickup[] result = new CustomPickup[count];
            DataList keys = attachedPickups.GetKeys();
            for (int i = 0; i < count; i++)
                result[i] = (CustomPickup)keys[i].Reference;
            return result;
        }

        public void Start()
        {
            localPlayer = Networking.LocalPlayer;
            localPlayerId = localPlayer.playerId;
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (!player.isLocal)
                return;
            // The OnAvatarChanged appears to get raised once the avatar has finished loading, however I do
            // not trust it as I've already observed oddities around positions of bones within the
            // OnAvatarChanged event when working with the ItemSystem, thus using a 0.1 second delay here just
            // as the ItemSystem is.
            SendCustomEventDelayedSeconds(nameof(OnLocalPlayerAvatarChangedDelayed), 0.1f);
        }

        public void OnLocalPlayerAvatarChangedDelayed()
        {
            int count = attachedPickups.Count;
            DataList keys = attachedPickups.GetKeys();
            DataList values = attachedPickups.GetKeys();
            for (int i = 0; i < count; i++)
            {
                HumanBodyBones bone = (HumanBodyBones)values[i].Int;
                if (localPlayer.GetBonePosition(bone) != Vector3.zero)
                    continue;
                DetachIfAttached((CustomPickup)keys[i].Reference);
            }
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
