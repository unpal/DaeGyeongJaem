using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(GameManager))]
public class GameFlowTestController : MonoBehaviour
{
    [SerializeField] private bool autoAdvance = true;
    [SerializeField] private float autoAdvanceDelay = 8f;

    private GameManager gameManager;
    private Coroutine autoAdvanceCoroutine;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        DisableOfflinePlayer();
    }

    private void Update()
    {
        if (gameManager.Object == null ||
            !gameManager.Object.HasStateAuthority ||
            gameManager.Phase != RoundPhase.Playing)
            return;

        if (autoAdvance && autoAdvanceCoroutine == null)
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());

        if (Input.GetKeyDown(KeyCode.F1))
            ReportFirstActivePlayer(true);

        if (Input.GetKeyDown(KeyCode.F2))
            ReportFirstActivePlayer(false);
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);

        if (gameManager.Phase == RoundPhase.Playing &&
            gameManager.Object.HasStateAuthority)
        {
            PlayerGameState[] players =
                FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);

            Debug.Log($"[Flow Test] 자동 탈출 처리, 플레이어 수: {players.Length}");

            foreach (PlayerGameState player in players)
            {
                if (player.IsInPlayground)
                    gameManager.ReportPlayerEscaped(player);
            }
        }

        autoAdvanceCoroutine = null;
    }

    private void ReportFirstActivePlayer(bool escaped)
    {
        PlayerGameState[] players =
            FindObjectsByType<PlayerGameState>(FindObjectsSortMode.None);

        foreach (PlayerGameState player in players)
        {
            if (!player.IsInPlayground)
                continue;

            if (escaped)
                gameManager.ReportPlayerEscaped(player);
            else
                gameManager.ReportPlayerDied(player);

            return;
        }
    }

    private void DisableOfflinePlayer()
    {
        GameObject offlinePlayer = GameObject.Find("Player");

        if (offlinePlayer == null ||
            offlinePlayer.GetComponent<NetworkObject>() != null)
            return;

        offlinePlayer.SetActive(false);
        Debug.Log("[Flow Test] DotoriScene의 로컬 Player를 비활성화했습니다.");
    }
}
