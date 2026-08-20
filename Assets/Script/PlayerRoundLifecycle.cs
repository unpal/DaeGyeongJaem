using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerGameState))]
[RequireComponent(typeof(PlayerCondition))]
public class PlayerRoundLifecycle : MonoBehaviour
{
    private PlayerGameState gameState;
    private PlayerCondition condition;
    private PlayerMove playerMove;
    private FallDamage fallDamage;
    private PlayerNoise playerNoise;
    private NetworkCharacterController controller;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;

    private void Awake()
    {
        gameState = GetComponent<PlayerGameState>();
        condition = GetComponent<PlayerCondition>();
        playerMove = GetComponent<PlayerMove>();
        fallDamage = GetComponent<FallDamage>();
        playerNoise = GetComponent<PlayerNoise>();
        controller = GetComponent<NetworkCharacterController>();
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;
    }

    public void ResetForRound()
    {
        ResetForRound(initialSpawnPosition, initialSpawnRotation);
    }

    public void ResetForRound(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (gameState == null || gameState.Object == null ||
            !gameState.Object.HasStateAuthority)
            return;

        gameState.ResetRoundResult();
        condition?.ResetForNextRound();
        playerMove?.ResetForNextRound();
        fallDamage?.ResetForNextRound();
        playerNoise?.RestartPeriodicNoise();

        if (controller != null)
            controller.Teleport(spawnPosition, spawnRotation);
        else
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }
}
