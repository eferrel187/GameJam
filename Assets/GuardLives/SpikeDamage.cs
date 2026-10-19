using UnityEngine;
public class SpikeDamage : MonoBehaviour
{
    void OnTriggerEnter(Collider other) { Hurt(other); }
    void OnTriggerStay(Collider other) { Hurt(other); }

    void Hurt(Collider other)
    {
        GuardHealth h = other.GetComponentInParent<GuardHealth>();
        if (h == null) return;
        int before = h.Lives;
        h.HitBySpikes();
        if (h.Lives < before) GameAudio.TrampaCepo(transform.position);   // la trampa se cierra
    }
    void OnCollisionEnter(Collision collision) { collision.collider.GetComponentInParent<GuardHealth>()?.HitBySpikes(); }
}
