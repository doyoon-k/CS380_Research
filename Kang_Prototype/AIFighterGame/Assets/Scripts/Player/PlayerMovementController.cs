using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("Components")]
    public PlayerStatsComponent stats;
    public SpriteRenderer spriteRenderer;

    [Header("Ground Check")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.2f;

    private Rigidbody2D rb;
    private float moveInput;
    private bool jumpQueued;
    private bool isGrounded;
    private float initialScaleX = 1f;

    public bool IsGrounded => isGrounded;
    public int FacingSign { get; private set; } = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (stats == null)
        {
            stats = GetComponent<PlayerStatsComponent>();
        }

        initialScaleX = Mathf.Abs(transform.localScale.x) > 0f ? Mathf.Abs(transform.localScale.x) : 1f;
    }

    void Update()
    {
        CheckGround();
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    public void RequestJump()
    {
        jumpQueued = true;
    }

    void ApplyMovement()
    {
        float speed = stats != null ? stats.GetStat(PlayerStatType.MovementSpeed) : 0f;
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        if (jumpQueued && isGrounded)
        {
            float jumpPower = stats != null ? stats.GetStat(PlayerStatType.JumpPower) : 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
        }

        jumpQueued = false;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            FacingSign = moveInput > 0 ? 1 : -1;
            UpdateFacing(FacingSign);
        }
    }

    void CheckGround()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    void UpdateFacing(int facing)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facing < 0;
        }
        else
        {
            Vector3 scale = transform.localScale;
            scale.x = initialScaleX * facing;
            transform.localScale = scale;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
