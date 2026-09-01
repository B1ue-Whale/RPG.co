using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGadgetController : MonoBehaviour
{
   
    private GadgetBase equippedGadget;


    public GadgetBase EquippedGadget => equippedGadget;

    [SerializeField] private GadgetBase gadget;
    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ToggleGadget(); //탈부착 
            
        }
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            UseGadget(); 
        }
    }

    public void ToggleGadget()
    {
        if (equippedGadget == null)
        {

            EquipGadget(gadget);
        }
        else
        {
            UnequipGadget();
        }

        
    }
    public void UseGadget()
    {
        if (equippedGadget == null)
        {
            Debug.Log("No Gadget Equipped");
            return;
        }

        equippedGadget.TryUse();
        Debug.Log("사용");
    }

    public void EquipGadget(GadgetBase gadget)
    {
        equippedGadget = gadget;
        Debug.Log("착용");
    }
    public void UnequipGadget()
    {
        equippedGadget = null;
        Debug.Log("탈착");
    }
}
