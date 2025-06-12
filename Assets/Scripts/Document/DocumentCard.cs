using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

namespace Document
{
    public class DocumentCard : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_Text titleText;
        public TMP_Text descriptionText;
        public TMP_Text nodeCountText;
        public Transform nodePreviewContainer;
        public GameObject nodePreviewPrefab;
        public Transform tagContainer;
        public GameObject tagPrefab;
        public TMP_Text imageCountText;
        public TMP_Text textCountText;
        public TMP_Text updateTimeText;
        public Button cardButton;
        
        public System.Action<DocumentData> OnCardClicked;
        
        private DocumentData documentData;
        
        private void Start()
        {
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(() => OnCardClicked?.Invoke(documentData));
            }
        }
        
        public void SetupCard(DocumentData document)
        {
            documentData = document;
            
            // 设置标题
            if (titleText != null)
                titleText.text = document.name;
            
            // 设置描述
            if (descriptionText != null)
                descriptionText.text = string.IsNullOrEmpty(document.description) ? "暂无描述" : document.description;
            
            // 设置节点数量
            if (nodeCountText != null)
                nodeCountText.text = $"{document.data_list.Count} 个节点";
            
            // 设置节点预览
            SetupNodePreview(document.data_list);
            
            // 设置标签
            SetupTags(document.tags);
            
            // 设置统计信息
            int imageCount = document.data_list.Count(item => !string.IsNullOrEmpty(item.image));
            int textCount = document.data_list.Count(item => !string.IsNullOrEmpty(item.text.Trim()));
            
            if (imageCountText != null)
                imageCountText.text = $"🖼️ {imageCount}";
                
            if (textCountText != null)
                textCountText.text = $"📄 {textCount}";
            
            // 设置更新时间
            if (updateTimeText != null)
                updateTimeText.text = FormatDate(document.updated_at);
        }
        
        private void SetupNodePreview(List<DataItem> dataList)
        {
            if (nodePreviewContainer == null || nodePreviewPrefab == null) return;
            
            // 清理现有预览
            foreach (Transform child in nodePreviewContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 显示前3个节点
            int previewCount = Mathf.Min(3, dataList.Count);
            for (int i = 0; i < previewCount; i++)
            {
                var item = dataList[i];
                GameObject previewObj = Instantiate(nodePreviewPrefab, nodePreviewContainer);
                
                // 设置节点预览内容
                TMP_Text sequenceText = previewObj.transform.Find("SequenceText")?.GetComponent<TMP_Text>();
                TMP_Text contentText = previewObj.transform.Find("ContentText")?.GetComponent<TMP_Text>();
                GameObject imageIcon = previewObj.transform.Find("ImageIcon")?.gameObject;
                
                if (sequenceText != null)
                    sequenceText.text = item.sequence.ToString();
                    
                if (contentText != null)
                    contentText.text = string.IsNullOrEmpty(item.text) ? "空文本" : item.text;
                    
                if (imageIcon != null)
                    imageIcon.SetActive(!string.IsNullOrEmpty(item.image));
            }
            
            // 如果有更多节点，显示"更多"提示
            if (dataList.Count > 3)
            {
                GameObject moreObj = Instantiate(nodePreviewPrefab, nodePreviewContainer);
                TMP_Text moreText = moreObj.GetComponentInChildren<TMP_Text>();
                if (moreText != null)
                    moreText.text = $"+{dataList.Count - 3} 更多";
            }
        }
        
        private void SetupTags(List<string> tags)
        {
            if (tagContainer == null || tagPrefab == null) return;
            
            // 清理现有标签
            foreach (Transform child in tagContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 显示前3个标签
            int tagCount = Mathf.Min(3, tags.Count);
            for (int i = 0; i < tagCount; i++)
            {
                GameObject tagObj = Instantiate(tagPrefab, tagContainer);
                TMP_Text tagText = tagObj.GetComponentInChildren<TMP_Text>();
                if (tagText != null)
                    tagText.text = tags[i];
            }
            
            // 如果有更多标签，显示数量
            if (tags.Count > 3)
            {
                GameObject moreTagObj = Instantiate(tagPrefab, tagContainer);
                TMP_Text moreTagText = moreTagObj.GetComponentInChildren<TMP_Text>();
                if (moreTagText != null)
                    moreTagText.text = $"+{tags.Count - 3}";
            }
        }
        
        private string FormatDate(string dateTime)
        {
            DateTime dt;
            if (!DateTime.TryParse(dateTime, out dt))
                return dateTime;

            TimeSpan diff = DateTime.Now - dt;

            if (diff.TotalMinutes < 1)
                return "刚刚";
            if (diff.TotalHours < 1)
                return $"{(int)diff.TotalMinutes}分钟前";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours}小时前";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}天前";
            return dt.ToString("yyyy-MM-dd");
        }
    }

}
