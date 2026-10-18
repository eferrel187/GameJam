#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Solo en el editor: registra posición, suelo y estado de animación del
/// jugador en Diag/playlog.txt (para comprobar saltos y agacharse).
public class PlayTestLogger : MonoBehaviour
{
    static readonly StringBuilder log = new StringBuilder();
    GuardPlayerMovement mv; Animator an; CharacterController cc;
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        // Desactivado salvo que exista Diag/registrar (para no escribir siempre).
        if (!System.IO.File.Exists("Diag/registrar")) return;
        log.Clear();
        SceneManager.sceneLoaded += (s, m) => Attach();
        Attach();
    }
    static void Attach()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null && p.GetComponent<PlayTestLogger>() == null) p.AddComponent<PlayTestLogger>();
    }
    void Start()
    {
        mv = GetComponent<GuardPlayerMovement>(); an = GetComponentInChildren<Animator>(); cc = GetComponent<CharacterController>();
        log.AppendLine("== " + SceneManager.GetActiveScene().name);
    }
    void LateUpdate()
    {
        if (Time.time < next || mv == null) return;
        next = Time.time + 0.1f;
        string st = "?";
        if (an != null)
        {
            var info = an.GetCurrentAnimatorStateInfo(0);
            foreach (string n in new[] { "Idle", "Movimiento", "Agachado", "Salto", "SaltoCorriendo", "Caer", "Golpe", "Muerte" })
                if (info.IsName(n)) st = n;
            if (an.IsInTransition(0)) st += ">";
        }
        float top = transform.TransformPoint(cc.center).y + cc.height * transform.lossyScale.y * 0.5f;
        log.AppendLine(string.Format("t={0:0.0} pos=({1:0.00},{2:0.00},{3:0.00}) v={4:0.00} vy={5:0.0} g={6} crouch={7} top={8:0.00} anim={9}",
            Time.time, transform.position.x, transform.position.y, transform.position.z, new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude,
            mv.VerticalSpeed, mv.IsGrounded ? 1 : 0, mv.IsCrouching ? 1 : 0, top, st));
        if (log.Length > 4000) Flush();
    }
    void OnDestroy() { Flush(); }
    void OnApplicationQuit() { Flush(); }
    static void Flush()
    {
        System.IO.Directory.CreateDirectory("Diag");
        System.IO.File.AppendAllText("Diag/playlog.txt", log.ToString());
        log.Clear();
    }
}
#endif
