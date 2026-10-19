using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Menú  Juego > Configurar animaciones del jugador
///
/// 1. Configura los FBX de Mixamo de Assets/03_Animations como Humanoid
///    (así se adaptan al guardia).
/// 2. Reconstruye PlayerAnimator.controller (el que ya usa el Player en
///    todas las escenas) con:
///      Idle  <->  Movimiento (caminar / correr según Speed)
///      Salto (Jump) y salto corriendo (Jumping)
///      Golpe de trampa (Falling_Flat_Impact) y muerte (Fall_Flat)
/// Se puede ejecutar de nuevo cuando agregues animaciones.
/// </summary>
public static class PlayerAnimationSetup
{
    const string Folder = "Assets/03_Animations";
    const string ControllerPath = Folder + "/PlayerAnimator.controller";

    // Nombre del FBX -> ¿se repite en bucle?
    static readonly Dictionary<string, bool> Clips = new Dictionary<string, bool>
    {
        { "Running", true },
        { "Jump", false },
        { "Jumping", false },
        { "Fall_Flat", false },
        { "Falling_Flat_Impact", false },
        { "Idle", true },              // opcionales: si las descargas,
        { "Crouch_Idle", true },       // se usan automáticamente
        { "Crouched_Walking", true },
        { "Walk_Forward_Arc", true },  // se usa para agacharse
        { "Walking", true },           // caminar de las escenas (puertas, epílogo)
    };

    [MenuItem("Juego/Configurar animaciones del jugador")]
    public static void Setup()
    {
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

        foreach (KeyValuePair<string, bool> entry in Clips)
        {
            AnimationClip clip = ConfigureFbx(entry.Key, entry.Value);
            if (clip != null) clips[entry.Key] = clip;
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            Debug.LogError("No se encontró " + ControllerPath);
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Clip de caminar que ya tenía el controlador.
        // (Primero el clip original: los de agacharse o de las escenas también dicen "walk".)
        AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/walk.com.anim");

        if (walk == null)
            walk = sm.states
                .Select(s => s.state.motion as AnimationClip)
                .FirstOrDefault(c => c != null && c.name.ToLower().Contains("walk") &&
                                     c.name != "Walking" && c.name != "Walk_Forward_Arc" && c.name != "Crouched_Walking");

        if (walk == null)
        {
            // Si ya se ejecutó antes, el caminar está dentro del blend tree.
            foreach (ChildAnimatorState s in sm.states)
                if (s.state.motion is BlendTree tree)
                    foreach (ChildMotion child in tree.children)
                        if (child.motion is AnimationClip c && c.name.ToLower().Contains("walk"))
                            walk = c;
        }

        // ---- Caminar: pegado al suelo por los pies, sin desplazarse ni girar solo.
        if (walk != null)
        {
            AnimationClipSettings ws = AnimationUtility.GetAnimationClipSettings(walk);
            ws.loopTime = true;
            ws.loopBlendOrientation = true;
            ws.keepOriginalOrientation = true;
            ws.loopBlendPositionY = true;
            ws.keepOriginalPositionY = false;
            ws.heightFromFeet = true;
            ws.loopBlendPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(walk, ws);
            EditorUtility.SetDirty(walk);
        }

        // ---- Limpiar
        foreach (AnimatorControllerParameter p in controller.parameters.ToArray())
            controller.RemoveParameter(p);
        foreach (ChildAnimatorState s in sm.states.ToArray())
            sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // ---- Parámetros
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Grounded",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true
        });
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "CrouchRate",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

        clips.TryGetValue("Running", out AnimationClip run);
        clips.TryGetValue("Idle", out AnimationClip idleClip);

        // ---- Quieto
        AnimatorState idle = sm.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = idleClip != null ? idleClip : walk;
        // Sin animación Idle: se queda en la pose de caminar congelada.
        idle.speed = idleClip != null ? 1f : 0f;
        sm.defaultState = idle;

