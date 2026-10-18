using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DoorGapDiag
{
    [MenuItem("Juego/Diagnóstico/Medir marcos de puertas")]
    public static void Run()
    {
        var o = new List<string>();
        foreach (string n in new[] { "Support_Right", "Support_Left", "Support_Center", "Support_Tall" })
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/" + n + ".prefab");
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(p);
            Bounds b = B(g);
            o.Add(n + " rot " + p.transform.rotation.eulerAngles + " scale " + p.transform.localScale + " bounds size " + b.size.ToString("F3") + " center-off " + (b.center - g.transform.position).ToString("F3") + " min-off " + (b.min - g.transform.position).ToString("F3"));
            Object.DestroyImmediate(g);
        }
        foreach (string scene in new[] { "Assets/00_Scenes/Nivel1.unity", "Assets/00_Scenes/Tutorial.unity" })
        {
            var s = EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            o.Add("=== " + scene);
            foreach (var root in s.GetRootGameObjects())
            foreach (var arco in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Arco"))
            {
                Bounds ab = B(arco.gameObject);
                o.Add("Arco de " + arco.parent.name + " " + ab.min.ToString("F2") + " .. " + ab.max.ToString("F2"));
                Bounds zone = ab; zone.Expand(new Vector3(3f, 2f, 3f));
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (r.transform.IsChildOf(arco.parent)) continue;
                    if (!zone.Intersects(r.bounds)) continue;
                    o.Add("   " + Path(r.transform) + "  " + r.bounds.min.ToString("F2") + " .. " + r.bounds.max.ToString("F2"));
                }
            }
        }
        System.IO.File.WriteAllLines("informe_marcos.txt", o);
        Debug.Log("Marcos medidos");
    }
    static string Path(Transform t) { string p = t.name; while (t.parent) { t = t.parent; p = t.name + "/" + p; } return p; }
    static Bounds B(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
}
