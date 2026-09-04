using UnityEngine;
using UnityEngine.UI;

public class GadgetSlotUI : MonoBehaviour
{
    private PlayerGadgetController controller;
    private int slotIndex = -1;
    private Image iconImage;
    private Image cooldownOverlay;
    private GameObject selectedBorder;

    private void Awake()
    {
        controller = FindAnyObjectByType<PlayerGadgetController>();
        slotIndex = GetSlotIndex();
        iconImage = FindChildImage("IconImage");
        cooldownOverlay = FindChildImage("CooldownOverlay");
        selectedBorder = FindChild("SelectedBorder");
    }

    private void Update()
    {
        if (controller == null)
        {
            controller = FindAnyObjectByType<PlayerGadgetController>();
        }

        GadgetBase gadget = controller != null ? controller.GetGadget(slotIndex) : null;
        if (gadget == null)
        {
            SetVisible(false);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = gadget.Icon;
            iconImage.color = gadget.CanUse ? Color.white : Color.gray;
            iconImage.enabled = gadget.Icon != null;
        }

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(IsSelectedGadget());
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(!gadget.CanUse);
            cooldownOverlay.fillAmount = gadget.CooldownRatio;
        }
    }

    private bool IsSelectedGadget()
    {
        return controller != null && controller.SelectedIndex == slotIndex;
    }

    private void SetVisible(bool visible)
    {
        if (iconImage != null)
        {
            iconImage.enabled = visible;
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(false);
        }

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(false);
        }
    }

    private Image FindChildImage(string childName)
    {
        GameObject child = FindChild(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private GameObject FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i].gameObject;
            }
        }

        return null;
    }

    private int GetSlotIndex()
    {
        int separatorIndex = name.LastIndexOf('_');
        if (separatorIndex >= 0 && int.TryParse(name.Substring(separatorIndex + 1), out int parsedIndex))
        {
            return parsedIndex;
        }

        return transform.GetSiblingIndex();
    }
}
