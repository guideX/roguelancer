using System;
using System.Collections.Generic;
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
    private readonly IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> _clips;
    private readonly int _rootNodeIndex;
    private readonly Matrix4x4[] _pose;
    private readonly Matrix4x4[] _sampledPose;
    private readonly Matrix4x4[] _blendFrom;
    private bool _blendActive;
    private float _blendRemaining;

    public CharacterAnimationController(CharacterGltfAsset asset, IReadOnlyDictionary<CharacterAnimationState, CharacterGltfAnimationClip?> clips)
    {
        _asset = asset;
        _clips = clips;
        _rootNodeIndex = asset.FindRootLikeNode();
        _pose = new Matrix4x4[asset.Nodes.Count];
        _sampledPose = new Matrix4x4[asset.Nodes.Count];
        _blendFrom = new Matrix4x4[asset.Nodes.Count];
        Reset();
    }

    public CharacterAnimationState State { get; private set; } = CharacterAnimationState.Idle;
    public float Time { get; private set; }
    public CharacterGltfAnimationClip? ActiveClip => GetClip(State == CharacterAnimationState.Idle
        ? (_clips.ContainsKey(CharacterAnimationState.Idle) ? CharacterAnimationState.Idle : CharacterAnimationState.WalkForward)
        : State);
    public bool IsOneShot => State == CharacterAnimationState.Jump;
    public bool IsFinished => IsOneShot && (ActiveClip is null || Time >= ActiveClip.Duration - 0.0001f);
    public bool HasClip(CharacterAnimationState state) => GetClip(state) is not null;
    public string ActiveClipName => State.ToString();

    public void SetState(CharacterAnimationState next)
    {
        if (next == State) return;
        State = next;
        Time = 0.0f;
        Array.Copy(_pose, _blendFrom, _pose.Length);
        _blendActive = true;
        _blendRemaining = 0.10f;
    }

    public void Reset(float startTime = 0.0f)
    {
        State = CharacterAnimationState.Idle;
        CharacterGltfAnimationClip? clip = ActiveClip;
        Time = clip is null || clip.Duration <= 0.0f ? 0.0f : MathF.Abs(startTime) % clip.Duration;
        for (int i = 0; i < _pose.Length; i++) _pose[i] = _asset.Nodes[i].LocalMatrix;
        _blendActive = false;
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
        for (int i = 0; i < _sampledPose.Length; i++)
            _sampledPose[i] = clip?.SampleLocal(_asset.Nodes[i], i, Time, _rootNodeIndex, stripJumpRootVertical && State == CharacterAnimationState.Jump) ?? _asset.Nodes[i].LocalMatrix;

        if (_blendActive && _blendRemaining > 0.0f)
        {
            float amount = 1.0f - _blendRemaining / 0.10f;
            for (int i = 0; i < _pose.Length; i++) _pose[i] = Matrix4x4.Lerp(_blendFrom[i], _sampledPose[i], amount);
        }
        else
        {
            Array.Copy(_sampledPose, _pose, _sampledPose.Length);
            _blendActive = false;
        }

        return _pose;
    }

    public static bool IsStanding(CharacterAnimationState state) => state != CharacterAnimationState.Jump;

    private CharacterGltfAnimationClip? GetClip(CharacterAnimationState state)
    {
        return _clips.TryGetValue(state, out CharacterGltfAnimationClip? clip) ? clip : null;
    }
}
