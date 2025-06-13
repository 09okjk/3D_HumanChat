using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using API;
using Tools;

namespace API
{
    [System.Serializable]
    public class VoiceChatStreamRequest
    {
        public string llm_model;
        public string system_prompt;
        public string session_id;
        // 音频文件将通过multipart/form-data上传
    }

    [System.Serializable]
    public class VoiceChatStreamResponse
    {
        public string type; // "text" 或 "audio"
        public string content; // 文本内容或base64音频数据
        public string session_id;
        public bool is_final;
        public float timestamp;
    }

    public class VoiceChatStreamService : ApiServiceBase
    {
        [Header("Voice Chat Settings")]
        public string defaultLlmModel = "gpt-3.5-turbo";
        public string defaultSystemPrompt = "你是一个友好的AI助手。";
        public string sessionId = "";
        
        [Header("Audio Settings")]
        public AudioSource audioSource;
        public int sampleRate = 16000;
        public int maxRecordTime = 60;
        
        [Header("Stream Settings")]
        public bool autoPlayAudio = true;
        public bool enableTextOutput = true;
        
        // 录音相关
        private AudioClip recordedClip;
        private bool isRecording = false;
        private string microphoneDevice;
        
        // 流式响应相关
        private Coroutine streamCoroutine;
        private List<AudioClip> audioQueue = new List<AudioClip>();
        private bool isPlayingAudio = false;
        
        // 事件回调
        public Action OnRecordingStarted;
        public Action OnRecordingStopped;
        public Action<string> OnTextReceived; // 接收到文本时触发
        public Action<AudioClip> OnAudioReceived; // 接收到音频时触发
        public Action<string> OnStreamCompleted; // 流式响应完成
        public Action OnVoiceChatStarted; // 语音对话开始
        public Action OnVoiceChatCompleted; // 语音对话完成
        
        protected override void InitializeService()
        {
            base.InitializeService();
            
            // 初始化音频源
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            // 获取默认麦克风设备
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
                if (enableDebugLogs)
                {
                    Debug.Log($"使用麦克风设备: {microphoneDevice}");
                }
            }
            else
            {
                Debug.LogError("未找到麦克风设备");
            }
            
