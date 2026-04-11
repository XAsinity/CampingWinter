using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float walkSpeed      = 3.5f;
    [SerializeField] float sprintSpeed    = 5.5f;
    [SerializeField] float gravity        = -15f;

    [Header("Mouse Look")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float lookClamp        = 85f;

    [Header("Crouch")]
    [SerializeField] float crouchHeight     = 1.0f;
    [SerializeField] float crouchSpeed      = 6f;

    [Header("Head Bob")]
    [SerializeField] float bobFrequency     = 1.8f;
    [SerializeField] float bobAmplitude     = 0.04f;

    [Header("Footsteps (Loop)")]
    [SerializeField] AudioSource footstepSource;
    [SerializeField] AudioClip footstepLoopClip;
    [SerializeField, Range(0f, 1f)] float walkFootstepVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] float sprintFootstepVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float crouchFootstepVolume = 0.3f;
    [SerializeField, Range(0.5f, 2f)] float walkFootstepPitch = 1.0f;
    [SerializeField, Range(0.5f, 2f)] float sprintFootstepPitch = 1.25f;
    [SerializeField, Range(0.5f, 2f)] float crouchFootstepPitch = 0.85f;
    [SerializeField, Range(1f, 30f)] float footstepBlendSpeed = 10f;

    CharacterController _cc;
    Transform           _camTransform;
    AudioSource         _footstepSource;
    float               _xRotation;
    Vector3             _velocity;
    float               _standHeight;
    float               _targetHeight;
    float               _bobTimer;
    bool                _isCrouching;
    bool                _isSprinting;
    bool                _crouchPressed;
    bool                _hasMoveInput;
    bool                _controlsEnabled = true;
    Vector2             _moveInput;
    Vector2             _lookInput;

    public bool IsSprinting  => _isSprinting;
    public bool IsCrouching  => _isCrouching;
    public bool IsGrounded   => _cc.isGrounded;
    public bool ControlsEnabled => _controlsEnabled;

    void Start()
    {
        _cc = GetComponent<CharacterController>();
        _camTransform = GetComponentInChildren<Camera>().transform;
        _footstepSource = footstepSource;
        if (_footstepSource == null)
        {
            Transform footstepNode = transform.Find("Audio/FootstepsAudio");
            if (footstepNode != null)
                _footstepSource = footstepNode.GetComponent<AudioSource>();
        }
        if (_footstepSource == null)
            _footstepSource = GetComponentInChildren<AudioSource>();

        if (_footstepSource != null)
        {
            _footstepSource.loop = true;
            _footstepSource.playOnAwake = false;

            if (footstepLoopClip != null)
                _footstepSource.clip = footstepLoopClip;
        }

        _standHeight  = _cc.height;
        _targetHeight = _standHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        if (!_controlsEnabled)
        {
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _hasMoveInput = false;
            _isSprinting = false;
            HandleFootsteps();
            return;
        }

        ReadInput();
        HandleMouseLook();
        HandleMovement();
        HandleCrouch();
        HandleHeadBob();
        HandleFootsteps();
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null)
        {
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _crouchPressed = false;
            _isSprinting = false;
            return;
        }

        float moveX = 0f;
        float moveY = 0f;
        if (kb.aKey.isPressed) moveX -= 1f;
        if (kb.dKey.isPressed) moveX += 1f;
        if (kb.sKey.isPressed) moveY -= 1f;
        if (kb.wKey.isPressed) moveY += 1f;
        _moveInput = new Vector2(moveX, moveY).normalized;
        _hasMoveInput = _moveInput.sqrMagnitude > 0.01f;

        var mouse = Mouse.current;
        _lookInput = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

        _crouchPressed = kb.cKey.wasPressedThisFrame;
        _isSprinting = kb.leftShiftKey.isPressed && !_isCrouching;
    }

    void HandleMouseLook()
    {
        float mx = _lookInput.x * mouseSensitivity * 0.1f;
        float my = _lookInput.y * mouseSensitivity * 0.1f;

        _xRotation -= my;
        _xRotation = Mathf.Clamp(_xRotation, -lookClamp, lookClamp);

        _camTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mx);
    }

    void HandleMovement()
    {
        if (_cc.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        float h = _moveInput.x;
        float v = _moveInput.y;

        Vector3 move = transform.right * h + transform.forward * v;
        float speed = IsSprinting ? sprintSpeed : walkSpeed;
        if (_isCrouching) speed *= 0.5f;

        _cc.Move(move * speed * Time.deltaTime);

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        if (_crouchPressed)
        {
            _isCrouching = !_isCrouching;
            _targetHeight = _isCrouching ? crouchHeight : _standHeight;
        }

        if (!Mathf.Approximately(_cc.height, _targetHeight))
        {
            _cc.height = Mathf.Lerp(_cc.height, _targetHeight, crouchSpeed * Time.deltaTime);
            _cc.center = new Vector3(0, _cc.height * 0.5f, 0);
        }
    }

    void HandleHeadBob()
    {
        if (!_cc.isGrounded) return;
        float h = _moveInput.x;
        float v = _moveInput.y;
        float baseY = _cc.height - 0.1f;
        Vector3 local = _camTransform.localPosition;

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
        {
            _bobTimer = 0f;
            local.y = Mathf.Lerp(local.y, baseY, 10f * Time.deltaTime);
            _camTransform.localPosition = local;
            return;
        }

        _bobTimer += Time.deltaTime * bobFrequency * (IsSprinting ? 1.4f : 1f);
        float bobY = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * bobAmplitude;
        local.y = Mathf.Lerp(local.y, baseY + bobY, 10f * Time.deltaTime);
        _camTransform.localPosition = local;
    }

    void HandleFootsteps()
    {
        if (_footstepSource == null) return;
        if (footstepLoopClip != null && _footstepSource.clip != footstepLoopClip)
            _footstepSource.clip = footstepLoopClip;
        if (_footstepSource.clip == null) return;

        bool isMoving = _cc.isGrounded && _hasMoveInput;

        float targetVolume = 0f;
        float targetPitch = walkFootstepPitch;

        if (isMoving)
        {
            if (_isCrouching)
            {
                targetVolume = crouchFootstepVolume;
                targetPitch = crouchFootstepPitch;
            }
            else if (IsSprinting)
            {
                targetVolume = sprintFootstepVolume;
                targetPitch = sprintFootstepPitch;
            }
            else
            {
                targetVolume = walkFootstepVolume;
                targetPitch = walkFootstepPitch;
            }

            if (!_footstepSource.isPlaying)
                _footstepSource.Play();
        }

        _footstepSource.volume = Mathf.Lerp(_footstepSource.volume, targetVolume, footstepBlendSpeed * Time.deltaTime);
        _footstepSource.pitch = Mathf.Lerp(_footstepSource.pitch, targetPitch, footstepBlendSpeed * Time.deltaTime);

        if (!isMoving && _footstepSource.isPlaying && _footstepSource.volume <= 0.02f)
            _footstepSource.Stop();
    }

    public void SetControlsEnabled(bool enabled)
    {
        _controlsEnabled = enabled;

        if (!enabled)
        {
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _hasMoveInput = false;
            _isSprinting = false;
            _velocity = Vector3.zero;

            if (_footstepSource != null)
            {
                _footstepSource.Stop();
                _footstepSource.volume = 0f;
            }
        }
    }

    public void ForceCrouch(bool crouched)
    {
        _isCrouching = crouched;
        _isSprinting = false;
        _targetHeight = _isCrouching ? crouchHeight : _standHeight;
        _cc.height = _targetHeight;
        _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
    }
}
