using System;
using System.Collections;
using Fusion;
using Script.sound;
using Script.UI;
using UnityEngine;

namespace Script.dotori
{
    public class SequentialPlayer : NetworkBehaviour
    {
        [Header("Managers")]
        public GameManager gameManager; // 게임 매니저 참조

        [Header("Prologue Settings")]
        [SerializeField] private float prologuePanelFade = 1.0f; // 대사 종료 후 패널 페이드아웃 시간
        [SerializeField] private AudioSource prologueAudioSource;
        [SerializeField] private AudioClip prologueEndAudio; // 프롤로그 종료 효과음

        [Header("Chaser Settings")]
        public GameObject chaserPrefab; // 추격자 프리팹
        public Transform chaserSpawnPoint; // 추격자 생성 위치, 방향

        [Header("Environment References")]
        public PortalManager portalManager; // 포탈 관리자

        [Header("Timeline Timers (Seconds)")]
        [SerializeField] private float countdownSeconds = 10f; // 카운트다운 시간
        [SerializeField] private float portalSpawnTime = 30f; // 포탈 활성화 시간
        [SerializeField] private float endgameTimer = 60f; // 추격자가 플레이어 위치를 알게 되는 시간

        private SoundFollowingAgent spawnedChaser; // 생성된 추격자 컴포넌트
        private Coroutine sequenceCoroutine;
        private float timer;

        public SoundFollowingAgent SpawnedChaser => spawnedChaser;
        public float TotalDuration => endgameTimer;

        public override void Spawned()
        {
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();
        }

        /// <summary>
        /// 시퀀스를 시작합니다. 1라운드인 경우 Phrases.csv의 Prologue_1, 2... 다이얼로그를 먼저 재생합니다.
        /// </summary>
        public void StartSequence(bool isFirstRound = false)
        {
            if (!Object.HasStateAuthority)
                return;

            StopSequence();
            sequenceCoroutine = StartCoroutine(GameFlowRoutine(isFirstRound));
        }

        public void StopSequence()
        {
            if (!Object.HasStateAuthority)
                return;

            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }

            // 생성했던 추격자 직접 정리 (Despawn)
            if (spawnedChaser != null)
            {
                NetworkObject chaserObject = spawnedChaser.GetComponent<NetworkObject>();
                if (chaserObject != null && chaserObject.IsValid)
                {
                    Runner.Despawn(chaserObject);
                }
                spawnedChaser = null;
            }

            // 포탈 비활성화
            if (portalManager != null)
            {
                portalManager.RpcDeactivateAllPortals();
            }

            // 사운드 및 UI 정리
            RpcStopBGM();
            if (DialogueController.Instance != null)
            {
                DialogueController.Instance.RpcClearAll();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopSequence();
        }

