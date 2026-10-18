using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DoorPhotos
{
    [MenuItem("Juego/Diagnóstico/Fotos de puertas")]
    public static void Run()
    {
        System.IO.Directory.CreateDirectory("Diag");
        foreach (string scene in new[] { "Nivel1", "Tutorial" })
        {
            var s = EditorSceneManager.OpenScene("Assets/00_Scenes/" + scene + ".unity", OpenSceneMode.Single);
            foreach (var root in s.GetRootGameObjects())
            foreach (var arco in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Arco"))
            {
                var rs = arco.GetComponentsInChildren<Renderer>();
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                Vector3 n = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                Vector3 c = new Vector3(b.center.x, b.min.y + 1.7f, b.center.z);
                Shot(scene + "_" + arco.parent.name + "_a", c - n * 4.2f, c + Vector3.up * 0.3f);
                Shot(scene + "_" + arco.parent.name + "_b", c + n * 4.2f, c + Vector3.up * 0.3f);
                Vector3 side = Vector3.Cross(Vector3.up, n);
                Shot(scene + "_" + arco.parent.name + "_c", c - n * 2.5f + side * 1.3f, c + side * 2f);
                Shot(scene + "_" + arco.parent.name + "_d", c - n * 2.5f - side * 1.3f, c - side * 2f);
            }
        }
        Debug.Log("Fotos listas");
    }

    public static void Shot(string name, Vector3 pos, Vector3 look)
    {
        var go = new GameObject("FotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 75f; cam.nearClipPlane = 0.05f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        var rt = new RenderTexture(960, 640, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 640, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes("Diag/" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
