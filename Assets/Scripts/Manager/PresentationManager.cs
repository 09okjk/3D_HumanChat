using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using API;
using TMPro;
using Tools;
using UnityEngine;

namespace Manager
{
    public class PresentationManager : MonoBehaviour
    {
        public static PresentationManager Instance;
        
        [Header("UI References")]
        public MeshRenderer screenMeshRenderer;
        public Material screenMaterial;
        public AudioSource audioSource;
        public TextMeshProUGUI contentText;
    
        [Header("Services")]
        public SpeechSynthesisService speechService;
    
        [Header("Settings")]
        public int preloadCount = 3; // 预加载音频的数量
        public float delayBetweenNodes = 0.5f; // 节点间的延迟时间
    
        [Header("Camera Settings")]
        public bool enableCameraSwitching = true; // 是否启用摄像机切换
        public CameraSwitchMode cameraSwitchMode = CameraSwitchMode.Sequential; // 切换模式
        public float cameraHoldDuration = 5f; // 每个摄像机持续时间（仅在时间模式下使用）
        
        // 摄像机切换模式枚举
        public enum CameraSwitchMode
        {
            Sequential, // 按顺序切换
            Random,     // 随机切换
            BasedOnContent, // 根据内容决定
            Timer       // 基于时间切换
        }
        
        // 私有字段
        private DocumentData currentDocument;
        private List<DataItem> dataItems;
        private int currentNodeIndex = -1;
        private bool isPlaying = false;
        private bool isPaused = false;
    
        // 音频缓存
        private Dictionary<int, AudioClip> audioCache = new Dictionary<int, AudioClip>();
        private Dictionary<int, bool> audioLoadingStatus = new Dictionary<int, bool>(); // true: 正在加载, false: 加载完成
        private Queue<int> preloadQueue = new Queue<int>();
        
        // 摄像机切换相关
        private int currentCameraIndex = 0;
        private ScreenCameraType[] availableCameraTypes = { 
            ScreenCameraType.MainCamera, 
            ScreenCameraType.FarCamera, 
            ScreenCameraType.FollowCamera 
        };
    
        // 事件
        public Action<int> OnNodeChanged;
        public Action OnPresentationStarted;
        public Action OnPresentationCompleted;
        public Action<string> OnError;

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
        }

        private void Start()
        {
            InitializePresentation();
        }
    
        private void InitializePresentation()
        {
            // 从MainManager获取当前文档数据
            currentDocument = MainManager.Instance.GetCurrentDocumentData();
        
            if (currentDocument == null || currentDocument.data_list == null || currentDocument.data_list.Count == 0)
            {
                OnError?.Invoke("没有找到有效的文档数据");
                return;
            }
        
            // 按sequence排序数据项
            dataItems = currentDocument.data_list.OrderBy(item => item.sequence).ToList();
        
            // 初始化音频服务事件
            if (speechService != null)
            {
                speechService.OnAudioClipReady += OnAudioClipReady;
                speechService.OnError += OnSpeechError;
            }
        
            // 初始化音频源
            audioSource.loop = false;
            audioSource.playOnAwake = false;
        
            // 开始演示
            StartPresentation();
        }
    
        public void StartPresentation()
        {
            if (isPlaying) return;
        
            isPlaying = true;
            isPaused = false;
            currentNodeIndex = -1;
        
            OnPresentationStarted?.Invoke();
        
            // 开始预加载前几个节点的音频
            StartPreloading();
        
            // 播放第一个节点
            PlayNextNode();
        }
    
        public void PausePresentation()
        {
            isPaused = true;
            if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }
    
        public void ResumePresentation()
        {
            if (!isPaused) return;
        
            isPaused = false;
            if (audioSource.clip != null)
            {
                audioSource.UnPause();
            }
        }
    
        public void StopPresentation()
        {
            isPlaying = false;
            isPaused = false;
            audioSource.Stop();
            currentNodeIndex = -1;
            
            // host停止移动
            
        
            // 清理音频缓存
            ClearAudioCache();
        }
    
