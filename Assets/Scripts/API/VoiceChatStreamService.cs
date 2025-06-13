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
        public string type; // "recognition", "text", "audio", "error", "done"
        public string request_id; // 请求ID
        public string segment_id; // 段落ID (可选)
        public string text; // 文本内容
        public string audio; // base64音频数据
        public string format; // 音频格式
        public string message; // 错误消息
        public float processing_time; // 处理时间
    
        // 兼容性属性
        public string content => text;
        public string session_id => request_id;
        public float timestamp => processing_time;
        public bool is_final => type == "done";
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
        
        // /// <summary>
        // /// 发送语音对话流式请求
        // /// </summary>
        // public void SendVoiceChatStream(AudioClip audioClip, string llmModel = null, string systemPrompt = null, string sessionId = null)
        // {
        //     if (audioClip == null)
        //     {
        //         OnError?.Invoke("音频片段为空");
        //         return;
        //     }
        //     
        //     recordedClip = audioClip;
        //     StartCoroutine(SendVoiceChatStreamRequest(llmModel, systemPrompt, sessionId));
        // }
        
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
    
            // 使用真正的流式处理
            yield return StartCoroutine(ProcessStreamingRequest(url, audioData));
    
            OnVoiceChatCompleted?.Invoke();
        }
        
        /// <summary>
        /// 处理流式请求 - 真正的流式实现
        /// </summary>
        private IEnumerator ProcessStreamingRequest(string url, byte[] audioData)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("audio_file", audioData, "audio.wav", "audio/wav"));
    
            using (UnityWebRequest request = UnityWebRequest.Post(url, formData))
            {
                // 设置请求头
                request.SetRequestHeader("Accept", "application/x-ndjson");
                request.SetRequestHeader("User-Agent", "Unity-VoiceChatStream/1.0");
        
                // 设置超时（流式请求需要更长时间）
                request.timeout = timeoutSeconds * 3; // 增加超时时间
        
                // 设置证书处理器
                if (skipSSLValidation && baseUrl.StartsWith("https://"))
                {
                    request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
                }
        
                if (enableDebugLogs)
                {
                    Debug.Log($"发送流式语音对话请求: {url}");
                }
        
                // 开始请求但不等待完成
                var operation = request.SendWebRequest();
        
                // 实时处理流式数据
                yield return StartCoroutine(ProcessRealTimeStream(request, operation));
        
                // 检查最终状态
                if (request.result != UnityWebRequest.Result.Success)
                {
                    HandleRequestError(request, "流式语音对话请求");
                }
            }
        }
        
        /// <summary>
        /// 实时处理流式数据
        /// </summary>
        private IEnumerator ProcessRealTimeStream(UnityWebRequest request, UnityWebRequestAsyncOperation operation)
        {
            string buffer = "";
            long lastProcessedBytes = 0;
    
            while (!operation.isDone)
            {
                // 获取已下载的数据
                byte[] data = request.downloadHandler?.data;
                if (data != null && data.Length > lastProcessedBytes)
                {
                    // 处理新接收到的数据
                    string newData = System.Text.Encoding.UTF8.GetString(
                        data, (int)lastProcessedBytes, (int)(data.Length - lastProcessedBytes));
            
                    buffer += newData;
                    lastProcessedBytes = data.Length;
            
                    // 处理缓冲区中的完整行
                    buffer = ProcessStreamBuffer(buffer);
            
                    if (enableDebugLogs && !string.IsNullOrEmpty(newData))
                    {
                        Debug.Log($"接收到流式数据: {newData.Length} 字节");
                    }
                }
        
                yield return new WaitForSeconds(0.01f); // 频繁检查新数据
            }
    
            // 处理剩余的缓冲区数据
            if (!string.IsNullOrEmpty(buffer.Trim()))
            {
                ProcessStreamBuffer(buffer + "\n"); // 确保最后一行被处理
            }
        }
        
        /// <summary>
        /// 处理流式数据缓冲区
        /// </summary>
        private string ProcessStreamBuffer(string buffer)
        {
            string[] lines = buffer.Split('\n');
    
            // 处理除最后一行外的所有完整行
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    ProcessSingleJsonLine(line);
                }
            }
    
            // 返回最后一行（可能不完整）
            return lines[lines.Length - 1];
        }

        /// <summary>
        /// 处理单行JSON数据
        /// </summary>
        private void ProcessSingleJsonLine(string jsonLine)
        {
            try
            {
                VoiceChatStreamResponse response = JsonUtility.FromJson<VoiceChatStreamResponse>(jsonLine);
                if (response != null)
                {
                    // 立即处理响应，不使用协程以保证实时性
                    ProcessStreamResponseImmediate(response);
                }
            }
            catch (Exception e)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"解析JSON行失败: {jsonLine}\n错误: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// 立即处理流式响应（非协程版本，保证实时性）
        /// </summary>
        private void ProcessStreamResponseImmediate(VoiceChatStreamResponse response)
        {
            switch (response.type)
            {
                case "recognition":
                    // 语音识别结果
                    if (enableTextOutput && !string.IsNullOrEmpty(response.text))
                    {
                        OnTextReceived?.Invoke($"[识别] {response.text}");
                        if (enableDebugLogs)
                        {
                            Debug.Log($"语音识别: {response.text}");
                        }
                    }
                    break;
            
                case "text":
                    // 文本响应片段
                    if (enableTextOutput && !string.IsNullOrEmpty(response.text))
                    {
                        OnTextReceived?.Invoke(response.text);
                        if (enableDebugLogs)
                        {
                            Debug.Log($"接收到文本片段: {response.text}");
                        }
                    }
                    break;
            
                case "audio":
                    // 音频响应片段
                    if (autoPlayAudio && !string.IsNullOrEmpty(response.audio))
                    {
                        // 启动协程处理音频（异步处理）
                        StartCoroutine(ProcessAudioSegment(response));
                    }
                    break;
            
                case "error":
                    // 错误信息
                    string errorMsg = response.message ?? "未知错误";
                    OnError?.Invoke(errorMsg);
                    if (enableDebugLogs)
                    {
                        Debug.LogError($"服务器错误: {errorMsg}");
                    }
                    break;
            
                case "done":
                    // 完成信号
                    OnStreamCompleted?.Invoke(response.request_id);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"流式响应完成，处理时间: {response.processing_time}s");
                    }
                    break;
            
                default:
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"未知的响应类型: {response.type}");
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 处理音频片段
        /// </summary>
        private IEnumerator ProcessAudioSegment(VoiceChatStreamResponse response)
        {
            AudioClip audioClip = ConvertBase64ToAudioClip(response);
    
            if (audioClip != null)
            {
                // 将音频添加到队列
                audioQueue.Add(audioClip);
        
                // 如果当前没有播放音频，开始播放队列
                if (!isPlayingAudio)
                {
                    yield return StartCoroutine(PlayAudioQueue());
                }
            }
        }

        /// <summary>
        /// 播放音频队列
        /// </summary>
        private IEnumerator PlayAudioQueue()
        {
            isPlayingAudio = true;
    
            while (audioQueue.Count > 0)
            {
                AudioClip clip = audioQueue[0];
                audioQueue.RemoveAt(0);
        
                if (clip != null)
                {
                    yield return PlayAudioClip(clip);
                }
            }
    
            isPlayingAudio = false;
        }
        
        // /// <summary>
        // /// 安全地解析JSON响应（不使用yield return）
        // /// </summary>
        // private VoiceChatStreamResponse ParseJsonSafely(string jsonData)
        // {
        //     try
        //     {
        //         return JsonUtility.FromJson<VoiceChatStreamResponse>(jsonData);
        //     }
        //     catch (Exception e)
        //     {
        //         if (enableDebugLogs)
        //         {
        //             Debug.LogWarning($"解析流式响应行失败: {jsonData}, 错误: {e.Message}");
        //         }
        //         return null;
        //     }
        // }
        //
        // /// <summary>
        // /// 处理单个流式响应项 - 修正版本
        // /// </summary>
        // private IEnumerator ProcessStreamResponseItem(VoiceChatStreamResponse response)
        // {
        //     switch (response.type)
        //     {
        //         case "recognition":
        //             // 语音识别结果
        //             if (enableTextOutput && !string.IsNullOrEmpty(response.text))
        //             {
        //                 OnTextReceived?.Invoke($"[识别] {response.text}");
        //                 if (enableDebugLogs)
        //                 {
        //                     Debug.Log($"语音识别: {response.text}");
        //                 }
        //             }
        //             break;
        //     
        //         case "text":
        //             // 文本响应
        //             if (enableTextOutput && !string.IsNullOrEmpty(response.text))
        //             {
        //                 OnTextReceived?.Invoke(response.text);
        //                 if (enableDebugLogs)
        //                 {
        //                     Debug.Log($"接收到文本: {response.text}");
        //                 }
        //             }
        //             break;
        //     
        //         case "audio":
        //             // 音频响应
        //             if (autoPlayAudio && !string.IsNullOrEmpty(response.audio))
        //             {
        //                 yield return ProcessAudioResponse(response);
        //             }
        //             break;
        //     
        //         case "done":
        //             // 完成信号
        //             if (enableDebugLogs)
        //             {
        //                 Debug.Log($"流式响应完成，处理时间: {response.processing_time}s");
        //             }
        //             break;
        //     
        //         default:
        //             if (enableDebugLogs)
        //             {
        //                 Debug.LogWarning($"未知的响应类型: {response.type}");
        //             }
        //             break;
        //     }
        //
        //     yield return null;
        // }
        //
        // /// <summary>
        // /// 处理音频响应 - 修正版本（修复try/catch中的yield return问题）
        // /// </summary>
        // private IEnumerator ProcessAudioResponse(VoiceChatStreamResponse response)
        // {
        //     if (string.IsNullOrEmpty(response.audio))
        //     {
        //         yield break;
        //     }
        //
        //     // 先处理音频数据转换（在try/catch中，但不使用yield）
        //     AudioClip audioClip = ConvertBase64ToAudioClip(response);
        //
        //     // 如果转换成功，播放音频（在try/catch外部，可以使用yield）
        //     if (audioClip != null && autoPlayAudio)
        //     {
        //         yield return PlayAudioClip(audioClip);
        //     }
        // }

        /// <summary>
        /// 将base64音频数据转换为AudioClip（不使用yield return）
        /// </summary>
        private AudioClip ConvertBase64ToAudioClip(VoiceChatStreamResponse response)
        {
            try
            {
                // 解码base64音频数据
                byte[] audioBytes = Convert.FromBase64String(response.audio);

                // 转换为AudioClip
                AudioClip audioClip = WavUtility.ToAudioClip(audioBytes);

                if (audioClip != null)
                {
                    audioClip.name = $"VoiceResponse_{response.segment_id}";
                    OnAudioReceived?.Invoke(audioClip);
    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"接收到音频: {audioClip.name}, 长度: {audioClip.length}s");
                    }
            
                    return audioClip;
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
    
            return null;
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
        
        // /// <summary>
        // /// 检查麦克风权限
        // /// </summary>
        // public bool CheckMicrophonePermission()
        // {
        //     return Application.HasUserAuthorization(UserAuthorization.Microphone);
        // }
        //
        // /// <summary>
        // /// 请求麦克风权限
        // /// </summary>
        // public void RequestMicrophonePermission()
        // {
        //     if (!CheckMicrophonePermission())
        //     {
        //         StartCoroutine(RequestMicrophonePermissionCoroutine());
        //     }
        // }
        //
        // private IEnumerator RequestMicrophonePermissionCoroutine()
        // {
        //     yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        //     
        //     if (CheckMicrophonePermission())
        //     {
        //         if (enableDebugLogs)
        //         {
        //             Debug.Log("麦克风权限已获取");
        //         }
        //     }
        //     else
        //     {
        //         OnError?.Invoke("麦克风权限被拒绝");
        //     }
        // }
        private void OnDestroy()
        {
            StopVoiceChatStream();
        }
    }
}