using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using API;
using UnityEngine.Networking;

public class DocumentService : ApiServiceBase
{
    // 文档服务特定事件
    public System.Action<DocumentListResponse> OnDocumentsLoaded;
    public System.Action<SearchResponse> OnSearchCompleted;
    
    /// <summary>
    /// 获取文档列表
    /// </summary>
    public void LoadDocuments(int page = 1, int pageSize = 20, string sortBy = "created_at", 
                             int sortOrder = -1, List<string> tags = null)
    {
        StartCoroutine(LoadDocumentsCoroutine(page, pageSize, sortBy, sortOrder, tags));
    }
    
    /// <summary>
    /// 搜索文档
    /// </summary>
    public void SearchDocuments(string query, int maxResults = 100)
    {
        if (string.IsNullOrEmpty(query.Trim()))
        {
            LoadDocuments();
            return;
        }
        
        StartCoroutine(SearchDocumentsCoroutine(query, maxResults));
    }
    
    private IEnumerator LoadDocumentsCoroutine(int page, int pageSize, string sortBy, 
                                              int sortOrder, List<string> tags)
    {
        var parameters = new Dictionary<string, object>
        {
            {"page", page},
            {"page_size", pageSize},
            {"sort_by", sortBy},
            {"sort_order", sortOrder}
        };
        
        if (tags != null && tags.Count > 0)
        {
            parameters["tags"] = string.Join(",", tags);
        }
        
        string url = BuildUrlWithParams($"{baseUrl}/api/data/documents", parameters);
        
        using (UnityWebRequest request = CreateGetRequest(url))
        {
            yield return ExecuteRequestWithRetry(
                request,
                "获取文档列表",
                OnDocumentsRequestSuccess
            );
        }
    }
    
    private IEnumerator SearchDocumentsCoroutine(string query, int maxResults)
    {
        var parameters = new Dictionary<string, object>
        {
            {"q", query},
            {"limit", maxResults}
        };
        
        string url = BuildUrlWithParams($"{baseUrl}/api/data/documents/search", parameters);
        
        using (UnityWebRequest request = CreateGetRequest(url))
        {
            yield return ExecuteRequestWithRetry(
                request,
                "搜索文档",
                OnSearchRequestSuccess
            );
        }
    }
    
    private void OnDocumentsRequestSuccess(UnityWebRequest request)
    {
        DocumentListResponse response = ParseJsonResponse<DocumentListResponse>(request.downloadHandler.text, "文档列表");
        if (response != null)
        {
            OnDocumentsLoaded?.Invoke(response);
        }
    }
    
    private void OnSearchRequestSuccess(UnityWebRequest request)
    {
        SearchResponse response = ParseJsonResponse<SearchResponse>(request.downloadHandler.text, "文档搜索");
        if (response != null)
        {
            OnSearchCompleted?.Invoke(response);
        }
    }
}