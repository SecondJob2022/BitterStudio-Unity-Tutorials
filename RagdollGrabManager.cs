
using BitterStudioVr;
using System.Collections;
using UnityEngine;

namespace BitterStudioVr
{
    public class RagdollGrabManager : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator animator;

        [Header("Idle Configuration")]
        [SerializeField] private string idleState = "Base Layer.Idle";
        [SerializeField] private string movementParameter = "Speed";
        [SerializeField, Min(0f)]
        private float transitionTime = 0.15f;

        private int activeGrabCount;
        private float previousAnimatorSpeed = 1f;
        private Coroutine freezeCoroutine;

        public bool IsGrabbed => activeGrabCount > 0;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        public void RegisterGrab()
        {
            activeGrabCount++;

            // Only the first socket initiates the freezing process..
            if (activeGrabCount == 1 && animator != null)
            {
                previousAnimatorSpeed = animator.speed;
                freezeCoroutine = StartCoroutine(FreezeInIdle());
            }
        }

        public void UnregisterGrab()
        {
            if (activeGrabCount <= 0)
                return;

            activeGrabCount--;

            // We are waiting for all hands to have released.
            if (activeGrabCount == 0)
            {
                if (freezeCoroutine != null)
                {
                    StopCoroutine(freezeCoroutine);
                    freezeCoroutine = null;
                }

                if (animator != null)
                    animator.speed = previousAnimatorSpeed;
            }
        }

        private IEnumerator FreezeInIdle()
        {
            int idleHash = Animator.StringToHash(idleState);

            if (!animator.HasState(0, idleHash))
            {
                Debug.LogWarning(
                    $"Idle state '{idleState}' not found.", this);

                freezeCoroutine = null;
                yield break;
            }

            // Optional parameter for Blend Treese.
            if (!string.IsNullOrEmpty(movementParameter))
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == movementParameter &&
                        parameter.type == AnimatorControllerParameterType.Float)
                    {
                        animator.SetFloat(movementParameter, 0f);
                        break;
                    }
                }
            }

            // The Animator must continue to function during the transition to Idle.
            animator.CrossFadeInFixedTime(
                idleHash,
                transitionTime,
                0
            );

            yield return null;

            // Wait for the transition to end.
            float elapsed = 0f;

            while (IsGrabbed &&
                   (animator.IsInTransition(0) ||
                    animator.GetCurrentAnimatorStateInfo(0).fullPathHash != idleHash))
            {
                elapsed += Time.unscaledDeltaTime;

                if (elapsed > 2f)
                {
                    Debug.LogWarning(
                        "Unable to reach Idle before freezing.", this);

                    freezeCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            if (IsGrabbed)
                animator.speed = 0f;

            freezeCoroutine = null;
        }

        private void OnDisable()
        {
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }

            if (animator != null && activeGrabCount > 0)
                animator.speed = previousAnimatorSpeed;

            activeGrabCount = 0;
        }
    }
}

/*
public class YourScript : MonoBehaviour
{
    [SerializeField] private RagdollGrabManager grabManager;

    public void YourMethod()
    {
        if (grabManager.IsGrabbed)
            {
                // Your logic for grabbed
                return;
            }
            

        // Your normal AI logic...
    }
}

*/

