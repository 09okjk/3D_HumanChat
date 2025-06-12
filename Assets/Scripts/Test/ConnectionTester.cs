using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ConnectionTester : MonoBehaviour
{
    [Header("Test Settings")]
    public string testHost = "192.168.18.122";
    public int testPort = 8017;
    public bool useHttps = true;
    
    [ContextMenu("Test Full Connection")]
    public void TestFullConnection()
    {
        StartCoroutine(RunFullConnectionTest());
    }
    
    private IEnumerator RunFullConnectionTest()
    {
        Debug.Log("=== 开始完整连接测试 ===");
        
        // 初始化SSL
        SSLHelper.InitializeSSL();
        
        string protocol = useHttps ? "https" : "http";
        string baseUrl = $"{protocol}://{testHost}:{testPort}";
        
        // 测试1: 基础连接
        yield return TestBasicConnection(baseUrl);
        
        // 测试2: API根路径
        yield return TestAPIRoot($"{baseUrl}/api");
        
        // 测试3: 数据API路径
        yield return TestDataAPI($"{baseUrl}/api/data");
        
        // 测试4: 文档列表API
        yield return TestDocumentsAPI($"{baseUrl}/api/data/documents");
        
        // 测试5: 统计API
        yield return TestStatisticsAPI($"{baseUrl}/api/data/statistics");
        
        Debug.Log("=== 连接测试完成 ===");
    }
    
    private IEnumerator TestBasicConnection(string url)
    {
        Debug.Log($"测试基础连接: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            
            yield return request.SendWebRequest();
            
            Debug.Log($"基础连接结果: {request.result} | 响应码: {request.responseCode} | 错误: {request.error}");
        }
    }
    
    private IEnumerator TestAPIRoot(string url)
    {
        Debug.Log($"测试API根路径: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            request.SetRequestHeader("Accept", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"API根路径结果: {request.result} | 响应码: {request.responseCode}");
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                Debug.Log($"响应内容: {request.downloadHandler.text.Substring(0, Mathf.Min(200, request.downloadHandler.text.Length))}...");
            }
        }
    }
    
    private IEnumerator TestDataAPI(string url)
    {
        Debug.Log($"测试数据API: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            request.SetRequestHeader("Accept", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"数据API结果: {request.result} | 响应码: {request.responseCode}");
        }
    }
    
    private IEnumerator TestDocumentsAPI(string url)
    {
        Debug.Log($"测试文档API: {url}?page=1&page_size=5");
        
        using (UnityWebRequest request = UnityWebRequest.Get($"{url}?page=1&page_size=5"))
        {
            request.timeout = 15;
            request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"文档API结果: {request.result} | 响应码: {request.responseCode}");
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"文档API响应: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"文档API错误: {request.error}");
            }
        }
    }
    
    private IEnumerator TestStatisticsAPI(string url)
    {
        Debug.Log($"测试统计API: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            request.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
            request.SetRequestHeader("Accept", "application/json");
            
            yield return request.SendWebRequest();
            
            Debug.Log($"统计API结果: {request.result} | 响应码: {request.responseCode}");
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"统计API响应: {request.downloadHandler.text}");
            }
        }
    }
}