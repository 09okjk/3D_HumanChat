using System;
using System.Collections;
using System.Collections.Generic;
using PPT_Item;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

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
    
        // 兼容性字段 - 保留原有功能
        public bool _useNewPresentationSystem = true; // 控制是否使用新系统
        private List<PPT_Entity> _presentationList = new List<PPT_Entity>();
        private int _currentIndex = -1;

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
            // 可以在这里添加演示开始时的逻辑
        }
    
        private void OnPresentationCompleted()
        {
            Debug.Log("演示完成");
            // 可以在这里添加演示完成后的逻辑，比如显示结束画面或返回主菜单
        }
    
        private void OnPresentationError(string error)
        {
            Debug.LogError($"演示错误: {error}");
            // 可以在这里显示错误提示给用户
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
    
        private void OnDestroy()
        {
            if (presentationManager != null)
            {
                presentationManager.OnNodeChanged -= OnNodeChanged;
                presentationManager.OnPresentationStarted -= OnPresentationStarted;
                presentationManager.OnPresentationCompleted -= OnPresentationCompleted;
                presentationManager.OnError -= OnPresentationError;
            }
        }
    }
}