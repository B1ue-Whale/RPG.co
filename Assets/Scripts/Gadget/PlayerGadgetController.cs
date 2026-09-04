using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGadgetController : MonoBehaviour
{
    private GadgetBase equippedGadget;

    [SerializeField] private GadgetBase[] gadgets = new GadgetBase[2]; //매 스테이지 2 개
    [SerializeField] private int selectedIndex;

    public GadgetBase[] Gadgets => gadgets;
    public int SelectedIndex => selectedIndex;
    public GadgetBase SelectedGadget => GetGadget(selectedIndex);
    public GadgetBase EquippedGadget => equippedGadget;

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

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectGadget(0);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectGadget(1);
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            UseGadget(); 
        }
    }

    public void SelectGadget(int index)
    {
        GadgetBase selectedGadget = GetGadget(index);
        if (selectedGadget == null)
        {
            return;
        }

        selectedIndex = index;

        if (equippedGadget != null)
        {
            EquipGadget(selectedGadget);
        }

        Debug.Log("선택");
    }

    public void ToggleGadget()
    {
        if (equippedGadget == null)
        {
            EquipGadget(SelectedGadget);
        }
        else
        {
            UnequipGadget();
        }
    }

    public void UseGadget()
    {
        TryUseSelectedGadget();
    }

    public bool TryUseSelectedGadget()
    {
        GadgetBase gadget = EquippedGadget;
        if (gadget == null)
        {
            Debug.Log("No Gadget Equipped");
            return false;
        }

        bool success = gadget.TryUse();
        Debug.Log(success ? "사용" : "쿨타임");
        return success;
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

    public GadgetBase GetGadget(int index)
    {
        if (gadgets == null || index < 0 || index >= gadgets.Length)
        {
            return null;
        }

        return gadgets[index];
    }
}
