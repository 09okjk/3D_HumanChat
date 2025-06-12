using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Document
{
    public class DocumentListManager : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField searchInput;
        public Button searchButton;
        public TMP_Dropdown tagDropdown;
        public TMP_Dropdown sortDropdown;
        public Button sortOrderButton;
        public Transform documentContainer;
        public GameObject documentCardPrefab;
        public TMP_Text totalCountText;
        public Button prevPageButton;
        public Button nextPageButton;
        public TMP_Text pageInfoText;
        
        [Header("Loading UI")]
        public GameObject loadingPanel;
        public TMP_Text errorText;
        
        [Header("Pagination")]
        public int pageSize = 20;
        
        [Header("Error Handling")]
        public Button retryButton; // 在Inspector中关联重试按钮
        
        private DocumentService documentService;
        private List<DocumentData> currentDocuments = new List<DocumentData>();
        private List<string> allTags = new List<string>();
        private List<string> selectedTags = new List<string>();
        
        // 分页和排序状态
        private int currentPage = 1;
        private int totalDocuments = 0;
        private string currentSortBy = "created_at";
        private int currentSortOrder = -1; // -1降序, 1升序
        private bool isSearchMode = false;
        
        private void Start()
        {
            InitializeService();
            SetupUI();
            LoadDocuments();
        }
        
        private void InitializeService()
        {
            documentService = GetComponent<DocumentService>();
            if (documentService == null)
            {
                documentService = gameObject.AddComponent<DocumentService>();
            }
            
            // 订阅事件
            documentService.OnDocumentsLoaded += OnDocumentsLoaded;
            documentService.OnSearchCompleted += OnSearchCompleted;
            documentService.OnError += OnError;
        }
        
        private void SetupUI()
        {
            // 搜索按钮事件
            searchButton.onClick.AddListener(HandleSearch);
            searchInput.onEndEdit.AddListener((value) => {
                if (Input.GetKeyDown(KeyCode.Return))
                    HandleSearch();
            });
            
            // 排序相关
            sortDropdown.onValueChanged.AddListener(OnSortChanged);
            sortOrderButton.onClick.AddListener(ToggleSortOrder);
            
            // 分页按钮
            prevPageButton.onClick.AddListener(PreviousPage);
            nextPageButton.onClick.AddListener(NextPage);
            
            // 设置排序选项
            sortDropdown.options.Clear();
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("创建时间"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("更新时间"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("名称"));
            sortDropdown.RefreshShownValue();
            
            UpdateSortOrderButtonText();
        }
        
        private void LoadDocuments()
        {
            ShowLoading(true);
            documentService.LoadDocuments(currentPage, pageSize, currentSortBy, currentSortOrder, selectedTags);
        }
        
        private void HandleSearch()
        {
            string query = searchInput.text.Trim();
            ShowLoading(true);
            
            if (string.IsNullOrEmpty(query))
            {
                isSearchMode = false;
                currentPage = 1;
                LoadDocuments();
            }
            else
            {
                isSearchMode = true;
                currentPage = 1;
                documentService.SearchDocuments(query, 100);
            }
        }
        
        private void OnDocumentsLoaded(DocumentListResponse response)
        {
            currentDocuments = response.documents;
            totalDocuments = response.total;
            UpdateAllTags();
            UpdateUI();
            ShowLoading(false);
        }
        
        private void OnSearchCompleted(SearchResponse response)
        {
            currentDocuments = response.results;
            totalDocuments = response.total_matches;
            UpdateAllTags();
            UpdateUI();
            ShowLoading(false);
        }
        
        private void OnError(string error)
        {
            ShowLoading(false);
    
            // 更详细的错误处理
            if (error.Contains("SSL") || error.Contains("certificate"))
            {
                ShowError("网络连接错误：SSL证书验证失败。请检查网络设置或联系管理员。");
                Debug.LogError($"SSL Error: {error}");
        
                // 可以提供重试选项
                if (retryButton != null)
                {
                    retryButton.gameObject.SetActive(true);
                    retryButton.onClick.RemoveAllListeners();
                    retryButton.onClick.AddListener(() => {
                        retryButton.gameObject.SetActive(false);
                        LoadDocuments();
                    });
                }
            }
            else if (error.Contains("timeout"))
            {
                ShowError("请求超时，请检查网络连接后重试。");
            }
            else if (error.Contains("Unable to resolve host"))
            {
                ShowError("无法连接到服务器，请检查网络连接。");
            }
            else
            {
                ShowError($"请求失败：{error}");
            }
    
            Debug.LogError($"Document Service Error: {error}");
        }

        
        private void UpdateAllTags()
        {
            HashSet<string> tagSet = new HashSet<string>();
            foreach (var doc in currentDocuments)
            {
                foreach (var tag in doc.tags)
                {
                    tagSet.Add(tag);
                }
            }
            allTags = tagSet.OrderBy(t => t).ToList();
            UpdateTagDropdown();
        }
        
        private void UpdateTagDropdown()
        {
            tagDropdown.options.Clear();
            tagDropdown.options.Add(new TMP_Dropdown.OptionData("所有标签"));
            foreach (var tag in allTags)
            {
                tagDropdown.options.Add(new TMP_Dropdown.OptionData(tag));
            }
            tagDropdown.RefreshShownValue();
        }
        
        private void UpdateUI()
        {
            // 清理现有的文档卡片
            foreach (Transform child in documentContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 创建文档卡片
            foreach (var document in currentDocuments)
            {
                CreateDocumentCard(document);
            }
            
            // 更新分页信息
            UpdatePaginationUI();
            
            // 更新总数显示
            if (totalCountText != null)
            {
                totalCountText.text = $"共 {totalDocuments} 个文档";
            }
        }
        
        private void CreateDocumentCard(DocumentData document)
        {
            GameObject cardObj = Instantiate(documentCardPrefab, documentContainer);
            DocumentCard cardScript = cardObj.GetComponent<DocumentCard>();
            
            if (cardScript != null)
            {
                cardScript.SetupCard(document);
                cardScript.OnCardClicked += OnDocumentCardClicked;
            }
        }
        
        private void OnDocumentCardClicked(DocumentData document)
        {
            // 处理文档卡片点击事件
            Debug.Log($"Document clicked: {document.name}");
            // 保存当前文档数据到DataManager
            MainManager.Instance.SetCurrentDocumentData(document);
            // 加载下一个场景
            SceneManager.LoadScene("MainScene");
        }
        
        private void OnSortChanged(int index)
        {
            string[] sortFields = { "created_at", "updated_at", "name" };
            currentSortBy = sortFields[index];
            currentPage = 1;
            
            if (isSearchMode)
            {
                HandleSearch();
            }
            else
            {
                LoadDocuments();
            }
        }
        
        private void ToggleSortOrder()
        {
            currentSortOrder = currentSortOrder == 1 ? -1 : 1;
            UpdateSortOrderButtonText();
            currentPage = 1;
            
            if (isSearchMode)
            {
                HandleSearch();
            }
            else
            {
                LoadDocuments();
            }
        }
        
        private void UpdateSortOrderButtonText()
        {
            if (sortOrderButton != null)
            {
                Text buttonText = sortOrderButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = currentSortOrder == -1 ? "降序" : "升序";
                }
            }
        }
        
        private void PreviousPage()
        {
            if (currentPage > 1)
            {
                currentPage--;
                if (isSearchMode)
                {
                    HandleSearch();
                }
                else
                {
                    LoadDocuments();
                }
            }
        }
        
        private void NextPage()
        {
            int maxPages = Mathf.CeilToInt((float)totalDocuments / pageSize);
            if (currentPage < maxPages)
            {
                currentPage++;
                if (isSearchMode)
                {
                    HandleSearch();
                }
                else
                {
                    LoadDocuments();
                }
            }
        }
        
        private void UpdatePaginationUI()
        {
            int maxPages = Mathf.CeilToInt((float)totalDocuments / pageSize);
            
            if (prevPageButton != null)
                prevPageButton.interactable = currentPage > 1;
                
            if (nextPageButton != null)
                nextPageButton.interactable = currentPage < maxPages;
                
            if (pageInfoText != null)
                pageInfoText.text = $"{currentPage} / {maxPages}";
        }
        
        private void ShowLoading(bool show)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(show);
        }
        
        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
                // 3秒后隐藏错误信息
                Invoke(nameof(HideError), 3f);
            }
        }
        
        private void HideError()
        {
            if (errorText != null)
                errorText.gameObject.SetActive(false);
        }
        
        private void OnDestroy()
        {
            if (documentService != null)
            {
                documentService.OnDocumentsLoaded -= OnDocumentsLoaded;
                documentService.OnSearchCompleted -= OnSearchCompleted;
                documentService.OnError -= OnError;
            }
        }
    }
}