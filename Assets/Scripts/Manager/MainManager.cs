using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance;
    
    public DocumentData currentDocumentData; // 当前文档数据

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持实例在场景切换时不被销毁
        }
        else
        {
            Destroy(gameObject); // 如果已经存在实例，则销毁当前实例
        }
    }
    
    public void SetCurrentDocumentData(DocumentData documentData)
    {
        currentDocumentData = documentData;
    }
    
    public DocumentData GetCurrentDocumentData()
    {
        return currentDocumentData;
    }
    
}
