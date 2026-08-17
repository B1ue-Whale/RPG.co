using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private Collider2D attackHitbox;
    [SerializeField] private SpriteRenderer hitboxRenderer;

    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private float attackOffset = 1f;

    public void Attack()
    {
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        int direction = motor.FacingDirection;

        attackHitbox.transform.localPosition =
            new Vector3(direction * attackOffset, 0f, 0f);

        attackHitbox.enabled = true;
        if (hitboxRenderer != null)
        {
            hitboxRenderer.enabled = true;
        }

        yield return new WaitForSeconds(attackDuration);


        attackHitbox.enabled = false;
        if (hitboxRenderer != null)
        {
            hitboxRenderer.enabled = false;
        }
    }
}


