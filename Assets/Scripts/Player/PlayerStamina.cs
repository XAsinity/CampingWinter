using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] float maxStamina      = 100f;
    [SerializeField] float sprintDrain     = 15f;
    [SerializeField] float staminaRegen    = 8f;
    [SerializeField] float regenDelay      = 1.5f;

    float       _stamina;
    float       _regenTimer;
    FirstPersonController _controller;

    public float StaminaPercent => (_stamina / maxStamina) * 100f;
    public bool  IsExhausted    => _stamina <= 0f;

    void Start()
    {
        _stamina  = maxStamina;
        _controller = GetComponent<FirstPersonController>();
    }

    void Update()
    {
        UpdateStamina();
    }

    void UpdateStamina()
    {
        if (_controller != null && _controller.IsSprinting && _controller.IsGrounded)
        {
            _stamina -= sprintDrain * Time.deltaTime;
            _stamina = Mathf.Max(0f, _stamina);
            _regenTimer = 0f;
        }
        else
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= regenDelay)
            {
                _stamina += staminaRegen * Time.deltaTime;
                _stamina = Mathf.Min(maxStamina, _stamina);
            }
        }
    }

    public void RestoreStamina(float amount)
    {
        _stamina = Mathf.Min(maxStamina, _stamina + amount);
    }
}
