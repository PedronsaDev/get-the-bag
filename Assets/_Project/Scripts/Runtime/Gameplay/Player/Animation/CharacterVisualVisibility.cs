using System;
using System.Collections.Generic;
using Project.Combat.Presentation;
using Project.Combat.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Player.Animation
{

[DisallowMultipleComponent]
public sealed class CharacterVisualVisibility : MonoBehaviour
{
    private readonly List<BodyRendererState> _bodyStates = new();
    private CharacterRigBinding _rig;
    private AnimatorCullingMode _bodyCulling;
    private bool _isLocalOwner;
    private bool _configured;

    private readonly struct BodyRendererState
    {
        public readonly Renderer Renderer;
        public readonly bool Enabled;
        public readonly ShadowCastingMode Shadows;

        public BodyRendererState(Renderer renderer)
        {
            Renderer = renderer;
            Enabled = renderer.enabled;
            Shadows = renderer.shadowCastingMode;
        }
    }

    public void Configure(bool isLocalOwner, CharacterRigBinding rig)
    {
        if (!rig || !rig.IsBindingUsable)
            throw new InvalidOperationException("Character visibility requires a usable character rig binding.");
        if (_rig != rig)
        {
            RestoreBody();
            _bodyStates.Clear();
            _rig = rig;
            _bodyCulling = rig.BodyAnimator.cullingMode;
            foreach (var renderer in rig.BodyRenderers)
                if (renderer) _bodyStates.Add(new BodyRendererState(renderer));
        }
        _isLocalOwner = isLocalOwner;
        _configured = true;
        ApplyBody();
    }

    public void ApplyWeaponViews(WeaponInstance world, WeaponInstance firstPerson)
    {
        if (!_configured) throw new InvalidOperationException("Configure visibility before equipping weapons.");
        if (world)
        {
            if (_isLocalOwner)
            {
                foreach (var animator in world.GetComponentsInChildren<Animator>(true))
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in world.GetComponentsInChildren<Renderer>(true))
                    ApplyShadowOnly(renderer, renderer.enabled, renderer.shadowCastingMode);
                foreach (var light in world.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var audio in world.GetComponentsInChildren<AudioSource>(true)) audio.mute = true;
            }
            foreach (var flash in world.GetComponentsInChildren<WeaponMuzzleFlash>(true))
                flash.SetPresentationEnabled(!_isLocalOwner);
        }
        if (firstPerson)
        {
            var layer = LayerMask.NameToLayer("LocalViewmodel");
            if (layer < 0) throw new InvalidOperationException("FP weapons require the LocalViewmodel layer.");
            foreach (var child in firstPerson.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            foreach (var renderer in firstPerson.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var light in firstPerson.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << layer;
            foreach (var flash in firstPerson.GetComponentsInChildren<WeaponMuzzleFlash>(true))
                flash.SetPresentationEnabled(true);
        }
    }

    private void ApplyBody()
    {

        _rig.BodyAnimator.cullingMode = _isLocalOwner ? AnimatorCullingMode.AlwaysAnimate : _bodyCulling;
        foreach (var state in _bodyStates)
        {
            if (!state.Renderer) continue;
            if (_isLocalOwner) ApplyShadowOnly(state.Renderer, state.Enabled, state.Shadows);
            else
            {
                state.Renderer.enabled = state.Enabled;
                state.Renderer.shadowCastingMode = state.Shadows;
            }
        }
    }

    private static void ApplyShadowOnly(Renderer renderer, bool enabled, ShadowCastingMode shadows)
    {
        var castsMeshShadow = (renderer is MeshRenderer or SkinnedMeshRenderer) && shadows != ShadowCastingMode.Off;
        renderer.enabled = enabled && castsMeshShadow;
        if (castsMeshShadow) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    private void RestoreBody()
    {
        if (_rig && _rig.BodyAnimator) _rig.BodyAnimator.cullingMode = _bodyCulling;
        foreach (var state in _bodyStates)
        {
            if (!state.Renderer) continue;
            state.Renderer.enabled = state.Enabled;
            state.Renderer.shadowCastingMode = state.Shadows;
        }
    }

    private void OnEnable()
    {
        if (_configured) ApplyBody();
    }

    private void OnDisable() => RestoreBody();
}
}
