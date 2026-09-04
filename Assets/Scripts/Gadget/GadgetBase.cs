using UnityEngine;

public abstract class GadgetBase : MonoBehaviour
{
    [SerializeField] private Sprite icon;
    [SerializeField] private string displayName;

    public Sprite Icon => icon; 
    public string DisplayName => displayName;

    [SerializeField]
    private float cooldown = 5f;

    private float lastUsedTime = -Mathf.Infinity;

    
    public float RemainingCooldown =>
       Mathf.Max(0f, cooldown - (Time.time - lastUsedTime));
    public bool CanUse => RemainingCooldown <= 0;

    public float CooldownDuration => cooldown;
    public float CooldownRatio => cooldown <= 0f ? 0f : RemainingCooldown / cooldown;
    // UI 용 
    public bool TryUse()
    {
        if (!CanUse)
        {
            return false; 
        }
        bool success = Use();
        if (! success)
        {
            return false; 
        }
        lastUsedTime = Time.time; //마지막 사용 
        return true; 

    }
    protected abstract bool Use();
    //모든 가젯은 Use()라는 기능을 반드시 하나씩 구현해야 한다.
}
