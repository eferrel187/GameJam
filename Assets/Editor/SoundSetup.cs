using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú  Juego > Configurar sonidos
/// Toma los audios de Assets/07_sonido, ajusta su importación y crea
/// Assets/Resources/SoundLibrary.asset (lo que usa GameAudio en el juego).
/// Si reemplazas un archivo por otro con el mismo nombre, se usa el nuevo.
/// </summary>
public static class SoundSetup
{
    const string Folder = "Assets/07_sonido";
    const string LibraryPath = "Assets/Resources/SoundLibrary.asset";

    [MenuItem("Juego/Configurar sonidos")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Debug.LogError("No existe " + Folder);
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Importación: los largos se leen poco a poco; los cortos, listos en memoria.
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter imp = (AudioImporter)AssetImporter.GetAtPath(path);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool longClip = name.StartsWith("ambiente") || name.StartsWith("terror");

            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = longClip ? AudioClipLoadType.Streaming
                       : name.StartsWith("antorcha") ? AudioClipLoadType.CompressedInMemory
                       : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.loadInBackground = longClip;
            imp.SaveAndReimport();
        }

        SoundLibrary lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }

        lib.pasos = Many("paso_");
        lib.pasosCorrer = Many("paso_correr_");
        lib.pasosAgachado = Many("paso_agachado_");
        lib.salto = One("salto");
        lib.aterrizar = One("aterrizar");
        lib.golpe = One("golpe");
        lib.muerte = One("muerte");
        lib.antorcha = One("antorcha_loop");
        lib.puerta = One("puerta_abrir");
        lib.terror = One("terror");
        lib.llave = One("llave");
        lib.trampaCepo = One("trampa_cepo");
        lib.trampaSuelo = One("trampa_suelo");
        lib.ambiente = One("ambiente_mazmorra");
        lib.manoSale = One("mano_sale");
        lib.manoAgarra = One("mano_agarra");
        lib.manoArrastra = One("mano_arrastra");

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();

        int count = lib.pasos.Length + lib.pasosCorrer.Length + lib.pasosAgachado.Length +
            new Object[] { lib.salto, lib.aterrizar, lib.golpe, lib.muerte, lib.antorcha, lib.puerta,
                lib.terror, lib.llave, lib.trampaCepo, lib.trampaSuelo, lib.ambiente,
                lib.manoSale, lib.manoAgarra, lib.manoArrastra }.Count(o => o != null);
        System.IO.File.WriteAllText("informe_sonidos.txt", "Sonidos configurados: " + count);
        Debug.Log("Sonidos configurados: " + count + " clips desde " + Folder);
    }

    static AudioClip[] Clips()
    {
        return AssetDatabase.FindAssets("t:AudioClip", new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).OrderBy(c => c.name).ToArray();
    }

    static AudioClip One(string name) => Clips().FirstOrDefault(c => c.name == name);

    /// "paso_" = paso_1, paso_2... (sin incluir paso_correr_ ni paso_agachado_).
    static AudioClip[] Many(string prefix) => Clips()
        .Where(c => c.name.StartsWith(prefix) && c.name.Length > prefix.Length && char.IsDigit(c.name[prefix.Length]))
        .ToArray();
}
