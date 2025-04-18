using MelonLoader;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Il2CppScheduleOne.Money;
using System.Reflection.Metadata;

namespace BankApp
{

    public static class ColorPalette
    {
        public static readonly Color32 PROGRESSBAR_BG = new Color32(31, 41, 55, 255);
        public static readonly Color32 PROGRESSBAR_FILL = new Color32(8, 145, 178, 255);
        public static readonly Color32 ONLINEBAL = new Color32(34, 211, 238, 255);
        public static readonly Color32 CASHBAL = new Color32(52, 211, 153, 255);
        public static readonly Color32 TABBUTTON_ACTIVE = new Color32(8, 145, 178, 255);
        public static readonly Color32 TABBUTTON_ACTIVE_HOVER = new Color32(14, 116, 144, 255);
        public static readonly Color32 TABBUTTON_DISABLED = new Color32(31, 41, 55, 255);
        public static readonly Color32 TABBUTTON_DISABLED_HOVER = new Color32(55, 65, 81, 255);
        public static readonly Color32 AMOUNTBUTTONS_DEFAULT = new Color32(31, 41, 55, 255);
        public static readonly Color32 AMOUNTBUTTONS_HOVER = new Color32(55, 65, 81, 255);
        public static readonly Color32 AMOUNTBUTTONS_CLICKED = new Color32(20, 24, 29, 255);
        public static readonly Color32 CONFIRMBUTTON_DEFAULT = new Color32(5, 150, 105, 255);
        public static readonly Color32 CONFIRMBUTTON_HOVERED = new Color32(4, 120, 87, 255);
    }

    public class BankingAppBuilder
    {
        private readonly GameObject _container;
        private readonly int[] _amounts = { 1, 5, 10, 25, 50, 100, 500, 1000 };

        private readonly Action<int> _onAmountSelected;
        private readonly Action _onConfirm;
        private readonly Action _onMax;
        private readonly Action _onReset;
        private readonly Action _onDepositTab;
        private readonly Action _onWithdrawTab;

        public Text WeeklyAmountText { get; private set; }
        public RectTransform WeeklyProgressFill { get; private set; }
        public Text OnlineBalanceText { get; private set; }
        public Text CashBalanceText { get; private set; }
        public Text SelectedAmountText { get; private set; }
        public Button ConfirmButton { get; private set; }
        public Button MaxButton { get; private set; }
        public Button ResetButton { get; private set; }
        public GameObject WithdrawTab { get; private set; }
        public GameObject DepositTab { get; private set; }

        public BankingAppBuilder(
            GameObject container,
            Action<int> onAmountSelected,
            Action onConfirm,
            Action onMax,
            Action onReset,
            Action onDepositTab,
            Action onWithdrawTab)
        {
            _container = container;
            _onAmountSelected = onAmountSelected;
            _onConfirm = onConfirm;
            _onMax = onMax;
            _onReset = onReset;
            _onDepositTab = onDepositTab;
            _onWithdrawTab = onWithdrawTab;
        }

        public void BuildUI()
        {
            if (_container == null)
            {
                MelonLoader.MelonLogger.Error("BankingAppBuilder: Container is null");
                return;
            }

            for (int i = _container.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_container.transform.GetChild(i).gameObject);

            Sprite defaultSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite roundedEdgeSprite = GetGameSprite("Rectangle_RoundedEdges") ?? defaultSprite;
            Sprite buttonSprite = roundedEdgeSprite;

            var cardGO = new GameObject("Card");
            cardGO.transform.SetParent(_container.transform, false);
            var cardRT = cardGO.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(400, 600);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.localScale = new Vector3(1.6145f, 2.0618f, 1);

            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = new Color32(18, 18, 18, 255);
            cardImg.raycastTarget = true;
            cardImg.m_PixelsPerUnitMultiplier = 40f;

            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(cardGO.transform, false);
            var headerRT = headerGO.AddComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 0.7f);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.offsetMin = headerRT.offsetMax = Vector2.zero;
            headerRT.pivot = new Vector2(0.5f, 1f);

