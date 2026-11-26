

using UnityEngine;

namespace BitterStudioVr
{
    [RequireComponent(typeof(Rigidbody))]
    public class ActiveRagdollHips : MonoBehaviour
    {

        [Header("Bacino animato")]
        [SerializeField] private Transform animatedHips;
        private RagdollImpactSensor[] bones;

        private Rigidbody rb;
        private bool knockout;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            bones = GetComponentsInChildren<RagdollImpactSensor>(true);

            knockout = false;
            rb.isKinematic = true;
        }

        private void FixedUpdate()
        {
            if (knockout || !animatedHips)
                return;

            FollowAnimation();
        }

        private void FollowAnimation()
        {
            rb.position = animatedHips.position;
            rb.rotation = animatedHips.rotation;
        }

        public void SetKnockOut(bool active)
        {
            if (active)
            {
                knockout = true;
                rb.isKinematic = false;
                rb.useGravity = true;

            }
            else
            {
                Debug.LogWarning("Teleport - Just for Test - If You Want Active Drive and write logic for restoerd");
                knockout = false;
                rb.isKinematic = true;
                rb.useGravity = true;
            }

            foreach (var ko in bones)
            {
                ko.KnockoutAndReset(active);
            }
        }
    }
}
