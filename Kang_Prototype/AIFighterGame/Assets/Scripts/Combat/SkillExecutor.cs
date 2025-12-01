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

    [Header("Prefabs")]
    public GameObject projectilePrefab;
    public Transform firePoint;

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
    private bool isFacingRight = true;

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

    void Update()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        if (moveInput > 0) isFacingRight = true;
        else if (moveInput < 0) isFacingRight = false;

        if (spriteRenderer != null && Mathf.Abs(moveInput) > 0.1f)
        {
            spriteRenderer.flipX = !isFacingRight;
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            FireProjectile("Physical", Color.white);
        }
    }

    public IEnumerator ExecuteSkill(SkillData skill)
    {
        Debug.Log($"=== Executing Skill: {skill.name} ===");

        string element = DetectElement(skill.name, skill.description);
        Color elementColor = GetColorForElement(element);

        StartCoroutine(FlashSprite(elementColor, 0.5f));

        foreach (string atomicSkill in skill.sequence)
        {
            Debug.Log($"Executing atomic skill: {atomicSkill}");
            yield return ExecuteAtomicSkill(atomicSkill, element, elementColor);
            yield return new WaitForSeconds(0.05f);
        }

        Debug.Log($"Skill {skill.name} complete!");
    }

    IEnumerator ExecuteAtomicSkill(string atomicSkill, string element = "Physical", Color color = default)
    {
        if (color == default) color = Color.white;
        string action = atomicSkill.ToUpper();

        if (action.Contains("PROJECTILE") || action.Contains("SHOOT") || action.Contains("FIRE"))
        {
            FireProjectile(element, color);
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        switch (action)
        {
            // ===== MOVE =====
            case "FORWARD": yield return MoveForward(); break;
            case "BACK": yield return MoveBackward(); break;
            case "LEFT": yield return MoveLeft(); break;
            case "RIGHT": yield return MoveRight(); break;
            case "JUMP": yield return Jump(); break;
            case "LAND": yield return Land(); break;
            case "DASH":
            case "RUSH":
                yield return MoveForward();
                break;

            // ===== ATTACK =====
            case "HIT":
            case "LOW":
            case "MID":
            case "HIGH":
            case "ATTACK":
                yield return Attack(1.0f);
                break;

            case "HEAVY_HIT":
                yield return Attack(heavyAttackMultiplier);
                break;

            // ===== POSE =====
            case "CROUCH": yield return Crouch(); break;
            case "STAND": yield return Stand(); break;
            case "ROLL": yield return Roll(); break;
            case "DODGE": yield return Dodge(); break;

            // ===== STATE =====
            case "INVINCIBLE": yield return SetInvincible(); break;
            case "GUARD": yield return SetGuard(); break;
            case "SUPERARMOR": yield return SetSuperArmor(); break;
            case "STUN_ENEMY":
            case "STUN": 
                yield return StunEnemy(); break;

            // ===== EFFECT =====
            case "HEAL": yield return Heal(); break;
            case "BUFF_SELF":
            case "BUFF": 
                yield return BuffSelf(); break;
            case "DEBUFF_ENEMY": yield return DebuffEnemy(); break;
            case "TAUNT": yield return Taunt(); break;

            // ===== TARGET =====
            case "AIM": yield return Aim(); break;
            case "MARK": yield return Mark(); break;
            case "TRACK": yield return Track(); break;

            default:
                Debug.LogWarning($"Unknown atomic skill: {atomicSkill} (Trying generic action)");
                if (atomicSkill.ToUpper().Contains("ATTACK")) yield return Attack(1.0f);
                break;
        }
    }    

    // ========================================
    // MOVE SKILLS
    // ========================================

    IEnumerator MoveForward()
    {
        if (rb == null) yield break;

        float speed = isFacingRight ? dashSpeed : -dashSpeed;

        Debug.Log($"Dashing {(isFacingRight ? "Right" : "Left")}!");
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    IEnumerator MoveBackward()
    {
        if (rb == null) yield break;
        float speed = isFacingRight ? -dashSpeed : dashSpeed;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Land()
    {
        if (rb == null) yield break;
        yield return new WaitUntil(() => Mathf.Abs(rb.linearVelocity.y) < 0.5f);
        yield return new WaitForSeconds(0.05f);
    }

    // ========================================
    // ATTACK SKILLS
    // ========================================

    IEnumerator Attack(float damageMultiplier)
    {
        if (attackHitbox == null) yield break;
        Debug.Log($"Attack ({damageMultiplier}x)!");
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
        if (spriteRenderer != null) transform.localScale = new Vector3(transform.localScale.x, 0.5f, 1f);
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator Stand()
    {
        if (spriteRenderer != null) transform.localScale = new Vector3(transform.localScale.x, 1f, 1f);
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

    void FireProjectile(string element, Color color)
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning("Projectile Prefab or FirePoint missing!");
            return;
        }

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ProjectileController pc = proj.GetComponent<ProjectileController>();

        Vector2 dir = isFacingRight ? Vector2.right : Vector2.left;
        float dmg = playerStats.currentStats.Attack;

        pc.Initialize(dir, dmg, element, color);
        Debug.Log($"Fired {element} projectile!");
    }

    // ========================================
    // HELPER FUNCTIONS
    // ========================================
    string DetectElement(string name, string desc)
    {
        string text = (name + desc).ToLower();

        // 1. Lightning
        if (text.Contains("lightning") || text.Contains("thunder") || text.Contains("volt") ||
            text.Contains("electric") || text.Contains("shock") || text.Contains("storm"))
            return "Lightning";

        // 2. Ice
        if (text.Contains("ice") || text.Contains("frost") || text.Contains("freez") ||
            text.Contains("cold") || text.Contains("chill") || text.Contains("snow") || text.Contains("crystal"))
            return "Ice";

        // 3. Fire
        if (text.Contains("fire") || text.Contains("burn") || text.Contains("flame") ||
            text.Contains("heat") || text.Contains("blaze") || text.Contains("ember") || text.Contains("inferno"))
            return "Fire";

        return "Physical";
    }

    Color GetColorForElement(string element)
    {
        switch (element)
        {
            case "Fire": return new Color(1f, 0.4f, 0.4f); // Red
            case "Ice": return new Color(0.4f, 0.9f, 1f);  // Cyan
            case "Lightning": return new Color(1f, 1f, 0.4f); // Yellow
            default: return Color.white;
        }
    }

    IEnumerator FlashSprite(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;
        Color original = Color.blue;
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