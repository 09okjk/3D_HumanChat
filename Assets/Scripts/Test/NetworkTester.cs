using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkTester : MonoBehaviour
{
    [Header("Test Configuration")]
    public string testUrl = "https://httpbin.org/get"; // 用于测试的公共API
    
    [ContextMenu("Test Network Connection")]
    public void TestConnection()
    {
        StartCoroutine(TestNetworkConnection());
    }
    
    private IEnumerator TestNetworkConnection()
    {
        Debug.Log("开始网络连接测试...");
        
        // 测试简单的HTTP连接
        using (UnityWebRequest request = UnityWebRequest.Get("http://httpbin.org/get"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("HTTP连接测试成功!");
            }
            else
            {
                Debug.LogError($"HTTP连接测试失败: {request.error}");
            }
        }
        
        // 测试HTTPS连接
        using (UnityWebRequest request = UnityWebRequest.Get("https://httpbin.org/get"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("HTTPS连接测试成功!");
            }
            else
            {
                Debug.LogError($"HTTPS连接测试失败: {request.error}");
            }
        }
    }
}