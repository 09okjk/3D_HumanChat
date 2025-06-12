using System;
using System.Collections;
using System.Collections.Generic;
using Tools;
using UnityEngine;
using UnityEngine.Networking;

namespace API
{
    
    [System.Serializable]
    public class SpeechSynthesisRequest
    {
        public string text;
        public string voice;
        public string language = "zh-CN";
        public float speed = 1.0f;
        public float pitch = 1.0f;
        public string tts_model;
    }

    [System.Serializable]
    public class SpeechSynthesisResponse
    {
        public bool success;
        public string message;
        public string request_id;
        public string timestamp;
        public string audio_data; // base64编码的音频数据
        public string format;
        public float duration;
        public float processing_time;
        public string model_used;
        public string synthesis_mode;
        public int sample_rate;
    }

    public class SpeechSynthesisService : ApiServiceBase
    {
        [Header("Speech Synthesis Settings")]
        public string defaultVoice = "default";
        public string defaultLanguage = "zh-CN";
        public float defaultSpeed = 1.0f;
        public float defaultPitch = 1.0f;
        public string defaultTtsModel = null;
        
        [Header("Audio Processing")]
        public bool autoConvertToAudioClip = true;
        
        // 语音合成特定事件
        public Action<SpeechSynthesisResponse> OnSynthesisCompleted; // 合成完成事件
        public Action<AudioClip, SpeechSynthesisResponse> OnAudioClipReady; // 音频片段准备就绪事件
        public Action OnSynthesisStarted; // 合成开始事件
        
        /// <summary>
        /// 合成语音 - 主要接口
        /// </summary>
        public void SynthesizeVoice(string text, string voice = null, string language = null, 
                                   float? speed = null, float? pitch = null, string ttsModel = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                OnError?.Invoke("要合成的文本不能为空");
                return;
            }
            
            StartCoroutine(SynthesizeVoiceCoroutine(text, voice, language, speed, pitch, ttsModel));
        }
        
        /// <summary>
        /// 快速合成语音 - 使用默认设置
        /// </summary>
        public void QuickSynthesize(string text)
        {
            SynthesizeVoice(text);
        }
        
        /// <summary>
        /// 批量合成语音
        /// </summary>
        public void BatchSynthesize(List<string> texts, string voice = null, string language = null)
        {
            if (texts == null || texts.Count == 0)
            {
                OnError?.Invoke("批量合成的文本列表不能为空");
                return;
            }
            
            StartCoroutine(BatchSynthesizeCoroutine(texts, voice, language));
        }
        
        private IEnumerator SynthesizeVoiceCoroutine(string text, string voice, string language, 
                                                    float? speed, float? pitch, string ttsModel)
        {
            OnSynthesisStarted?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log($"开始语音合成: {text.Substring(0, Math.Min(50, text.Length))}...");
            }
            
            // 构建请求数据
            var requestData = new SpeechSynthesisRequest
            {
                text = text,
                voice = voice ?? defaultVoice,
                language = language ?? defaultLanguage,
                speed = speed ?? defaultSpeed,
                pitch = pitch ?? defaultPitch,
                tts_model = ttsModel ?? defaultTtsModel
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            string url = $"{baseUrl}/api/speech/synthesize";
            
            using (UnityWebRequest request = CreatePostRequest(url, jsonData))
            {
                yield return ExecuteRequestWithRetry(
                    request, 
                    "语音合成",
                    OnSynthesisRequestSuccess,
                    OnSynthesisRequestFailure
                );
            }
        }
        
        private IEnumerator BatchSynthesizeCoroutine(List<string> texts, string voice, string language)
        {
            int totalTexts = texts.Count;
            int completedCount = 0;
            
            if (enableDebugLogs)
            {
                Debug.Log($"开始批量语音合成，共{totalTexts}个文本");
            }
            
            foreach (string text in texts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    yield return SynthesizeVoiceCoroutine(text, voice, language, null, null, null);
                    completedCount++;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"批量合成进度: {completedCount}/{totalTexts}");
                    }
                    
