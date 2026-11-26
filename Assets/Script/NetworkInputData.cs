using Fusion;
using UnityEngine;

public enum PlayerButtons
{
    Jump,
    Attack,
    Sprint,
    Whistle,
    Catch,
    OpenEyes,
    Revive,
    UseItem,
    DropItem,
    ThrowItem
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public Vector3 AimDirection;
    public float BodyYaw;
    public int SelectedSlot;
    public NetworkButtons Buttons;
}
