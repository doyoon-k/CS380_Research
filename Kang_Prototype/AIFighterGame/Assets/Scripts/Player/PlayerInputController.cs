using UnityEngine;

/// <summary>
/// Polls keyboard input and forwards it to movement/combat/ability controllers.
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [Header("Targets")]
    public PlayerMovementController movement;
    public PlayerCombatController combat;
    public PlayerAbilityController ability;

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovementController>();
        if (combat == null) combat = GetComponent<PlayerCombatController>();
        if (ability == null) ability = GetComponent<PlayerAbilityController>();
    }

    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        movement?.SetMoveInput(horizontal);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            movement?.RequestJump();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            combat?.TryAttack();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ability?.TryUseActiveSkill(0);
        }
    }
}
