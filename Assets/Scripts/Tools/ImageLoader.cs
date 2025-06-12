using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class ImageLoader
{
    public static IEnumerator LoadImage(string imageData, Action<Texture2D> onComplete, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(imageData))
        {
            onError?.Invoke("图片数据为空");
            yield break;
        }
        
        // 检查是否是base64数据
        if (IsBase64Data(imageData))
        {
            // 同步处理base64数据
            Texture2D texture = LoadTextureFromBase64(imageData);
            
            if (texture != null)
            {
                onComplete?.Invoke(texture);
            }
            else
            {
                onError?.Invoke("无法从base64数据创建纹理");
            }
        }
        else if (IsValidUrl(imageData))
        {
            // 异步处理URL数据
            using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageData))
            {
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                    onComplete?.Invoke(texture);
                }
                else
                {
                    onError?.Invoke($"网络请求失败: {webRequest.error}");
                }
            }
        }
        else
        {
            onError?.Invoke($"无效的图片数据格式: {imageData.Substring(0, Math.Min(50, imageData.Length))}...");
        }
    }
    
    private static Texture2D LoadTextureFromBase64(string base64Data)
    {
        try
        {
            byte[] imageBytes = DecodeBase64Image(base64Data);
            if (imageBytes == null)
                return null;
            
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageBytes))
            {
                return texture;
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"解析base64图片时发生错误: {e.Message}");
            return null;
        }
    }
    
    private static byte[] DecodeBase64Image(string base64Data)
    {
        try
        {
            // 处理data URL格式
            if (base64Data.StartsWith("data:image/"))
            {
                int commaIndex = base64Data.IndexOf(',');
                if (commaIndex >= 0)
                {
                    string base64String = base64Data.Substring(commaIndex + 1);
                    return Convert.FromBase64String(base64String);
                }
                else
                {
                    Debug.LogError("无效的data URL格式");
                    return null;
                }
            }
            else
            {
                // 直接是base64字符串
                return Convert.FromBase64String(base64Data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"解码base64数据时发生错误: {e.Message}");
            return null;
        }
    }
    
    private static bool IsBase64Data(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
            
        // 检查data URL格式
        if (str.StartsWith("data:image/"))
            return true;
            
        // 简化检查：长度很长且不是URL
        if (str.Length > 100)
        {
            return !str.Contains("http://") && !str.Contains("https://");
        }
        
        return false;
    }
    
    private static bool IsValidUrl(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
            
        return str.StartsWith("http://") || str.StartsWith("https://");
    }
}