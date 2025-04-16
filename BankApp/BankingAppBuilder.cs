using MelonLoader;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BankApp
{
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

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(_container.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            var gradTex = new Texture2D(1, 256);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                var top = new Color(0.18f, 0.48f, 0.95f);
                var bottom = new Color(0.18f, 0.31f, 0.51f);
                gradTex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }
            gradTex.Apply();

            var gradSpr = Sprite.Create(
                gradTex,
                new Rect(0, 0, gradTex.width, gradTex.height),
                new Vector2(0.5f, 0.5f)
            );

            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = gradSpr;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;

            var infoGO = new GameObject("InfoPanel");
            infoGO.transform.SetParent(_container.transform, false);
            var vlg = infoGO.AddComponent<VerticalLayoutGroup>();
            var rt = infoGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.75f);
            rt.anchorMax = new Vector2(0.95f, 0.98f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            vlg.spacing = 5;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            WeeklyAmountText = CreateText("WeeklyLimit", infoGO.transform, 34, Color.white);
            OnlineBalanceText = CreateText("OnlineBalance", infoGO.transform, 33, Color.cyan);
            CashBalanceText = CreateText("CashBalance", infoGO.transform, 33, Color.green);
            SelectedAmountText = CreateText("SelectedAmount", infoGO.transform, 33, Color.yellow);
            SelectedAmountText.text = "Deposit: $0.00";

            var tabsGO = new GameObject("TabPanel");
            tabsGO.transform.SetParent(_container.transform, false);
            var tabsRt = tabsGO.AddComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0.05f, 0.8f);
            tabsRt.anchorMax = new Vector2(0.95f, 0.9f);
            tabsRt.offsetMin = tabsRt.offsetMax = Vector2.zero;
            tabsGO.transform.localPosition = new Vector3(0, 200, 0);

            var hlg = tabsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;

            DepositTab = CreateTabButton(tabsGO, "Deposit", true, _onDepositTab);
            WithdrawTab = CreateTabButton(tabsGO, "Withdraw", false, _onWithdrawTab);

            var gridGO = new GameObject("AmountButtons");
            gridGO.transform.SetParent(_container.transform, false);
            var gridRt = gridGO.AddComponent<RectTransform>();
            gridGO.transform.localPosition = new Vector2(249, -400);
            gridGO.transform.localScale = new Vector2(1.4f, 1.4f);
            gridRt.anchorMin = new Vector2(0.05f, 0.25f);
            gridRt.anchorMax = new Vector2(0.95f, 0.74f);

            var grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 50);
            grid.spacing = new Vector2(15, 15);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            foreach (var amt in _amounts)
            {
                var bgo = new GameObject($"Amount_{amt}");
                bgo.transform.SetParent(gridGO.transform, false);
                var brt = bgo.AddComponent<RectTransform>();
                brt.sizeDelta = new Vector2(160, 50);

                var img = bgo.AddComponent<Image>();
                img.color = new Color(0.8f, 0.85f, 0.9f);

                var btn = bgo.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                var cb = btn.colors;
                cb.normalColor = img.color;
                cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(0.6f, 0.7f, 0.8f);
                cb.disabledColor = Color.gray;
                btn.colors = cb;

                var tgo = new GameObject("Text");
                tgo.transform.SetParent(bgo.transform, false);
                var trt = tgo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;

                var txt = tgo.AddComponent<Text>();
                txt.text = $"${amt}";
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.fontSize = 20;

                btn.onClick.AddListener((UnityAction)(() => _onAmountSelected(amt)));
            }

            var cgo = CreateActionButton(_container, "Confirm",
                new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.15f),
                new Color(0.3f, 0.6f, 0.3f), 35, 150, 1);
            ConfirmButton = cgo.GetComponent<Button>();
            ConfirmButton.onClick.AddListener((UnityAction)(() => _onConfirm()));

            var mgo = CreateActionButton(_container, "MAX",
                new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.2f),
                new Color(0.6f, 0.7f, 0.8f), 35, -37, -49.6f);
            MaxButton = mgo.GetComponent<Button>();
            MaxButton.transform.localPosition = new Vector2(124.269f, -320f);
            MaxButton.onClick.AddListener((UnityAction)(() => _onMax()));

            var rgo = CreateActionButton(_container, "Clear",
                new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.2f),
                new Color(0.6f, 0.7f, 0.8f), 35, -37, -49.6f);
            ResetButton = rgo.GetComponent<Button>();
            ResetButton.transform.localPosition = new Vector2(-122.169f, -320f);
            ResetButton.onClick.AddListener((UnityAction)(() => _onReset()));

            MelonLogger.Msg("BankingAppBuilder: UI build complete.");
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

        private GameObject CreateTabButton(
            GameObject parent, string label, bool active, Action onClick)
        {
            var go = new GameObject($"Tab_{label}");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 50);

            var img = go.AddComponent<Image>();
            img.color = active
                ? new Color(0.2f, 0.4f, 0.8f)
                : new Color(0.5f, 0.5f, 0.5f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = img.color;
            cb.highlightedColor = img.color + new Color(0.1f, 0.1f, 0.1f);
            cb.pressedColor = img.color - new Color(0.1f, 0.1f, 0.1f);
            btn.colors = cb;

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var txt = tgo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;

            btn.onClick.AddListener((UnityAction)(() => onClick()));
            return go;
        }

        private GameObject CreateActionButton(
            GameObject parent, string label,
            Vector2 aMin, Vector2 aMax,
            Color col, int fs, float w, float h)
        {
            var go = new GameObject($"{label}Button");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = col;
            go.AddComponent<Button>();

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var txt = tgo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = fs;
            txt.fontStyle = FontStyle.Bold;

            return go;
        }
    }
}
