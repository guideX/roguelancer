using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Roguelancer;

public enum CharacterAnimationState
{
    Idle,
    WalkForward,
    WalkBackward,
    RunForward,
    StrafeLeft,
    StrafeRight,
    Jump,
}

/// <summary>Per-character state machine; no animation state is global.</summary>
public sealed class CharacterAnimationController
{
    private readonly CharacterGltfAsset _asset;
    private readonly Dictionary<CharacterAnimationState, CharacterGltfAnimationClip?> _clips;
    private readonly int _rootNodeIndex;
    private Matrix4x4[] _lastPose;
    private Matrix4x4[]? _blendFrom;
    private float _blendRemaining;

    public CharacterAnimationController(CharacterGltfAsset asset, IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> clips)
    {
        _asset = asset;
        _clips = new Dictionary<CharacterAnimationState, CharacterGltfAnimationClip?>(clips);
        _rootNodeIndex = asset.FindRootLikeNode();
        _lastPose = asset.Nodes.Select(node => node.LocalMatrix).ToArray();
    }

    public CharacterAnimationState State { get; private set; } = CharacterAnimationState.Idle;
    public float Time { get; private set; }
    public CharacterGltfAnimationClip? ActiveClip => State == CharacterAnimationState.Idle
        ? (_clips.GetValueOrDefault(CharacterAnimationState.Idle) ?? _clips.GetValueOrDefault(CharacterAnimationState.WalkForward))
        : _clips.GetValueOrDefault(State);
    public bool IsOneShot => State == CharacterAnimationState.Jump;
    public bool IsFinished => IsOneShot && (ActiveClip is null || Time >= ActiveClip.Duration - 0.0001f);
    public bool HasClip(CharacterAnimationState state) => _clips.GetValueOrDefault(state) is not null;
    public string ActiveClipName => State.ToString();

    public void SetState(CharacterAnimationState next)
    {
        if (next == State) return;
        State = next;
        Time = 0.0f;
        _blendFrom = _lastPose.ToArray();
        _blendRemaining = 0.10f;
    }

    public void Reset()
    {
        State = CharacterAnimationState.Idle;
        Time = 0.0f;
        _lastPose = _asset.Nodes.Select(node => node.LocalMatrix).ToArray();
        _blendFrom = null;
        _blendRemaining = 0.0f;
    }

    public void Update(float deltaSeconds)
    {
        CharacterGltfAnimationClip? clip = ActiveClip;
        if (clip is null)
        {
            Time = 0.0f;
            _blendRemaining = MathF.Max(_blendRemaining - deltaSeconds, 0.0f);
            return;
        }

        if (IsOneShot) Time = MathF.Min(Time + deltaSeconds, clip.Duration);
        else Time = (Time + deltaSeconds) % clip.Duration;
        _blendRemaining = MathF.Max(_blendRemaining - deltaSeconds, 0.0f);
    }

    public Matrix4x4[] EvaluateLocalPose(bool stripJumpRootVertical)
    {
        CharacterGltfAnimationClip? clip = ActiveClip;
        Matrix4x4[] target = new Matrix4x4[_asset.Nodes.Count];
        for (int i = 0; i < target.Length; i++)
            target[i] = clip?.SampleLocal(_asset.Nodes[i], i, Time, _rootNodeIndex, stripJumpRootVertical && State == CharacterAnimationState.Jump) ?? _asset.Nodes[i].LocalMatrix;

        if (_blendFrom is not null && _blendRemaining > 0.0f)
        {
            float amount = 1.0f - _blendRemaining / 0.10f;
            for (int i = 0; i < target.Length; i++) target[i] = Matrix4x4.Lerp(_blendFrom[i], target[i], amount);
        }
        else _blendFrom = null;
        _lastPose = target;
        return target;
    }

    public static bool IsStanding(CharacterAnimationState state) => state != CharacterAnimationState.Jump;
}
