using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public static class SSLHelper
{
    private static bool _isInitialized = false;
    
    public static void InitializeSSL()
    {
        if (_isInitialized) return;
        
        Debug.Log("正在初始化SSL设置...");
        
        try
        {
            // 设置服务器证书验证回调
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            
            // 设置安全协议
            ServicePointManager.SecurityProtocol = 
                SecurityProtocolType.Tls12 | 
                SecurityProtocolType.Tls11 | 
                SecurityProtocolType.Tls;
            
            // 设置其他网络参数
            ServicePointManager.DefaultConnectionLimit = 50;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            
            _isInitialized = true;
            Debug.Log("SSL设置初始化完成");
        }
        catch (Exception e)
        {
            Debug.LogError($"SSL设置初始化失败: {e.Message}");
        }
    }
    
    private static bool ValidateServerCertificate(
        object sender, 
        X509Certificate certificate, 
        X509Chain chain, 
        SslPolicyErrors sslPolicyErrors)
    {
        Debug.Log($"SSL证书验证 - 错误: {sslPolicyErrors}");
        
        // 在开发环境中跳过所有SSL验证
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log("开发环境 - 跳过SSL证书验证");
            return true;
        }
        
        // 对于特定的本地IP地址，跳过验证
        if (sender is HttpWebRequest request)
        {
            string host = request.RequestUri.Host;
            if (host.StartsWith("192.168.") || host.StartsWith("10.") || host.StartsWith("172."))
            {
                Debug.Log($"本地网络地址 {host} - 跳过SSL证书验证");
                return true;
            }
        }
        
        // 生产环境中的正常验证
        return sslPolicyErrors == SslPolicyErrors.None;
    }
}