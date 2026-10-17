using UnityEditor;
using UnityEngine;

public static class RestoreGuardAppearance
{
    [MenuItem("Tools/Guardia/Restaurar texturas originales")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Detén Play antes de restaurar el material.");
            return;
        }

        const string folder = "Assets/Textures/Guard/";
        AssetDatabase.Refresh();

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(
            folder + "Guard_02__diffuse.png");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        GameObject player = GameObject.Find("Player");

        if (diffuse == null || shader == null || player == null)
        {
            Debug.LogError(
                "Falta la textura diffuse, el shader URP/Lit o el objeto Player.");
            return;
        }

        string normalPath = folder + "Guard_02__normal.png";
        TextureImporter importer =
            AssetImporter.GetAtPath(normalPath) as TextureImporter;

        if (importer != null &&
            importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        Texture2D normal =
            AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        Texture2D specular = AssetDatabase.LoadAssetAtPath<Texture2D>(
            folder + "Guard_02__specular.png");

        const string materialPath = folder + "GuardOriginal.mat";
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            Undo.RecordObject(material, "Restaurar material del guardia");
            material.shader = shader;
        }

        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", diffuse);
        material.SetFloat("_Smoothness", 0.25f);

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        material.SetFloat("_WorkflowMode", 0f);
        material.EnableKeyword("_SPECULAR_SETUP");

        if (specular != null)
        {
            material.SetTexture("_SpecGlossMap", specular);
            material.SetColor("_SpecColor", Color.white);
            material.EnableKeyword("_SPECGLOSSMAP");
        }

        int count = 0;
        foreach (SkinnedMeshRenderer renderer in
                 player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Undo.RecordObject(renderer, "Asignar textura del guardia");

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            count++;
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Texturas originales asignadas a {count} malla(s). Guarda la escena.");
    }
}
