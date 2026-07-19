using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Script.UI
{
    public class DialogueController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image panelImage;
        [SerializeField] private TextMeshProUGUI tmpText;
        
        [Header("Audio Components")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip endAudioClip;

        [Header("Dialogue Settings")]
        [TextArea(3, 10)]
        [SerializeField] private string[] dialogueLines;
        
        [Tooltip("m초: 텍스트 페이드 인/아웃에 걸리는 시간")]
        [SerializeField] private float textFadeDuration = 1.0f;
        
        [Tooltip("n초: 텍스트가 완전히 켜져있는 유지 시간")]
        [SerializeField] private float textDisplayDuration = 2.0f;
        
        [Tooltip("모든 대사 출력 후 패널이 페이드 아웃되는 시간")]
        [SerializeField] private float panelFadeOutDuration = 1.0f;

        // 외부에서 호출할 수 있는 코루틴 (이 시퀀스를 시작합니다)
        public IEnumerator PlayDialogue()
        {
            // 시작 시 텍스트 컴포넌트 알파값을 0으로 초기화
            tmpText.text = "";
            Color textColor = tmpText.color;
            textColor.a = 0f;
            tmpText.color = textColor;
            
            // 등록된 문자열들을 순회
            foreach (string line in dialogueLines)
            {
                tmpText.text = line;
                
                // 1. 텍스트 Fade In (m초)
                float elapsedTime = 0f;
                while (elapsedTime < textFadeDuration)
                {
                    elapsedTime += Time.deltaTime;
                    textColor.a = Mathf.Clamp01(elapsedTime / textFadeDuration);
                    tmpText.color = textColor;
                    yield return null;
                }
                textColor.a = 1f;
                tmpText.color = textColor;

                // 2. n초간 텍스트 표시 유지
                yield return new WaitForSeconds(textDisplayDuration);

                // 3. 텍스트 Fade Out (m초)
                elapsedTime = 0f;
                while (elapsedTime < textFadeDuration)
                {
                    elapsedTime += Time.deltaTime;
                    textColor.a = Mathf.Clamp01(1f - (elapsedTime / textFadeDuration));
                    tmpText.color = textColor;
                    yield return null;
                }
                textColor.a = 0f;
                tmpText.color = textColor;
            }

            // 모든 대사 출력 완료 후 패널(Image) Fade Out
            if (panelImage != null)
            {
                audioSource.PlayOneShot(endAudioClip);
                Color panelColor = panelImage.color;
                float panelElapsedTime = 0f;
                while (panelElapsedTime < panelFadeOutDuration)
                {
                    panelElapsedTime += Time.deltaTime;
                    panelColor.a = Mathf.Clamp01(1f - (panelElapsedTime / panelFadeOutDuration));
                    panelImage.color = panelColor;
                    yield return null;
                }
                panelColor.a = 0f;
                panelImage.color = panelColor;
            }

        }
    }
}