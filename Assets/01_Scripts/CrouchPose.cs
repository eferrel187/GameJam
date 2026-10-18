using UnityEngine;

/// <summary>
/// Pose agachada por IK: baja la cadera, inclina el torso y mantiene los
/// pies en su sitio, así las rodillas se doblan sobre cualquier animación
/// (caminar, quieto...). Lo añade GuardWalkAnimation automáticamente.
/// Requiere "IK Pass" en la capa del Animator (lo activa
/// Juego > Configurar animaciones del jugador).
/// </summary>
[RequireComponent(typeof(Animator))]
public class CrouchPose : MonoBehaviour
{
    [Tooltip("Cuánto baja la cadera agachado, en metros reales.")]
    public float hipDrop = 0.36f;
    [Tooltip("Inclinación del torso hacia delante (grados).")]
    public float leanAngle = 22f;
    [Tooltip("Velocidad de la transición de pie <-> agachado.")]
    public float blendSpeed = 6f;

    Animator animator;
    GuardPlayerMovement movement;
    float amount;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<GuardPlayerMovement>();
    }

    void Update()
    {
        bool crouching = movement != null && movement.enabled && movement.IsCrouching;
        amount = Mathf.MoveTowards(amount, crouching ? 1f : 0f, Time.deltaTime * blendSpeed);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || amount <= 0.001f) return;

        float k = Mathf.SmoothStep(0f, 1f, amount);

        // Posición animada de los pies (antes de mover la cadera).
        Vector3 leftFoot = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 rightFoot = animator.GetIKPosition(AvatarIKGoal.RightFoot);
        Quaternion leftRot = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
        Quaternion rightRot = animator.GetIKRotation(AvatarIKGoal.RightFoot);

        // Baja la cadera e inclina el torso hacia delante.
        animator.bodyPosition -= Vector3.up * hipDrop * k;
        animator.bodyRotation =
            Quaternion.AngleAxis(leanAngle * k, transform.right) * animator.bodyRotation;

        // Los pies se quedan donde estaban: las rodillas se doblan.
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, k);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, k);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, k);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, k);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftRot);
        animator.SetIKRotation(AvatarIKGoal.RightFoot, rightRot);

        // Rodillas hacia delante.
        Vector3 forward = transform.forward * 0.6f + Vector3.up * 0.3f;
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, k);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, k);
        animator.SetIKHintPosition(AvatarIKHint.LeftKnee, leftFoot + forward);
        animator.SetIKHintPosition(AvatarIKHint.RightKnee, rightFoot + forward);
    }
}
