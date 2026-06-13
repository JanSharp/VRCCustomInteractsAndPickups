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
        public CustomPickupAttachedState stateForController;
        public CustomPickupController fallbackPickupController;
        private VRCPlayerApi localPlayer;

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
        private DataDictionary attachedPickupsLut = new DataDictionary();
        [System.NonSerialized] public CustomPickup[] attachedPickups = new CustomPickup[ArrList.MinCapacity];
        [System.NonSerialized] public int attachedPickupsCount = 0;

        public CustomPickup[] GetAllAttachedPickups()
        {
            CustomPickup[] result = new CustomPickup[attachedPickupsCount];
            System.Array.Copy(attachedPickups, result, attachedPickupsCount);
            return result;
        }

        public void Start()
        {
            localPlayer = Networking.LocalPlayer;
            attachableBoneValuesLut = new int[3][];
            attachableBoneValuesLut[(int)DroppingHandType.None] = attachableBoneValuesFromNone;
            attachableBoneValuesLut[(int)DroppingHandType.Left] = attachableBoneValuesFromLeft;
            attachableBoneValuesLut[(int)DroppingHandType.Right] = attachableBoneValuesFromRight;
        }

        private void PopulateStateForController(CustomPickup pickup, HumanBodyBones bone)
        {
            stateForController.pickup = pickup;
            stateForController.pickupTransform = pickup.transform;
            stateForController.bonePosition = localPlayer.GetBonePosition(bone);
            stateForController.boneRotation = localPlayer.GetBoneRotation(bone);
        }

        public void DetachIfAttached(CustomPickup pickup)
        {
            if (!attachedPickupsLut.Remove(pickup, out DataToken boneToken))
                return;
            // Using this bone rather than pickup.attachedToBone
            // because the latter could have been modified by an external script.
            HumanBodyBones bone = (HumanBodyBones)boneToken.Int;

            RemoveFromAttachedPickupsList(pickup.internalAttachedIndex);

            pickup.BeginStateModification();
            PopulateStateForController(pickup, bone);
            (pickup.pickupController ?? fallbackPickupController).HandleDetaching(stateForController);
            pickup.isAttached = false;
            // Keep the attachedToBone value untouched such that scripts can continue to read what the last
            // attached bone was.
            pickup.DispatchOnPickupDetach();
            pickup.FinishStateModification();
        }

        private void RemoveFromAttachedPickupsList(int indexToRemove)
        {
            attachedPickupsCount--;
            if (indexToRemove >= attachedPickupsCount) // Micro optimization, no need to move anything if the removed index was top.
                return;
            CustomPickup top = attachedPickups[attachedPickupsCount];
            if (top == null)
                return;
            attachedPickups[indexToRemove] = top;
            top.internalAttachedIndex = indexToRemove;
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
            attachedPickupsLut.Add(pickup, (int)attachedToBone);
            pickup.internalAttachedIndex = attachedPickupsCount;
            ArrList.Add(ref attachedPickups, ref attachedPickupsCount, pickup);
            pickup.manager = manager;
            pickup.isAttached = true;
            pickup.attachedToBone = attachedToBone;
            PopulateStateForController(pickup, attachedToBone);
            (pickup.pickupController ?? fallbackPickupController).HandleAttaching(stateForController);
            pickup.DispatchOnPickupAttach();
            pickup.FinishStateModification();
        }

        public void DetachAll()
        {
            int count = attachedPickupsLut.Count;
            DataList keys = attachedPickupsLut.GetKeys();
            for (int i = 0; i < count; i++)
                DetachIfAttached((CustomPickup)keys[i].Reference);
        }

        public void DetachAllWhichUseDefaultModeFromManager()
        {
            int count = attachedPickupsLut.Count;
            DataList keys = attachedPickupsLut.GetKeys();
            for (int i = 0; i < count; i++)
            {
                CustomPickup pickup = (CustomPickup)keys[i].Reference;
                // Pickups can be forced to get attached, so do not use CanAttach here as that could detach
                // unrelated pickups.
                if (pickup.AttachmentMode == CustomPickupAttachmentMode.UseDefaultModeFromManager)
                    DetachIfAttached(pickup);
            }
        }

        public void UpdateAttachedPickups()
        {
            for (int i = attachedPickupsCount - 1; i >= 0; i--)
            {
                CustomPickup pickup = attachedPickups[i];
                if (pickup == null)
                {
                    RemoveFromAttachedPickupsList(i);
                    continue;
                }
                stateForController.pickup = pickup;
                stateForController.pickupTransform = pickup.transform;
                HumanBodyBones bone = pickup.attachedToBone;
                Vector3 bonePosition = localPlayer.GetBonePosition(bone);
                if (bonePosition == Vector3.zero) // Handles avatar switches.
                {
                    DetachIfAttached(pickup);
                    continue;
                }
                stateForController.bonePosition = bonePosition;
                stateForController.boneRotation = localPlayer.GetBoneRotation(bone);
                (pickup.pickupController ?? fallbackPickupController).MoveAttachedPickup(stateForController);
            }
        }
    }
}
