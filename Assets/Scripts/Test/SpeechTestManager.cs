using API;
using UnityEngine;
using UnityEngine.UI;

namespace Test
{
    public class SpeechTestManager : MonoBehaviour
    {
        [Header("UI References")]
        public InputField textInput;
        public Button synthesizeButton;
        public Text statusText;
        public AudioSource audioSource;
        
        private SpeechSynthesisService speechService;
        
        private void Start()
        {
            speechService = GetComponent<SpeechSynthesisService>();
            if (speechService == null)
            {
                speechService = gameObject.AddComponent<SpeechSynthesisService>();
            }
            
            // 订阅事件
            speechService.OnSynthesisStarted += OnSynthesisStarted;
            speechService.OnSynthesisCompleted += OnSynthesisCompleted;
            speechService.OnAudioClipReady += OnAudioClipReady;
            speechService.OnError += OnSynthesisError;
            
            // 设置UI事件
            if (synthesizeButton != null)
                synthesizeButton.onClick.AddListener(OnSynthesizeButtonClicked);
        }
        
        private void OnSynthesizeButtonClicked()
        {
            if (textInput != null && !string.IsNullOrEmpty(textInput.text))
            {
                speechService.SynthesizeVoice(textInput.text);
            }
        }
        
        private void OnSynthesisStarted()
        {
            UpdateStatus("正在合成语音...");
        }
        
        private void OnSynthesisCompleted(SpeechSynthesisResponse response)
        {
            UpdateStatus($"语音合成完成，时长: {response.duration:F1}秒");
        }
        
        private void OnAudioClipReady(AudioClip audioClip, SpeechSynthesisResponse response)
        {
            if (audioSource != null)
            {
                audioSource.clip = audioClip;
                audioSource.Play();
                UpdateStatus("正在播放语音...");
            }
        }
        
        private void OnSynthesisError(string error)
        {
            UpdateStatus($"错误: {error}");
            Debug.LogError(error);
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"语音状态: {message}");
        }
        
        // 公共接口供其他脚本调用
        public void SpeakText(string text)
        {
            speechService?.SynthesizeVoice(text);
        }
    }
}