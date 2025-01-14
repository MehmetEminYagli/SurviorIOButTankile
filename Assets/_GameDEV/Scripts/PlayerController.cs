using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Transform cameraTransform;

    [Header("Components")]
    [SerializeField] private IMoveable _movement;
    [SerializeField] private IGroundChecker _groundChecker;
    [SerializeField] private IMaterialScroller _materialScroller;
    [SerializeField] private DashController _dashController;
    [SerializeField] private IShooter _shooter;

    private IInputHandler[] _inputHandlers;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (!isInitialized)
        {
            // Get required components
            _movement = GetComponent<PlayerMovement>();
            _groundChecker = GetComponent<GroundChecker>();
            _materialScroller = GetComponent<MaterialScroller>();
            _dashController = GetComponent<DashController>();
            _shooter = GetComponent<ShootingSystem>();

            // Check required components
            if (_movement == null)
            {
                Debug.LogError("PlayerMovement component is missing!");
                enabled = false;
                return;
            }

            if (_groundChecker == null)
            {
                Debug.LogError("GroundChecker component is missing!");
                enabled = false;
                return;
            }

            // Initialize input handlers
            _inputHandlers = new IInputHandler[]
            {
                joystick != null ? new JoystickInput(joystick) : null,
                new KeyboardInput()
            };

            // Remove null handlers
            _inputHandlers = System.Array.FindAll(_inputHandlers, handler => handler != null);

            if (_inputHandlers.Length == 0)
            {
                Debug.LogError("No input handlers available!");
                enabled = false;
                return;
            }

            isInitialized = true;
        }
    }

    public void SetCameraTransform(Transform newCamera)
    {
        cameraTransform = newCamera;
    }

    private void Update()
    {
        if (!isInitialized) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetRotation();
        }
        if (Input.GetKeyDown(KeyCode.E) && _shooter != null)
        {
            HandleShooting();
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;
        HandleMovement();
    }

    private void HandleMovement()
    {
        _groundChecker.CheckGround();
        if (!_groundChecker.IsGrounded) return;

        Vector3 inputDirection = GetActiveInputDirection();
        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 moveDirection = CalculateMoveDirection(inputDirection);
            float currentSpeed = CalculateCurrentSpeed();

            _movement.Move(moveDirection, currentSpeed);
            _movement.Rotate(moveDirection);
            if (_materialScroller != null)
            {
                _materialScroller.ScrollMaterial(moveDirection);
            }
        }
    }

    private void HandleShooting()
    {
        if (_shooter != null && _shooter.CanShoot)
        {
            Vector3 shootDirection = transform.forward;
            Vector3 spawnPosition = transform.position + (transform.forward * 1.5f) + (Vector3.up * 0.5f);
            _shooter.Shoot(shootDirection, spawnPosition);
        }
    }

    private Vector3 GetActiveInputDirection()
    {
        if (_inputHandlers == null || _inputHandlers.Length == 0)
            return Vector3.zero;

        foreach (var inputHandler in _inputHandlers)
        {
            if (inputHandler != null && inputHandler.HasInput)
            {
                return inputHandler.GetMovementInput();
            }
        }
        return Vector3.zero;
    }

    private Vector3 CalculateMoveDirection(Vector3 inputDirection)
    {
        if (cameraTransform == null) return inputDirection;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        return (cameraForward.normalized * inputDirection.z +
                cameraRight.normalized * inputDirection.x).normalized;
    }

    private float CalculateCurrentSpeed()
    {
        return _dashController != null && _dashController.IsDashing
            ? _dashController.DashSpeed
            : moveSpeed;
    }

    private void ResetRotation()
    {
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
    }
}