            var headerVLG = headerGO.AddComponent<VerticalLayoutGroup>();
            headerVLG.spacing = 10;
            headerVLG.padding = new RectOffset(20, 20, 20, 20);
            headerVLG.childAlignment = TextAnchor.UpperCenter;
            headerVLG.childForceExpandWidth = true;
            headerVLG.childForceExpandHeight = false;

            var progressGO = new GameObject("Progress");
            progressGO.transform.SetParent(headerGO.transform, false);

            var progressHLG = progressGO.AddComponent<HorizontalLayoutGroup>();
            progressHLG.childAlignment = TextAnchor.MiddleCenter;
            progressHLG.childForceExpandWidth = true;
            progressHLG.childForceExpandHeight = false;

            var progressText = CreateText("ProgressText", progressGO.transform, 16, new Color32(209, 213, 219, 255));
            progressText.text = "Weekly Progress";
            progressText.alignment = TextAnchor.MiddleLeft;

            var progressAmount = CreateText("ProgressAmount", progressGO.transform, 16, new Color32(209, 213, 219, 255));
            progressAmount.text = $"${ATM.WeeklyDepositSum} / ${ATM.WEEKLY_DEPOSIT_LIMIT}";
            progressAmount.alignment = TextAnchor.MiddleRight;
            WeeklyAmountText = progressAmount;

            var progressBarGO = new GameObject("ProgressBar");
            progressBarGO.transform.SetParent(headerGO.transform, false);
            var barLayout = progressBarGO.AddComponent<LayoutElement>();
            barLayout.minHeight = 8;
            barLayout.preferredHeight = 8;

            var progressBarBg = progressBarGO.AddComponent<Image>();
            progressBarBg.sprite = roundedEdgeSprite;
            progressBarBg.type = Image.Type.Sliced;
            progressBarBg.color = ColorPalette.PROGRESSBAR_BG;
            progressBarBg.m_PixelsPerUnitMultiplier = 40f;

            var progressBarFillGO = new GameObject("ProgressFill");
            progressBarFillGO.transform.SetParent(progressBarGO.transform, false);
            var progressBarFillRT = progressBarFillGO.AddComponent<RectTransform>();
            progressBarFillRT.anchorMin = Vector2.zero;
            float initRatio = Mathf.Clamp01(ATM.WeeklyDepositSum / (float)ATM.WEEKLY_DEPOSIT_LIMIT);
            progressBarFillRT.anchorMax = new Vector2(initRatio, 1);
            progressBarFillRT.offsetMin = progressBarFillRT.offsetMax = Vector2.zero;
            WeeklyProgressFill = progressBarFillRT;

            var progressBarFill = progressBarFillGO.AddComponent<Image>();
            progressBarFill.sprite = roundedEdgeSprite;
            progressBarFill.type = Image.Type.Simple;
            progressBarFill.color = ColorPalette.PROGRESSBAR_FILL;
            progressBarFill.m_PixelsPerUnitMultiplier = 1f;

            var balanceGO = new GameObject("Balance");
            balanceGO.transform.SetParent(headerGO.transform, false);
            var balanceRT = balanceGO.AddComponent<RectTransform>();
            balanceRT.sizeDelta = new Vector2(0, 60);
            Canvas.ForceUpdateCanvases();

            balanceRT.anchorMin = new Vector2(0.5f, 0.5f);
            balanceRT.anchorMax = new Vector2(0.5f, 0.5f);
            balanceRT.pivot = new Vector2(0.5f, 0.5f);
            balanceRT.anchoredPosition = new Vector2(0, 40);

            var balanceHLG = balanceGO.AddComponent<HorizontalLayoutGroup>();
            balanceHLG.spacing = 20;
            balanceHLG.childAlignment = TextAnchor.MiddleCenter;
            balanceHLG.childForceExpandWidth = true;
            balanceHLG.childForceExpandHeight = true;

