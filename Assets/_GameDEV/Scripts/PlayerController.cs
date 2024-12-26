using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Transform cameraTransform;

    [SerializeField] private IMoveable _movement;
    [SerializeField] private IGroundChecker _groundChecker;
    [SerializeField] private IMaterialScroller _materialScroller;
    [SerializeField] private DashController _dashController;
    [SerializeField] private IInputHandler[] _inputHandlers;
    [SerializeField] private IShooter _shooter;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        _movement = GetComponent<PlayerMovement>();
        _groundChecker = GetComponent<GroundChecker>();
        _materialScroller = GetComponent<MaterialScroller>();
        _dashController = GetComponent<DashController>();
        _shooter = GetComponent<ShootingSystem>();

        _inputHandlers = new IInputHandler[]
        {
            new JoystickInput(joystick),
            new KeyboardInput()
        };
    }

    private void Update()
    {
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
            _materialScroller.ScrollMaterial(moveDirection);
        }
    }

    private void HandleShooting()
    {

        Vector3 shootDirection = transform.forward;
        _shooter.Shoot(shootDirection);

    }

    private Vector3 GetActiveInputDirection()
    {
        foreach (var inputHandler in _inputHandlers)
        {
            if (inputHandler.HasInput)
            {
                return inputHandler.GetMovementInput();
            }
        }
        return Vector3.zero;
    }

    private Vector3 CalculateMoveDirection(Vector3 inputDirection)
    {
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