        private void StartPreloading()
        {
            // 将前几个节点加入预加载队列
            for (int i = 0; i < Mathf.Min(preloadCount, dataItems.Count); i++)
            {
                if (!audioCache.ContainsKey(i) && !audioLoadingStatus.ContainsKey(i))
                {
                    preloadQueue.Enqueue(i);
                }
            }
        
            // 开始预加载
            ProcessPreloadQueue();
        }
    
        private void ProcessPreloadQueue()
        {
            if (preloadQueue.Count == 0) return;
        
            int nodeIndex = preloadQueue.Dequeue();
            LoadAudioForNode(nodeIndex);
        
            // 继续处理队列中的下一个
            if (preloadQueue.Count > 0)
            {
                StartCoroutine(DelayedPreload());
            }
        }
    
        private IEnumerator DelayedPreload()
        {
            yield return new WaitForSeconds(0.1f); // 避免同时发送过多请求
            ProcessPreloadQueue();
        }
    
        private void LoadAudioForNode(int nodeIndex)
        {
            if (nodeIndex >= dataItems.Count) return;
        
            DataItem dataItem = dataItems[nodeIndex];
            if (string.IsNullOrEmpty(dataItem.text)) return;
        
            // 标记为正在加载
            audioLoadingStatus[nodeIndex] = true;
        
            Debug.Log($"开始加载节点 {nodeIndex} 的音频: {dataItem.text.Substring(0, Mathf.Min(50, dataItem.text.Length))}...");
        
            // 请求语音合成
            speechService.SynthesizeVoice(dataItem.text);
        }
    
        private void OnAudioClipReady(AudioClip audioClip, SpeechSynthesisResponse response)
        {
            // 找到对应的节点索引
            int nodeIndex = FindNodeIndexByRequestId(response.request_id);
            if (nodeIndex == -1)
            {
                // 通过文本内容匹配
                nodeIndex = FindNodeIndexByText(response);
            }
        
            if (nodeIndex != -1)
            {
                // 缓存音频
                audioCache[nodeIndex] = audioClip;
                audioLoadingStatus[nodeIndex] = false;
            
                Debug.Log($"节点 {nodeIndex} 的音频加载完成");
            
                // 继续预加载后续节点
                ContinuePreloading(nodeIndex);
            }
        }
    
        private int FindNodeIndexByRequestId(string requestId)
        {
            // 这里可以根据request_id来匹配，需要在请求时保存映射关系
            // 暂时返回-1，使用文本匹配作为备选方案
            return -1;
        }
    
        private int FindNodeIndexByText(SpeechSynthesisResponse response)
        {
            // 通过文本内容来匹配节点
            for (int i = 0; i < dataItems.Count; i++)
            {
                if (audioLoadingStatus.ContainsKey(i) && audioLoadingStatus[i] && 
                    !audioCache.ContainsKey(i))
                {
                    // 找到第一个正在加载且未缓存的节点
                    return i;
                }
            }
            return -1;
        }
    
        private void ContinuePreloading(int completedNodeIndex)
        {
            // 预加载后续节点
            int nextPreloadIndex = completedNodeIndex + preloadCount;
            if (nextPreloadIndex < dataItems.Count && 
                !audioCache.ContainsKey(nextPreloadIndex) && 
                !audioLoadingStatus.ContainsKey(nextPreloadIndex))
            {
                preloadQueue.Enqueue(nextPreloadIndex);
                ProcessPreloadQueue();
            }
        }
    
        private void PlayNextNode()
        {
            if (!isPlaying || isPaused) return;
        
            currentNodeIndex++;
        
            if (currentNodeIndex >= dataItems.Count)
            {
                // 演示完成
                OnPresentationCompleted?.Invoke();
                StopPresentation();
                return;
            }
        
            DataItem currentItem = dataItems[currentNodeIndex];
        
            // 更新UI显示
            UpdateNodeDisplay(currentItem);
        
            // 播放音频
            PlayNodeAudio(currentNodeIndex);
        
            OnNodeChanged?.Invoke(currentNodeIndex);
        }
    
