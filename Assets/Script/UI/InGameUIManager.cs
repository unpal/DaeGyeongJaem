using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    /// <summary>
    /// 인게임 UI(스태미너 게이지, 팀원 상태, 소생 진행 바)를 총괄 관리하는 매니저 컴포넌트입니다.
    /// 외부 코드의 침습 없이 수동 API 호출을 통해 UI를 갱신합니다.
    /// </summary>
    public class InGameUIManager : MonoBehaviour
    {
        public static InGameUIManager Instance { get; private set; }

        [Header("1. Stamina Bar")]
        [Tooltip("스태미너를 표시할 Filled 타입 이미지 (Horizontal)")]
        [SerializeField] private Image staminaFillImage;

        [Header("2. Team Member Status")]
        [Tooltip("팀원 각각의 슬롯 루트 오브젝트 (0 ~ 3)")]
        [SerializeField] private GameObject[] memberSlots;

        [Tooltip("각 팀원의 상태 아이콘을 표시할 Image 컴포넌트 (0 ~ 3)")]
        [SerializeField] private Image[] memberImages;

        [Tooltip("상태별 스프라이트 (0, 1, 2, 3...)")]
        [SerializeField] private Sprite[] stateSprites;

        [Header("3. Revive Progress")]
        [Tooltip("소생 바 투명도/표시 제어용 CanvasGroup")]
        [SerializeField] private CanvasGroup reviveCanvasGroup;

        [Tooltip("소생 진행도를 표시할 Filled 타입 이미지 (Vertical)")]
        [SerializeField] private Image reviveFillImage;

        [Tooltip("소생 캔슬 시 투명해지는 데 걸리는 시간(초)")]
        public const float DEFAULT_CANCEL_FADE_DURATION = 0.5f;
        [SerializeField] private float cancelFadeDuration = DEFAULT_CANCEL_FADE_DURATION;

        private Coroutine cancelFadeCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("[InGameUIManager] 중복 인스턴스가 존재하여 기존 인스턴스를 유지합니다.");
            }

            SetupInitialUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void SetupInitialUI()
        {
            // 스태미너 이미지 세팅 검증 및 기본값 보정
            if (staminaFillImage != null)
            {
                staminaFillImage.type = Image.Type.Filled;
                staminaFillImage.fillMethod = Image.FillMethod.Horizontal;
                staminaFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            // 소생 이미지 세팅 검증 및 기본값 보정
            if (reviveFillImage != null)
            {
                reviveFillImage.type = Image.Type.Filled;
                reviveFillImage.fillMethod = Image.FillMethod.Vertical;
                reviveFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                reviveFillImage.fillAmount = 0f;
            }

            // 평상시 소생 바는 화면에서 숨김 (Alpha = 0)
            if (reviveCanvasGroup != null)
            {
                reviveCanvasGroup.alpha = 0f;
                reviveCanvasGroup.blocksRaycasts = false;
                reviveCanvasGroup.interactable = false;
            }
        }

        // ========================================================================
        // 1. 스태미너 게이지 (Stamina Gauge)
        // ========================================================================

        /// <summary>
        /// 현재 스태미너와 최대 스태미너 값을 받아 게이지를 갱신합니다.
        /// </summary>
        /// <param name="currentStamina">현재 스태미너</param>
        /// <param name="maxStamina">최대 스태미너</param>
        public void SetStamina(float currentStamina, float maxStamina)
        {
            if (maxStamina <= 0f)
            {
                SetStaminaRatio(0f);
                return;
            }

            float ratio = Mathf.Clamp01(currentStamina / maxStamina);
            SetStaminaRatio(ratio);
        }

        /// <summary>
        /// 0.0 ~ 1.0 사이의 비율을 직접 전달받아 스태미너 게이지를 갱신합니다.
        /// </summary>
        /// <param name="ratio">스태미너 비율 (0.0 ~ 1.0)</param>
        public void SetStaminaRatio(float ratio)
        {
            if (staminaFillImage == null) return;
            staminaFillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        // ========================================================================
        // 2. 팀원 상태 (Team Member Status)
        // ========================================================================

        /// <summary>
        /// 참여하는 팀원 수에 맞춰 UI 슬롯을 활성화/비활성화합니다.
        /// </summary>
        /// <param name="memberCount">참여 팀원 인원수</param>
        public void Init(int memberCount)
        {
            if (memberSlots == null) return;

            for (int i = 0; i < memberSlots.Length; i++)
            {
                if (memberSlots[i] != null)
                {
                    memberSlots[i].SetActive(i < memberCount);
                }
            }
        }

        /// <summary>
        /// 대상 팀원(0, 1, 2, 3...)의 상태(0, 1, 2, 3...)에 맞춰 아이콘 이미지를 변경합니다.
        /// </summary>
        /// <param name="memberIndex">팀원 인덱스</param>
        /// <param name="state">상태 인덱스</param>
        public void SetMemberState(int memberIndex, int state)
        {
            if (memberImages == null || memberIndex < 0 || memberIndex >= memberImages.Length)
            {
                Debug.LogWarning($"[InGameUIManager] 유효하지 않은 팀원 인덱스입니다: {memberIndex}");
                return;
            }

            Image targetImage = memberImages[memberIndex];
            if (targetImage == null) return;

            if (stateSprites == null || state < 0 || state >= stateSprites.Length)
            {
                Debug.LogWarning($"[InGameUIManager] 유효하지 않은 상태 인덱스입니다: {state}");
                return;
            }

            Sprite newSprite = stateSprites[state];
            if (newSprite != null)
            {
                targetImage.sprite = newSprite;
            }
        }

        // ========================================================================
        // 3. 소생 진행 바 (Revive Progress Bar)
        // ========================================================================

        /// <summary>
        /// 소생 진행 정도(0.0 ~ 1.0)를 반영합니다. 진행 중에는 UI가 완전히 표시됩니다.
        /// </summary>
        /// <param name="progress">0.0 ~ 1.0 사이의 진행률</param>
        public void SetReviveProgress(float progress)
        {
            if (cancelFadeCoroutine != null)
            {
                StopCoroutine(cancelFadeCoroutine);
                cancelFadeCoroutine = null;
            }

            if (reviveCanvasGroup != null)
            {
                reviveCanvasGroup.alpha = 1f;
            }

            if (reviveFillImage != null)
            {
                reviveFillImage.fillAmount = Mathf.Clamp01(progress);
            }
        }

        /// <summary>
        /// 소생이 취소되었을 때 호출되며, 지정된 시간 동안 투명도(Alpha)가 0으로 선형 감소합니다.
        /// </summary>
        public void CancelRevive()
        {
            if (cancelFadeCoroutine != null)
            {
                StopCoroutine(cancelFadeCoroutine);
            }

            cancelFadeCoroutine = StartCoroutine(CancelFadeRoutine());
        }

        /// <summary>
        /// 소생 UI를 페이드 없이 즉시 숨깁니다.
        /// </summary>
        public void HideRevive()
        {
            if (cancelFadeCoroutine != null)
            {
                StopCoroutine(cancelFadeCoroutine);
                cancelFadeCoroutine = null;
            }

            if (reviveCanvasGroup != null)
            {
                reviveCanvasGroup.alpha = 0f;
            }

            if (reviveFillImage != null)
            {
                reviveFillImage.fillAmount = 0f;
            }
        }

        private IEnumerator CancelFadeRoutine()
        {
            if (reviveCanvasGroup == null)
                yield break;

            float startAlpha = reviveCanvasGroup.alpha;
            float duration = Mathf.Max(0.01f, cancelFadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                reviveCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            reviveCanvasGroup.alpha = 0f;
            if (reviveFillImage != null)
            {
                reviveFillImage.fillAmount = 0f;
            }

            cancelFadeCoroutine = null;
        }
    }
}

