using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Completa las referencias tras importar, sin reconstruir escenas.</summary>
public class LevelVisualAssetsSetup : AssetPostprocessor
{
    const string AssetPath = "Assets/Resources/LevelVisualAssets.asset";

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
    {
        if (!didDomainReload && !importedAssets.Any(p => p == AssetPath || p.EndsWith(".mat")
            || p.EndsWith("maxwell.fbx"))) return;
        EditorApplication.delayCall -= UpdateResources;
        EditorApplication.delayCall += UpdateResources;
    }

    [MenuItem("Juego/Actualizar recursos de niveles 2 y 3")]
    public static void UpdateResources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        LevelVisualAssets assets = AssetDatabase.LoadAssetAtPath<LevelVisualAssets>(AssetPath);
        if (!assets) return;
        bool changed = false;
        if (!assets.openBearTrap)
        {
            assets.openBearTrap = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/BearTrap_Open.prefab");
            changed |= assets.openBearTrap != null;
        }
        if (!assets.closedBearTrap)
        {
            assets.closedBearTrap = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04_Prefabs/BearTrap_Closed.prefab");
            changed |= assets.closedBearTrap != null;
        }
        if (!assets.catModel)
        {
            assets.catModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05_models/maxwell.fbx");
            changed |= assets.catModel != null;
        }
        if (!assets.catBody)
        {
            assets.catBody = FindMaterial("Dingus");
            changed |= assets.catBody != null;
        }
        if (!assets.catWhiskers)
        {
            assets.catWhiskers = FindMaterial("Whiskers");
            changed |= assets.catWhiskers != null;
        }
        if (changed)
        {
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }
    }

    static Material FindMaterial(string materialName)
    {
        string[] paths = AssetDatabase.FindAssets(materialName + " t:Material", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p.Contains("/06_textura/") ? 0 : 1).ToArray();
        foreach (string path in paths)
            foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                if (material.name.Equals(materialName, System.StringComparison.OrdinalIgnoreCase)) return material;
        return null;
    }
}
