using UnityEngine;

public class FollowBoneLocalRotation : MonoBehaviour
{
    [SerializeField] private Transform targetBone;

    private void LateUpdate()
    {
        if (targetBone == null)
            return;

        transform.localRotation = targetBone.localRotation;
    }
}