        private IEnumerator GameFlowRoutine(bool isFirstRound)
        {
            if (!Object.HasStateAuthority)
                yield break;

            // 0. 1라운드 초회 프롤로그 다이얼로그 재생 (Phrases.csv의 Prologue_1, Prologue_2... 자동 순회)
            // (프롤로그 동안 GameManager.Phase는 Starting 상태를 유지합니다)
            if (isFirstRound && PhraseTable.TryGet("Prologue_1", out PhraseCue firstCue))
            {
                if (DialogueController.Instance != null)
                {
                    DialogueController.Instance.RpcShowPanel(0f, 9999f, 0f); // 검은 패널 유지
                }

                int prologueIndex = 1;
                while (PhraseTable.TryGet($"Prologue_{prologueIndex}", out PhraseCue cue))
                {
                    if (DialogueController.Instance != null)
                    {
                        DialogueController.Instance.RpcShowTitle(
                            cue.titleType,
                            cue.fadeIn,
                            cue.duration,
                            cue.fadeOut,
                            cue.text);
                    }

                    yield return new WaitForSeconds(cue.fadeIn + cue.duration + cue.fadeOut);
                    prologueIndex++;
                }

                // 프롤로그 종료 효과음 재생 및 패널 페이드아웃
                RpcPlayPrologueEndSound();
                if (DialogueController.Instance != null)
                {
                    DialogueController.Instance.RpcShowPanel(0f, 0f, prologuePanelFade);
                }

                yield return new WaitForSeconds(prologuePanelFade);
            }

            // 1. 카운트다운 (0초 ~ countdownSeconds)
            // (카운트다운 동안도 GameManager.Phase는 Starting 상태입니다)
            timer = 0f;
            RpcPlayBGM(false); // 게임 시작 시 기본 BGM 재생

            int lastCountdownNumber = -1;
            while (timer < countdownSeconds)
            {
                timer += Time.deltaTime;
                int currentCountdown = Mathf.CeilToInt(countdownSeconds - timer);

                if (currentCountdown != lastCountdownNumber && currentCountdown > 0)
                {
                    lastCountdownNumber = currentCountdown;
                    if (DialogueController.Instance != null)
                    {
                        DialogueController.Instance.RpcShowTitle(
                            TitleType.CenterText,
                            0.05f,
                            0.85f,
                            0.1f,
                            currentCountdown.ToString());
                    }
                }

                yield return null;
            }

            // 2. 카운트다운 완료 -> 본격적인 플레이(Playing) 시작 통보
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();

            if (gameManager != null)
            {
                gameManager.OnGameplayStarted();
            }

            // 3. 추격자 생성 (10초 시점)
            if (chaserPrefab != null)
            {
                Vector3 spawnPosition = chaserSpawnPoint != null ? chaserSpawnPoint.position : Vector3.zero;
                Quaternion spawnRotation = chaserSpawnPoint != null ? chaserSpawnPoint.rotation : Quaternion.identity;

                NetworkObject chaserNetworkPrefab = chaserPrefab.GetComponent<NetworkObject>();
                if (chaserNetworkPrefab == null)
                {
                    Debug.LogError("[SequentialPlayer] Chaser Prefab에 NetworkObject가 없습니다.");
                    yield break;
                }

                NetworkObject chaserObject = Runner.Spawn(chaserNetworkPrefab, spawnPosition, spawnRotation);
                spawnedChaser = chaserObject.GetComponent<SoundFollowingAgent>();

                if (DialogueController.Instance != null)
                {
                    DialogueController.Instance.RpcShow("ChaserSpawn");
                    DialogueController.Instance.RpcShow("ChaserSpawn_Sub");
                }

                RpcPlayChaserSpawnSound();
                Debug.Log($"[SequentialPlayer] 추격자 생성 완료: {spawnPosition}");
            }
            else
            {
                Debug.LogError("[SequentialPlayer] Chaser Prefab이 비어 있습니다.");
            }

            // 4. 포탈 활성화 대기 (30초 시점까지)
            while (timer < portalSpawnTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (DialogueController.Instance != null)
            {
                DialogueController.Instance.RpcShow("PortalSpawn");
            }

            if (portalManager != null)
            {
                portalManager.ActivateRandomPortals(1);
                RpcPlayPortalSpawnSound();
            }

            // 5. 엔드게임 대기 (60초 시점까지)
            while (timer < endgameTimer)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 6. 엔드게임 (추격자 폭주 모드)
            if (spawnedChaser != null)
            {
                if (DialogueController.Instance != null)
                {
                    DialogueController.Instance.RpcShow("Endgame");
                }

                spawnedChaser.SetStateToKnowWhereYouAre();
                RpcPlayBGM(true); // 긴장 BGM으로 변경
            }
        }

        // ================= 사운드 RPC =================

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayPrologueEndSound()
        {
            if (prologueAudioSource != null && prologueEndAudio != null)
            {
                prologueAudioSource.PlayOneShot(prologueEndAudio);
            }
            else if (PublicSpeaker.Instance != null && PublicSpeaker.Instance.OtherSpeaker != null && prologueEndAudio != null)
            {
                PublicSpeaker.Instance.OtherSpeaker.PlayOneShot(prologueEndAudio);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayBGM(bool isEndgame)
        {
            if (PublicSpeaker.Instance != null)
                PublicSpeaker.Instance.PlayBGM(isEndgame);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcStopBGM()
        {
            if (PublicSpeaker.Instance != null)
                PublicSpeaker.Instance.StopBGM();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayChaserSpawnSound()
        {
            if (PublicSpeaker.Instance != null)
                PublicSpeaker.Instance.PlayChaserSpawn();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayPortalSpawnSound()
        {
            if (PublicSpeaker.Instance != null)
                PublicSpeaker.Instance.PlayPortalSpawn();
        }
    }
}