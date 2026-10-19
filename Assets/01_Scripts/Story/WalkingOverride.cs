using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mordida de serpiente: mientras los controles están invertidos el personaje
/// camina con la animación Walking.fbx (aturdido, sin poder correr).
/// El resto del tiempo camina igual que en los demás niveles.
/// </summary>
public class WalkingOverride : MonoBehaviour
{
    Animator anim;
    GuardPlayerMovement movement;
    RuntimeAnimatorController normal;
    AnimatorOverrideController bitten;
    bool usingBitten;

    void Start()
    {
        AnimationClip walking = StoryAssets.Get.walkingClip;
        GameObject p = GameObject.Find("Player");
        anim = p ? p.GetComponentInChildren<Animator>() : null;
        movement = p ? p.GetComponent<GuardPlayerMovement>() : null;
        if (!walking || !anim || !anim.runtimeAnimatorController || !movement) { enabled = false; return; }

        normal = anim.runtimeAnimatorController;
        bitten = new AnimatorOverrideController(normal);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(bitten.overridesCount);
        bitten.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            AnimationClip original = pairs[i].Key;
            if (!original) continue;
            string n = original.name.ToLowerInvariant();
            if (n.Contains("walk") && !n.Contains("crouch") && !n.Contains("arc") && original != walking)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, walking);
        }
        bitten.ApplyOverrides(pairs);
    }

    void LateUpdate()
    {
        bool want = movement.ControlsInverted;
        if (want == usingBitten) return;
        usingBitten = want;
        anim.runtimeAnimatorController = want ? bitten : normal;
    }
}
