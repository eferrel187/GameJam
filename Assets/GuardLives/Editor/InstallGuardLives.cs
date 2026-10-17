using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class InstallGuardLives : EditorWindow
{
    GameObject player, left, right, spikes;
    [MenuItem("GameJam/Instalar corazones y trampa")]
    static void Open() { GetWindow<InstallGuardLives>("Vidas y trampa").Show(); }
    void OnEnable()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
            string n = t.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (n == "player") player = t.gameObject;
            if (n.Contains("floorizq") || n.Contains("floorzq")) left = t.gameObject;
            if (n.Contains("floorder")) right = t.gameObject;
            if (n == "spikesplatform") spikes = t.gameObject;
        }
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Arrastra los objetos de Hierarchy. Guarda la escena antes de instalar. Solo una pareja de pisos por instalación.", MessageType.Info);
        player = (GameObject)EditorGUILayout.ObjectField("Player", player, typeof(GameObject), true);
        left = (GameObject)EditorGUILayout.ObjectField("trap floorIzq", left, typeof(GameObject), true);
        right = (GameObject)EditorGUILayout.ObjectField("trap floorDer", right, typeof(GameObject), true);
        spikes = (GameObject)EditorGUILayout.ObjectField("Spikes_Platform", spikes, typeof(GameObject), true);
        using (new EditorGUI.DisabledScope(!player || !left || !right || !spikes || EditorApplication.isPlaying))
            if (GUILayout.Button("Instalar en esta escena")) Install();
    }
    static Bounds GetBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(target.transform.position, Vector3.zero);
        if (renderers.Length > 0) { b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); }
        else foreach (var c in target.GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
        return b;
    }
    static GameObject Sensor(string name, Transform parent, Bounds b)
    {
        var obj = new GameObject(name); Undo.RegisterCreatedObjectUndo(obj, "Instalar vidas");
        obj.transform.SetParent(parent, true); obj.transform.position = b.center;
        var box = obj.AddComponent<BoxCollider>(); box.size = b.size; box.isTrigger = true;
        var rb = obj.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        return obj;
    }
    void Install()
    {
        if (FindFirstObjectByType<LinkedFloorTrap>() != null) { EditorUtility.DisplayDialog("Ya instalado", "Ya hay una trampa instalada. No se duplicó el sistema.", "OK"); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        if (!player.GetComponent<GuardHealth>()) Undo.AddComponent<GuardHealth>(player);
        if (!spikes.GetComponent<SpikeDamage>()) Undo.AddComponent<SpikeDamage>(spikes);
        var root = new GameObject("SistemaVidasTrampa"); Undo.RegisterCreatedObjectUndo(root, "Instalar vidas");
        var trap = root.AddComponent<LinkedFloorTrap>(); trap.leftFloor = left; trap.rightFloor = right;
        foreach (var floor in new[] { left, right }) {
            Bounds b = GetBounds(floor);
            b.center = new Vector3(b.center.x, b.max.y + .65f, b.center.z);
            b.size = new Vector3(Mathf.Max(.1f, b.size.x), 1.3f, Mathf.Max(.1f, b.size.z));
            var sensor = Sensor("Detector_" + floor.name, root.transform, b).AddComponent<FloorPassSensor>(); sensor.trap = trap;
        }
        Bounds sb = GetBounds(spikes); sb.Expand(new Vector3(.02f, .12f, .02f));
        Sensor("Detector_Pinchos", root.transform, sb).AddComponent<SpikeDamage>();
        EditorSceneManager.MarkSceneDirty(player.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("Listo", "5 corazones instalados. Guarda la escena con Command+S. Revisa que los detectores cubran solamente los pisos y los pinchos.", "OK");
    }
}
