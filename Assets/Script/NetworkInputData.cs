using Fusion;
using UnityEngine;

public enum PlayerButtons
{
    Jump,
    Attack,
    Sprint,
    Whistle
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public NetworkButtons Buttons;
}