            // 生成会话ID
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = System.Guid.NewGuid().ToString();
            }
        }
        
        /// <summary>
        /// 开始录音
        /// </summary>
        public void StartRecording()
        {
            if (isRecording)
            {
                Debug.LogWarning("已经在录音中");
                return;
            }
            
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                OnError?.Invoke("没有可用的麦克风设备");
                return;
            }
            
            recordedClip = Microphone.Start(microphoneDevice, false, maxRecordTime, sampleRate);
            isRecording = true;
            
            OnRecordingStarted?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log("开始录音...");
            }
        }
        
        /// <summary>
        /// 停止录音并发送语音对话请求
        /// </summary>
        public void StopRecordingAndSendVoiceChat()
        {
            if (!isRecording)
            {
                Debug.LogWarning("当前没有在录音");
                return;
            }
            
            Microphone.End(microphoneDevice);
            isRecording = false;
            
            OnRecordingStopped?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log("录音结束，开始发送语音对话请求...");
            }
            
            // 发送语音对话流式请求
            StartCoroutine(SendVoiceChatStreamRequest());
        }
        
        /// <summary>
        /// 发送语音对话流式请求
        /// </summary>
        public void SendVoiceChatStream(AudioClip audioClip, string llmModel = null, string systemPrompt = null, string sessionId = null)
        {
            if (audioClip == null)
            {
                OnError?.Invoke("音频片段为空");
                return;
            }
            
            recordedClip = audioClip;
            StartCoroutine(SendVoiceChatStreamRequest(llmModel, systemPrompt, sessionId));
        }
        
        private IEnumerator SendVoiceChatStreamRequest(string llmModel = null, string systemPrompt = null, string sessionIdParam = null)
        {
            OnVoiceChatStarted?.Invoke();
            
            if (recordedClip == null)
            {
                OnError?.Invoke("没有录音数据");
                yield break;
            }
            
            // 将AudioClip转换为WAV字节数组
            byte[] audioData = ConvertAudioClipToWav(recordedClip);
            
            if (audioData == null)
            {
                OnError?.Invoke("音频数据转换失败");
                yield break;
            }
            
            // 构建URL参数
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(llmModel ?? defaultLlmModel))
            {
                parameters["llm_model"] = llmModel ?? defaultLlmModel;
            }
            
            if (!string.IsNullOrEmpty(systemPrompt ?? defaultSystemPrompt))
            {
                parameters["system_prompt"] = systemPrompt ?? defaultSystemPrompt;
            }
            
            if (!string.IsNullOrEmpty(sessionIdParam ?? sessionId))
            {
                parameters["session_id"] = sessionIdParam ?? sessionId;
            }
            
            string url = BuildUrlWithParams($"{baseUrl}/api/chat/voice/stream", parameters);
            
            // 创建multipart/form-data请求
            using (UnityWebRequest request = CreateVoiceChatStreamRequest(url, audioData))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"发送语音对话流式请求: {url}");
                }
                
                yield return request.SendWebRequest();
                
                if (IsRequestSuccessful(request))
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log("语音对话流式请求成功");
                    }
                    
                    // 处理流式响应
                    yield return ProcessVoiceChatStreamResponse(request.downloadHandler.text);
                }
                else
                {
                    HandleRequestError(request, "语音对话流式请求");
                }
            }
            
            OnVoiceChatCompleted?.Invoke();
        }
        
        /// <summary>
        /// 创建语音对话流式请求
        /// </summary>
        private UnityWebRequest CreateVoiceChatStreamRequest(string url, byte[] audioData)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("audio", audioData, "audio.wav", "audio/wav"));
            
            UnityWebRequest request = UnityWebRequest.Post(url, formData);
            
            // 设置超时
            request.timeout = timeoutSeconds;
            
            // 设置请求头
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", $"Unity-VoiceChatStream/1.0");
            
            // 设置证书处理器
            if (skipSSLValidation && baseUrl.StartsWith("https://"))
            {
                request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            }
            
            return request;
        }
        
        /// <summary>
        /// 处理流式响应
        /// </summary>
        private IEnumerator ProcessVoiceChatStreamResponse(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                OnError?.Invoke("流式响应为空");
                yield break;
            }
            
            // 解析流式响应（假设后端返回的是换行分隔的JSON）
            string[] responseLines = responseText.Split('\n');
            
            foreach (string line in responseLines)
            {
                if (string.IsNullOrEmpty(line.Trim()))
                    continue;
                
                VoiceChatStreamResponse response = ParseJsonResponse<VoiceChatStreamResponse>(line, "语音对话流式响应");
                
                if (response != null)
                {
                    yield return ProcessStreamResponseItem(response);
                }
                
                // 添加小延迟以模拟流式效果
                yield return new WaitForSeconds(0.1f);
            }
            
            OnStreamCompleted?.Invoke(sessionId);
        }
        
        /// <summary>
        /// 处理单个流式响应项
        /// </summary>
        private IEnumerator ProcessStreamResponseItem(VoiceChatStreamResponse response)
        {
            if (response.type == "text" && enableTextOutput)
            {
                OnTextReceived?.Invoke(response.content);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"接收到文本: {response.content}");
                }
            }
            else if (response.type == "audio" && autoPlayAudio)
            {
                yield return ProcessAudioResponse(response);
            }
            
            yield return null;
        }
        
        /// <summary>
        /// 处理音频响应
        /// </summary>
        private IEnumerator ProcessAudioResponse(VoiceChatStreamResponse response)
        {
            AudioClip audioClip = null;
    
            try
            {
                if (string.IsNullOrEmpty(response.content))
                {
                    yield break;
                }

                // 解码base64音频数据
                byte[] audioBytes = Convert.FromBase64String(response.content);

                // 转换为AudioClip
                audioClip = WavUtility.ToAudioClip(audioBytes);

                if (audioClip != null)
                {
                    audioClip.name = $"VoiceResponse_{response.timestamp}";
                    OnAudioReceived?.Invoke(audioClip);
            
                    if (enableDebugLogs)
                    {
                        Debug.Log($"接收到音频: {audioClip.name}, 长度: {audioClip.length}s");
                    }
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"音频响应处理失败: {e.Message}");
                if (enableDebugLogs)
                {
                    Debug.LogError($"音频响应处理异常: {e.Message}");
                }
            }
    
            // 将播放逻辑移到try/catch块外部
            if (audioClip != null && autoPlayAudio)
            {
                yield return PlayAudioClip(audioClip);
            }
        }
        
        /// <summary>
        /// 播放音频片段
        /// </summary>
        private IEnumerator PlayAudioClip(AudioClip clip)
        {
            if (audioSource == null || clip == null)
                yield break;
            
            // 等待当前音频播放完成
            while (isPlayingAudio)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            isPlayingAudio = true;
            audioSource.clip = clip;
            audioSource.Play();
            
            // 等待音频播放完成
            yield return new WaitForSeconds(clip.length);
            
            isPlayingAudio = false;
        }
        
        /// <summary>
        /// 将AudioClip转换为WAV字节数组
        /// </summary>
        private byte[] ConvertAudioClipToWav(AudioClip clip)
        {
            try
            {
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                
                return WavUtility.FromAudioClip(clip, samples, clip.frequency, clip.channels);
            }
            catch (Exception e)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError($"音频转换异常: {e.Message}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// 停止当前的语音对话流
        /// </summary>
        public void StopVoiceChatStream()
        {
            if (streamCoroutine != null)
            {
                StopCoroutine(streamCoroutine);
                streamCoroutine = null;
            }
            
            if (isRecording)
            {
                Microphone.End(microphoneDevice);
                isRecording = false;
                OnRecordingStopped?.Invoke();
            }
            
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            isPlayingAudio = false;
            audioQueue.Clear();
            
            if (enableDebugLogs)
            {
                Debug.Log("语音对话流已停止");
            }
        }
        
        /// <summary>
        /// 检查麦克风权限
        /// </summary>
        public bool CheckMicrophonePermission()
        {
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
        }
        
        /// <summary>
        /// 请求麦克风权限
        /// </summary>
        public void RequestMicrophonePermission()
        {
            if (!CheckMicrophonePermission())
            {
                StartCoroutine(RequestMicrophonePermissionCoroutine());
            }
        }
        
        private IEnumerator RequestMicrophonePermissionCoroutine()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            
            if (CheckMicrophonePermission())
            {
                if (enableDebugLogs)
                {
                    Debug.Log("麦克风权限已获取");
                }
            }
            else
            {
                OnError?.Invoke("麦克风权限被拒绝");
            }
        }
        
        /// <summary>
        /// 获取当前会话ID
        /// </summary>
        public string GetSessionId()
        {
            return sessionId;
        }
        
        /// <summary>
        /// 设置新的会话ID
        /// </summary>
        public void SetSessionId(string newSessionId)
        {
            sessionId = newSessionId;
        }
        
        /// <summary>
        /// 重置会话（生成新的会话ID）
        /// </summary>
        public void ResetSession()
        {
            sessionId = System.Guid.NewGuid().ToString();
            audioQueue.Clear();
            
            if (enableDebugLogs)
            {
                Debug.Log($"会话已重置，新会话ID: {sessionId}");
            }
        }
        
        private void OnDestroy()
        {
            StopVoiceChatStream();
        }
    }
}