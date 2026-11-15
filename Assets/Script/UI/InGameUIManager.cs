using System.Collections;
using System.Collections.Generic;
using Script.item;
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
        [Tooltip("팀원 상태 UI 전체 루트 오브젝트 (도망자 전용)")]
        [SerializeField] private GameObject teamStatusRoot;

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

        [Header("4. Role Visibility")]
        [Tooltip("도망자 전용으로 표시할 추가 UI 오브젝트 목록 (술래일 때 숨김)")]
        [SerializeField] private GameObject[] evaderOnlyObjects;

        [Header("5. Item Slots (Evader Only)")]
        [Tooltip("좌측 하단 3슬롯 아이템 UI 전체 루트 오브젝트 (도망자 전용)")]
        [SerializeField] private GameObject itemSlotsRoot;

        [Tooltip("각 아이템 슬롯(0 ~ 2)의 배경/테두리 Image 컴포넌트")]
        [SerializeField] private Image[] itemSlotBackgrounds;

        [Tooltip("각 아이템 슬롯(0 ~ 2)의 아이템 아이콘 Image 컴포넌트")]
        [SerializeField] private Image[] itemSlotIcons;

        [Tooltip("각 아이템 슬롯(0 ~ 2) 선택 시 활성화할 별도 강조 오브젝트 (선택 사항)")]
        [SerializeField] private GameObject[] itemSlotHighlights;

        [SerializeField] private Color normalSlotColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color selectedSlotColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private float selectedSlotScale = 1.1f;

        private Coroutine cancelFadeCoroutine;
        private bool isSeekerMode;

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

            ClearItemSlotsUI();
        }

        // ========================================================================
        // 0. 역할별 HUD 표시 전환 (Role Visibility)
        // ========================================================================

        /// <summary>
        /// 로컬 플레이어의 역할(술래/도망자)에 맞춰 도망자 전용 HUD 요소의 표시 여부를 전환합니다.
        /// </summary>
        /// <param name="isSeeker">술래 여부 (true: 술래, false: 도망자)</param>
        public void SetRoleUI(bool isSeeker)
        {
            isSeekerMode = isSeeker;
            bool isEvader = !isSeeker;

            if (teamStatusRoot != null && teamStatusRoot.activeSelf != isEvader)
            {
                teamStatusRoot.SetActive(isEvader);
            }

            if (itemSlotsRoot != null && itemSlotsRoot.activeSelf != isEvader)
            {
                itemSlotsRoot.SetActive(isEvader);
            }

            if (isSeeker)
            {
                Init(0);
                HideRevive();
                ClearItemSlotsUI();
            }

            if (reviveCanvasGroup != null && reviveCanvasGroup.gameObject.activeSelf != isEvader)
            {
                reviveCanvasGroup.gameObject.SetActive(isEvader);
            }

            if (evaderOnlyObjects != null)
            {
                for (int i = 0; i < evaderOnlyObjects.Length; i++)
                {
                    if (evaderOnlyObjects[i] != null && evaderOnlyObjects[i].activeSelf != isEvader)
                    {
                        evaderOnlyObjects[i].SetActive(isEvader);
                    }
                }
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

            int activeCount = isSeekerMode ? 0 : memberCount;
            for (int i = 0; i < memberSlots.Length; i++)
            {
                if (memberSlots[i] != null)
                {
                    memberSlots[i].SetActive(i < activeCount);
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
            if (isSeekerMode) return;

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
            if (isSeekerMode) return;

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
            if (isSeekerMode || reviveCanvasGroup == null ||
                !reviveCanvasGroup.gameObject.activeInHierarchy ||
                reviveCanvasGroup.alpha <= 0f)
            {
                HideRevive();
                return;
            }

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

        // ========================================================================
        // 4. 아이템 슬롯 (Item Slots - Evader Only)
        // ========================================================================

        /// <summary>
        /// 도망자 전용 3슬롯 아이템 UI의 아이콘과 선택 강조 상태를 갱신합니다.
        /// </summary>
        /// <param name="items">보유 중인 아이템 목록 (최대 3개)</param>
        /// <param name="selectedSlot">현재 선택된 슬롯 인덱스 (0 ~ 2, 미선택 시 -1)</param>
        public void UpdateItemSlots(IReadOnlyList<ItemData> items, int selectedSlot)
        {
            if (isSeekerMode) return;

            ApplyItemSlotsVisual(items, selectedSlot);
        }

        private void ClearItemSlotsUI()
        {
            ApplyItemSlotsVisual(null, -1);
        }

        private void ApplyItemSlotsVisual(IReadOnlyList<ItemData> items, int selectedSlot)
        {
            int bgCount = itemSlotBackgrounds != null ? itemSlotBackgrounds.Length : 0;
            int iconCount = itemSlotIcons != null ? itemSlotIcons.Length : 0;
            int highlightCount = itemSlotHighlights != null ? itemSlotHighlights.Length : 0;
            int slotCount = Mathf.Max(bgCount, Mathf.Max(iconCount, highlightCount));

            for (int i = 0; i < slotCount; i++)
            {
                ItemData item = (items != null && i < items.Count) ? items[i] : null;
                bool hasItem = item != null;

                if (itemSlotIcons != null && i < itemSlotIcons.Length && itemSlotIcons[i] != null)
                {
                    itemSlotIcons[i].sprite = hasItem ? item.ItemSprite : null;
                    itemSlotIcons[i].enabled = hasItem;
                }

                bool isSelected = !isSeekerMode && i == selectedSlot;

                if (itemSlotBackgrounds != null && i < itemSlotBackgrounds.Length && itemSlotBackgrounds[i] != null)
                {
                    itemSlotBackgrounds[i].color = isSelected ? selectedSlotColor : normalSlotColor;
                    itemSlotBackgrounds[i].rectTransform.localScale = Vector3.one * (isSelected ? selectedSlotScale : 1f);
                }

                if (itemSlotHighlights != null && i < itemSlotHighlights.Length && itemSlotHighlights[i] != null)
                {
                    if (itemSlotHighlights[i].activeSelf != isSelected)
                    {
                        itemSlotHighlights[i].SetActive(isSelected);
                    }
                }
            }
        }
    }
}

