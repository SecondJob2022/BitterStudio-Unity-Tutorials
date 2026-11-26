using UnityEngine;

namespace BitterStudioVr
{
    [RequireComponent(typeof(ConfigurableJoint))]
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollImpactSensor : MonoBehaviour
    {
        [ContextMenu("Test Activate Physics")]
        private void TestActivatePhysics()
        {
            ActivatePhysics();
        }


        [Header("Bones of the animate model")]
        [SerializeField] private Transform targetAnimationBone;
        [SerializeField] private Transform targetConnectedAnimationBone;

        [Header("Extra bones to trigger impact")]
        [SerializeField] private RagdollImpactSensor[] additionalBones;

        [Header("Physical duration")]
        [SerializeField, Min(0f)] private float physicsDuration = 0.5f;

        [Header("Strength for the pose")]
        [SerializeField, Min(0f)] private float spring = 1000f;
        [SerializeField, Min(0f)] private float damper = 100f;
        [SerializeField, Min(0f)] private float maximumForce = 10000f;

        [Header("Animation return parameters")]
        [SerializeField, Min(0f)] private float recoveryAngle = 10f;
        [SerializeField, Min(0f)] private float recoveryAngularSpeed = 0.99f;
        [SerializeField, Min(0f)] private float recoveryStableTime = 0.15f;
        [SerializeField, Min(0f)] private float recoveryDistance = 0.05f;
        [SerializeField, Min(0f)] private float recoveryLinearSpeed = 0.25f;

        private ConfigurableJoint joint;
        private Rigidbody rb;

        private Quaternion initialRelativeRotation;
        private Quaternion jointSpace;

        private bool physicsActive;
        private bool recoveryRequested;
        private bool timedActivation;

        private float recoveryTimer;
        private float physicsTimer;

        public bool IsPhysicsActive => physicsActive;


        //Grab
        private RagdollGrabManager grabManager;
        private int grabCount;
        public bool InGrab => grabCount > 0;


        //Manager
        private RagdollRecoveryManager recoveryManager;

        private int groupRecoveryLocks;
        private bool recoveryReady;

        private int directGrabCount;

        internal bool IsRecoveryReady => physicsActive && recoveryReady;

        internal void AddRecoveryLock()
        {
            groupRecoveryLocks++;
        }

        internal void RemoveRecoveryLock()
        {
            groupRecoveryLocks = Mathf.Max(0, groupRecoveryLocks - 1);
        }

        private void Awake()
        {

            joint = GetComponent<ConfigurableJoint>();
            rb = GetComponent<Rigidbody>();

            if (!targetAnimationBone || !targetConnectedAnimationBone || !joint.connectedBody)
            {
                Debug.LogError("Assign the animated bones and the Connected Body.", this);

                enabled = false;
                return;
            }

            if (joint.configuredInWorldSpace || joint.swapBodies)
            {
                Debug.LogError("Turn off Configured In World Space and Swap Bodies.", this);

                enabled = false;
                return;
            }

            initialRelativeRotation = Quaternion.Inverse(targetConnectedAnimationBone.rotation) * targetAnimationBone.rotation;

            Vector3 right = joint.axis.normalized;

            Vector3 forward = Vector3.Cross(right, joint.secondaryAxis).normalized;

            Vector3 up = Vector3.Cross(forward, right).normalized;

            jointSpace = Quaternion.LookRotation(forward, up);

            joint.rotationDriveMode = RotationDriveMode.XYAndZ;

            physicsActive = false;
            rb.isKinematic = true;

            grabManager = GetComponentInParent<RagdollGrabManager>();
            recoveryManager = GetComponentInParent<RagdollRecoveryManager>();

            DisableDrives();
        }


        private void FixedUpdate()
        {
            if (recoveryManager.IsKnockOut)
                return;


            if (!physicsActive)
            {
                FollowAnimation();
                return;
            }

            // Joint Drives are always active.
            UpdateJointTarget();

            if (InGrab)
            {
                recoveryRequested = false;
                recoveryTimer = 0f;
                recoveryReady = false;
            }
            else if (timedActivation)
            {
                physicsTimer -= Time.fixedDeltaTime;

                if (physicsTimer <= 0f)
                {
                    physicsTimer = 0f;
                    timedActivation = false;
                    TryReturnToAnimation();
                }
            }

            if (recoveryRequested && !InGrab)
                UpdateRecovery();
        }



        private void FollowAnimation()
        {
            rb.position = targetAnimationBone.position;
            rb.rotation = targetAnimationBone.rotation;
        }


        private void UpdateJointTarget()
        {
            JointDrive drive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maximumForce
            };

            joint.angularXDrive = drive;
            joint.angularYZDrive = drive;

            Quaternion targetRelativeRotation = Quaternion.Inverse(targetConnectedAnimationBone.rotation) * targetAnimationBone.rotation;

            joint.targetRotation = Quaternion.Inverse(jointSpace) * Quaternion.Inverse(targetRelativeRotation) * initialRelativeRotation * jointSpace;
        }

        // Called by the weapon or hand trigger.
        public void ActivatePhysics()
        {
            // Registra il gruppo da recuperare insieme.
            recoveryManager?.RegisterImpact(this, additionalBones);

            ActivatePhysicsFor(physicsDuration);

            foreach (RagdollImpactSensor bone in additionalBones)
            {
                if (bone != null && bone != this)
                {
                    bone.ActivatePhysicsFor(physicsDuration);
                }
                    
            }
        }

