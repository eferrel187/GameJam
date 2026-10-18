using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class TrapDiag
{
    [MenuItem("Juego/Diagnóstico/Medir trampas")]
    public static void Run()
    {
        var o = new List<string>();
        foreach (string sc in new[] { "Nivel1", "Nivel2", "Tutorial" })
        {
            var s = EditorSceneManager.OpenScene("Assets/00_Scenes/" + sc + ".unity", OpenSceneMode.Single);
            o.Add("== " + sc);
            foreach (var root in s.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLower();
                if (!(n.Contains("spike") || n.Contains("trap") || n.Contains("trampa"))) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                string b = "";
                if (rs.Length > 0) { Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); b = bb.min.ToString("F2") + ".." + bb.max.ToString("F2"); }
                o.Add((t.parent ? t.parent.name + "/" : "") + t.name + " active=" + t.gameObject.activeInHierarchy + " " + b + " comps=" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name)));
            }
            if (sc == "Nivel1")
            {
                DoorPhotos.Shot("trampa_n1_a", new Vector3(-1f, 2.2f, -0.9f), new Vector3(3f, 0f, -0.9f));
            }
        }
        System.IO.File.WriteAllLines("informe_trampas.txt", o);
    }
}
