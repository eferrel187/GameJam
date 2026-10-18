using UnityEngine;
public class SpikeDamage : MonoBehaviour
{
    void OnTriggerEnter(Collider other) { other.GetComponentInParent<GuardHealth>()?.Hit(); }
    void OnTriggerStay(Collider other) { other.GetComponentInParent<GuardHealth>()?.Hit(); }
    void OnCollisionEnter(Collision collision) { collision.collider.GetComponentInParent<GuardHealth>()?.Hit(); }
}