        // Enable physics for a specific duration.
        public void ActivatePhysicsFor(float duration)
        {
            if (!enabled)
                return;

            duration = Mathf.Max(0f, duration);

            if (!physicsActive)
                SetPhysicsActive(true);

            recoveryRequested = false;
            recoveryTimer = 0f;
            recoveryReady = false;

            physicsTimer = duration;
            timedActivation = true;
        }

        public void SetPhysicsActive(bool active)
        {
            if (!active && (InGrab || groupRecoveryLocks > 0))
                return;


            if (!enabled)
                return;

            recoveryRequested = false;
            recoveryTimer = 0f;
            recoveryReady = false;

            physicsTimer = 0f;
            timedActivation = false;

            if (active == physicsActive)
                return;

            physicsActive = active;

            if (active)
            {

                FollowAnimation();
                rb.isKinematic = false;
                UpdateJointTarget();
            }
            else
            {
                rb.isKinematic = true;
                DisableDrives();
                FollowAnimation();
            }
        }

        public void TryReturnToAnimation()
        {
            if (!physicsActive)
                return;

            recoveryRequested = true;
            recoveryTimer = 0f;
            recoveryReady = false;

            timedActivation = false;
            physicsTimer = 0f;
        }




        private void UpdateRecovery()
        {
            float angle = Quaternion.Angle(
                rb.rotation,
                targetAnimationBone.rotation
            );

            float angularSpeed = rb.angularVelocity.magnitude;

            float distance = Vector3.Distance(
                rb.position,
                targetAnimationBone.position
            );

            float linearSpeed = rb.linearVelocity.magnitude;

            if (angle <= recoveryAngle &&
                angularSpeed <= recoveryAngularSpeed &&
                distance <= recoveryDistance && 
                linearSpeed <= recoveryLinearSpeed)
            {

                recoveryTimer += Time.fixedDeltaTime;

                if (recoveryTimer >= recoveryStableTime)
                {
                    recoveryReady = true;

                    // No active group: autonomous recovery.
                    if (groupRecoveryLocks == 0)
                        SetPhysicsActive(false);

                    
                }
            }
            else
            {
                recoveryTimer = 0f;
                recoveryReady = false;
            }
        }




        // Call from events in the Grab inspector for Virtual Reality
        public void OnGrabbed()
        {
            if (!isActiveAndEnabled)
                return;

            if (directGrabCount == 0)
                recoveryManager?.RegisterGrab(this, additionalBones);

            directGrabCount++;

            grabManager?.RegisterGrab();

            grabCount++;
            ActivatePhysicsFor(physicsDuration);

            foreach (RagdollImpactSensor bone in additionalBones)
            {
                if (bone != null && bone != this)
                {
                    bone.grabCount++;
                    bone.ActivatePhysicsFor(physicsDuration);
                }
            }
        }

        // Call from events in the Grab inspector for Virtual Reality
        public void OnReleased()
        {
            if (directGrabCount <= 0)
                return;

            directGrabCount--;

            grabManager?.UnregisterGrab();

            grabCount = Mathf.Max(0, grabCount - 1);

            if (grabCount == 0)
                ActivatePhysicsFor(physicsDuration);

            foreach (RagdollImpactSensor bone in additionalBones)
            {
                if (bone != null && bone != this && bone.grabCount > 0)
                {
                    bone.grabCount--;

                    if (bone.grabCount == 0)
                        bone.ActivatePhysicsFor(physicsDuration);
                }
            }

            if (directGrabCount == 0)
                recoveryManager?.EndGrab(this);
        }


        private void DisableDrives()
        {
            joint.angularXDrive = new JointDrive();
            joint.angularYZDrive = new JointDrive();
        }

        private void OnDisable()
        {
            recoveryManager?.UnregisterSensor(this);

            if (joint)
                DisableDrives();
        }



        public void KnockoutAndReset(bool inKnockout)
        {
            physicsActive = inKnockout;

            
            recoveryRequested = false;
            recoveryReady = false;
            timedActivation = false;

            recoveryTimer = 0f;
            physicsTimer = 0f;

            
            DisableDrives();

            if (inKnockout)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            else
            {
                Debug.LogWarning("Teleport - Just for Test - If You Want Active Drive and write logic for restoerd");
                rb.isKinematic = true;
                FollowAnimation();
            }
        }



        //Test method 
        public string GetRecoveryDebug()
        {
            if (rb == null || targetAnimationBone == null)
                return "NOT INITIALIZED";

            float angle = Quaternion.Angle(
                rb.rotation, targetAnimationBone.rotation);

            float distance = Vector3.Distance(
                rb.position, targetAnimationBone.position);

            return
                $"{name}\n" +
                $"Kinematic: {rb.isKinematic}\n" +
                $"Grab: {InGrab}\n" +
                $"Timer: {physicsTimer:F2}\n" +
                $"Angle: {angle:F1} / {recoveryAngle:F1}\n" +
                $"Distance: {distance * 100f:F1} / {recoveryDistance * 100f:F1} cm\n" +
                $"AngularSpeed: {rb.angularVelocity.magnitude:F2} / {recoveryAngularSpeed:F2}\n" +
                $"LinearSpeed: {rb.linearVelocity.magnitude:F2} / {recoveryLinearSpeed:F2}\n";
        }
    }
}