                    // 添加短暂延迟避免过快请求
                    yield return new WaitForSeconds(0.5f);
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"批量语音合成完成，共处理{completedCount}个文本");
            }
        }
        
        private void OnSynthesisRequestSuccess(UnityWebRequest request)
        {
            StartCoroutine(ProcessSynthesisResponse(request.downloadHandler.text));
        }
        
        private void OnSynthesisRequestFailure(UnityWebRequest request, string operation)
        {
            HandleRequestError(request, operation);
        }
        
        private IEnumerator ProcessSynthesisResponse(string responseText)
        {
            SpeechSynthesisResponse response = ParseJsonResponse<SpeechSynthesisResponse>(responseText, "语音合成");
            
            if (response == null)
            {
                yield break;
            }
            
            if (!response.success)
            {
                OnError?.Invoke($"语音合成失败: {response.message}");
                yield break;
            }
            
            // 触发合成完成事件
            OnSynthesisCompleted?.Invoke(response);
            
            // 如果需要自动转换为AudioClip
            if (autoConvertToAudioClip && !string.IsNullOrEmpty(response.audio_data))
            {
                yield return ConvertToAudioClip(response);
            }
        }
        
        private IEnumerator ConvertToAudioClip(SpeechSynthesisResponse response)
        {
            try
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"开始转换音频数据，格式: {response.format}, 时长: {response.duration}s");
                }
                
                // 解码base64音频数据
                byte[] audioBytes = Convert.FromBase64String(response.audio_data);
                
                // 根据格式转换为AudioClip
                AudioClip audioClip = null;
                
                if (response.format == "WAV" || response.format == "wav")
                {
                    audioClip = WavUtility.ToAudioClip(audioBytes);
                }
                else if (response.format == "PCM_16" || response.format == "pcm")
                {
                    audioClip = CreateAudioClipFromPCM(audioBytes, response.sample_rate > 0 ? response.sample_rate : 16000);
                }
                else
                {
                    Debug.LogWarning($"未知音频格式: {response.format}，尝试作为WAV处理");
                    audioClip = WavUtility.ToAudioClip(audioBytes);
                }
                
                if (audioClip != null)
                {
                    audioClip.name = $"SynthesizedVoice_{response.request_id}";
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"音频转换成功: {audioClip.name}, 长度: {audioClip.length}s");
                    }
                    
                    OnAudioClipReady?.Invoke(audioClip, response);
                }
                else
                {
                    OnError?.Invoke("音频数据转换失败");
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"音频转换异常: {e.Message}");
                if (enableDebugLogs)
                {
                    Debug.LogError($"音频转换异常: {e.Message}");
                }
            }
            
            yield return null;
        }
        
        private AudioClip CreateAudioClipFromPCM(byte[] pcmData, int sampleRate)
        {
            // 将PCM字节数据转换为float数组
            float[] samples = new float[pcmData.Length / 2];
            
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                samples[i] = sample / 32768f; // 转换为-1到1的范围
            }
            
            AudioClip audioClip = AudioClip.Create("PCMAudio", samples.Length, 1, sampleRate, false);
            audioClip.SetData(samples, 0);
            
            return audioClip;
        }
        
        /// <summary>
        /// 获取可用的语音列表
        /// </summary>
        public void GetAvailableVoices()
        {
            StartCoroutine(GetAvailableVoicesCoroutine());
        }
        
        private IEnumerator GetAvailableVoicesCoroutine()
        {
            string url = $"{baseUrl}/api/speech/voices"; // 假设后端有这个接口
            
            using (UnityWebRequest request = CreateGetRequest(url))
            {
                yield return ExecuteRequestWithRetry(
                    request,
                    "获取语音列表",
                    (req) => {
                        if (enableDebugLogs)
                        {
                            Debug.Log($"可用语音列表: {req.downloadHandler.text}");
                        }
                    }
                );
            }
        }
        
        /// <summary>
        /// 检查服务状态
        /// </summary>
        public void CheckServiceStatus()
        {
            StartCoroutine(CheckServiceStatusCoroutine());
        }
        
        private IEnumerator CheckServiceStatusCoroutine()
        {
            string url = $"{baseUrl}/api/health";
            
            using (UnityWebRequest request = CreateGetRequest(url))
            {
                yield return ExecuteRequestWithRetry(
                    request,
                    "检查服务状态",
                    (req) => {
                        if (enableDebugLogs)
                        {
                            Debug.Log($"服务状态: {req.downloadHandler.text}");
                        }
                    }
                );
            }
        }
    }
}
