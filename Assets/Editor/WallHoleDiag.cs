using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class WallHoleDiag
{
    [MenuItem("Juego/Diagnóstico/Medir hueco pared")]
    public static void Run()
    {
        var o = new List<string>();
        foreach (string n in new[] { "Wall_Hole", "Wall_Double_Hole", "Wall_Double_Broken", "Wall" })
        {
            GameObject g = null;
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == n) g = r;
            if (!g) continue;
            bool act = g.activeSelf; g.SetActive(true);
            var mf = g.GetComponentInChildren<MeshFilter>();
            var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();
            o.Add("== " + n);
            foreach (float y in new[] { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f })
            {
                string line = "y=" + y + ": ";
                for (float x = -1.4f; x <= 1.3f; x += 0.1f)
                {
                    bool hit = mc.Raycast(new Ray(new Vector3(x, y, -0.5f), Vector3.forward), out _, 4f);
                    line += hit ? "#" : ".";
                }
                o.Add(line);
            }
            Object.DestroyImmediate(mc); g.SetActive(act);
        }
        System.IO.File.WriteAllLines("Diag/hueco.txt", o);
    }
}
