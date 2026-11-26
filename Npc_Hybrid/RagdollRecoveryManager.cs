
using System.Collections.Generic;
using UnityEngine;

namespace BitterStudioVr
{
    [DefaultExecutionOrder(100)]
    public class RagdollRecoveryManager : MonoBehaviour
    {
        [ContextMenu("Test Activate KnockOutAndRelease")]
        private void TestActivatePhysics()
        {
            isKnockout = !isKnockout;
            SetKnockOutAndRelease(isKnockout);
        }

        private class RecoveryGroup
        {
            public RagdollImpactSensor source;
            public bool isGrab;
            public bool grabActive;

            public List<RagdollImpactSensor> bones = new List<RagdollImpactSensor>();
        }

        private readonly List<RecoveryGroup> groups =  new List<RecoveryGroup>();
        private ActiveRagdollHips ragdollHips;
        private bool isKnockout = false;
        public bool IsKnockOut => isKnockout;

        private void Awake()
        {
            ragdollHips = GetComponentInChildren<ActiveRagdollHips>();
            Debug.Log("Call SetKnockOutAndRelease for KO o Death");
        }

        public void SetKnockOutAndRelease(bool setKnockOut)
        {
            isKnockout = setKnockOut;
            ragdollHips.SetKnockOut(setKnockOut);
            //animator.speed = 0f; -> write logic.
        }


        public void RegisterImpact(RagdollImpactSensor source, RagdollImpactSensor[] extraBones)
        {
            RegisterGroup(source, extraBones, false);
        }

        public void RegisterGrab(RagdollImpactSensor source, RagdollImpactSensor[] extraBones)
        {
            RegisterGroup(source, extraBones, true);
        }

        private void RegisterGroup(RagdollImpactSensor source, RagdollImpactSensor[] extraBones, bool isGrab)
        {
            if (source == null || !source.isActiveAndEnabled)
                return;

            foreach (RecoveryGroup group in groups)
            {
                if (group.source == source && group.isGrab == isGrab)
                {
                    if (isGrab)
                        group.grabActive = true;

                    return;
                }
            }

            RecoveryGroup newGroup = new RecoveryGroup
            {
                source = source,
                isGrab = isGrab,
                grabActive = isGrab
            };

            AddBone(newGroup, source);

            if (extraBones != null)
            {
                foreach (RagdollImpactSensor bone in extraBones)
                    AddBone(newGroup, bone);
            }

            groups.Add(newGroup);
        }

        private void AddBone(RecoveryGroup group, RagdollImpactSensor bone)
        {
            if (bone == null || !bone.isActiveAndEnabled)
                return;

            if (group.bones.Contains(bone))
                return;

            group.bones.Add(bone);
            bone.AddRecoveryLock();
        }

        public void EndGrab(RagdollImpactSensor source)
        {
            foreach (RecoveryGroup group in groups)
            {
                if (group.source == source && group.isGrab)
                {
                    group.grabActive = false;
                    return;
                }
            }
        }

        private void FixedUpdate()
        {
            if (isKnockout)
                return;

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                RecoveryGroup group = groups[i];

                if (group.grabActive)
                    continue;

                bool allReady = true;

                foreach (RagdollImpactSensor bone in group.bones)
                {
                    if (bone == null || !bone.isActiveAndEnabled || !bone.IsRecoveryReady || bone.InGrab)
                    {
                        allReady = false;
                        break;
                    }
                }

                if (allReady)
                    ReleaseGroup(i);
            }
        }

        private void ReleaseGroup(int index)
        {
            RecoveryGroup group = groups[index];

            groups.RemoveAt(index);

            // First, let's release all the group's constraints.
            foreach (RagdollImpactSensor bone in group.bones)
            {
                if (bone != null)
                    bone.RemoveRecoveryLock();
            }

            // Then they recover only the bones that are not requested by other groups.
            foreach (RagdollImpactSensor bone in group.bones)
            {
                if (bone != null &&  bone.isActiveAndEnabled && bone.IsRecoveryReady && !bone.InGrab)
                {
                    bone.SetPhysicsActive(false);
                }
            }
        }

        public void UnregisterSensor(RagdollImpactSensor sensor)
        {
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i].bones.Contains(sensor))
                    ReleaseGroup(i);
            }
        }
    }
}

