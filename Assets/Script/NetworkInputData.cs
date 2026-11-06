using Fusion;
using UnityEngine;

public enum PlayerButtons
{
    Jump,
    Attack,
    Sprint,
    Whistle,
    Fire,
    OpenEyes,
    Revive,
    UseItem
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public Vector3 AimDirection;
    public float BodyYaw;
    public NetworkButtons Buttons;
}
