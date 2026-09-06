using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGadgetController : MonoBehaviour
{
    [SerializeField] private GadgetBase[] gadgets = new GadgetBase[2]; //매 스테이지 2 개
    [SerializeField] private int selectedIndex;

    public GadgetBase[] Gadgets => gadgets;
    public int SelectedIndex => selectedIndex;
    public GadgetBase SelectedGadget => GetGadget(selectedIndex);

    private void Start()
    {
        SelectGadget(0);
    }
    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
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
        Debug.Log("선택");
    }

    public void UseGadget()
    {
        TryUseSelectedGadget();
    }

    public bool TryUseSelectedGadget()
    {
        GadgetBase gadget = SelectedGadget;
        if (gadget == null)
        {
            Debug.Log("No Gadget Selected");
            return false;
        }

        bool success = gadget.TryUse();
        Debug.Log(success ? "사용" : $"쿨타임{gadget.RemainingCooldown:F0}초 남음");
        return success;
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
