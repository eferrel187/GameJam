using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class SupportPhoto
{
    [MenuItem("Juego/Diagnóstico/Foto Support")]
    public static void Run()
    {
        var s = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        float x = 0f;
        foreach (string n in new[] { "Support_Right", "Support_Left", "Support_Center", "Wall_ArchRound" })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/" + n + ".prefab");
            var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
            g.transform.position = new Vector3(x, 0, 0); x += 5f;
        }
        DoorPhotos.Shot("supports_front", new Vector3(7.5f, 2f, -14f), new Vector3(7.5f, 2f, 0f));
        DoorPhotos.Shot("supports_angle", new Vector3(-4f, 5f, -8f), new Vector3(5f, 1.5f, 0f));
        DoorPhotos.Shot("supports_top", new Vector3(7.5f, 14f, -0.01f), new Vector3(7.5f, 0f, 0f));
        Debug.Log("ok support");
    }
}
