using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class GadgetSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerGadgetController controller;
    private int slotIndex = -1;
    private Image iconImage;
    private Image cooldownOverlay;
    private GameObject selectedBorder;
    private Image raycastTarget;

    [SerializeField] private Image tooltipBackground;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private TMP_FontAsset tooltipFont;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 120f);
    [SerializeField] private Vector2 tooltipSize = new Vector2(90f, 260f);
    [SerializeField] private float tooltipFontSize = 18f; // 20 > 

    private RectTransform tooltipRect;
   
    private void Awake()
    {
        controller = FindAnyObjectByType<PlayerGadgetController>();
        slotIndex = GetSlotIndex();
        iconImage = FindChildImage("IconImage");
        cooldownOverlay = FindChildImage("CooldownOverlay");
        selectedBorder = FindChild("SelectedBorder");

        EnsureRaycastTarget();
        InitializeTooltip();
        HideTooltip();
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
            HideTooltip();
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
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }
    private void ShowTooltip()
    {
        GadgetBase gadget = controller != null ? controller.GetGadget(slotIndex) : null;
        if (gadget == null || tooltipRect == null)
        {
            return;
        }

        if (tooltipText != null)
        {
            string displayName = string.IsNullOrWhiteSpace(gadget.DisplayName)
                ? GetFallbackDisplayName(gadget)
                : gadget.DisplayName;
            string description = string.IsNullOrWhiteSpace(gadget.Description)
                ? "설명이 아직 없습니다."
                : gadget.Description;

            tooltipText.text = $"{displayName}\n{description}";
        }

        tooltipRect.gameObject.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipRect != null)
        {
            tooltipRect.gameObject.SetActive(false);
        }
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

    private void InitializeTooltip()
    {
        if (tooltipBackground == null)
        {
            GameObject tooltipObject = new GameObject("Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(transform, false);
            tooltipRect = tooltipObject.GetComponent<RectTransform>();
            tooltipBackground = tooltipObject.AddComponent<Image>();
        }
        else
        {
            tooltipRect = tooltipBackground.rectTransform;
        }

        tooltipBackground.color = new Color(0f, 0f, 0f, 0.65f);
        tooltipBackground.raycastTarget = false;

        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.anchoredPosition = tooltipOffset;
        tooltipRect.sizeDelta = tooltipSize;

        if (tooltipText == null)
        {
            tooltipText = tooltipRect.GetComponentInChildren<TMP_Text>(true);
        }

        if (tooltipText == null)
        {
            GameObject textObject = new GameObject("TooltipText", typeof(RectTransform));
            textObject.transform.SetParent(tooltipRect, false);
            tooltipText = textObject.AddComponent<TextMeshProUGUI>();
        }

        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.color = Color.white;
        tooltipText.font = ResolveTooltipFont();
        tooltipText.fontSize = tooltipFontSize;
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.raycastTarget = false;

        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);
    }

    private void EnsureRaycastTarget()
    {
        raycastTarget = GetComponent<Image>();
        if (raycastTarget == null)
        {
            raycastTarget = gameObject.AddComponent<Image>();
        }

        raycastTarget.color = Color.clear;
        raycastTarget.raycastTarget = true;
    }

    private TMP_FontAsset ResolveTooltipFont()
    {
        if (tooltipFont != null)
        {
            return tooltipFont;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform searchRoot = canvas != null ? canvas.transform : transform.root;
        TMP_Text[] labels = searchRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i] != tooltipText && labels[i].font != null)
            {
                return labels[i].font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private string GetFallbackDisplayName(GadgetBase gadget)
    {
        if (gadget is Potion_Gadget)
        {
            return "포션";
        }

        if (gadget is Racing_Gadget)
        {
            return "레이싱";
        }

        if (gadget is FPS_Gadget)
        {
            return "수류탄";
        }

        if (gadget is GarryMode_Gadget)
        {
            return "중력";
        }

        return gadget.GetType().Name.Replace("_Gadget", "");
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
