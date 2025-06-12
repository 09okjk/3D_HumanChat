using System;
using System.Collections;
using System.Collections.Generic;
using PPT_Item;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance;
        
    public Material screenMaterial;
    public MeshRenderer screenMeshRenderer;
    public AudioSource audioSource;
    public TextMeshProUGUI contentText;
    
    private List<PPT_Entity> _presentationList = new List<PPT_Entity>();
    private int _currentIndex = -1; // 当前屏幕索引，初始为 -1

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 如果已经存在实例，则销毁当前实例
            return;
        }
        screenMeshRenderer.material = screenMaterial;
        // 调用后端接口，获取演讲列表
        FetchPresentationList();
    }

    private void FetchPresentationList()
    {
        // 使用模拟数据
        _presentationList = PPT_EntityMockData.GetMockPresentationList();
    }

    void Start()
    {
        // 订阅音频源播放完成事件
        audioSource.loop = false; // 确保音频源不循环播放
        audioSource.playOnAwake = false; // 确保音频源不会在开始时自动播放
        audioSource.clip = null; // 初始化音频源
    }
    
    void Update()
    {
        if (audioSource.isPlaying)
        {
        }
        else if (_currentIndex < _presentationList.Count)
        {
            OnAudioSourceFinished();
        }
    }
    
    public void ChangeScreen(int index)
    {
        _currentIndex = index;
        if (index < 0 || index >= _presentationList.Count)
        {
            Debug.LogError("Index out of range");
            return;
        }

        var entity = _presentationList[index];
        
        // 更新屏幕材质
        if (entity.ImageUrl != null)
        {
            StartCoroutine(LoadTextureFromUrl(entity.ImageUrl, texture =>
            {
                screenMaterial.mainTexture = (Texture2D)texture;
            }));
        }
        
        // 更新音频源
        if (entity.AudioData != null && entity.AudioData.Length > 0)
        {
            // AudioData 是 base64 编码的音频数据
            // 添加解码逻辑，将 AudioData 转换为 AudioClip
            AudioClip audioClip = AudioClip.Create(entity.Id, entity.AudioData.Length, 1, 44100, false);
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        
        // 更新说话内容
        contentText.text = !string.IsNullOrEmpty(entity.Content) ? entity.Content : "No content available";
        
        // 这里可以根据 CameraType 做其他处理
        CameraManager.Instance.SetCameraActive(entity.ScreenCameraType);
        Debug.Log($"Changed screen to {entity.Content} with camera type {entity.ScreenCameraType}");
    }
    
    private void OnAudioSourceFinished()
    {
        // 音频播放完成后的处理逻辑
        Debug.Log("Audio playback finished.");
        ChangeScreen((_currentIndex + 1) % _presentationList.Count);
        
    }

    private IEnumerator LoadTextureFromUrl(string entityImageUrl, Action<object> action)
    {
        UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(entityImageUrl);
        yield return webRequest.SendWebRequest();
        Texture2D texture = null;
        if (webRequest.result != UnityWebRequest.Result.ConnectionError && webRequest.result != UnityWebRequest.Result.ProtocolError)
        {
            texture = DownloadHandlerTexture.GetContent(webRequest);
        }
        action?.Invoke(texture);
    }
}
