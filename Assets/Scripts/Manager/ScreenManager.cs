using System;
using System.Collections;
using System.Collections.Generic;
using PPT_Item;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using API;

namespace Manager
{
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance;
    
        [Header("UI Components")]    
        public Material screenMaterial;
        public MeshRenderer screenMeshRenderer;
        public AudioSource audioSource;
        public TextMeshProUGUI contentText;
    
        [Header("Presentation Management")]
        public PresentationManager presentationManager;
        
        [Header("Voice Chat Components")]
        public Button voiceChatButton;
        public TextMeshProUGUI voiceChatButtonText;
        public GameObject voiceChatPanel; // 可选：包含按钮的面板
    
        // 兼容性字段 - 保留原有功能
        public bool _useNewPresentationSystem = true; // 控制是否使用新系统
        private List<PPT_Entity> _presentationList = new List<PPT_Entity>();
        private int _currentIndex = -1;
        
        // 语音对话相关
        private VoiceChatStreamService voiceChatService;
        private bool isRecording = false;
        private bool isButtonCooldown = false;
        private string currentVoiceText = "";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        
            screenMeshRenderer.material = screenMaterial;
        
            // 初始化语音对话服务
            InitializeVoiceChatService();
        
            // 检查是否使用新的演示系统
            if (_useNewPresentationSystem && presentationManager != null)
            {
                // 使用新的演示管理器
                SetupNewPresentationSystem();
            }
            else
            {
                // 使用原有系统
                FetchPresentationList();
            }
        }
        
        private void InitializeVoiceChatService()
        {
            // 添加语音对话服务组件
            voiceChatService = gameObject.GetComponent<VoiceChatStreamService>();
            if (voiceChatService == null)
            {
                voiceChatService = gameObject.AddComponent<VoiceChatStreamService>();
            }
            
            // 设置音频源
            voiceChatService.audioSource = audioSource;
            
            // 订阅语音对话事件
            voiceChatService.OnRecordingStarted += OnVoiceRecordingStarted;
            voiceChatService.OnRecordingStopped += OnVoiceRecordingStopped;
            voiceChatService.OnTextReceived += OnVoiceTextReceived;
            voiceChatService.OnAudioReceived += OnVoiceAudioReceived;
            voiceChatService.OnVoiceChatCompleted += OnVoiceChatCompleted;
            voiceChatService.OnError += OnVoiceChatError;
            
            // 初始化UI
            SetupVoiceChatUI();
        }
        
        private void SetupVoiceChatUI()
        {
            // 隐藏语音对话按钮（演示完成后才显示）
            if (voiceChatButton != null)
            {
                voiceChatButton.onClick.AddListener(OnVoiceChatButtonClick);
                voiceChatButton.gameObject.SetActive(false);
            }
            
            if (voiceChatPanel != null)
            {
                voiceChatPanel.SetActive(false);
            }
            
            UpdateVoiceChatButtonText("开始录音");
        }
    
        private void SetupNewPresentationSystem()
        {
            // 订阅演示管理器的事件
            presentationManager.OnNodeChanged += OnNodeChanged;
            presentationManager.OnPresentationStarted += OnPresentationStarted;
            presentationManager.OnPresentationCompleted += OnPresentationCompleted;
            presentationManager.OnError += OnPresentationError;
        }
    
        // 新系统事件处理
        private void OnNodeChanged(int nodeIndex)
        {
            Debug.Log($"切换到节点 {nodeIndex}");
            // 可以在这里添加额外的UI更新逻辑
        }
    
        private void OnPresentationStarted()
        {
            Debug.Log("演示开始");
            // 隐藏语音对话UI
            HideVoiceChatUI();
        }
    
        private void OnPresentationCompleted()
        {
            Debug.Log("演示完成");
            // 显示语音对话UI
            ShowVoiceChatUI();
            
            // 可选：显示演示完成的提示文本
            if (contentText != null)
            {
                contentText.text = "演示已完成，您可以开始语音对话了！";
            }
        }
    
        private void OnPresentationError(string error)
        {
            Debug.LogError($"演示错误: {error}");
            // 可以在这里显示错误提示给用户
        }
        
        // 语音对话UI控制
        private void ShowVoiceChatUI()
        {
            if (voiceChatButton != null)
            {
                voiceChatButton.gameObject.SetActive(true);
            }
            
            if (voiceChatPanel != null)
            {
                voiceChatPanel.SetActive(true);
            }
        }
        
        private void HideVoiceChatUI()
        {
            if (voiceChatButton != null)
            {
                voiceChatButton.gameObject.SetActive(false);
            }
            
            if (voiceChatPanel != null)
            {
                voiceChatPanel.SetActive(false);
            }
        }
        
        // 语音对话按钮点击事件
        private void OnVoiceChatButtonClick()
        {
            if (isButtonCooldown)
            {
                Debug.Log("按钮冷却中，请稍后再试");
                return;
            }
            
            if (!isRecording)
            {
                // 开始录音
                StartVoiceRecording();
            }
            else
            {
                // 停止录音并发送
                StopVoiceRecording();
            }
        }
        
        private void StartVoiceRecording()
        {
            if (voiceChatService != null)
            {
                voiceChatService.StartRecording();
            }
        }
        
        private void StopVoiceRecording()
        {
            if (voiceChatService != null)
            {
                voiceChatService.StopRecordingAndSendVoiceChat();
            }
        }
        
        // 语音对话事件处理
        private void OnVoiceRecordingStarted()
        {
            isRecording = true;
            StartButtonCooldown();
            UpdateVoiceChatButtonText("录音中...");
            
            // 清空之前的文本
            currentVoiceText = "";
            if (contentText != null)
            {
                contentText.text = "正在录音，请说话...";
            }
            
            Debug.Log("开始录音");
        }
        
        private void OnVoiceRecordingStopped()
        {
            isRecording = false;
            UpdateVoiceChatButtonText("停止录音");
            
            if (contentText != null)
            {
                contentText.text = "录音完成，正在处理...";
            }
            
            Debug.Log("录音停止");
        }
        
        private void OnVoiceTextReceived(string text)
        {
            // 累积接收到的文本（流式显示）
            currentVoiceText += text;
            
            if (contentText != null)
            {
                contentText.text = currentVoiceText;
            }
            
            Debug.Log($"接收到文本: {text}");
        }
        
        private void OnVoiceAudioReceived(AudioClip audioClip)
        {
            // 音频会自动在audioSource上播放（由VoiceChatStreamService处理）
            Debug.Log($"接收到语音: {audioClip.name}");
        }
        
        private void OnVoiceChatCompleted()
        {
            UpdateVoiceChatButtonText("开始录音");
            
            Debug.Log("语音对话完成");
        }
        
        private void OnVoiceChatError(string error)
        {
            isRecording = false;
            UpdateVoiceChatButtonText("开始录音");
            
            if (contentText != null)
            {
                contentText.text = $"语音对话错误: {error}";
            }
            
            Debug.LogError($"语音对话错误: {error}");
        }
        
        // 按钮冷却逻辑
        private void StartButtonCooldown()
        {
            StartCoroutine(ButtonCooldownCoroutine());
        }
        
        private IEnumerator ButtonCooldownCoroutine()
        {
            isButtonCooldown = true;
            
            // 禁用按钮0.5秒
            if (voiceChatButton != null)
            {
                voiceChatButton.interactable = false;
            }
            
            yield return new WaitForSeconds(0.5f);
            
            // 重新启用按钮
            if (voiceChatButton != null)
            {
                voiceChatButton.interactable = true;
            }
            
            // 如果还在录音状态，更新按钮文本为"停止录音"
            if (isRecording)
            {
                UpdateVoiceChatButtonText("停止录音");
            }
            
            isButtonCooldown = false;
        }
        
        private void UpdateVoiceChatButtonText(string text)
        {
            if (voiceChatButtonText != null)
            {
                voiceChatButtonText.text = text;
            }
        }
    
        // 原有系统方法 - 保持兼容性
        private void FetchPresentationList()
        {
            _presentationList = PPT_EntityMockData.GetMockPresentationList();
        }

        void Start()
        {
            if (!_useNewPresentationSystem)
            {
                audioSource.loop = false;
                audioSource.playOnAwake = false;
                audioSource.clip = null;
            }
        }
    
        void Update()
        {
            // 只在使用原有系统时执行
            if (!_useNewPresentationSystem)
            {
                if (audioSource.isPlaying)
                {
                    // 音频正在播放
                }
                else if (_currentIndex < _presentationList.Count)
                {
                    OnAudioSourceFinished();
                }
            }
        }
    
        // 原有方法 - 保持兼容性
        public void ChangeScreen(int index)
        {
            if (_useNewPresentationSystem)
            {
                // 使用新系统跳转到指定节点
                if (presentationManager != null)
                {
                    presentationManager.JumpToNode(index);
                }
                return;
            }
        
            // 原有系统逻辑
            _currentIndex = index;
            if (index < 0 || index >= _presentationList.Count)
            {
                Debug.LogError("Index out of range");
                return;
            }

            var entity = _presentationList[index];
        
            if (entity.ImageUrl != null)
            {
                StartCoroutine(LoadTextureFromUrl(entity.ImageUrl, texture =>
                {
                    screenMaterial.mainTexture = (Texture2D)texture;
                }));
            }
        
            if (entity.AudioData != null && entity.AudioData.Length > 0)
            {
                AudioClip audioClip = AudioClip.Create(entity.Id, entity.AudioData.Length, 1, 44100, false);
                audioSource.clip = audioClip;
                audioSource.Play();
            }
        
            contentText.text = !string.IsNullOrEmpty(entity.Content) ? entity.Content : "No content available";
        
            CameraManager.Instance.SetCameraActive(entity.ScreenCameraType);
            Debug.Log($"Changed screen to {entity.Content} with camera type {entity.ScreenCameraType}");
        }
    
        private void OnAudioSourceFinished()
        {
            Debug.Log("Audio playback finished.");
            ChangeScreen((_currentIndex + 1) % _presentationList.Count);
        }

        private IEnumerator LoadTextureFromUrl(string entityImageUrl, Action<object> action)
        {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(entityImageUrl);
            yield return webRequest.SendWebRequest();
            Texture2D texture = null;
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                texture = DownloadHandlerTexture.GetContent(webRequest);
            }
            action?.Invoke(texture);
        }
    
        // 新的公共接口方法
        public void StartPresentation()
        {
            if (_useNewPresentationSystem && presentationManager != null)
            {
                presentationManager.StartPresentation();
            }
        }
    
        public void PausePresentation()
        {
            if (_useNewPresentationSystem && presentationManager != null)
            {
                presentationManager.PausePresentation();
            }
        }
    
        public void ResumePresentation()
        {
            if (_useNewPresentationSystem && presentationManager != null)
            {
                presentationManager.ResumePresentation();
            }
        }
    
        public void StopPresentation()
        {
            if (_useNewPresentationSystem && presentationManager != null)
            {
                presentationManager.StopPresentation();
            }
        }
        
        // 公共语音对话接口
        public void StartVoiceChat()
        {
            if (!isRecording && !isButtonCooldown)
            {
                StartVoiceRecording();
            }
        }
        
        public void StopVoiceChat()
        {
            if (isRecording && !isButtonCooldown)
            {
                StopVoiceRecording();
            }
        }
        
        public bool IsVoiceChatting()
        {
            return isRecording || (voiceChatService != null && voiceChatService.audioSource.isPlaying);
        }
        
        // 手动显示/隐藏语音对话UI（用于测试或特殊情况）
        public void ShowVoiceChatUIManually()
        {
            ShowVoiceChatUI();
        }
        
        public void HideVoiceChatUIManually()
        {
            HideVoiceChatUI();
        }
    
        private void OnDestroy()
        {
            if (presentationManager != null)
            {
                presentationManager.OnNodeChanged -= OnNodeChanged;
                presentationManager.OnPresentationStarted -= OnPresentationStarted;
                presentationManager.OnPresentationCompleted -= OnPresentationCompleted;
                presentationManager.OnError -= OnPresentationError;
            }
            
            // 清理语音对话事件订阅
            if (voiceChatService != null)
            {
                voiceChatService.OnRecordingStarted -= OnVoiceRecordingStarted;
                voiceChatService.OnRecordingStopped -= OnVoiceRecordingStopped;
                voiceChatService.OnTextReceived -= OnVoiceTextReceived;
                voiceChatService.OnAudioReceived -= OnVoiceAudioReceived;
                voiceChatService.OnVoiceChatCompleted -= OnVoiceChatCompleted;
                voiceChatService.OnError -= OnVoiceChatError;
            }
        }
    }
}