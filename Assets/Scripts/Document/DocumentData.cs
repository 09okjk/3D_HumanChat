using System;
using System.Collections.Generic;

[Serializable]
public class DocumentData
{
    public string id;
    public string name;
    public string description;
    public List<DataItem> data_list;
    public List<string> tags;
    public string created_at; // 改为string，因为后端返回的是字符串格式
    public string updated_at; // 改为string
    public int version;

    public DocumentData()
    {
        data_list = new List<DataItem>();
        tags = new List<string>();
    }
    
    // 添加日期解析方法
    public DateTime GetCreatedAt()
    {
        if (DateTime.TryParse(created_at, out DateTime result))
            return result;
        return DateTime.MinValue;
    }
    
    public DateTime GetUpdatedAt()
    {
        if (DateTime.TryParse(updated_at, out DateTime result))
            return result;
        return DateTime.MinValue;
    }
}

[Serializable]
public class DataItem
{
    public int sequence;
    public string text;
    public string image;
    
    public DataItem()
    {
        text = "";
        image = "";
    }
}

[Serializable]
public class DocumentListResponse
{
    public List<DocumentData> documents;
    public int total;
    
    public DocumentListResponse()
    {
        documents = new List<DocumentData>();
        total = 0;
    }
}

[Serializable]
public class SearchResponse
{
    public List<DocumentData> results;
    public int total_matches;
    
    public SearchResponse()
    {
        results = new List<DocumentData>();
        total_matches = 0;
    }
}