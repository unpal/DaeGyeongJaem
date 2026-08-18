using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    public enum TitleType
    {
        PanelText,
        CenterText,
        BottomText
    }

    public class DialogueController : NetworkBehaviour
    {
        public static DialogueController Instance { get; private set; }

        [Header("UI Components")]
        [SerializeField] private Image panelImage; // 인트로/페이드용 배경 이미지
        [SerializeField] private TextMeshProUGUI panelText; // 패널 내부 텍스트
        [SerializeField] private TextMeshProUGUI centerText; // 화면 중앙 텍스트 (카운트다운, 주요 알림)
        [SerializeField] private TextMeshProUGUI bottomText; // 화면 하단 텍스트 (자막, 안내)

        private Coroutine _panelImageCoroutine;
        private Coroutine _panelTextCoroutine;
        private Coroutine _centerTextCoroutine;
        private Coroutine _bottomTextCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            PhraseTable.EnsureLoaded();
        }

        public override void Spawned()
        {
            Instance = this;
            PhraseTable.EnsureLoaded();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private TextMeshProUGUI GetTMP(TitleType titleType)
        {
            return titleType switch
            {
                TitleType.PanelText => panelText,
                TitleType.CenterText => centerText,
                TitleType.BottomText => bottomText,
                _ => null
            };
        }

        // ================= RPC 메서드 (어디서든 호출 가능) =================

        /// <summary>
        /// CSV Key를 기반으로 타이밍(In-Stay-Out) 및 슬롯, 텍스트를 자동으로 찾아 출력하는 RPC
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcShow(string key, string arg0 = "")
        {
            if (PhraseTable.TryGet(key, out PhraseCue cue, arg0))
            {
                SetTitle(cue.titleType, cue.fadeIn, cue.duration, cue.fadeOut, cue.text);
            }
            else
            {
                Debug.LogWarning($"[DialogueController] PhraseTable에서 키를 찾을 수 없습니다: {key}");
            }
        }

        /// <summary>
        /// 직접 타이밍과 텍스트를 지정하여 출력하는 RPC (프롤로그 또는 커스텀 텍스트용)
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcShowTitle(TitleType titleType, float fadeIn, float duration, float fadeOut, string text)
        {
            SetTitle(titleType, fadeIn, duration, fadeOut, text);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcShowPanel(float fadeIn, float duration, float fadeOut)
        {
            SetPanel(fadeIn, duration, fadeOut);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcClearTitle(TitleType titleType)
        {
            ClearTitle(titleType);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcClearAll()
        {
            ClearAll();
        }

        // ================= 로컬 UI 연출 메서드 =================

        public void SetTitle(TitleType titleType, float fadeIn, float duration, float fadeOut, string text)
        {
            TextMeshProUGUI tmp = GetTMP(titleType);
            if (tmp == null)
                return;

            switch (titleType)
            {
                case TitleType.PanelText:
                    if (_panelTextCoroutine != null) StopCoroutine(_panelTextCoroutine);
                    _panelTextCoroutine = StartCoroutine(Entitle(tmp, fadeIn, duration, fadeOut, text));
                    break;
                case TitleType.CenterText:
                    if (_centerTextCoroutine != null) StopCoroutine(_centerTextCoroutine);
                    _centerTextCoroutine = StartCoroutine(Entitle(tmp, fadeIn, duration, fadeOut, text));
                    break;
                case TitleType.BottomText:
                    if (_bottomTextCoroutine != null) StopCoroutine(_bottomTextCoroutine);
                    _bottomTextCoroutine = StartCoroutine(Entitle(tmp, fadeIn, duration, fadeOut, text));
                    break;
            }
        }

        public void SetPanel(float fadeIn, float duration, float fadeOut)
        {
            if (panelImage == null)
                return;

            if (_panelImageCoroutine != null)
                StopCoroutine(_panelImageCoroutine);

            _panelImageCoroutine = StartCoroutine(PanelRoutine(fadeIn, duration, fadeOut));
        }

        public void ClearTitle(TitleType titleType)
        {
            TextMeshProUGUI tmp = GetTMP(titleType);
            if (tmp == null)
                return;

            switch (titleType)
            {
                case TitleType.PanelText:
                    if (_panelTextCoroutine != null) { StopCoroutine(_panelTextCoroutine); _panelTextCoroutine = null; }
                    break;
                case TitleType.CenterText:
                    if (_centerTextCoroutine != null) { StopCoroutine(_centerTextCoroutine); _centerTextCoroutine = null; }
                    break;
                case TitleType.BottomText:
                    if (_bottomTextCoroutine != null) { StopCoroutine(_bottomTextCoroutine); _bottomTextCoroutine = null; }
                    break;
            }

            tmp.alpha = 0f;
            tmp.text = string.Empty;
        }

        public void ClearAll()
        {
            ClearTitle(TitleType.PanelText);
            ClearTitle(TitleType.CenterText);
            ClearTitle(TitleType.BottomText);

            if (_panelImageCoroutine != null)
            {
                StopCoroutine(_panelImageCoroutine);
                _panelImageCoroutine = null;
            }

            if (panelImage != null)
            {
                Color c = panelImage.color;
                c.a = 0f;
                panelImage.color = c;
            }
        }

        private IEnumerator Entitle(TextMeshProUGUI tmp, float fadeIn, float duration, float fadeOut, string text)
        {
            tmp.text = text;
            tmp.alpha = 0f;

            if (fadeIn > 0f)
            {
                float timer = 0f;
                while (timer < fadeIn)
                {
                    timer += Time.deltaTime;
                    tmp.alpha = Mathf.Clamp01(timer / fadeIn);
                    yield return null;
                }
            }
            tmp.alpha = 1f;

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            if (fadeOut > 0f)
            {
                float timer = 0f;
                while (timer < fadeOut)
                {
                    timer += Time.deltaTime;
                    tmp.alpha = Mathf.Clamp01(1f - (timer / fadeOut));
                    yield return null;
                }
            }

            tmp.alpha = 0f;
            tmp.text = string.Empty;
        }

        private IEnumerator PanelRoutine(float fadeIn, float duration, float fadeOut)
        {
            Color c = panelImage.color;
            c.a = 0f;
            panelImage.color = c;

            if (fadeIn > 0f)
            {
                float timer = 0f;
                while (timer < fadeIn)
                {
                    timer += Time.deltaTime;
                    c.a = Mathf.Clamp01(timer / fadeIn);
                    panelImage.color = c;
                    yield return null;
                }
            }
            c.a = 1f;
            panelImage.color = c;

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            if (fadeOut > 0f)
            {
                float timer = 0f;
                while (timer < fadeOut)
                {
                    timer += Time.deltaTime;
                    c.a = Mathf.Clamp01(1f - (timer / fadeOut));
                    panelImage.color = c;
                    yield return null;
                }
            }

            c.a = 0f;
            panelImage.color = c;
        }
    }
}