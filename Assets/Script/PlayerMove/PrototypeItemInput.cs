using System;
using UnityEngine.InputSystem;

// Role-specific actions are separate even when their default physical keys match.
public sealed class PrototypeItemInput : IDisposable
{
    private readonly InputActionMap seeker = new InputActionMap("Seeker");
    private readonly InputActionMap evader = new InputActionMap("Evader");
    public InputAction Catch { get; }
    public InputAction OpenEyes { get; }
    public InputAction Use { get; }
    public InputAction Revive { get; }
    public InputAction Drop { get; }
    public InputAction Throw { get; }

    public PrototypeItemInput()
    {
        Catch = seeker.AddAction("Catch", InputActionType.Button, "<Mouse>/rightButton");
        OpenEyes = seeker.AddAction("OpenEyes", InputActionType.Button, "<Keyboard>/q");
        Use = evader.AddAction("UseItem", InputActionType.Button, "<Mouse>/rightButton");
        Use.AddBinding("<Keyboard>/f");
        Revive = evader.AddAction("Revive", InputActionType.Button, "<Keyboard>/e");
        Drop = evader.AddAction("Drop", InputActionType.Button, "<Keyboard>/g");
        Throw = evader.AddAction("Throw", InputActionType.Button, "<Keyboard>/v");
    }

    public void SetRole(bool isSeeker, bool blocked)
    {
        SetEnabled(seeker, isSeeker && !blocked);
        SetEnabled(evader, !isSeeker && !blocked);
    }

    private static void SetEnabled(InputActionMap map, bool enabled)
    {
        if (map.enabled == enabled) return;
        if (enabled) map.Enable(); else map.Disable();
    }

    public void Dispose() { seeker.Dispose(); evader.Dispose(); }
}