        private void UpdateNodeDisplay(DataItem dataItem)
        {
            // 更新文本显示
            if (contentText != null)
            {
                contentText.text = !string.IsNullOrEmpty(dataItem.text) ? dataItem.text : "无内容";
            }
    
            // 更新屏幕图片
            if (!string.IsNullOrEmpty(dataItem.image) && screenMeshRenderer != null)
            {
                StartCoroutine(ImageLoader.LoadImage(
                    dataItem.image,
                    onComplete: (texture) => {
                        if (texture != null && screenMaterial != null)
                        {
                            screenMaterial.mainTexture = texture;
                            Debug.Log($"成功加载图片，尺寸: {texture.width}x{texture.height}");
                        }
                    },
                    onError: (error) => {
                        Debug.LogWarning($"加载图片失败: {error}");
                    }
                ));
            }
            
            // 处理摄像机切换
            if (enableCameraSwitching)
            {
                SwitchCamera(dataItem);
            }
    
            Debug.Log($"显示节点 {currentNodeIndex}: {dataItem.text}");
        }
        
        #region 摄像机切换逻辑
        private void SwitchCamera(DataItem dataItem)
        {
            ScreenCameraType targetCameraType;
        
            // 如果DataItem有摄像机类型信息，直接使用
            if (HasCameraTypeInfo(dataItem))
            {
                targetCameraType = GetCameraTypeFromDataItem(dataItem);
            }
            else
            {
                // 根据切换模式决定摄像机类型
                targetCameraType = DetermineCameraType(dataItem);
            }
        
            // 切换摄像机
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetCameraActive(targetCameraType);
                Debug.Log($"切换到摄像机: {targetCameraType}");
            }
        }
        
        private bool HasCameraTypeInfo(DataItem dataItem)
        {
            // 检查DataItem是否有摄像机类型信息
            // 如果您修改了DataItem类添加了cameraType字段，这里返回true
            // 否则返回false
            return false; // 暂时返回false，如果修改了DataItem可以改为true
        }
    
        private ScreenCameraType GetCameraTypeFromDataItem(DataItem dataItem)
        {
            // 如果DataItem有cameraType字段，返回该值
            // return dataItem.cameraType;
            return ScreenCameraType.MainCamera; // 默认值
        }
        
        private ScreenCameraType DetermineCameraType(DataItem dataItem)
        {
            switch (cameraSwitchMode)
            {
                case CameraSwitchMode.Sequential:
                    return GetSequentialCameraType();
                
                case CameraSwitchMode.Random:
                    return GetRandomCameraType();
                
                case CameraSwitchMode.BasedOnContent:
                    return GetContentBasedCameraType(dataItem);
                
                case CameraSwitchMode.Timer:
                    return GetTimerBasedCameraType();
                
                default:
                    return ScreenCameraType.MainCamera;
            }
        }
        
        private ScreenCameraType GetSequentialCameraType()
        {
            ScreenCameraType cameraType = availableCameraTypes[currentCameraIndex];
            currentCameraIndex = (currentCameraIndex + 1) % availableCameraTypes.Length;
            return cameraType;
        }
    
        private ScreenCameraType GetRandomCameraType()
        {
            int randomIndex = UnityEngine.Random.Range(0, availableCameraTypes.Length);
            return availableCameraTypes[randomIndex];
        }
    
        private ScreenCameraType GetContentBasedCameraType(DataItem dataItem)
        {
            // 根据内容决定摄像机类型的逻辑
            if (!string.IsNullOrEmpty(dataItem.image))
            {
                // 有图片的节点使用远景摄像机更好展示
                return ScreenCameraType.FarCamera;
            }
            else if (!string.IsNullOrEmpty(dataItem.text) && dataItem.text.Length > 100)
            {
                // 长文本使用跟随摄像机增加动态感
                return ScreenCameraType.FollowCamera;
            }
            else
            {
                // 默认使用主摄像机
                return ScreenCameraType.MainCamera;
            }
        }
    
        private ScreenCameraType GetTimerBasedCameraType()
        {
            // 基于时间的切换逻辑
            float timeElapsed = Time.time;
            int cameraIndex = Mathf.FloorToInt(timeElapsed / cameraHoldDuration) % availableCameraTypes.Length;
            return availableCameraTypes[cameraIndex];
        }
    
        // 公共方法：手动设置摄像机切换模式
        public void SetCameraSwitchMode(CameraSwitchMode mode)
        {
            cameraSwitchMode = mode;
        }
    
        // 公共方法：启用/禁用摄像机切换
        public void SetCameraSwitchingEnabled(bool enabled)
        {
            enableCameraSwitching = enabled;
        }
    
        // 公共方法：手动切换到指定摄像机
        public void SwitchToCamera(ScreenCameraType cameraType)
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetCameraActive(cameraType);
            }
        }
        
        #endregion
    
        private void PlayNodeAudio(int nodeIndex)
        {
            if (audioCache.ContainsKey(nodeIndex))
            {
                // 音频已缓存，直接播放
                AudioClip audioClip = audioCache[nodeIndex];
                audioSource.clip = audioClip;
                audioSource.Play();
            
                // 监听音频播放完成
                StartCoroutine(WaitForAudioComplete(audioClip.length));
            }
            else if (audioLoadingStatus.ContainsKey(nodeIndex) && audioLoadingStatus[nodeIndex])
            {
                // 音频正在加载，等待加载完成
                StartCoroutine(WaitForAudioLoad(nodeIndex));
            }
            else
            {
                // 音频未开始加载，立即加载
                LoadAudioForNode(nodeIndex);
                StartCoroutine(WaitForAudioLoad(nodeIndex));
            }
        }
    
        private IEnumerator WaitForAudioLoad(int nodeIndex)
        {
            float timeout = 30f; // 30秒超时
            float elapsed = 0f;
        
            while (!audioCache.ContainsKey(nodeIndex) && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
        
            if (audioCache.ContainsKey(nodeIndex))
            {
                // 加载完成，播放音频
                AudioClip audioClip = audioCache[nodeIndex];
                audioSource.clip = audioClip;
                audioSource.Play();
            
                StartCoroutine(WaitForAudioComplete(audioClip.length));
            }
            else
            {
                // 加载超时，跳过当前节点
                Debug.LogWarning($"节点 {nodeIndex} 音频加载超时，跳过");
                OnAudioPlaybackComplete();
            }
        }
    
        private IEnumerator WaitForAudioComplete(float duration)
        {
            yield return new WaitForSeconds(duration);
        
            // 检查音频是否真的播放完成
            while (audioSource.isPlaying && !isPaused)
            {
                yield return new WaitForSeconds(0.1f);
            }
        
            if (!isPaused)
            {
                OnAudioPlaybackComplete();
            }
        }
    
        private void OnAudioPlaybackComplete()
        {
            if (!isPlaying || isPaused) return;
        
            // 添加节点间延迟
            StartCoroutine(DelayedNextNode());
        }
    
        private IEnumerator DelayedNextNode()
        {
            yield return new WaitForSeconds(delayBetweenNodes);
            PlayNextNode();
        }
    
        private void OnSpeechError(string error)
        {
            Debug.LogError($"语音合成错误: {error}");
            OnError?.Invoke($"语音合成失败: {error}");
        }
    
        private void ClearAudioCache()
        {
            foreach (var audioClip in audioCache.Values)
            {
                if (audioClip != null)
                {
                    Destroy(audioClip);
                }
            }
            audioCache.Clear();
            audioLoadingStatus.Clear();
            preloadQueue.Clear();
        }
    
        // 公共接口方法
        public void JumpToNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= dataItems.Count) return;
        
            audioSource.Stop();
            currentNodeIndex = nodeIndex - 1; // -1因为PlayNextNode会+1
            PlayNextNode();
        }
    
        public int GetCurrentNodeIndex()
        {
            return currentNodeIndex;
        }
    
        public int GetTotalNodeCount()
        {
            return dataItems != null ? dataItems.Count : 0;
        }
    
        public bool IsPlaying()
        {
            return isPlaying;
        }
    
        public bool IsPaused()
        {
            return isPaused;
        }
    
        private void OnDestroy()
        {
            if (speechService != null)
            {
                speechService.OnAudioClipReady -= OnAudioClipReady;
                speechService.OnError -= OnSpeechError;
            }
        
            ClearAudioCache();
        }
    }
}