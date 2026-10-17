using System.Collections.Generic;
using UnityEngine;
public class FloorPassSensor : MonoBehaviour
{
    public LinkedFloorTrap trap;
    readonly HashSet<Collider> contacts = new HashSet<Collider>();
    void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponentInParent<GuardHealth>()) return;
        if (contacts.Add(other) && contacts.Count == 1) trap.Enter(this);
    }
    void OnTriggerExit(Collider other)
    {
        if (contacts.Remove(other) && contacts.Count == 0) trap.Exit(this);
    }
    public void Clear() { contacts.Clear(); }
    void OnDisable() { if (contacts.Count > 0 && trap) trap.Exit(this); contacts.Clear(); }
}
