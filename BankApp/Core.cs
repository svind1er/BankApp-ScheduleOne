using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;
using UnityEngine.Networking;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using UnityEngine.Events;

[assembly: MelonInfo(typeof(BankApp.Core), "BankApp", "1.0.0", "svindler, Lalisa", null)]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonColor(0, 255, 0, 249)]
[assembly: MelonAuthorColor(0, 234, 0, 255)]

namespace BankApp
{
    public class Core : MelonMod
    {
        private readonly string iconUrl = "https://i.imgur.com/khfAWKl.jpeg";

        private readonly string iconFileName = "BankAppIcon.png";
        private GameObject _bankingAppPanel;

        private bool _appSetupAttempted = false;
        private bool _appCreated = false;
        private bool _iconModified = false;

        private float _selectedAmount = 0f;
        private int[] amounts = new int[] { 1, 5, 10, 25, 50, 100, 500, 1000 };
        private int _weeklyDepositLimit = 10000;
        private int _maxDeposit;
        private int _weeklyDepositSum = 0;

        private Text _onlineBalanceText;
        private Text _cashBalanceText;
        private Text _selectedAmountText;
        private Text _weeklyAmountText;

        private const string TEMPLATE_APP_NAME = "Messages";
        private const string APP_OBJECT_NAME = "BankingApp";
        private const string APP_LABEL_TEXT = "Banking";

        private Button _withdrawButton;
        private Button _depositButton;
        private Button _resetButton;
        private Button _maxButton;
        private Button _confirmButton;

        private enum TabType
        { Withdraw, Deposit }

        private TabType _currentTab = TabType.Withdraw;

        private GameObject _withdrawTab;
        private GameObject _depositTab;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("BankingApp: Initializing mod...");

            MelonLoader.MelonCoroutines.Start(EnsureIconFileExists());

