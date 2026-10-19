using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class SceneDump
{
    [MenuItem("Juego/Diagnóstico/Volcar niveles nuevos")]
    public static void Run()
    {
        foreach (string sc in new[] { "Nivel4", "Nivel6", "Nivel7" })
        {
            var o = new List<string>();
            string path = "Assets/00_Scenes/" + sc + ".unity";
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = false;
            if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
            foreach (var root in s.GetRootGameObjects())
                Dump(root.transform, 0, o);
            System.IO.File.WriteAllLines("Diag/dump_" + sc + ".txt", o);
            // Fotos cenitales
            Bounds all = new Bounds(); bool first = true;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            { if (r.gameObject.scene != s) continue; if (r.bounds.size.magnitude > 200) continue; if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds); }
            var go = new GameObject("FotoCam"); var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(all.size.x, all.size.z) * 0.55f;
            go.transform.SetPositionAndRotation(all.center + Vector3.up * 60f, Quaternion.Euler(90, 0, 0));
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.gray;
            // sin techo: oculta los objetos por encima de 3.5m
            var hidden = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && (r.gameObject.scene != s || r.bounds.min.y > 3.0f)) { r.enabled = false; hidden.Add(r); }
            var rt = new RenderTexture(1600, 1600, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1600, 1600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 1600), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes("Diag/top_" + sc + ".png", tex.EncodeToPNG());
            RenderTexture.active = null; float ortho = cam.orthographicSize; cam.targetTexture = null; Object.DestroyImmediate(go);
            foreach (var r in hidden) r.enabled = true;
            if (sc == "Nivel2") WallShots(s);
            if (opened) EditorSceneManager.CloseScene(s, true);
            System.IO.File.AppendAllText("Diag/dump_" + sc + ".txt", "BOUNDS " + all.min.ToString("F2") + " .. " + all.max.ToString("F2") + " ortho " + ortho + "\n");
        }
        Debug.Log("volcado");
    }
    static void WallShots(UnityEngine.SceneManagement.Scene s)
    {
        string[] names = { "Wall", "Wall_Hole", "Wall_Double_Hole", "Wall_Double_Broken" };
        var objs = new List<GameObject>();
        foreach (var root in s.GetRootGameObjects()) if (names.Contains(root.name)) objs.Add(root);
        var state = objs.Select(o => o.activeSelf).ToList();
        foreach (var keep in objs)
        {
            foreach (var o in objs) o.SetActive(o == keep);
            DoorPhotos.Shot("pared_" + keep.name, new Vector3(0.1f, 1.6f, -2.4f), new Vector3(0.1f, 1.4f, 1.2f));
        }
        for (int i = 0; i < objs.Count; i++) objs[i].SetActive(state[i]);
        DoorPhotos.Shot("pared_todas", new Vector3(0.1f, 1.6f, -2.4f), new Vector3(0.1f, 1.4f, 1.2f));
        DoorPhotos.Shot("cuarto_desde_puerta", new Vector3(1.0f, 1.7f, 1.6f), new Vector3(1.0f, 1.4f, 10f));
        DoorPhotos.Shot("cuarto_desde_llave", new Vector3(1.0f, 1.7f, 10.5f), new Vector3(1.0f, 1.4f, 1f));
        DoorPhotos.Shot("pasillo_rodillo", new Vector3(3.0f, 1.6f, -1f), new Vector3(9f, 1.6f, -1f));
        DoorPhotos.Shot("pasillo_pilar", new Vector3(-3.5f, 1.6f, -1.5f), new Vector3(2f, 1.6f, -0.5f));
    }

    static void Dump(Transform t, int d, List<string> o)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        string b = "";
        if (rs.Length > 0) { Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); b = " B[" + bb.min.ToString("F2") + ".." + bb.max.ToString("F2") + "]"; }
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)).Select(c => c.GetType().Name));
        bool missing = t.GetComponents<Component>().Any(c => c == null);
        o.Add(new string(' ', d * 2) + t.name + (t.gameObject.activeSelf ? "" : " [OFF]") + " p" + t.position.ToString("F2") + " r" + t.eulerAngles.ToString("F0") + b + (comps.Length > 0 ? " {" + comps + "}" : "") + (missing ? " MISSINGSCRIPT" : ""));
        if (d < 3 || t.childCount < 40)
            foreach (Transform c in t) Dump(c, d + 1, o);
        else o.Add(new string(' ', d * 2 + 2) + "... " + t.childCount + " hijos");
    }
}
