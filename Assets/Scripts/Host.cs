using System;
using Manager;
using UnityEngine;
    
public class Host : MonoBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 360f;
    public float standingTime = 2f; // 停顿时间

    private Transform _transform;
    private float _standStartTime = 0f;
    private bool _isTurning = false;
    private Quaternion _targetRotation;
    
    bool canMove = true; // 是否可以移动

    private void Awake()
    {
        _transform = transform;
    }

    private void Start()
    {
        PresentationManager.Instance.OnPresentationCompleted += () =>
        {
            SetState(HostState.Idle); // 重置状态为Idle
            canMove = false; // 演示完成后可以移动
        };
    }
    
    private void OnDestroy()
    {
        if (PresentationManager.Instance != null)
        {
            PresentationManager.Instance.OnPresentationCompleted -= () => canMove = true;
        }
    }

    private void Update()
    {
        if (!canMove)
        {
            return; // 如果不能移动，直接返回
        }
        
        if (_isTurning)
        {
            // 旋转到目标朝向
            _transform.rotation = Quaternion.RotateTowards(_transform.rotation, _targetRotation, rotationSpeed * Time.deltaTime);
            if (Quaternion.Angle(_transform.rotation, _targetRotation) < 1f)
            {
                _isTurning = false;
                SetState(HostState.Walking);
            }
            return;
        }

        if (currentState == HostState.Standing)
        {
            if (Time.time - _standStartTime > standingTime)
            {
                // 停顿结束，开始掉头
                StartTurn();
            }
        }
        else if (currentState == HostState.Walking)
        {
            _transform.position += _transform.forward * speed * Time.deltaTime;
        }
    }

    public void TurnAndGo()
    {
        SetState(HostState.Standing);
    }

    private void StartTurn()
    {
        // 沿Y轴旋转180度
        _targetRotation = Quaternion.Euler(0, _transform.eulerAngles.y + 180f, 0);
        _isTurning = true;
    }

    public void SetState(HostState state)
    {
        currentState = state;
        if (state == HostState.Standing)
        {
            _standStartTime = Time.time;
        }
    }

    [SerializeField] private HostState currentState = HostState.Standing;
}

public enum HostState
{
    Idle,
    Standing,
    Walking,
    Speaking,
    Sitting
}