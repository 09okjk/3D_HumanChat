using System;
using PPT_Item;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraManager:MonoBehaviour
{
    public static CameraManager Instance;
    
    public Camera mainCamera;
    public Camera farCamera;
    public Camera followCamera;
    
    public GameObject hostObject;

    private ScreenCameraType _currentCameraType = ScreenCameraType.MainCamera;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        // 初始化摄像头状态
        SetCameraActive(ScreenCameraType.MainCamera);
    }

    private void LateUpdate()
    {
        if (_currentCameraType == ScreenCameraType.FollowCamera && hostObject != null)
        {
            FollowHostObject();
        }
    }

    public void SetCameraActive(ScreenCameraType screenCameraType)
    {
        // 先将所有摄像头禁用
        mainCamera.gameObject.SetActive(false);
        farCamera.gameObject.SetActive(false);
        followCamera.gameObject.SetActive(false);

        // 根据传入的摄像头类型启用对应的摄像头
        switch (screenCameraType)
        {
            case ScreenCameraType.MainCamera:
                mainCamera.gameObject.SetActive(true);
                break;
            case ScreenCameraType.FarCamera:
                farCamera.gameObject.SetActive(true);
                break;
            case ScreenCameraType.FollowCamera:
                followCamera.gameObject.SetActive(true);
                break;
            default:
                Debug.LogWarning("Unknown camera type: " + screenCameraType);
                break;
        }

        _currentCameraType = screenCameraType;
    }
    
    // 跟随摄像头移动
    public Vector3 followOffset = new Vector3(0, 5, -10); // 可在Inspector调整

    private Vector3 _initOffset;
    private bool _offsetInitialized = false;

    public void FollowHostObject()
    {
        if (_currentCameraType == ScreenCameraType.FollowCamera && hostObject != null)
        {
            if (!_offsetInitialized)
            {
                _initOffset = followCamera.transform.position - hostObject.transform.position;
                _offsetInitialized = true;
            }
            followCamera.transform.position = hostObject.transform.position + _initOffset;
            // 不再调用 LookAt 或修改 rotation
        }
        else
        {
            _offsetInitialized = false;
        }
    }
}