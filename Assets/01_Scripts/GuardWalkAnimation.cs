using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GuardWalkAnimation : MonoBehaviour
{
    private Animator animator;
    private CharacterController controller;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void LateUpdate()
    {
        if (animator == null) return;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        animator.speed = velocity.sqrMagnitude > 0.01f ? 1f : 0f;
    }

    private void OnDisable()
    {
        if (animator != null)
            animator.speed = 1f;
    }
}