            var cashBalanceGO = new GameObject("CashBalance");
            cashBalanceGO.transform.SetParent(balanceGO.transform, false);
            var cashBalanceVLG = cashBalanceGO.AddComponent<VerticalLayoutGroup>();
            cashBalanceVLG.spacing = 5;
            cashBalanceVLG.childAlignment = TextAnchor.MiddleCenter;
            cashBalanceVLG.childForceExpandWidth = false;
            cashBalanceVLG.childForceExpandHeight = false;

            var cashBalanceLabel = CreateText("CashLabel", cashBalanceGO.transform, 20, new Color32(139, 145, 155, 255));
            cashBalanceLabel.text = "Cash Balance";
            cashBalanceLabel.alignment = TextAnchor.MiddleCenter;

            CashBalanceText = CreateText("CashBalanceText", cashBalanceGO.transform, 20, ColorPalette.CASHBAL);
            CashBalanceText.text = "$0.00";
            CashBalanceText.alignment = TextAnchor.MiddleCenter;
            CashBalanceText.fontStyle = FontStyle.Bold;

            var onlineBalanceGO = new GameObject("OnlineBalance");
            onlineBalanceGO.transform.SetParent(balanceGO.transform, false);
            var onlineBalanceVLG = onlineBalanceGO.AddComponent<VerticalLayoutGroup>();
            onlineBalanceVLG.spacing = 5;
            onlineBalanceVLG.childAlignment = TextAnchor.MiddleCenter;
            onlineBalanceVLG.childForceExpandWidth = false;
            onlineBalanceVLG.childForceExpandHeight = false;

            var onlineBalanceLabel = CreateText("OnlineLabel", onlineBalanceGO.transform, 20, new Color32(139, 145, 155, 255));
            onlineBalanceLabel.text = "Online Balance";
            onlineBalanceLabel.alignment = TextAnchor.MiddleCenter;

            OnlineBalanceText = CreateText("OnlineBalanceText", onlineBalanceGO.transform, 20, ColorPalette.ONLINEBAL);
            OnlineBalanceText.text = "$0.00";
            OnlineBalanceText.alignment = TextAnchor.MiddleCenter;
            OnlineBalanceText.fontStyle = FontStyle.Bold;

            var amountDisplayGO = new GameObject("AmountDisplay");
            amountDisplayGO.transform.SetParent(headerGO.transform, false);
            var amountDisplayRT = amountDisplayGO.AddComponent<RectTransform>();
            amountDisplayRT.sizeDelta = new Vector2(0, 40);

            var amountDisplayHLG = amountDisplayGO.AddComponent<HorizontalLayoutGroup>();
            amountDisplayHLG.spacing = 10;
            amountDisplayHLG.childAlignment = TextAnchor.MiddleCenter;
            amountDisplayHLG.childForceExpandWidth = true;
            amountDisplayHLG.childForceExpandHeight = true;

            var amountLabel = CreateText("AmountLabel", amountDisplayGO.transform, 14, new Color32(209, 213, 219, 255));
            amountLabel.text = "Amount";
            amountLabel.alignment = TextAnchor.MiddleLeft;
            amountLabel.fontStyle = FontStyle.Bold;
            amountLabel.fontSize = 18;

            SelectedAmountText = CreateText("AmountText", amountDisplayGO.transform, 18, Color.white);
            SelectedAmountText.text = "$0.00";
            SelectedAmountText.alignment = TextAnchor.MiddleRight;
            SelectedAmountText.fontStyle = FontStyle.Bold;

            var tabsGO = new GameObject("Tabs");
            tabsGO.transform.SetParent(cardGO.transform, false);
            var tabsRT = tabsGO.AddComponent<RectTransform>();
            tabsRT.anchorMin = new Vector2(0.5f, 1);
            tabsRT.anchorMax = new Vector2(0.5f, 1);
            tabsRT.pivot = new Vector2(0.5f, 1);
            tabsRT.sizeDelta = new Vector2(360, 35);
            tabsRT.anchoredPosition = new Vector2(0, -165);

