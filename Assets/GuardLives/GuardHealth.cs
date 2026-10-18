using System.Collections;
using UnityEngine;

public class GuardHealth : MonoBehaviour
{
    public int maxLives = 5;
    public float immunitySeconds = 2f;
    public Transform respawnPoint;
    public int Lives { get; private set; }
    Vector3 spawn; Quaternion facing; float nextHit; bool busy;
    Texture2D heart;
    void Awake() { Lives = maxLives; spawn = transform.position; facing = transform.rotation; heart = Resources.Load<Texture2D>("GuardHeart"); }
    public void Hit()
    {
        if (busy || Lives <= 0 || Time.time < nextHit) return;
        Lives--; nextHit = Time.time + immunitySeconds;
        StartCoroutine(Respawn());
    }
    IEnumerator Respawn()
    {
        busy = true;
        // Preserve whether the camera intro has already enabled movement.
        var movement = GetComponent<GuardPlayerMovement>();
        bool wasEnabled = movement != null && movement.enabled;
        if (movement != null) movement.enabled = false;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(respawnPoint ? respawnPoint.position : spawn, respawnPoint ? respawnPoint.rotation : facing);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
        foreach (var trap in FindObjectsByType<LinkedFloorTrap>(FindObjectsSortMode.None)) trap.ResetTrap();
        yield return null;
        if (movement != null) movement.enabled = Lives > 0 && wasEnabled;
        busy = false;
    }
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.GetComponentInParent<SpikeDamage>() != null) Hit();
    }
    void OnGUI()
    {
        if (!heart) return;
        var old = GUI.color;
        float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 2f);
        for (int i = 0; i < maxLives; i++) {
            GUI.color = i < Lives ? Color.white : new Color(.25f, .25f, .25f, .4f);
            GUI.DrawTexture(new Rect((18 + i * 52) * scale, 18 * scale, 46 * scale, 46 * scale), heart, ScaleMode.ScaleToFit, true);
        }
        GUI.color = old;
        if (Lives == 0) GUI.Box(new Rect(Screen.width / 2 - 120, Screen.height / 2 - 30, 240, 60), "SIN VIDAS\nDetén Play para volver a intentar");
    }
}
