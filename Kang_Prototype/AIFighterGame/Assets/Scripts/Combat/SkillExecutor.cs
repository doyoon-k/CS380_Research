using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using UnityEngine.InputSystem.Interactions;


public class SkillExecutor : MonoBehaviour
{
    [Header("Components")]
    public PlayerStats playerStats;
    public Rigidbody2D rb;
    public AttackHitbox attackHitbox;
    public SpriteRenderer spriteRenderer;

    [Header("Prefabs")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Movement Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float jumpForce = 12f;
    public float blinkDistance = 4f;

    [Header("Attack Settings")]
    public float attackDelay = 0.2f;
    public float projectileCooldown = 1.0f;
    private float lastProjectileTime = -999f;

    [Header("State Tracking")]
    private bool isInvincible = false;
    private bool isFacingRight = true;
    private bool isDashing = false;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        if (attackHitbox == null) attackHitbox = GetComponentInChildren<AttackHitbox>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (attackHitbox != null) attackHitbox.gameObject.SetActive(false);
    }

    void Update()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        if(!isDashing)
        {
            if (moveInput > 0) isFacingRight = true;
            else if (moveInput < 0) isFacingRight = false;
        }

        if (spriteRenderer != null && Mathf.Abs(moveInput) > 0.1f)
        {
            spriteRenderer.flipX = !isFacingRight;
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            if (Time.time - lastProjectileTime >= projectileCooldown)
            {
                lastProjectileTime = Time.time;
                FireProjectile("Physical", Color.white);
            }
        }
    }

    public float GetProjectileCooldown()
    {
        return Mathf.Max(0f, projectileCooldown - (Time.time - lastProjectileTime));
    }

    public IEnumerator ExecuteSkill(SkillData skill)
    {
        Debug.Log($"=== Executing Skill: {skill.name} ===");

        foreach (string atomicSkill in skill.sequence)
        {
            yield return ExecuteAtomicSkill(atomicSkill);
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator ExecuteAtomicSkill(string atomicSkill)
    {
        string action = atomicSkill.ToUpper();

        switch (action)
        {
            // ===== ATTACK =====
            case "FIREPROJECTILE": yield return FireProjectileCoroutine(); break;
            case "EXPLOSIVEPROJECTILE": yield return ExplosiveProjectile(); break;
            case "PIERCINGPROJECTILE": yield return PiercingProjectile(); break;
            case "MELEESTRIKE": yield return MeleeStrike(); break;
            case "GROUNDSLAM": yield return GroundSlam(); break;

            // ===== MOVE =====
            case "DASH": yield return Dash(); break;
            case "MULTIJUMP": yield return MultiJump(); break;
            case "BLINK": yield return Blink(); break;

            // ===== DEFENCE =====
            case "SHIELDBUFF": yield return ShieldBuff(); break;
            case "INSTANTHEAL": yield return InstantHeal(); break;
            case "INVULNERABILITYWINDOW": yield return InvulnerabilityWindow(); break;
            case "DAMAGEREDUCTIONBUFF": yield return DamageReductionBuff(); break;

            // ===== Utility =====
            case "STUN": yield return Stun(); break;
            case "SLOW": yield return Slow(); break;
            case "AIRBORNE": yield return Airborne(); break;
        }
    }

    // ========================================
    // Movement-related primitive Skills
    // ========================================

    IEnumerator Dash()
    {
        isDashing = true;
        float startTime = Time.time;
        float originalGravity = rb.gravityScale;

        rb.gravityScale = 0;
        Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;

        while (Time.time < startTime + dashDuration)
        {
            rb.linearVelocity = dir * dashSpeed;
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGravity;
        isDashing = false;
    }

    IEnumerator MultiJump()
    {
        Debug.Log($"MultiJump!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Blink()
    {
        Debug.Log($"Blink!");
        yield return new WaitForSeconds(0.1f);
    }

    // ========================================
    // Attack-related primitive Skills
    // ========================================

    // NEED TO FIX
    void FireProjectile(string element, Color color)
    {
        if (projectilePrefab == null || firePoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ProjectileController pc = proj.GetComponent<ProjectileController>();

        if (pc != null)
        {
            Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;
            float dmg = (playerStats != null) ? playerStats.GetStat("AttackPower") : 10f;
            pc.Initialize(dir, dmg, element, color);
        }
    }

    IEnumerator FireProjectileCoroutine()
    {
        FireProjectile("Normal", Color.white);
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator ExplosiveProjectile()
    {
        Debug.Log($"ExplosiveProjectile!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator PiercingProjectile()
    {
        Debug.Log($"PiercingProjectile!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator MeleeStrike()
    {
        Debug.Log($"MeleeStrike!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator GroundSlam()
    {
        Debug.Log($"GroundSlam!");
        yield return new WaitForSeconds(0.1f);
    }

    // ========================================
    // Defense-related primitive Skills
    // ========================================
    IEnumerator ShieldBuff()
    {
        Debug.Log($"ShieldBuff!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator InstantHeal()
    {
        if (playerStats == null) yield break;

        float healAmount = playerStats.GetStat("MaxHealth") * 0.2f;
        playerStats.Heal(healAmount);

        Debug.Log($"Healed {healAmount} HP!");

        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator InvulnerabilityWindow()
    {
        Debug.Log($"InvulnerabilityWindow!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator DamageReductionBuff()
    {
        Debug.Log($"DamageReductionBuff!");
        yield return new WaitForSeconds(0.1f);
    }

    // ========================================
    // Utility-related primitive Skills
    // ========================================

    IEnumerator Stun()
    {
        Debug.Log("Attempting to stun nearby enemies!");

        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, 3f);

        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.gameObject != gameObject && col.GetComponent<PlayerStats>() != null)
            {
                Debug.Log($"Stunned: {col.gameObject.name}");
            }
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Slow()
    {
        Debug.Log($"Slow!");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Airborne()
    {
        Debug.Log($"Airborne!");
        yield return new WaitForSeconds(0.1f);
    }

    // ========================================
    // HELPER FUNCTIONS
    // ========================================
}