using UnityEngine;
using UnityEngine.UI;

public class DamagePopup : MonoBehaviour
{
    public Text damageText;
    public float lifetime = 1f;
    public float moveSpeed = 1f;
    public Color criticalColor = Color.red;
    public Color normalColor = Color.white;

    private float timer = 0f;
    private Vector3 moveDirection;

    public void Initialize(float damage, bool isCritical = false)
    {
        if (damageText != null)
        {
            damageText.text = damage.ToString("F0");
            damageText.color = isCritical ? criticalColor : normalColor;
            damageText.fontSize = isCritical ? 36 : 24;
        }

        moveDirection = Vector3.up + new Vector3(Random.Range(-0.3f, 0.3f), 0, 0);
    }

    void Update()
    {
        timer += Time.deltaTime;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        if (damageText != null)
        {
            float alpha = 1f - (timer / lifetime);
            Color color = damageText.color;
            color.a = alpha;
            damageText.color = color;
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}