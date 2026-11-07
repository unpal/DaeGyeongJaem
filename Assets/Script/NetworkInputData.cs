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
    ItemUse,
    UseItem, //Temporary. 이쪽의 변경 사항을 무시해 주세요.
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public Vector3 AimDirection;
    public float BodyYaw;
    public NetworkButtons Buttons;
}