            var tabsHLG = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 10;
            tabsHLG.childAlignment = TextAnchor.MiddleCenter;
            tabsHLG.childForceExpandWidth = true;
            tabsHLG.childForceExpandHeight = true;



            DepositTab = CreateTabButton(tabsGO, "⇧ Deposit", true, _onDepositTab, buttonSprite);
            WithdrawTab = CreateTabButton(tabsGO, "⇩ Withdraw", false, _onWithdrawTab, buttonSprite);

            var gridGO = new GameObject("AmountButtons");
            gridGO.transform.SetParent(cardGO.transform, false);
            var gridRT = gridGO.AddComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0.0515f, 1);
            gridRT.anchorMax = new Vector2(1f, 1);
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;
            gridRT.anchoredPosition = new Vector2(0, -210);

            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(174.6f, 50);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            foreach (var amt in _amounts)
            {
                var btnGO = new GameObject($"Amount_{amt}");
                btnGO.transform.SetParent(gridGO.transform, false);

                var btnImg = btnGO.AddComponent<Image>();
                btnImg.sprite = buttonSprite;
                btnImg.type = Image.Type.Sliced;
                btnImg.color = ColorPalette.AMOUNTBUTTONS_DEFAULT;
                btnImg.m_PixelsPerUnitMultiplier = 40f;

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnImg;

                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = ColorPalette.AMOUNTBUTTONS_HOVER;
                colors.pressedColor = ColorPalette.AMOUNTBUTTONS_CLICKED;
                colors.selectedColor = btnImg.color;
                btn.colors = colors;

                var btnText = CreateText("Text", btnGO.transform, 16, Color.white);
                btnText.text = $"${amt}";
                btnText.alignment = TextAnchor.MiddleCenter;

                btn.onClick.AddListener((UnityAction)(() => _onAmountSelected(amt)));
            }

            var actionButtonsGO = new GameObject("ActionButtons");
            actionButtonsGO.transform.SetParent(cardGO.transform, false);
            var actionButtonsRT = actionButtonsGO.AddComponent<RectTransform>();
            actionButtonsRT.anchorMin = new Vector2(0.5f, 0);
            actionButtonsRT.anchorMax = new Vector2(0.5f, 0);
            actionButtonsRT.pivot = new Vector2(0.5f, 0);
            actionButtonsRT.sizeDelta = new Vector2(360, 50);
            actionButtonsRT.anchoredPosition = new Vector2(0, 100);

            var actionButtonsHLG = actionButtonsGO.AddComponent<HorizontalLayoutGroup>();
            actionButtonsHLG.spacing = 10;
            actionButtonsHLG.childAlignment = TextAnchor.MiddleCenter;
            actionButtonsHLG.childForceExpandWidth = true;
            actionButtonsHLG.childForceExpandHeight = true;

            var clearGO = new GameObject("ClearButton");
            clearGO.transform.SetParent(actionButtonsGO.transform, false);
            var clearImg = clearGO.AddComponent<Image>();
            clearImg.sprite = buttonSprite;
            clearImg.type = Image.Type.Sliced;
            clearImg.color = Color.white;
            clearImg.m_PixelsPerUnitMultiplier = 40f;

            ResetButton = clearGO.AddComponent<Button>();
            ResetButton.targetGraphic = clearImg;
            var clearColors = ResetButton.colors;
            clearImg.color = ColorPalette.AMOUNTBUTTONS_DEFAULT;
            clearColors.normalColor = Color.white;
            clearColors.highlightedColor = ColorPalette.AMOUNTBUTTONS_HOVER;
            clearColors.pressedColor = ColorPalette.AMOUNTBUTTONS_CLICKED;
            ResetButton.colors = clearColors;

            var clearText = CreateText("Text", clearGO.transform, 16, Color.white);
            clearText.text = "✕ Clear";
            clearText.alignment = TextAnchor.MiddleCenter;

            ResetButton.onClick.AddListener((UnityAction)(() => _onReset()));