            ClassInjector.RegisterTypeInIl2Cpp<BankingAppComponent>();
            MelonLogger.Msg("BankingApp: Successfully registered BankingAppComponent type");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName.Equals("Main", StringComparison.OrdinalIgnoreCase))
            {
                MelonLogger.Msg("Main scene loaded. Starting BankingAppComponent.");
                _appSetupAttempted = false;
                _appCreated = false;
                _iconModified = false;
                if (GameObject.Find("BankingAppComponentHolder") == null)
                {
                    GameObject holder = new GameObject("BankingAppComponentHolder");
                    UnityEngine.Object.DontDestroyOnLoad(holder);
                    holder.AddComponent<BankingAppComponent>();
                }
            }
        }

        public override void OnUpdate()
        {
            if (!_appSetupAttempted &&
                GameObject.Find(
                    "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas") != null)
            {
                MelonLogger.Msg("AppsCanvas found, creating app and icon.");
                _appSetupAttempted = true;
                CreateOrEnsureAppAndIcon();
            }
            UpdateBalanceText();
        }

        private IEnumerator EnsureIconFileExists()
        {
            string modFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modFolderPath, iconFileName);
            if (!File.Exists(filePath))
            {
                MelonLogger.Msg("Icon file not found, downloading icon from: " + iconUrl);
                UnityWebRequest uwr = UnityWebRequest.Get(iconUrl);
                yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (uwr.result != UnityWebRequest.Result.Success)
#else
                if (uwr.isNetworkError || uwr.isHttpError)
#endif
                {
                    MelonLogger.Error("Error downloading icon: " + uwr.error);
                    uwr.Dispose();
                    yield break;
                }

                File.WriteAllBytes(filePath, uwr.downloadHandler.data);
                MelonLogger.Msg("Icon downloaded and saved to: " + filePath);
                uwr.Dispose();
            }
            else
            {
                MelonLogger.Msg("Icon file already exists at: " + filePath);
            }
        }

        private void CreateOrEnsureAppAndIcon()
        {
            MelonLogger.Msg("Entering CreateOrEnsureAppAndIcon.");

            if (_appCreated && _iconModified)
            {
                MelonLogger.Msg("App already created and icon modified. Exiting method.");
                return;
            }

            GameObject appsCanvas = GameObject.Find(
                "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas");
            if (appsCanvas == null)
            {
                MelonLogger.Error("AppsCanvas not found.");
                return;
            }

            MelonLogger.Msg("AppsCanvas found.");

            Transform existingApp = appsCanvas.transform.Find(APP_OBJECT_NAME);
            if (existingApp != null)
            {
                MelonLogger.Msg($"Existing app '{APP_OBJECT_NAME}' found. Setting up app panel.");
                _bankingAppPanel = existingApp.gameObject;
                EnsureAppPanelIsSetup(_bankingAppPanel);
            }
            else
            {
                MelonLogger.Msg(
                    $"No existing app named '{APP_OBJECT_NAME}' found. Looking for template app '{TEMPLATE_APP_NAME}'.");

                Transform templateApp = appsCanvas.transform.Find(TEMPLATE_APP_NAME);
                if (templateApp == null)
                {
                    MelonLogger.Error(
                        $"Cannot create app: Template '{TEMPLATE_APP_NAME}' not found inside AppsCanvas.");
                    return;
                }

                MelonLogger.Msg($"Template app '{TEMPLATE_APP_NAME}' found. Cloning template.");

                _bankingAppPanel = UnityEngine.Object.Instantiate(templateApp.gameObject, appsCanvas.transform);
                _bankingAppPanel.name = APP_OBJECT_NAME;
                MelonLogger.Msg($"Template cloned. New app panel name set to '{APP_OBJECT_NAME}'.");

                GameObject container = _bankingAppPanel.transform.Find("Container")?.gameObject;
                if (container != null)
                {
                    MelonLogger.Msg("Container found in cloned app panel. Clearing UI elements.");
                    ClearContainerUI(container);

                    MelonLogger.Msg("Building Banking App UI.");
                    BuildBankingAppUI(container);
                }
                else
                {
                    MelonLogger.Warning($"Could not find 'Container' in new '{_bankingAppPanel.name}'.");
                }

                GameObject appIconList = GameObject.Find(
                    "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/HomeScreen/AppIcons");
                if (appIconList == null)
                {
                    MelonLogger.Error("AppIcons list not found in HomeScreen.");
                }
                else
                {
                    MelonLogger.Msg("AppIcons list found. Attempting to locate the app icon at the expected index.");
                    Transform appIcon = appIconList.transform.childCount > 7 ? appIconList.transform.GetChild(7) : null;
                    GameObject bankinAppIcon = appIcon?.gameObject;

                    if (bankinAppIcon != null)
                    {
                        MelonLogger.Msg("App Icon found. Starting coroutine to update app icon label and sprite.");
                        MelonLoader.MelonCoroutines.Start(UpdateAppIconLabelAndSpriteCoroutine(bankinAppIcon));
                    }
                    else
                    {
                        MelonLogger.Error("App Icon not found at expected index.");
                    }
                }

                AttachButtonClickEvent(_bankingAppPanel);
                MelonLogger.Msg("Button click event attached to the app panel.");

                _appCreated = true;
                MelonLogger.Msg("App creation flag set to true.");
            }

            MelonLogger.Msg("Exiting CreateOrEnsureAppAndIcon.");
        }

        private IEnumerator UpdateAppIconLabelAndSpriteCoroutine(GameObject bankinAppIcon)
        {
            Transform labelTransform = null;
            for (int i = 0; i < 10 && labelTransform == null; i++)
            {
                labelTransform = bankinAppIcon.transform.Find("Label");
                yield return null;
            }

            if (labelTransform != null)
            {
                Text labelText = labelTransform.GetComponent<Text>();
                if (labelText != null)
                {
                    labelText.text = APP_LABEL_TEXT;
                    MelonLogger.Msg($"Label text set to '{APP_LABEL_TEXT}'");
                }
                else
                {
                    MelonLogger.Error("Text-Komponente im Label nicht gefunden.");
                }
            }
            else
            {
                MelonLogger.Error("Label transform wurde auch nach Verzögerung nicht gefunden.");
            }

            Transform iconMask = bankinAppIcon.transform.Find("Mask");
            if (iconMask == null)
            {
                MelonLogger.Error("Icon Mask not found in banking app icon.");
                yield break;
            }

            Transform iconMaskImage = iconMask.Find("Image");
            Image iconImage = iconMaskImage.GetComponent<Image>();
            if (iconImage == null)
            {
                MelonLogger.Error("Image component not found in Icon Mask.");
                yield break;
            }

            yield return SetActiveIconSprite(iconImage);

            bankinAppIcon =
                GameObject.Find(
                    "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/HomeScreen/AppIcons");
            GameObject appIconID = bankinAppIcon.transform.GetChild(7).gameObject;
            appIconID.name = APP_OBJECT_NAME;

            _iconModified = true;
        }

        private IEnumerator SetActiveIconSprite(Image targetImage)
        {
            string modFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modFolderPath, iconFileName);
            if (!File.Exists(filePath))
            {
                MelonLogger.Error("Icon file not found in SetActiveIconSprite!");
                yield break;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D loadedTex = CreatePersistentTexture(fileData);
            if (loadedTex != null)
            {
                Sprite sprite = Sprite.Create(loadedTex, new Rect(0, 0, loadedTex.width, loadedTex.height),
                    new Vector2(0.5f, 0.5f));
                sprite.name = "BankAppIconSprite";
                targetImage.sprite = sprite;
                targetImage.preserveAspect = true;
                targetImage.SetAllDirty();
                MelonLogger.Msg("Active sprite applied successfully to icon image!");
            }
            else
            {
                MelonLogger.Error("Failed to create texture from image data in SetActiveIconSprite!");
            }

            yield break;
        }

        private void EnsureAppPanelIsSetup(GameObject appPanel)
        {
            if (_appCreated)
                return;

            GameObject container = appPanel?.transform.Find("Container")?.gameObject;
            if (container != null)
            {
                MelonLogger.Msg($"Rebuilding UI in existing panel '{appPanel.name}'...");
                ClearContainerUI(container);
                BuildBankingAppUI(container);
            }

            AttachButtonClickEvent(appPanel);
            _appCreated = true;
        }

        private void ClearContainerUI(GameObject container)
        {
            if (container == null)
                return;

            for (int i = container.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = container.transform.GetChild(i);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private void SetActiveTab(TabType tab)
        {
            _currentTab = tab;

            Image withdrawImg = _withdrawTab.GetComponent<Image>();
            Image depositImg = _depositTab.GetComponent<Image>();

            withdrawImg.color = (_currentTab == TabType.Withdraw) ?
                new Color(0.2f, 0.4f, 0.8f) : new Color(0.5f, 0.5f, 0.5f);
            depositImg.color = (_currentTab == TabType.Deposit) ?
                new Color(0.2f, 0.4f, 0.8f) : new Color(0.5f, 0.5f, 0.5f);

            Button withdrawBtn = _withdrawTab.GetComponent<Button>();
            Button depositBtn = _depositTab.GetComponent<Button>();

            ColorBlock withdrawColors = withdrawBtn.colors;
            ColorBlock depositColors = depositBtn.colors;

            withdrawColors.normalColor = withdrawImg.color;
            depositColors.normalColor = depositImg.color;

            withdrawBtn.colors = withdrawColors;
            depositBtn.colors = depositColors;

            UpdateUIForCurrentTab();

            UpdateConfirmButtonLayout();
        }

        private void UpdateUIForCurrentTab()
        {
            if (_currentTab == TabType.Withdraw)
            {
                _selectedAmountText.text = $"Withdraw: ${_selectedAmount:N2}";
            }
            else
            {
                _selectedAmountText.text = $"Deposit: ${_selectedAmount:N2}";
            }
            UpdateSelectedAmount(0);
        }

        private void BuildBankingAppUI(GameObject container)
        {
            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;

            if (container == null)
            {
                MelonLogger.Error("Cannot build UI: Container is null.");
                return;
            }

            GameObject background = new GameObject("Background");
            background.transform.SetParent(container.transform, false);
            RectTransform bgRt = background.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            Texture2D gradientTex = new Texture2D(1, 256);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;

                Color topColor = new Color(0.18f, 0.48f, 0.95f);
                Color bottomColor = new Color(0.18f, 0.31f, 0.51f);

                Color gradientColor = Color.Lerp(bottomColor, topColor, t);
                gradientTex.SetPixel(0, y, gradientColor);
            }
            gradientTex.Apply();

            Sprite gradientSprite = Sprite.Create(
                gradientTex,
                new Rect(0, 0, gradientTex.width, gradientTex.height),
                new Vector2(0.5f, 0.5f)
            );

            Image bgImage = background.AddComponent<Image>();
            bgImage.sprite = gradientSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = false;

            GameObject infoPanel = new GameObject("InfoPanel");
            infoPanel.transform.SetParent(container.transform, false);
            VerticalLayoutGroup infoLayoutGroup = infoPanel.AddComponent<VerticalLayoutGroup>();
            RectTransform infoRt = infoPanel.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0.05f, 0.75f);
            infoRt.anchorMax = new Vector2(0.95f, 0.98f);
            infoRt.offsetMin = Vector2.zero;
            infoRt.offsetMax = Vector2.zero;
            infoLayoutGroup.spacing = 5;
            infoLayoutGroup.childAlignment = TextAnchor.MiddleCenter;

            _weeklyAmountText = CreateText("WeeklyLimit", infoPanel.transform, 34, Color.white);
            _onlineBalanceText = CreateText("OnlineBalance", infoPanel.transform, 33, Color.cyan);
            _cashBalanceText = CreateText("CashBalance", infoPanel.transform, 33, Color.green);
            _selectedAmountText = CreateText("SelectedAmount", infoPanel.transform, 33, Color.yellow);
            _selectedAmountText.text = "Deposit: $0.00";

            GameObject tabPanel = new GameObject("TabPanel");
            tabPanel.transform.SetParent(container.transform, false);
            RectTransform tabRt = tabPanel.AddComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0.05f, 0.8f);
            tabRt.anchorMax = new Vector2(0.95f, 0.9f);
            tabRt.offsetMin = Vector2.zero;
            tabRt.offsetMax = Vector2.zero;

            tabPanel.transform.localPosition = new Vector3(0f, 200f, 0f);

            HorizontalLayoutGroup tabLayout = tabPanel.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 15;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childForceExpandWidth = true;

            GameObject depositTab = CreateTabButton(tabPanel, "Deposit", true);
            _depositTab = depositTab;
            GameObject withdrawTab = CreateTabButton(tabPanel, "Withdraw", false);
            _withdrawTab = withdrawTab;

            GameObject buttonGrid = new GameObject("AmountButtons");
            buttonGrid.transform.SetParent(container.transform, false);
            RectTransform gridRt = buttonGrid.AddComponent<RectTransform>();
            buttonGrid.transform.localPosition = new Vector2(249f, -400);
            buttonGrid.transform.localScale = new Vector2(1.4f, 1.4f);
            gridRt.anchorMin = new Vector2(0.05f, 0.25f);
            gridRt.anchorMax = new Vector2(0.95f, 0.74f);
            GridLayoutGroup grid = buttonGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160f, 50f);
            grid.spacing = new Vector2(15f, 15f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            foreach (int amt in amounts)
            {
                GameObject btnObj = new GameObject($"Amount_{amt}");
                btnObj.transform.SetParent(buttonGrid.transform, false);
                RectTransform btnRt = btnObj.AddComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(160f, 50f);

                Image img = btnObj.AddComponent<Image>();
                img.color = new Color(0.8f, 0.85f, 0.9f, 1f);

                Button btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = img.color;
                cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
                cb.pressedColor = new Color(.6f, .7f, .8f, 1f);
                cb.disabledColor = Color.gray;
                btn.colors = cb;

                GameObject textGO = new GameObject("Text");
                textGO.transform.SetParent(btnObj.transform, false);
                RectTransform textRt = textGO.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;

                Text btnText = textGO.AddComponent<Text>();
                btnText.text = $"${amt}";
                btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                btnText.fontSize = 20;
                AddAmountButtonListener(btn, amt);
            }

            GameObject actionBtn = CreateActionButton(container, "Confirm", new Vector2(0.3f, 0.05f),
                new Vector2(0.7f, 0.15f), new Color(0.3f, 0.6f, 0.3f), 35, 150f, 1f);
            _confirmButton = actionBtn.GetComponent<Button>();
            _confirmButton.onClick.AddListener((UnityAction)(() => OnConfirmPressed()));

            UpdateConfirmButtonLayout();

            GameObject maxBtn = CreateActionButton(container, "MAX", new Vector2(0.3f, 0.1f),
                new Vector2(0.7f, 0.2f), new Color(.6f, .7f, .8f, 1f), 35, -37f, -49.6f);
            _maxButton = maxBtn.GetComponent<Button>();
            _maxButton.transform.localPosition = new Vector2(124.269f, -320f);
            _maxButton.onClick.RemoveAllListeners();
            _maxButton.onClick.AddListener((UnityAction)(() =>
            {
                MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;
                if (moneyManager == null) return;

                if (_currentTab == TabType.Withdraw)
                {
                    SetSelectedAmount((int)moneyManager.onlineBalance);
                }
                else
                {
                    int depositMax = (int)Mathf.Min(moneyManager.cashBalance, DepositMax());
                    SetSelectedAmount(depositMax);
                }
            }));

            GameObject resetbtn = CreateActionButton(container, "Clear", new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.2f), new Color(.6f, .7f, .8f, 1f), 35, -37f, -49.6f);
            _resetButton = resetbtn.GetComponent<Button>();
            _resetButton.transform.localPosition = new Vector2(-122.169f, -320f);
            _resetButton.onClick.AddListener((UnityAction)(() => UpdateSelectedAmount(0)));

            MelonLogger.Msg("bank app working smile?");
        }

        private void UpdateConfirmButtonLayout()
        {
            if (_confirmButton == null)
                return;

            Text btnText = _confirmButton.GetComponentInChildren<Text>();
            if (_currentTab == TabType.Withdraw)
            {
                btnText.text = "Withdraw";
                _confirmButton.GetComponent<Image>().color = new Color(0.8f, 0.3f, 0.3f, 1f);
                _confirmButton.interactable = true;
            }
            else
            {
                btnText.text = "Deposit";
                if (DepositMax() <= 0)
                {
                    _confirmButton.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 1f);
                    _confirmButton.interactable = false;
                }
                else
                {
                    _confirmButton.GetComponent<Image>().color = new Color(0.3f, 0.8f, 0.3f, 1f);
                    _confirmButton.interactable = true;
                }
            }
        }

        private void OnConfirmPressed()
        {
            if (_currentTab == TabType.Withdraw)
                OnWithdrawPressed();
            else
                OnDepositPressed();
        }

        private void AttachButtonClickEvent(GameObject appPanelGameObject)
        {
            if (appPanelGameObject == null)
                return;

            Button button = appPanelGameObject.GetComponent<Button>() ?? appPanelGameObject.AddComponent<Button>();

            if (button.targetGraphic == null)
            {
                Image image = appPanelGameObject.GetComponent<Image>() ??
                              appPanelGameObject.GetComponentInChildren<Image>(true);
                if (image != null)
                    button.targetGraphic = image;
                else
                    MelonLogger.Warning($"Could not find target graphic for Button on '{appPanelGameObject.name}'.");
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(new Action(() => { ShowApp(appPanelGameObject); }));
        }

        private void ShowApp(GameObject appGameObject)
        {
            if (appGameObject != null)
            {
                appGameObject.SetActive(true);
                appGameObject.transform.SetAsLastSibling();
                MelonLogger.Msg("Banking app displayed.");
            }
            else
            {
                MelonLogger.Error("ShowApp: Called with null appGameObject!");
            }
        }

        private Texture2D CreatePersistentTexture(byte[] imageData)
        {
            try
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(imageData))
                {
                    tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    return tex;
                }
                else
                {
                    MelonLogger.Msg("Failed to load image data into texture!");
                    return null;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error creating texture: " + ex.Message);
                return null;
            }
        }

        private GameObject CreateTabButton(GameObject parent, string label, bool isActive)
        {
            GameObject tabObj = new GameObject($"Tab_{label}");
            tabObj.transform.SetParent(parent.transform, false);
            RectTransform rt = tabObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180f, 50f);

            Image img = tabObj.AddComponent<Image>();
            img.color = isActive ? new Color(0.2f, 0.4f, 0.8f) : new Color(0.5f, 0.5f, 0.5f);

            Button btn = tabObj.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.normalColor = img.color;
            cb.highlightedColor = new Color(img.color.r + 0.1f, img.color.g + 0.1f, img.color.b + 0.1f);
            cb.pressedColor = new Color(img.color.r - 0.1f, img.color.g - 0.1f, img.color.b - 0.1f);
            btn.colors = cb;

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(tabObj.transform, false);
            RectTransform textRt = textGO.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text btnText = textGO.AddComponent<Text>();
            btnText.text = label;
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.fontSize = 24;
            btnText.fontStyle = FontStyle.Bold;

            if (label == "Withdraw")
                btn.onClick.AddListener((UnityAction)(() => SetActiveTab(TabType.Withdraw)));
            else
                btn.onClick.AddListener((UnityAction)(() => SetActiveTab(TabType.Deposit)));

            return tabObj;
        }

        private GameObject CreateActionButton(GameObject parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize, float buttonLength, float buttonWidth)
        {
            GameObject btnObj = new GameObject($"{label}Button");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            rt.sizeDelta = new Vector2(buttonLength, buttonWidth);

            Image img = btnObj.AddComponent<Image>();
            img.color = color;

            Button btn = btnObj.AddComponent<Button>();

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textGO.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text btnText = textGO.AddComponent<Text>();
            btnText.text = label;
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.fontSize = fontSize;
            btnText.fontStyle = FontStyle.Bold;

            return btnObj;
        }

        private void AddAmountButtonListener(Button button, int amount)
        {
            if (button == null) return;

            void AddListener(int amt)
            {
                button.onClick.AddListener((UnityAction)(() => OnSelectAmount(amt)));
            }

            AddListener(amount);
        }

        private void OnSelectAmount(int amount)
        {
            UpdateSelectedAmount(amount);
        }

        private void OnWithdrawPressed()
        {
            if (_selectedAmount <= 0)
            {
                MelonLogger.Msg("[WITHDRAW] Cannot withdraw zero :dentge:");
                return;
            }

            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;
            if (moneyManager != null)
            {
                if (moneyManager.onlineBalance >= _selectedAmount)
                {
                    moneyManager.CreateOnlineTransaction(
                        "ATM Withdrawal",
                        -_selectedAmount,
                        1f,
                        "Funds withdrawn from ATM"
                    );

                    moneyManager.ChangeCashBalance(_selectedAmount, true, true);
                    MelonLogger.Msg($"[WITHDRAW] Successful withdrawal for ${_selectedAmount:N2}");

                    UpdateSelectedAmount(0);
                    UpdateBalanceText();
                }
                else
                {
                    MelonLogger.Msg($"[WITHDRAW] Insufficient funds smile. Online Balance: ${moneyManager.onlineBalance:N2}, Attempted: ${_selectedAmount:N2}");
                }
            }
            else
            {
                MelonLogger.Error("[WITHDRAW] MoneyManager instance not found!");
            }
        }

        private void OnDepositPressed()
        {
            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;

            if (_selectedAmount <= 0)
            {
                MelonLogger.Msg("[DEPOSIT] Cannot deposit zero :dentgE:.");
                return;
            }

            if (moneyManager != null)
            {
                if (moneyManager.cashBalance >= _selectedAmount)
                {
                    moneyManager.CreateOnlineTransaction(
                        "ATM Deposit",
                        _selectedAmount,
                        1f,
                        "Funds deposited to ATM"
                    );
                    moneyManager.ChangeCashBalance(-_selectedAmount, true, true);
                    ATM.WeeklyDepositSum += _selectedAmount;
                    MelonLogger.Msg($"[DEPOSIT] Successful deposit of ${_selectedAmount:N2}");
                    UpdateSelectedAmount(0);
                    UpdateBalanceText();
                }
                else
                {
                    MelonLogger.Msg(
                        $"[DEPOSIT] Insufficient cash smile. Cash Balance: ${moneyManager.cashBalance:N2}, Attempted: ${_selectedAmount:N2}");
                }
            }
            else
            {
                MelonLogger.Error("[DEPOSIT] MoneyManager instance not found!");
            }
        }

        private void UpdateSelectedAmount(int amount)
        {
            if (amount == 0)
            {
                _selectedAmount = 0;
            }
            else
            {
                _selectedAmount += amount;
            }

            if (_selectedAmountText != null)
            {
                if (_currentTab == TabType.Withdraw)
                    _selectedAmountText.text = $"Withdraw: ${_selectedAmount:N2}";
                else
                    _selectedAmountText.text = $"Deposit: ${_selectedAmount:N2}";
            }
        }

        private void SetSelectedAmount(int amount)
        {
            _selectedAmount = amount;
            if (_selectedAmountText != null)
            {
                if (_currentTab == TabType.Withdraw)
                    _selectedAmountText.text = $"Withdraw: ${_selectedAmount:N2}";
                else
                    _selectedAmountText.text = $"Deposit: ${_selectedAmount:N2}";
            }
        }

        private void UpdateBalanceText()
        {
            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;

            if (moneyManager != null)
            {
                if (_onlineBalanceText != null)
                {
                    _onlineBalanceText.text = $"Online Balance: ${moneyManager.onlineBalance:N2}";
                }

                if (_cashBalanceText != null)
                {
                    _cashBalanceText.text = $"Cash Balance: ${moneyManager.cashBalance:N2}";
                }
                if (_weeklyAmountText != null)
                {
                    _weeklyAmountText.text = $"Weekly Limit: ${ATM.WeeklyDepositSum}/${_weeklyDepositLimit}";
                }
            }

            if (_currentTab == TabType.Deposit)
                UpdateConfirmButtonLayout();
        }

        private int DepositMax()
        {
            return _weeklyDepositLimit - (int)ATM.WeeklyDepositSum;
        }

        private Text CreateText(string name, Transform parent, int fontSize, Color color)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
        }
    }

    [RegisterTypeInIl2Cpp]
    public class BankingAppComponent : MonoBehaviour
    {
        public BankingAppComponent(IntPtr ptr) : base(ptr)
        {
        }

        public void Start()
        {
            MelonLogger.Msg("BankingAppComponent: Starting");
        }
    }
}