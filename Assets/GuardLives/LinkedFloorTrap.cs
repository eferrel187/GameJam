using System.Collections.Generic;
using UnityEngine;
public class LinkedFloorTrap : MonoBehaviour
{
    public GameObject leftFloor, rightFloor;
    public int passesBeforeCollapse = 3;
    public int CompletedPasses { get; private set; }
    readonly HashSet<FloorPassSensor> occupied = new HashSet<FloorPassSensor>();
    bool pendingExit, collapsed;
    public void Enter(FloorPassSensor sensor)
    {
        if (collapsed) return;
        bool beginning = occupied.Count == 0 && !pendingExit;
        occupied.Add(sensor); pendingExit = false;
        // First and second full passes count on exit; third entry collapses underfoot.
        if (beginning && CompletedPasses >= passesBeforeCollapse - 1) {
            collapsed = true;
            GameAudio.TrampaSuelo(sensor.transform.position);
            if (leftFloor) leftFloor.SetActive(false);
            if (rightFloor) rightFloor.SetActive(false);
            Debug.Log("Trampa: tercera pasada, ambos pisos desaparecen.");
        }
    }
    public void Exit(FloorPassSensor sensor) { occupied.Remove(sensor); if (!collapsed && occupied.Count == 0) pendingExit = true; }
    void LateUpdate()
    {
        // Wait for all trigger callbacks: moving between adjacent halves is one pass.
        if (!pendingExit || collapsed || occupied.Count != 0) return;
        pendingExit = false; CompletedPasses++;
        Debug.Log("Pasadas de la trampa: " + CompletedPasses);
    }
    public void ResetTrap()
    {
        occupied.Clear(); pendingExit = collapsed = false; CompletedPasses = 0;
        foreach (var sensor in GetComponentsInChildren<FloorPassSensor>()) sensor.Clear();
        if (leftFloor) leftFloor.SetActive(true);
        if (rightFloor) rightFloor.SetActive(true);
    }
}
