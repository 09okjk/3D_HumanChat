using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

namespace API
{
    public abstract class ApiServiceBase : MonoBehaviour
    {
        [Header("API Configuration")]
        public string baseUrl = "http://192.168.18.122:8000";
        
        [Header("SSL Configuration")]
        public bool skipSSLValidation = true;
        public bool enableDebugLogs = true;
        
        [Header("Request Configuration")]
        public int timeoutSeconds = 60;
        public int maxRetries = 3;
        public float retryDelaySeconds = 2f;
        
        // 通用错误回调
        public System.Action<string> OnError;
        
        protected virtual void Awake()
        {
            InitializeService();
        }
        
        protected virtual void InitializeService()
        {
            if (skipSSLValidation)
            {
                SSLHelper.InitializeSSL();
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"{GetType().Name} initialized with base URL: {baseUrl}");
            }
        }
        
        /// <summary>
        /// 创建GET请求
        /// </summary>
        protected UnityWebRequest CreateGetRequest(string url)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            ConfigureRequest(request);
            return request;
        }
        
        /// <summary>
        /// 创建POST请求
        /// </summary>
        protected UnityWebRequest CreatePostRequest(string url, string jsonData)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            
            if (!string.IsNullOrEmpty(jsonData))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            
            request.downloadHandler = new DownloadHandlerBuffer();
            ConfigureRequest(request);
            return request;
        }
        
        /// <summary>
        /// 创建带文件上传的POST请求
        /// </summary>
        protected UnityWebRequest CreatePostRequestWithFile(string url, byte[] fileData, string fileName, string fieldName = "file")
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection(fieldName, fileData, fileName, "application/octet-stream"));
            
            UnityWebRequest request = UnityWebRequest.Post(url, formData);
            ConfigureRequest(request);
            return request;
        }
        
        /// <summary>
        /// 配置请求的通用设置
        /// </summary>
        protected virtual void ConfigureRequest(UnityWebRequest request)
        {
            // 设置超时
            request.timeout = timeoutSeconds;
            
            // 设置请求头
            if (request.method == "POST" && request.uploadHandler != null)
            {
                // POST请求且有数据上传时设置Content-Type
                if (!(request.uploadHandler is UploadHandlerFile))
                {
                    request.SetRequestHeader("Content-Type", "application/json");
                }
            }
            
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", $"Unity-{GetType().Name}/1.0");
            request.SetRequestHeader("Cache-Control", "no-cache");
            
            // 设置证书处理器
            if (skipSSLValidation && baseUrl.StartsWith("https://"))
            {
                request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            }
        }
        
        /// <summary>
        /// 检查请求是否成功
        /// </summary>
        protected bool IsRequestSuccessful(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success;
        }
        
        /// <summary>
        /// 带重试机制的请求执行
        /// </summary>
        protected IEnumerator ExecuteRequestWithRetry(UnityWebRequest request, string operation, 
                                                     System.Action<UnityWebRequest> onSuccess, 
                                                     System.Action<UnityWebRequest, string> onFailure = null,
                                                     int currentRetry = 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"执行{operation} (第{currentRetry + 1}次): {request.url}");
            }
            
            yield return request.SendWebRequest();
            
            if (IsRequestSuccessful(request))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"{operation}成功");
                }
                onSuccess?.Invoke(request);
            }
            else if (currentRetry < maxRetries - 1)
            {
                Debug.LogWarning($"{operation}失败，{retryDelaySeconds}秒后重试... (错误: {request.error})");
                yield return new WaitForSeconds(retryDelaySeconds);
                
                // 重新创建请求（因为UnityWebRequest只能使用一次）
                using (UnityWebRequest retryRequest = CloneRequest(request))
                {
                    yield return ExecuteRequestWithRetry(retryRequest, operation, onSuccess, onFailure, currentRetry + 1);
                }
            }
            else
            {
                if (onFailure != null)
                {
                    onFailure.Invoke(request, operation);
                }
                else
                {
                    HandleRequestError(request, operation);
                }
            }
        }
        
        /// <summary>
        /// 克隆请求（用于重试）
        /// </summary>
        protected virtual UnityWebRequest CloneRequest(UnityWebRequest originalRequest)
        {
            UnityWebRequest newRequest;
            
            if (originalRequest.method == "GET")
            {
                newRequest = CreateGetRequest(originalRequest.url);
            }
            else if (originalRequest.method == "POST")
            {
                string jsonData = "";
                if (originalRequest.uploadHandler != null && originalRequest.uploadHandler.data != null)
                {
                    jsonData = Encoding.UTF8.GetString(originalRequest.uploadHandler.data);
                }
                newRequest = CreatePostRequest(originalRequest.url, jsonData);
            }
            else
            {
                newRequest = UnityWebRequest.Get(originalRequest.url);
                ConfigureRequest(newRequest);
            }
            
            return newRequest;
        }
        
        /// <summary>
        /// 处理请求错误
        /// </summary>
        protected virtual void HandleRequestError(UnityWebRequest request, string operation)
        {
            StringBuilder errorBuilder = new StringBuilder();
            errorBuilder.AppendLine($"{operation}失败:");
            errorBuilder.AppendLine($"错误: {request.error}");
            errorBuilder.AppendLine($"URL: {request.url}");
            errorBuilder.AppendLine($"响应码: {request.responseCode}");
            
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                errorBuilder.AppendLine($"响应内容: {request.downloadHandler.text}");
            }
            
            string errorMessage = errorBuilder.ToString();
            OnError?.Invoke(errorMessage);
            
            if (enableDebugLogs)
            {
                Debug.LogError(errorMessage);
            }
        }
        
        /// <summary>
        /// 安全的JSON解析
        /// </summary>
        protected T ParseJsonResponse<T>(string jsonString, string operation) where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(jsonString))
                {
                    throw new Exception("响应内容为空");
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"{operation}响应: {jsonString.Substring(0, Math.Min(200, jsonString.Length))}...");
                }
                
                T result = JsonUtility.FromJson<T>(jsonString);
                return result;
            }
            catch (Exception e)
            {
                string errorMessage = $"{operation}响应解析失败: {e.Message}";
                OnError?.Invoke(errorMessage);
                
                if (enableDebugLogs)
                {
                    Debug.LogError($"{errorMessage}\n响应内容: {jsonString}");
                }
                
                return null;
            }
        }
        
        /// <summary>
        /// 构建URL参数
        /// </summary>
        protected string BuildUrlWithParams(string baseUrl, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return baseUrl;
            }
            
            StringBuilder urlBuilder = new StringBuilder(baseUrl);
            urlBuilder.Append("?");
            
            bool first = true;
            foreach (var param in parameters)
            {
                if (!first)
                {
                    urlBuilder.Append("&");
                }
                
                urlBuilder.Append($"{param.Key}={UnityWebRequest.EscapeURL(param.Value?.ToString() ?? "")}");
                first = false;
            }
            
            return urlBuilder.ToString();
        }
    }
}