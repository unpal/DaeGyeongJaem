using System.Collections.Generic;
using Cinemachine;
using Fusion;
using UnityEngine;

public class PrototypeRoundView : MonoBehaviour
{
    private PrototypeRoundManager manager;
    private GameObject portal;
    private GUIStyle titleStyle;
    private GUIStyle rowStyle;
    private NetworkObject configuredCameraOwner;
    private readonly HashSet<NetworkObject> damageConfiguredPlayers = new();

    private void Awake()
    {
        manager = GetComponent<PrototypeRoundManager>();
        //BuildWorld();
    }

    private void Update()
    {
        if (manager == null || manager.Object == null)
            return;

        if (portal != null)
            portal.SetActive(manager.Phase == PrototypeRoundPhase.Playing);

        EnsurePlayerDamageComponents();

        NetworkRunner runner = manager.Runner;
        if (!runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localObject) ||
            localObject == null)
            return;

        PlayerGameState state = localObject.GetComponent<PlayerGameState>();
        PlayerMove move = localObject.GetComponent<PlayerMove>();

        if (configuredCameraOwner != localObject)
            ConfigureFirstPersonCamera(localObject);

        if (state != null && move != null && move.playerInput != null)
            move.playerInput.enabled = state.IsInPlayground &&
                                       manager.Phase == PrototypeRoundPhase.Playing;
    }

    private void ConfigureFirstPersonCamera(NetworkObject localPlayer)
    {
        configuredCameraOwner = localPlayer;

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

        Transform cameraLook = localPlayer.transform.Find("CameraLook");
        Transform virtualCameraTransform = localPlayer.transform.Find("CameraLook/Virtual Camera");

        if (cameraLook == null || virtualCameraTransform == null)
        {
            Debug.LogError("[Prototype] Capsule의 CameraLook/Virtual Camera를 찾지 못했습니다.");
            return;
        }

        CameraMove oldCameraMove = virtualCameraTransform.GetComponent<CameraMove>();
        if (oldCameraMove != null)
            oldCameraMove.enabled = false;

        cameraLook.localPosition = new Vector3(0f, 1.6f, 0f);
        virtualCameraTransform.localPosition = Vector3.zero;
        virtualCameraTransform.localRotation = Quaternion.identity;

        CinemachineVirtualCamera virtualCamera =
            virtualCameraTransform.GetComponent<CinemachineVirtualCamera>();
        if (virtualCamera != null)
        {
            virtualCamera.Priority = 100;
            virtualCamera.m_Lens.FieldOfView = 75f;
            virtualCamera.m_Lens.NearClipPlane = 0.05f;
        }

        foreach (Renderer bodyRenderer in localPlayer.GetComponentsInChildren<Renderer>(true))
            bodyRenderer.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    //private void BuildWorld()
    //{
    //    GameObject floor = CreateCube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(28f, 1f, 28f), new Color(0.16f, 0.2f, 0.24f));
    //    floor.tag = "Ground";

    //    int wallLayer = LayerMask.NameToLayer("Wall");
    //    CreateCube("WallNorth", new Vector3(0f, 2f, 14f), new Vector3(29f, 5f, 1f), Color.gray).layer = wallLayer;
    //    CreateCube("WallSouth", new Vector3(0f, 2f, -14f), new Vector3(29f, 5f, 1f), Color.gray).layer = wallLayer;
    //    CreateCube("WallEast", new Vector3(14f, 2f, 0f), new Vector3(1f, 5f, 29f), Color.gray).layer = wallLayer;
    //    CreateCube("WallWest", new Vector3(-14f, 2f, 0f), new Vector3(1f, 5f, 29f), Color.gray).layer = wallLayer;
    //    CreateCube("CenterObstacle", new Vector3(0f, 1f, 0f), new Vector3(5f, 2f, 5f), new Color(0.3f, 0.32f, 0.36f)).layer = wallLayer;

    //    GameObject lava = CreateCube("LavaDamageTest", new Vector3(8f, 0.15f, 0f), new Vector3(5f, 0.3f, 5f), new Color(1f, 0.2f, 0.02f, 0.9f));
    //    lava.GetComponent<Collider>().isTrigger = true;
    //    LavaBurn lavaBurn = lava.AddComponent<LavaBurn>();
    //    lavaBurn.tickInterval = 0.5f;
    //    lavaBurn.burnDamage = 10f;

    //    GameObject platform = CreateCube("FallDamagePlatform", new Vector3(-8f, 5f, 0f), new Vector3(6f, 0.5f, 6f), new Color(0.45f, 0.3f, 0.2f));
    //    platform.tag = "Ground";
    //    platform.layer = wallLayer;
    //    for (int i = 0; i < 5; i++)
    //    {
    //        GameObject step = CreateCube(
    //            $"FallTestStep{i + 1}",
    //            new Vector3(-3.5f - i, 0.5f + i, 4.5f),
    //            new Vector3(2f, 1f, 2f),
    //            new Color(0.4f, 0.28f, 0.18f));
    //        step.tag = "Ground";
    //        step.layer = wallLayer;
    //    }

    //    portal = CreateZone("EscapePortal", new Vector3(0f, 1.5f, 11.5f), new Vector3(4f, 3f, 1f), PrototypeZoneType.Escape, new Color(0.1f, 0.9f, 0.8f, 0.65f));
    //    CreateZone("EliminationZone", new Vector3(0f, 0.05f, -11f), new Vector3(9f, 0.1f, 4f), PrototypeZoneType.Eliminate, new Color(0.9f, 0.12f, 0.08f, 0.75f));
    //}

    private void EnsurePlayerDamageComponents()
    {
        foreach (PlayerRef playerRef in manager.Runner.ActivePlayers)
        {
            if (!manager.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) ||
                playerObject == null || damageConfiguredPlayers.Contains(playerObject))
                continue;

            damageConfiguredPlayers.Add(playerObject);
            FallDamage fallDamage = playerObject.GetComponent<FallDamage>();
            if (fallDamage == null)
                fallDamage = playerObject.gameObject.AddComponent<FallDamage>();

            fallDamage.safeHeight = 3f;
            fallDamage.damagePerMeter = 12f;
        }
    }

    private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetPositionAndRotation(position, Quaternion.identity);
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().material.color = color;
        return cube;
    }

    //private GameObject CreateZone(string name, Vector3 position, Vector3 scale, PrototypeZoneType type, Color color)
    //{
    //    GameObject zone = CreateCube(name, position, scale, color);
    //    zone.GetComponent<Collider>().isTrigger = true;
    //    PrototypeRoundZone component = zone.AddComponent<PrototypeRoundZone>();
    //    component.Initialize(manager, type);
    //    return zone;
    //}

    private void OnGUI()
    {
        if (manager == null || manager.Object == null)
            return;

        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        rowStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };

        GUI.Box(new Rect(15, 15, 315, 55 + CountPlayers() * 32), $"ROUND {manager.RoundNumber}");
        int row = 0;
        foreach (PlayerRef playerRef in manager.Runner.ActivePlayers)
        {
            PlayerGameState state = manager.GetPlayerState(playerRef);
            if (state == null) continue;
            string status = state.HasEscaped ? "탈출/관전" : state.IsDead ? "탈락/관전" : "플레이 중";
            string crowns = new string('★', Mathf.Clamp(state.Crowns, 0, 2));
            GUI.Label(new Rect(30, 48 + state.SortNum * 32, 285, 30), $"{state.DisplayName}  {status}  {crowns}", rowStyle);
            row++;
        }

        string center = GetCenterMessage();
        if (!string.IsNullOrEmpty(center))
            GUI.Label(new Rect(Screen.width / 2f - 280f, 35f, 560f, 60f), center, titleStyle);

        DrawLocalStamina();
    }

    private void DrawLocalStamina()
    {
        if (!manager.Runner.TryGetPlayerObject(
                manager.Runner.LocalPlayer,
                out NetworkObject localObject) ||
            localObject == null)
            return;

        PlayerGameState state = localObject.GetComponent<PlayerGameState>();
        PlayerCondition condition = localObject.GetComponent<PlayerCondition>();
        if (state == null || condition == null || condition.BaseMaxStamina <= 0f)
            return;

        float width = 320f;
        float baseMaximum = condition.BaseMaxStamina;
        float currentRatio = Mathf.Clamp01(state.CurrentStamina / baseMaximum);
        float maximumRatio = Mathf.Clamp01(state.MaxStamina / baseMaximum);
        Rect background = new Rect(
            Screen.width / 2f - width / 2f,
            Screen.height - 55f,
            width,
            24f);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(background, GUIContent.none);

        Rect inner = new Rect(
            background.x + 3f,
            background.y + 3f,
            background.width - 6f,
            background.height - 6f);

        GUI.color = new Color(0.25f, 0.27f, 0.3f, 1f);
        GUI.Box(new Rect(inner.x, inner.y,
            inner.width * maximumRatio, inner.height), GUIContent.none);

        GUI.color = currentRatio > 0.25f
            ? new Color(0.2f, 0.85f, 0.35f, 1f)
            : new Color(0.95f, 0.2f, 0.15f, 1f);
        GUI.Box(new Rect(inner.x, inner.y,
            inner.width * currentRatio, inner.height), GUIContent.none);

        GUI.color = new Color(0.55f, 0.08f, 0.06f, 1f);
        GUI.Box(new Rect(
            inner.x + inner.width * maximumRatio,
            inner.y,
            inner.width * (1f - maximumRatio),
            inner.height), GUIContent.none);

        GUI.color = Color.white;
        GUI.Label(new Rect(background.x, background.y - 25f, width, 24f),
            $"STAMINA {Mathf.CeilToInt(state.CurrentStamina)} / " +
            $"{Mathf.CeilToInt(state.MaxStamina)}  (BASE {Mathf.CeilToInt(baseMaximum)})");
    }

    private int CountPlayers()
    {
        int count = 0;
        foreach (PlayerRef ignored in manager.Runner.ActivePlayers)
            count++;
        return count;
    }

    private string GetCenterMessage()
    {
        switch (manager.Phase)
        {
            case PrototypeRoundPhase.Starting:
                float remaining = manager.PhaseTimer.RemainingTime(manager.Runner) ?? 0f;
                return $"라운드 시작 {Mathf.CeilToInt(remaining)}";
            case PrototypeRoundPhase.RoundEnding:
                if (manager.PendingRoundWinner == PlayerRef.None)
                    return "전원 탈락 - 왕관 지급 없음";
                PlayerGameState roundWinner = manager.GetPlayerState(manager.PendingRoundWinner);
                return roundWinner == null ? "라운드 종료" : $"{roundWinner.DisplayName} + ★";
            case PrototypeRoundPhase.MatchEnding:
                PlayerGameState finalWinner = manager.GetPlayerState(manager.FinalWinner);
                return finalWinner == null ? "경기 종료" : $"{finalWinner.DisplayName} 님 승리!";
            default:
                return string.Empty;
        }
    }
}



