using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkillExecutor : MonoBehaviour
{
    [Header("Components")]
    public PlayerStats playerStats;
    public Rigidbody2D rb;
    public AttackHitbox attackHitbox;
    public SpriteRenderer spriteRenderer;

    [Header("Movement Settings")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.25f;

    [Header("Attack Settings")]
    public float attackDelay = 0.2f;
    public float heavyAttackMultiplier = 1.5f;

    [Header("State Tracking")]
    private bool isInvincible = false;
    private bool hasGuard = false;
    private bool hasSuperArmor = false;
    private List<GameObject> markedEnemies = new List<GameObject>();
    private GameObject aimedTarget = null;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (attackHitbox == null)
            attackHitbox = GetComponentInChildren<AttackHitbox>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        Debug.Log($"SkillExecutor initialized - rb: {rb != null}, stats: {playerStats != null}, hitbox: {attackHitbox != null}");
    }

    public IEnumerator ExecuteSkill(SkillData skill)
    {
        Debug.Log($"=== Executing Skill: {skill.name} ===");

        foreach (string atomicSkill in skill.sequence)
        {
            Debug.Log($"Executing atomic skill: {atomicSkill}");
            yield return ExecuteAtomicSkill(atomicSkill);
            yield return new WaitForSeconds(0.05f);
        }

        Debug.Log($"Skill {skill.name} complete!");
    }

    IEnumerator ExecuteAtomicSkill(string atomicSkill)
    {
        switch (atomicSkill.ToUpper())
        {
            // ===== MOVE =====
            case "FORWARD":
                yield return MoveForward();
                break;

            case "BACK":
                yield return MoveBackward();
                break;

            case "LEFT":
                yield return MoveLeft();
                break;

            case "RIGHT":
                yield return MoveRight();
                break;

            case "JUMP":
                yield return Jump();
                break;

            case "LAND":
                yield return Land();
                break;

            // ===== ATTACK =====
            case "HIT":
            case "LOW":
            case "MID":
            case "HIGH":
                yield return Attack(1.0f);
                break;

            case "HEAVY_HIT":
                yield return Attack(heavyAttackMultiplier);
                break;

            // ===== POSE =====
            case "CROUCH":
                yield return Crouch();
                break;

            case "STAND":
                yield return Stand();
                break;

            case "ROLL":
                yield return Roll();
                break;

            case "DODGE":
                yield return Dodge();
                break;

            // ===== STATE =====
            case "INVINCIBLE":
                yield return SetInvincible();
                break;

            case "GUARD":
                yield return SetGuard();
                break;

            case "SUPERARMOR":
                yield return SetSuperArmor();
                break;

            case "STUN_ENEMY":
                yield return StunEnemy();
                break;

            // ===== EFFECT =====
            case "HEAL":
                yield return Heal();
                break;

            case "BUFF_SELF":
                yield return BuffSelf();
                break;

            case "DEBUFF_ENEMY":
                yield return DebuffEnemy();
                break;

            case "TAUNT":
                yield return Taunt();
                break;

            // ===== TARGET =====
            case "AIM":
                yield return Aim();
                break;

            case "MARK":
                yield return Mark();
                break;

            case "TRACK":
                yield return Track();
                break;

            default:
                Debug.LogWarning($"Unknown atomic skill: {atomicSkill}");
                break;
        }
    }

    // ========================================
    // MOVE SKILLS
    // ========================================

    IEnumerator MoveForward()
    {
        if (rb == null) yield break;

        Debug.Log($"Dashing forward at speed {dashSpeed}!");
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(dashSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Debug.Log("Forward dash complete!");
    }

    IEnumerator MoveBackward()
    {
        if (rb == null) yield break;

        Debug.Log($"Dashing backward at speed {dashSpeed}!");
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(-dashSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Debug.Log("Backward dash complete!");
    }

    IEnumerator MoveLeft()
    {
        if (rb == null) yield break;

        Debug.Log("Moving left!");
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            rb.linearVelocity = new Vector2(-dashSpeed * 0.7f, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Debug.Log("Left movement complete!");
    }

    IEnumerator MoveRight()
    {
        if (rb == null) yield break;

        Debug.Log("Moving right!");
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            rb.linearVelocity = new Vector2(dashSpeed * 0.7f, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Debug.Log("Right movement complete!");
    }

    IEnumerator Jump()
    {
        if (rb == null || playerStats == null) yield break;

        float jumpForce = playerStats.currentStats.Jump;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        Debug.Log($"Jumping with force {jumpForce}!");

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Land()
    {
        if (rb == null) yield break;

        Debug.Log("Waiting to land...");

        yield return new WaitUntil(() => Mathf.Abs(rb.linearVelocity.y) < 0.5f);

        Debug.Log("Landed!");
        yield return new WaitForSeconds(0.05f);
    }

    // ========================================
    // ATTACK SKILLS
    // ========================================

    IEnumerator Attack(float damageMultiplier)
    {
        if (attackHitbox == null) yield break;

        Debug.Log($"Executing attack with {damageMultiplier}x damage!");

        float originalDamage = attackHitbox.damage;
        attackHitbox.damage = originalDamage * damageMultiplier;

        attackHitbox.ActivateHitbox(0.2f);

        yield return new WaitForSeconds(attackDelay);

        attackHitbox.damage = originalDamage;
    }

    // ========================================
    // POSE SKILLS
    // ========================================

    IEnumerator Crouch()
    {
        Debug.Log("Crouching!");

        if (spriteRenderer != null)
        {
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(scale.x, scale.y * 0.5f, scale.z);
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Stand()
    {
        Debug.Log("Standing up!");

        if (spriteRenderer != null)
        {
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(scale.x, Mathf.Abs(scale.y) * 2f, scale.z);
        }

        yield return new WaitForSeconds(0.05f);
    }

    IEnumerator Roll()
    {
        if (rb == null) yield break;

        Debug.Log("Rolling!");

        isInvincible = true;
        float rollSpeed = dashSpeed * 1.5f;
        float rollDuration = 0.2f;
        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            rb.linearVelocity = new Vector2(rollSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        isInvincible = false;
        Debug.Log("Roll complete!");
    }

    IEnumerator Dodge()
    {
        Debug.Log("Dodging - Brief invincibility!");

        isInvincible = true;

        if (spriteRenderer != null)
        {
            Color original = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);

            yield return new WaitForSeconds(0.2f);

            spriteRenderer.color = original;
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }

        isInvincible = false;
        Debug.Log("Dodge complete!");
    }

    // ========================================
    // STATE SKILLS
    // ========================================

    IEnumerator SetInvincible()
    {
        Debug.Log("Invincibility activated!");
        isInvincible = true;

        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(Color.cyan, 0.5f));
        }

        yield return new WaitForSeconds(0.5f);

        isInvincible = false;
        Debug.Log("Invincibility ended!");
    }

    IEnumerator SetGuard()
    {
        Debug.Log("Guard stance activated!");
        hasGuard = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.blue;
        }

        yield return new WaitForSeconds(0.5f);

        hasGuard = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
        Debug.Log("Guard ended!");
    }

    IEnumerator SetSuperArmor()
    {
        Debug.Log("Super Armor activated!");
        hasSuperArmor = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.yellow;
        }

        yield return new WaitForSeconds(1f);

        hasSuperArmor = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
        Debug.Log("Super Armor ended!");
    }

    IEnumerator StunEnemy()
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

    // ========================================
    // EFFECT SKILLS
    // ========================================

    IEnumerator Heal()
    {
        if (playerStats == null) yield break;

        float healAmount = playerStats.currentStats.MaxHP * 0.2f;
        playerStats.Heal(healAmount);

        Debug.Log($"Healed {healAmount} HP!");

        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(Color.green, 0.2f));
        }

        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator BuffSelf()
    {
        Debug.Log("Buff Self - Visual effect!");

        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(Color.yellow, 0.3f));
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator DebuffEnemy()
    {
        Debug.Log("Attempting to debuff nearby enemies!");

        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, 3f);

        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.gameObject != gameObject && col.GetComponent<PlayerStats>() != null)
            {
                Debug.Log($"Debuffed: {col.gameObject.name}");
            }
        }

        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator Taunt()
    {
        Debug.Log("Taunting enemies!");

        if (spriteRenderer != null)
        {
            StartCoroutine(FlashSprite(Color.red, 0.3f));
        }

        yield return new WaitForSeconds(0.3f);
    }

    // ========================================
    // TARGET SKILLS
    // ========================================

    IEnumerator Aim()
    {
        Debug.Log("Aiming at nearest enemy!");

        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, 10f);
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.gameObject != gameObject && col.GetComponent<PlayerStats>() != null)
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    aimedTarget = col.gameObject;
                }
            }
        }

        if (aimedTarget != null)
        {
            Debug.Log($"Aimed at: {aimedTarget.name}");
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Mark()
    {
        Debug.Log("Marking nearby enemies!");

        Collider2D[] nearbyEnemies = Physics2D.OverlapCircleAll(transform.position, 5f);
        markedEnemies.Clear();

        foreach (Collider2D col in nearbyEnemies)
        {
            if (col.gameObject != gameObject && col.GetComponent<PlayerStats>() != null)
            {
                markedEnemies.Add(col.gameObject);
                Debug.Log($"Marked: {col.gameObject.name}");
            }
        }

        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Track()
    {
        Debug.Log("Tracking target!");

        if (aimedTarget != null && rb != null)
        {
            Vector2 direction = (aimedTarget.transform.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direction.x * dashSpeed * 0.5f, rb.linearVelocity.y);

            yield return new WaitForSeconds(0.3f);

            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
        else
        {
            Debug.Log("No target to track!");
            yield return new WaitForSeconds(0.05f);
        }
    }

    // ========================================
    // HELPER FUNCTIONS
    // ========================================

    IEnumerator FlashSprite(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        Color original = spriteRenderer.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            spriteRenderer.color = Color.Lerp(original, flashColor, Mathf.PingPong(elapsed * 10f, 1f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.color = original;
    }

    public bool IsInvincible()
    {
        return isInvincible;
    }

    public bool HasGuard()
    {
        return hasGuard;
    }

    public bool HasSuperArmor()
    {
        return hasSuperArmor;
    }
}