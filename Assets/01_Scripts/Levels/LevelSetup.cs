using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reglas de cada nivel, montadas por código al cargar la escena (así los
/// compañeros pueden seguir moviendo cosas en la escena sin romper nada):
///  - Todas las escenas: colisiones en paredes, suelos, soportes y arcos que
///    no las tengan (la cámara ya no atraviesa nada).
///  - Niveles con Hazard_Cylinder (2 y 3): cilindros de púas que giran y
///    matan, pared que se derrumba con E, puerta con llave, puerta de salida
///    al siguiente nivel, suelo trampa convertido en suelo normal y púas/cepos
///    que hacen daño.
/// </summary>
public static class LevelSetup
{
    static readonly string[] SolidPrefixes = { "Wall", "Support", "Arco", "Floor", "Doors_GothicArch" };
    static readonly string[] BreakableParts = { "Wall_Hole", "Wall_Double_Hole", "Wall_Double_Broken" };

    public static void Install(Scene scene)
    {
        // Historia: personaje en los niveles 5 y 6 y reglas de los niveles 4, 5 y 6.
        StoryLevels.Install(scene);

        List<GameObject> roots = scene.GetRootGameObjects().ToList();
        GameObject breakable = FindBreakableWall(roots);

        AddEnvironmentColliders(roots, breakable);

        // Sombras solo en las antorchas cercanas (rendimiento y nitidez).
        GameObject budget = new GameObject("SombrasAntorchas");
        SceneManager.MoveGameObjectToScene(budget, scene);
        budget.AddComponent<TorchShadowBudget>();

        bool hasHazards = roots.Any(r => r.name.StartsWith("Hazard_Cylinder"));
        if (!hasHazards) return;

        // Nivel 3: dos llaves. La de la derecha (Key_fake en la escena) es la
        // que abre; la de la izquierda (Key4) es la falsa.
        GameObject left = roots.FirstOrDefault(r => r.name == "Key4");
        GameObject right = roots.FirstOrDefault(r => r.name == "Key_fake");
        if (left != null && right != null)
        {
            left.name = "Llave_Falsa";
            right.name = "Key4";            // la que busca el sistema de puerta y llave
            left.AddComponent<FakeKey>();
        }

        GroupDoors(roots);

        GameObject go = new GameObject("ReglasDelNivel");
        SceneManager.MoveGameObjectToScene(go, scene);
        LevelRules rules = go.AddComponent<LevelRules>();
        rules.breakableWall = breakable;
    }

    // ------------------------------------------------------------ colisiones

    static void AddEnvironmentColliders(List<GameObject> roots, GameObject breakable)
    {
        foreach (GameObject root in roots)
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                string n = mf.gameObject.name;
                if (!SolidPrefixes.Any(p => n.StartsWith(p))) continue;
                if (BreakableParts.Any(p => n.StartsWith(p))) continue;
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                if (mf.sharedMesh.isReadable || Application.isEditor)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                else if (!n.StartsWith("Arco"))
                {
                    // Malla no legible (build): caja con su tamaño.
                    BoxCollider box = mf.gameObject.AddComponent<BoxCollider>();
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }
    }

    // ------------------------------------------------------------ pared rompible

    static GameObject FindBreakableWall(List<GameObject> roots)
    {
        foreach (GameObject wall in roots.Where(r => r.name == "Wall"))
        {
            Vector3 c = Center(wall);
            if (roots.Any(r => BreakableParts.Contains(r.name) && Vector3.Distance(Center(r), c) < 1.5f))
                return wall;
        }
        return null;
    }

    // ------------------------------------------------------------ puertas

    static void GroupDoors(List<GameObject> roots)
    {
        if (roots.Any(r => r.name == "Puerta_Llave")) return;

        GameObject l1 = roots.FirstOrDefault(r => r.name == "Doors_RoundArch_L (1)");
        GameObject r1 = roots.FirstOrDefault(r => r.name == "Doors_RoundArch_R (1)");
        GameObject l2 = roots.FirstOrDefault(r => r.name == "Doors_RoundArch_L");
        GameObject r2 = roots.FirstOrDefault(r => r.name == "Doors_RoundArch_R");

        Vector3 c1 = l1 && r1 ? (Center(l1) + Center(r1)) * 0.5f : Vector3.zero;
        Vector3 c2 = l2 && r2 ? (Center(l2) + Center(r2)) * 0.5f : Vector3.zero;

        if (l1 && r1)
        {
            Vector3 towards = l2 && r2 ? Flat(c2 - c1) : Vector3.right;
            // Abre hacia el eje perpendicular a la puerta más parecido a "towards".
            Vector3 normal = DoorNormal(l1, r1);
            if (Vector3.Dot(normal, towards) < 0f) normal = -normal;
            MakeDoor("Puerta_Llave", l1, r1, normal);
        }

        if (l2 && r2)
        {
            Vector3 normal = DoorNormal(l2, r2);
            Vector3 away = l1 && r1 ? Flat(c2 - c1) : Vector3.forward;
            if (Vector3.Dot(normal, away) < 0f) normal = -normal;
            MakeDoor("Puerta_Salida", l2, r2, normal);
        }
    }

    static Vector3 DoorNormal(GameObject a, GameObject b)
    {
        Vector3 along = Flat(Center(b) - Center(a));
        return Vector3.Cross(Vector3.up, along).normalized;
    }

    static void MakeDoor(string name, GameObject left, GameObject right, Vector3 openTowards)
    {
        GameObject group = new GameObject(name);
        SceneManager.MoveGameObjectToScene(group, left.scene);
        group.transform.position = (Center(left) + Center(right)) * 0.5f;
        left.transform.SetParent(group.transform, true);
        right.transform.SetParent(group.transform, true);
        ArchDoor door = group.AddComponent<ArchDoor>();
        door.leftLeaf = left.transform;
        door.rightLeaf = right.transform;
        door.openTowards = openTowards;
    }

    // ------------------------------------------------------------ utilidades

    public static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

    public static Bounds BoundsOf(GameObject g)
    {
        Renderer[] rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public static Vector3 Center(GameObject g) => BoundsOf(g).center;
}