        // ---- Movimiento (Speed 0.5 caminar, 1 correr)
        BlendTree moveTree = new BlendTree
        {
            name = "Movimiento",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(moveTree, controller);
        moveTree.AddChild(walk, 0.25f);
        moveTree.AddChild(walk, 0.5f);
        moveTree.AddChild(run != null ? run : walk, 1f);

        AnimatorState move = sm.AddState("Movimiento", new Vector3(300, 120, 0));
        move.motion = moveTree;

        // Caminar de las escenas (Walking.fbx): se activa por código con CrossFade.
        if (clips.TryGetValue("Walking", out AnimationClip sceneWalk))
        {
            AnimatorState sceneState = sm.AddState("Caminar_Escena", new Vector3(600, -120, 0));
            sceneState.motion = sceneWalk;
        }

        Transition(idle, move, 0.2f).AddCondition(
            AnimatorConditionMode.Greater, 0.05f, "Speed");
        Transition(move, idle, 0.25f).AddCondition(
            AnimatorConditionMode.Less, 0.05f, "Speed");

        // ---- Agachado (solo si existen las animaciones)
        clips.TryGetValue("Crouch_Idle", out AnimationClip crouchIdle);
        // Agachado: "Crouched_Walking" o, si no, "Walk_Forward_Arc" (caminar
        // agachado de Mixamo). Solo si no hay ninguna, el caminar normal.
        if (!clips.TryGetValue("Crouched_Walking", out AnimationClip crouchWalk) &&
            !clips.TryGetValue("Walk_Forward_Arc", out crouchWalk))
            crouchWalk = walk;

        if (crouchIdle == null && crouchWalk != null)
        {
            // Una sola animación: se congela al estar quieto y, al moverse,
            // va al ritmo real de los pasos (CrouchRate lo calcula el script).
            AnimatorState crouch = sm.AddState("Agachado", new Vector3(600, 60, 0));
            crouch.motion = crouchWalk;
            crouch.speed = 1f;
            crouch.speedParameterActive = true;
            crouch.speedParameter = "CrouchRate";

            AnimatorStateTransition toCrouch = sm.AddAnyStateTransition(crouch);
            toCrouch.duration = 0.25f;
            toCrouch.canTransitionToSelf = false;
            toCrouch.AddCondition(AnimatorConditionMode.If, 0, "Crouch");

            // Al levantarse vuelve directo a caminar o a quieto, sin saltos.
            Transition(crouch, move, 0.25f).AddCondition(
                AnimatorConditionMode.IfNot, 0, "Crouch");
        }
        else if (crouchIdle != null || crouchWalk != null)
        {
            BlendTree crouchTree = new BlendTree
            {
                name = "Agachado",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(crouchTree, controller);
            crouchTree.AddChild(crouchIdle != null ? crouchIdle : crouchWalk, 0f);
            crouchTree.AddChild(crouchWalk != null ? crouchWalk : crouchIdle, 0.5f);

            AnimatorState crouch = sm.AddState("Agachado", new Vector3(600, 60, 0));
            crouch.motion = crouchTree;

            AnimatorStateTransition toCrouch = sm.AddAnyStateTransition(crouch);
            toCrouch.duration = 0.15f;
            toCrouch.canTransitionToSelf = false;
            toCrouch.AddCondition(AnimatorConditionMode.If, 0, "Crouch");

            Transition(crouch, idle, 0.15f).AddCondition(
                AnimatorConditionMode.IfNot, 0, "Crouch");
        }

        // ---- Saltos
        // Corriendo -> Jump.  Caminando o quieto -> Jumping.
        // ---- Caer (sin haber saltado: bordes, fosos). Pose congelada en el aire
        // del salto: nunca se ve caminar en el aire.
        AnimatorState fall = null;
        AnimationClip airClip = clips.TryGetValue("Jumping", out AnimationClip jc) ? jc
                              : clips.TryGetValue("Jump", out AnimationClip jc2) ? jc2 : null;
        if (airClip != null)
        {
            fall = sm.AddState("Caer", new Vector3(300, 360, 0));
            fall.motion = airClip;
            fall.cycleOffset = 0.55f;
            fall.speed = 0f;
            foreach (AnimatorState from in new[] { idle, move })
                Transition(from, fall, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Airborne");
            Transition(fall, move, 0.12f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        }

        // Umbral complementario: siempre entra uno de los dos.
        AddJump(sm, clips, "Jump", "SaltoCorriendo", AnimatorConditionMode.Greater, 0.749f,
            move, fall, new Vector3(150, 240, 0));
        AddJump(sm, clips, "Jumping", "Salto", AnimatorConditionMode.Less, 0.75f,
            move, fall, new Vector3(450, 240, 0));

        // Sin IK: el personaje mantiene su pose y altura normales.
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = false;
        controller.layers = layers;

        // ---- Golpe y muerte (se quedan en el suelo hasta reaparecer)
        if (clips.TryGetValue("Falling_Flat_Impact", out AnimationClip hit))
            AnyState(sm, "Golpe", hit, "Hit", new Vector3(600, 200, 0));
        if (clips.TryGetValue("Fall_Flat", out AnimationClip dead))
            AnyState(sm, "Muerte", dead, "Dead", new Vector3(600, 300, 0));

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        string summary = string.Join(", ", clips.Select(c =>
            c.Key + " (" + c.Value.length.ToString("0.00") + "s)"));
        Debug.Log("Animaciones del jugador configuradas: " + summary);
    }

    // ------------------------------------------------------------ utilidades

    static AnimationClip ConfigureFbx(string name, bool loop)
    {
        string path = Folder + "/" + name + ".fbx";
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = true;

        ModelImporterClipAnimation[] source = importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations;

        if (source.Length == 0)
        {
            Debug.LogWarning(path + " no tiene animación.");
            return null;
        }

        ModelImporterClipAnimation clip = source[0];
        clip.name = name;
        clip.loopTime = loop;
        clip.loopPose = loop;

        // Giro horneado en la pose (no gira solo).
        clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;

        // El avance de la animación (p. ej. el salto corriendo empuja el
        // cuerpo hacia delante) NO se hornea: va al root motion, que está
        // apagado. Así el cuerpo nunca se sale de su colisión ni atraviesa
        // paredes; el movimiento real lo hace GuardPlayerMovement.
        clip.lockRootPositionXZ = false;
        clip.keepOriginalPositionXZ = false;

        // Altura horneada y basada en los PIES: los pies quedan en el suelo
        // (no flota) y el salto real lo pone la física.
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = false;
        clip.heightFromFeet = true;

        importer.clipAnimations = new[] { clip };
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        return t;
    }

    static void AddJump(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips,
        string clipName, string stateName, AnimatorConditionMode speedMode, float threshold,
        AnimatorState back, AnimatorState fall, Vector3 position)
    {
        if (!clips.TryGetValue(clipName, out AnimationClip clip))
        {
            // Si falta uno de los dos saltos, usa el otro.
            clip = clips.TryGetValue("Jump", out AnimationClip a) ? a
                 : clips.TryGetValue("Jumping", out AnimationClip b) ? b : null;
            if (clip == null) return;
        }

        AnimatorState state = sm.AddState(stateName, position);
        state.motion = clip;

        // Mixamo empieza el salto agachándose para tomar impulso, pero el
        // salto del juego despega al instante: se salta ese inicio (offset)
        // y el resto se ajusta a ~0.7 s, lo que dura en el aire + aterrizar.
        const float startAt = 0.22f;
        state.speed = Mathf.Clamp(clip.length * (1f - startAt) / 0.7f, 0.8f, 2.5f);

        AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
        enter.duration = 0.12f;
        enter.offset = startAt;
        enter.canTransitionToSelf = false;
        enter.interruptionSource = TransitionInterruptionSource.None;
        enter.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        enter.AddCondition(speedMode, threshold, "Speed");

        // Al tocar el suelo vuelve a caminar/correr con una mezcla suave.
        // Sin "exit time": en cuanto toca el suelo (si no, seguía la animación
        // del salto mientras ya caminaba).
        AnimatorStateTransition exit = state.AddTransition(back);
        exit.hasExitTime = false;
        exit.duration = 0.15f;
        exit.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        // Caída larga (p. ej. saltar a un foso): pasa a la pose de caída.
        if (fall != null)
        {
            AnimatorStateTransition toFall = state.AddTransition(fall);
            toFall.hasExitTime = true;
            toFall.exitTime = 0.92f;
            toFall.duration = 0.2f;
            toFall.AddCondition(AnimatorConditionMode.If, 0, "Airborne");
        }
    }

    static void AnyState(AnimatorStateMachine sm, string stateName, AnimationClip clip,
        string trigger, Vector3 position)
    {
        AnimatorState state = sm.AddState(stateName, position);
        state.motion = clip;

        AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
        enter.duration = 0.1f;
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }
}
