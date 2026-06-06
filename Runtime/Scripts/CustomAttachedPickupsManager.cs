using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp.Internal
{
    public enum DroppingHandType
    {
        /// <summary>
        /// <para>The pickup will be able to attach to both left and right arm bones.</para>
        /// </summary>
        None = 0, // Also used as an index into an array.
        /// <summary>
        /// <para>The pickup will be able to attach to right arm bones.</para>
        /// </summary>
        Left = 1, // Also used as an index into an array.
        /// <summary>
        /// <para>The pickup will be able to attach to left arm bones.</para>
        /// </summary>
        Right = 2, // Also used as an index into an array.
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CustomAttachedPickupsManager : UdonSharpBehaviour
    {
        public CustomInteractablesManager manager;
        [HideInInspector][SerializeField][SingletonReference] private BoneAttachmentManager boneAttachment;
        private VRCPlayerApi localPlayer;
        private int localPlayerId;

        // Very much copy paste, however I decided I prefer this over having setup logic in Start.

        private int[] attachableBoneValuesFromNone = new int[]
        {
            (int)HumanBodyBones.Head,
            (int)HumanBodyBones.Chest,
            (int)HumanBodyBones.Hips,
            (int)HumanBodyBones.LeftUpperArm, //
            (int)HumanBodyBones.LeftLowerArm, //
            (int)HumanBodyBones.LeftUpperLeg,
            (int)HumanBodyBones.LeftLowerLeg,
            (int)HumanBodyBones.LeftFoot,
            (int)HumanBodyBones.RightUpperArm, //
            (int)HumanBodyBones.RightLowerArm, //
            (int)HumanBodyBones.RightUpperLeg,
            (int)HumanBodyBones.RightLowerLeg,
            (int)HumanBodyBones.RightFoot,
        };

        private int[] attachableBoneValuesFromLeft = new int[]
        {
            (int)HumanBodyBones.Head,
            (int)HumanBodyBones.Chest,
            (int)HumanBodyBones.Hips,
            // (int)HumanBodyBones.LeftUpperArm, //
            // (int)HumanBodyBones.LeftLowerArm, //
            (int)HumanBodyBones.LeftUpperLeg,
            (int)HumanBodyBones.LeftLowerLeg,
            (int)HumanBodyBones.LeftFoot,
            (int)HumanBodyBones.RightUpperArm, //
            (int)HumanBodyBones.RightLowerArm, //
            (int)HumanBodyBones.RightUpperLeg,
            (int)HumanBodyBones.RightLowerLeg,
            (int)HumanBodyBones.RightFoot,
        };

        private int[] attachableBoneValuesFromRight = new int[]
        {
            (int)HumanBodyBones.Head,
            (int)HumanBodyBones.Chest,
            (int)HumanBodyBones.Hips,
            (int)HumanBodyBones.LeftUpperArm, //
            (int)HumanBodyBones.LeftLowerArm, //
            (int)HumanBodyBones.LeftUpperLeg,
            (int)HumanBodyBones.LeftLowerLeg,
            (int)HumanBodyBones.LeftFoot,
            // (int)HumanBodyBones.RightUpperArm, //
            // (int)HumanBodyBones.RightLowerArm, //
            (int)HumanBodyBones.RightUpperLeg,
            (int)HumanBodyBones.RightLowerLeg,
            (int)HumanBodyBones.RightFoot,
        };

        private int[][] attachableBoneValuesLut;

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
            attachableBoneValuesLut = new int[3][];
            attachableBoneValuesLut[(int)DroppingHandType.None] = attachableBoneValuesFromNone;
            attachableBoneValuesLut[(int)DroppingHandType.Left] = attachableBoneValuesFromLeft;
            attachableBoneValuesLut[(int)DroppingHandType.Right] = attachableBoneValuesFromRight;
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
            DataList values = attachedPickups.GetValues();
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

        public void AttachToNearestBone(CustomPickup pickup, DroppingHandType droppingHand)
        {
            // Using a colliders closest point rather than the pickup position would yield more
            // predictable results... however since I allowed having multiple colliders on a pickup this
            // kind of becomes really annoying and similarly im-performant.
            Vector3 pickupPosition = pickup.transform.position;
            int foundBoneValue = -1;
            float foundDistance = float.PositiveInfinity;
            foreach (int boneValue in attachableBoneValuesLut[(int)droppingHand])
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
            if (pickup.receivedOnDestroy)
                return;
            pickup.BeginStateModification();
            boneAttachment.AttachToBone(localPlayer, attachedToBone, pickup.transform);
            attachedPickups.Add(pickup, (int)attachedToBone);
            pickup.manager = manager;
            pickup.isAttached = true;
            pickup.attachedToBone = attachedToBone;
            pickup.DispatchOnPickupAttach();
            pickup.FinishStateModification();
        }

        public void DetachAll()
        {
            int count = attachedPickups.Count;
            DataList keys = attachedPickups.GetKeys();
            for (int i = 0; i < count; i++)
                DetachIfAttached((CustomPickup)keys[i].Reference);
        }

        public void DetachAllWhichUseDefaultModeFromManager()
        {
            int count = attachedPickups.Count;
            DataList keys = attachedPickups.GetKeys();
            for (int i = 0; i < count; i++)
            {
                CustomPickup pickup = (CustomPickup)keys[i].Reference;
                // Pickups can be forced to get attached, so do not use CanAttach here as that could detach
                // unrelated pickups.
                if (pickup.AttachmentMode == CustomPickupAttachmentMode.UseDefaultModeFromManager)
                    DetachIfAttached(pickup);
            }
        }
    }
}
