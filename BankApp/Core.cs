using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Reflection;
using System;
using Il2CppFluffyUnderware.DevTools.Extensions;
using UnityEngine.Networking;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using Il2CppSystem.Linq.Expressions;
using UnityEngine.Events;
using UnityEngine.PlayerLoop;

[assembly: MelonInfo(typeof(BankApp.Core), "BankApp", "1.0.0", "svindler, Lalisa", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

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
        private float _weeklyDepositSum = 0f;
        private float _weeklyDepositLimit = 0f;

        private Text _onlineBalanceText;
        private Text _cashBalanceText;
        private Text _selectedAmountText;
        private Text _weeklyAmountText;

        private const string TEMPLATE_APP_NAME = "Messages";
        private const string APP_OBJECT_NAME = "BankingApp";
        private const string APP_LABEL_TEXT = "Banking";

        private Button _withdrawButton;
        private Button _depositButton;

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
                MelonLogger.Msg($"No existing app named '{APP_OBJECT_NAME}' found. Looking for template app '{TEMPLATE_APP_NAME}'.");

                Transform templateApp = appsCanvas.transform.Find(TEMPLATE_APP_NAME);
                if (templateApp == null)
                {
                    MelonLogger.Error($"Cannot create app: Template '{TEMPLATE_APP_NAME}' not found inside AppsCanvas.");
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
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            GameObject onlineBalanceObj = new GameObject("OnlineBalance");
            onlineBalanceObj.transform.SetParent(container.transform, false);
            RectTransform onlineRt = onlineBalanceObj.AddComponent<RectTransform>();
            onlineRt.anchorMin = new Vector2(0f, 0.85f);
            onlineRt.anchorMax = new Vector2(1f, 0.95f);
            _onlineBalanceText = onlineBalanceObj.AddComponent<Text>();
            _onlineBalanceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _onlineBalanceText.fontSize = 22;
            _onlineBalanceText.alignment = TextAnchor.MiddleCenter;
            _onlineBalanceText.color = Color.white;

            GameObject cashBalanceObj = new GameObject("CashBalance");
            cashBalanceObj.transform.SetParent(container.transform, false);
            RectTransform cashRt = cashBalanceObj.AddComponent<RectTransform>();
            cashRt.anchorMin = new Vector2(0f, 0.75f);
            cashRt.anchorMax = new Vector2(1f, 0.85f);
            _cashBalanceText = cashBalanceObj.AddComponent<Text>();
            _cashBalanceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _cashBalanceText.fontSize = 22;
            _cashBalanceText.alignment = TextAnchor.MiddleCenter;
            _cashBalanceText.color = Color.white;

            GameObject weeklyLimitObj = new GameObject("WeeklyDepositLimit");
            weeklyLimitObj.transform.SetParent(container.transform, false);
            RectTransform limitRt = weeklyLimitObj.AddComponent<RectTransform>();
            limitRt.anchorMin = new Vector2(0f, 0.75f);
            limitRt.anchorMax = new Vector2(1f, 0.85f);
            _weeklyAmountText = weeklyLimitObj.AddComponent<Text>();
            _weeklyAmountText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _weeklyAmountText.fontSize = 22;
            _weeklyAmountText.alignment = TextAnchor.MiddleCenter;
            _weeklyAmountText.color = Color.white;

            GameObject selectedAmountObj = new GameObject("SelectedAmountText");
            selectedAmountObj.transform.SetParent(container.transform, false);
            RectTransform selectedRt = selectedAmountObj.AddComponent<RectTransform>();
            selectedRt.anchorMin = new Vector2(0f, 0.68f);
            selectedRt.anchorMax = new Vector2(1f, 0.74f);
            _selectedAmountText = selectedAmountObj.AddComponent<Text>();
            _selectedAmountText.text = "Selected: $0.00";
            _selectedAmountText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _selectedAmountText.fontSize = 22;
            _selectedAmountText.alignment = TextAnchor.MiddleCenter;
            _selectedAmountText.color = Color.yellow;

            GameObject buttonGrid = new GameObject("AmountButtons");
            buttonGrid.transform.SetParent(container.transform, false);
            RectTransform gridRt = buttonGrid.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.1f, 0.3f);
            gridRt.anchorMax = new Vector2(0.9f, 0.68f);
            GridLayoutGroup grid = buttonGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 60f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            int[] amounts = new int[] { 0, 5, 10, 20, 50, 100, 500, 1000, 5000 };
            foreach (int amt in amounts)
            {
                GameObject btnObj = new GameObject($"Amount_{amt}");
                btnObj.transform.SetParent(buttonGrid.transform, false);
                RectTransform btnRt = btnObj.AddComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(180f, 60f);

                Image img = btnObj.AddComponent<Image>();
                img.color = new Color(0.85f, 0.85f, 0.85f, 1f);

                Button btn = btnObj.AddComponent<Button>();

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
                btnText.color = Color.black;

                AddAmountButtonListener(btn, amt);
            }

            GameObject withdrawBtn = CreateActionButton(container, "Withdraw", new Vector2(0.1f, 0.1f),
                new Vector2(0.45f, 0.2f), Color.red);
            _withdrawButton = withdrawBtn.GetComponent<Button>();
            _withdrawButton.onClick.AddListener((UnityAction)(() => OnWithdrawPressed()));

            GameObject depositBtn = CreateActionButton(container, "Deposit", new Vector2(0.55f, 0.1f),
                new Vector2(0.9f, 0.2f), Color.green);
            _depositButton = depositBtn.GetComponent<Button>();
            _depositButton.onClick.AddListener((UnityAction)(() => OnDepositPressed()));

            MelonLogger.Msg("bank app working smile?.");
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

        private GameObject CreateActionButton(GameObject parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            GameObject btnObj = new GameObject($"{label}Button");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

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
            btnText.color = Color.black;

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
            if (_selectedAmount <= 0)
            {
                MelonLogger.Msg("[DEPOSIT] Cannot deposit zero :dentgE:.");
                return;
            }

            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;
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
                    MelonLogger.Msg($"[DEPOSIT] Successful deposit of ${_selectedAmount:N2}");
                    UpdateSelectedAmount(0);
                    UpdateBalanceText();
                }
                else
                {
                    MelonLogger.Msg($"[DEPOSIT] Insufficient cash smile. Cash Balance: ${moneyManager.cashBalance:N2}, Attempted: ${_selectedAmount:N2}");
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
                _selectedAmountText.text = $"Selected: ${_selectedAmount:N2}";
            }
        }

        private void UpdateBalanceText()
        {
            MoneyManager moneyManager = NetworkSingleton<MoneyManager>.Instance;
            _weeklyDepositSum = Il2CppScheduleOne.Money.ATM.WeeklyDepositSum;
            _weeklyDepositLimit = Il2CppScheduleOne.Money.ATM.WEEKLY_DEPOSIT_LIMIT;

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
                    _weeklyAmountText.text = $"Weekly Limit: ${_weeklyDepositSum}/${_weeklyDepositLimit}";
                }
            }
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
