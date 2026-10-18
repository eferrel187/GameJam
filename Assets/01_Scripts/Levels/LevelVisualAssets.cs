using UnityEngine;

/// <summary>Referencias a los prefabs originales, incluidas tambien en el Build.</summary>
public class LevelVisualAssets : ScriptableObject
{
    public GameObject openBearTrap;
    public GameObject closedBearTrap;
    public GameObject catModel;
    public Material catBody;
    public Material catWhiskers;

    static LevelVisualAssets cached;
    public static LevelVisualAssets Load()
    {
        if (!cached) cached = Resources.Load<LevelVisualAssets>("LevelVisualAssets");
        return cached;
    }

    public static Bounds Measure(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer renderer in renderers)
        {
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    public static void EnsureCat(GameObject cage, Vector3 facing)
    {
        foreach (Transform child in cage.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("maxwell", System.StringComparison.OrdinalIgnoreCase)) return;
        LevelVisualAssets assets = Load();
        if (!assets || !assets.catModel)
        {
            Debug.LogWarning("Falta maxwell en los recursos de niveles. Usa Juego/Actualizar recursos de niveles 2 y 3.");
            return;
        }
        Bounds cell = Measure(cage);
        GameObject cat = Instantiate(assets.catModel, cage.transform, false);
        cat.name = "maxwell";
        cat.transform.position = cell.center;
        if (facing.sqrMagnitude > 0.001f) cat.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        foreach (Collider collider in cat.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Renderer renderer in cat.GetComponentsInChildren<Renderer>(true))
        {
            bool whiskers = renderer.name.IndexOf("whisk", System.StringComparison.OrdinalIgnoreCase) >= 0;
            Material material = whiskers ? assets.catWhiskers : assets.catBody;
            if (material) renderer.sharedMaterial = material;
        }
        Bounds b = Measure(cat);
        float scale = Mathf.Min(cell.size.x * 0.55f / Mathf.Max(0.001f, b.size.x),
            cell.size.z * 0.55f / Mathf.Max(0.001f, b.size.z),
            cell.size.y * 0.45f / Mathf.Max(0.001f, b.size.y));
        cat.transform.localScale *= scale;
        b = Measure(cat);
        cat.transform.position += new Vector3(cell.center.x - b.center.x,
            cell.min.y + cell.size.y * 0.12f - b.min.y, cell.center.z - b.center.z);
    }
}