            var maxGO = new GameObject("MaxButton");
            maxGO.transform.SetParent(actionButtonsGO.transform, false);
            var maxImg = maxGO.AddComponent<Image>();
            maxImg.sprite = buttonSprite;
            maxImg.type = Image.Type.Sliced;
            maxImg.color = Color.white;
            maxImg.m_PixelsPerUnitMultiplier = 40f;

            MaxButton = maxGO.AddComponent<Button>();
            MaxButton.targetGraphic = maxImg;
            var maxColors = MaxButton.colors;
            maxImg.color = ColorPalette.AMOUNTBUTTONS_DEFAULT;
            maxColors.normalColor = Color.white;
            maxColors.highlightedColor = ColorPalette.AMOUNTBUTTONS_HOVER;
            maxColors.pressedColor = ColorPalette.AMOUNTBUTTONS_CLICKED;
            MaxButton.colors = maxColors;

            var maxText = CreateText("Text", maxGO.transform, 16, Color.white);
            maxText.text = "MAX";
            maxText.alignment = TextAnchor.MiddleCenter;
            MaxButton.onClick.AddListener((UnityAction)(() => _onMax()));

            var confirmGO = new GameObject("ConfirmButton");
            confirmGO.transform.SetParent(cardGO.transform, false);
            var confirmRT = confirmGO.AddComponent<RectTransform>();
            confirmRT.anchorMin = new Vector2(0.05f, 0.05f);
            confirmRT.anchorMax = new Vector2(0.95f, 0.143f);
            confirmRT.offsetMin = new Vector2(0, 4);
            confirmRT.offsetMax = new Vector2(0, 4);

            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.sprite = buttonSprite;
            confirmImg.type = Image.Type.Sliced;
            confirmImg.color = ColorPalette.CONFIRMBUTTON_DEFAULT;
            confirmImg.m_PixelsPerUnitMultiplier = 40f;

            ConfirmButton = confirmGO.AddComponent<Button>();
            ConfirmButton.targetGraphic = confirmImg;
            var confirmColors = ConfirmButton.colors;
            confirmColors.normalColor = ColorPalette.CONFIRMBUTTON_DEFAULT;
            confirmColors.highlightedColor = ColorPalette.CONFIRMBUTTON_HOVERED;
            confirmColors.pressedColor = confirmColors.pressedColor;
            confirmColors.selectedColor = confirmImg.color;
            ConfirmButton.colors = confirmColors;

            var confirmText = CreateText("Text", confirmGO.transform, 18, Color.white);
            confirmText.text = "";
            confirmText.alignment = TextAnchor.MiddleCenter;
            confirmText.fontStyle = FontStyle.Bold;
            ConfirmButton.onClick.AddListener((UnityAction)(() => _onConfirm()));

            MelonLogger.Msg("BankingAppBuilder: UI build complete.");
        }

        private GameObject CreateTabButton(
            GameObject parent,
            string label,
            bool active,
            Action onClick,
            Sprite buttonSprite)
        {
            var go = new GameObject($"{label}Tab");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);

            var img = go.AddComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.color = active
                ? new Color32(8, 145, 178, 255)
                : new Color32(31, 41, 55, 255);
            img.m_PixelsPerUnitMultiplier = 40f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = img.color;
            colors.highlightedColor = active
                ? new Color32(14, 116, 144, 255)
                : new Color32(55, 65, 81, 255);
            colors.pressedColor = active
                ? new Color32(7, 89, 110, 255)
                : new Color32(20, 24, 29, 255);
            colors.selectedColor = img.color;
            btn.colors = colors;

            var text = CreateText($"{label}Text", go.transform, 20, Color.white);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 20;

            btn.onClick.AddListener((UnityAction)(() => onClick()));
            return go;
        }

        private Text CreateText(string name, Transform parent, int size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        private Sprite GetGameSprite(string spriteName)
        {
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var sprite in sprites)
            {
                if (sprite.name.Equals(spriteName, StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }

            MelonLogger.Error($"Sprite '{spriteName}' not found!");
            return null;
        }
    }
}