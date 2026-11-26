
using UnityEngine;

namespace BitterStudioVr
{
    [RequireComponent(typeof(Collider))]
    public class RagdollImpactActivator : MonoBehaviour
    {
        [SerializeField]private Collider triggerCollider;

        private void Awake()
        {
            if(triggerCollider == null)
                triggerCollider = GetComponent<Collider>();

            if (!triggerCollider.isTrigger)
            {
                Debug.LogError(
                    "Il Collider deve essere un Trigger.",
                    this);

                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RagdollImpactSensor sensor))
            {
                sensor.ActivatePhysics();
            }
        }
    }
}
