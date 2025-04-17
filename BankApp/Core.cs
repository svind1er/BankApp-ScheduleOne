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
        private readonly string iconUrl = "https://i.imgur.com/hsVNqRx.png";

        private readonly string iconFileName = "BankAppIcon.png";
        private GameObject _bankingAppPanel;

        private bool _appSetupAttempted = false;
        private bool _appCreated = false;
        private bool _iconModified = false;

        private float _selectedAmount = 0f;
        private int[] amounts = new int[] { 1, 5, 10, 25, 50, 100, 500, 1000 };
        private int _weeklyDepositLimit = (int)ATM.WEEKLY_DEPOSIT_LIMIT;

        private Text _onlineBalanceText;
        private Text _cashBalanceText;
        private Text _selectedAmountText;
        private Text _weeklyAmountText;
        private RectTransform _weeklyProgressFill;

        private const string TEMPLATE_APP_NAME = "Messages";
        private const string APP_OBJECT_NAME = "BankingApp";
        private const string APP_LABEL_TEXT = "Bank";

        private Button _confirmButton;
        private Button _maxButton;
        private Button _resetButton;
        private GameObject _withdrawTab;
        private GameObject _depositTab;

        private enum TabType { Withdraw, Deposit }
        private TabType _currentTab = TabType.Deposit;

        private float _lastProgressRatio = 0f;

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
                MelonLogger.Msg("Main scene loaded. Setting up BankingAppComponent.");
                _appSetupAttempted = _appCreated = _iconModified = false;

                if (GameObject.Find("BankingAppComponentHolder") == null)
                {
                    var holder = new GameObject("BankingAppComponentHolder");
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
            string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modFolder, iconFileName);

            if (!File.Exists(filePath))
            {
                MelonLogger.Msg("Downloading icon from " + iconUrl);
                var uwr = UnityWebRequest.Get(iconUrl);
                yield return uwr.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
                if (uwr.result != UnityWebRequest.Result.Success)
#else
                if (uwr.isNetworkError || uwr.isHttpError)
#endif
                {
                    MelonLogger.Error("Icon download failed: " + uwr.error);
                    uwr.Dispose();
                    yield break;
                }
                File.WriteAllBytes(filePath, uwr.downloadHandler.data);
                MelonLogger.Msg("Icon saved to " + filePath);
                uwr.Dispose();
            }
            else
            {
                MelonLogger.Msg("Icon already present at " + filePath);
            }
        }

        private void CreateOrEnsureAppAndIcon()
        {
            MelonLogger.Msg("Enter CreateOrEnsureAppAndIcon");

            if (_appCreated && _iconModified)
            {
                MelonLogger.Msg("Already created & icon set—skipping.");
                return;
            }

            var appsCanvas = GameObject.Find(
                "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas");
            if (appsCanvas == null)
            {
                MelonLogger.Error("AppsCanvas not found.");
                return;
            }

            var existing = appsCanvas.transform.Find(APP_OBJECT_NAME);
            if (existing != null)
            {
                MelonLogger.Msg("Found existing BankingApp—rebuilding UI if needed.");
                _bankingAppPanel = existing.gameObject;
                EnsureAppPanelIsSetup(_bankingAppPanel);
            }
            else
            {
                MelonLogger.Msg("Cloning template '" + TEMPLATE_APP_NAME + "'...");
                var template = appsCanvas.transform.Find(TEMPLATE_APP_NAME);
                if (template == null)
                {
                    MelonLogger.Error($"Template '{TEMPLATE_APP_NAME}' missing.");
                    return;
                }

                _bankingAppPanel = UnityEngine.Object.Instantiate(template.gameObject, appsCanvas.transform);
                _bankingAppPanel.name = APP_OBJECT_NAME;

                var container = _bankingAppPanel.transform.Find("Container")?.gameObject;
                if (container != null)
                {
                    var builder = new BankingAppBuilder(
                        container,
                        amt => OnSelectAmount(amt),
                        () => OnConfirmPressed(),
                        () =>
                        {
                            var mm = NetworkSingleton<MoneyManager>.Instance;
                            if (mm == null) return;
                            if (_currentTab == TabType.Withdraw)
                                SetSelectedAmount((int)mm.onlineBalance);
                            else
                                SetSelectedAmount((int)Mathf.Min(mm.cashBalance, DepositMax()));
                        },
                        () => UpdateSelectedAmount(0),
                        () => SetActiveTab(TabType.Deposit),
                        () => SetActiveTab(TabType.Withdraw)
                    );
                    builder.BuildUI();

                    _weeklyAmountText = builder.WeeklyAmountText;
                    _weeklyProgressFill = builder.WeeklyProgressFill;
                    if (_weeklyProgressFill != null)
                        _lastProgressRatio = _weeklyProgressFill.anchorMax.x;

                    _onlineBalanceText = builder.OnlineBalanceText;
                    _cashBalanceText = builder.CashBalanceText;
                    _selectedAmountText = builder.SelectedAmountText;
                    _confirmButton = builder.ConfirmButton;
                    _maxButton = builder.MaxButton;
                    _resetButton = builder.ResetButton;
                    _depositTab = builder.DepositTab;
                    _withdrawTab = builder.WithdrawTab;
                }
                else
                {
                    MelonLogger.Warning("No 'Container' under cloned app panel.");
                }

                var iconList = GameObject.Find(
                    "Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/HomeScreen/AppIcons");
                if (iconList != null && iconList.transform.childCount > 7)
                {
                    var iconGO = iconList.transform.GetChild(7).gameObject;
                    MelonLoader.MelonCoroutines.Start(UpdateAppIconLabelAndSpriteCoroutine(iconGO));
                }
                else
                {
                    MelonLogger.Error("Can't find BankingApp icon slot.");
                }

                AttachButtonClickEvent(_bankingAppPanel);
                _appCreated = true;
                MelonLogger.Msg("BankingApp created.");
            }

            MelonLogger.Msg("Exit CreateOrEnsureAppAndIcon");
        }

        private IEnumerator UpdateAppIconLabelAndSpriteCoroutine(GameObject iconGO)
        {
            Transform lbl = null;
            for (int i = 0; i < 10 && lbl == null; i++)
            {
                lbl = iconGO.transform.Find("Label");
                yield return null;
            }
            if (lbl != null && lbl.GetComponent<Text>() is Text t) t.text = APP_LABEL_TEXT;
            else MelonLogger.Error("Icon label text component not found.");

            var mask = iconGO.transform.Find("Mask")?.Find("Image");
            if (mask == null || !(mask.GetComponent<Image>() is Image img))
            {
                MelonLogger.Error("Icon Mask/Image missing.");
                yield break;
            }
            yield return SetActiveIconSprite(img);

            iconGO.name = APP_OBJECT_NAME;

            _iconModified = true;
        }

        private IEnumerator SetActiveIconSprite(Image targetImage)
        {
            string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modFolder, iconFileName);
            if (!File.Exists(filePath))
            {
                MelonLogger.Error("Icon file not found at " + filePath);
                yield break;
            }

            var data = File.ReadAllBytes(filePath);
            var tex = CreatePersistentTexture(data);
            if (tex != null)
            {
                var spr = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f)
                );
                targetImage.sprite = spr;
                targetImage.preserveAspect = true;
                targetImage.SetAllDirty();
            }
            else
            {
                MelonLogger.Error("Failed to load icon texture.");
            }
            yield break;
        }

        private void EnsureAppPanelIsSetup(GameObject appPanel)
        {
            if (_appCreated) return;

            var container = appPanel.transform.Find("Container")?.gameObject;
            if (container != null)
            {
                var builder = new BankingAppBuilder(
                    container,
                    amt => OnSelectAmount(amt),
                    () => OnConfirmPressed(),
                    () =>
                    {
                        var mm = NetworkSingleton<MoneyManager>.Instance;
                        if (mm == null) return;
                        if (_currentTab == TabType.Withdraw)
                            SetSelectedAmount((int)mm.onlineBalance);
                        else
                            SetSelectedAmount((int)Mathf.Min(mm.cashBalance, DepositMax()));
                    },
                    () => UpdateSelectedAmount(0),
                    () => SetActiveTab(TabType.Deposit),
                    () => SetActiveTab(TabType.Withdraw)
                );
                builder.BuildUI();

                _weeklyAmountText = builder.WeeklyAmountText;
                _weeklyProgressFill = builder.WeeklyProgressFill;
                if (_weeklyProgressFill != null)
                    _lastProgressRatio = _weeklyProgressFill.anchorMax.x;

                _onlineBalanceText = builder.OnlineBalanceText;
                _cashBalanceText = builder.CashBalanceText;
                _selectedAmountText = builder.SelectedAmountText;
                _confirmButton = builder.ConfirmButton;
                _maxButton = builder.MaxButton;
                _resetButton = builder.ResetButton;
                _depositTab = builder.DepositTab;
                _withdrawTab = builder.WithdrawTab;
            }

            AttachButtonClickEvent(appPanel);
            _appCreated = true;
        }

        private void SetActiveTab(TabType tab)
        {
            _currentTab = tab;
            var wImg = _withdrawTab.GetComponent<Image>();
            var dImg = _depositTab.GetComponent<Image>();
            wImg.color = (tab == TabType.Withdraw)
                ? new Color32(8, 145, 178, 255)
                : new Color32(31, 41, 55, 255);

            dImg.color = (tab == TabType.Deposit)
                ? new Color32(8, 145, 178, 255)
                : new Color32(31, 41, 55, 255);

            var wBtn = _withdrawTab.GetComponent<Button>();
            var dBtn = _depositTab.GetComponent<Button>();

            var wc = wBtn.colors;
            wc.normalColor = wImg.color;
            wc.highlightedColor = tab == TabType.Withdraw
                ? new Color32(14, 116, 144, 255)
                : new Color32(55, 65, 81, 255);
            wc.pressedColor = tab == TabType.Withdraw
                ? new Color32(7, 89, 110, 255)
                : new Color32(25, 33, 44, 255);
            wc.selectedColor = wc.normalColor;
            wBtn.colors = wc;

            var dc = dBtn.colors;
            dc.normalColor = dImg.color;
            dc.highlightedColor = tab == TabType.Deposit
                ? new Color32(14, 116, 144, 255)
                : new Color32(55, 65, 81, 255);
            dc.pressedColor = tab == TabType.Deposit
                ? new Color32(7, 89, 110, 255)
                : new Color32(25, 33, 44, 255);
            dc.selectedColor = dc.normalColor;
            dBtn.colors = dc;

            wBtn.OnDeselect(null);
            dBtn.OnDeselect(null);

            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            UpdateUIForCurrentTab();
            UpdateConfirmButtonLayout();
        }

        private void UpdateUIForCurrentTab()
        {
            if (_currentTab == TabType.Withdraw)
                _selectedAmountText.text = $"${_selectedAmount:N0}";
            else
                _selectedAmountText.text = $"${_selectedAmount:N0}";
            UpdateSelectedAmount(0);
        }

        private void UpdateConfirmButtonLayout()
        {
            if (_confirmButton == null) return;

            var txt = _confirmButton.GetComponentInChildren<Text>();
            if (_currentTab == TabType.Withdraw)
            {
                txt.text = "Withdraw";
                _confirmButton.GetComponent<Image>().color = new Color32(204, 77, 77, 255);

                var colors = _confirmButton.colors;
                colors.normalColor = new Color32(204, 77, 77, 255);
                colors.highlightedColor = new Color32(184, 57, 57, 255);     
                colors.pressedColor = new Color32(164, 37, 37, 255);        
                colors.selectedColor = colors.normalColor;
                _confirmButton.colors = colors;

                _confirmButton.interactable = true;
            }
            else
            {
                txt.text = "Deposit";
                if (DepositMax() <= 0)
                {
                    _confirmButton.GetComponent<Image>().color = Color.gray;
                    _confirmButton.interactable = false;
                }
                else
                {
                    _confirmButton.GetComponent<Image>().color = new Color32(5, 150, 105, 255);  

                    var colors = _confirmButton.colors;
                    colors.normalColor = new Color32(5, 150, 105, 255);   
                    colors.highlightedColor = new Color32(4, 120, 87, 255);  
                    colors.pressedColor = new Color32(3, 89, 64, 255);       
                    colors.selectedColor = colors.normalColor;
                    _confirmButton.colors = colors;

                    _confirmButton.interactable = true;
                }
            }
        }

        private void OnConfirmPressed()
        {
            if (_currentTab == TabType.Withdraw) OnWithdrawPressed();
            else OnDepositPressed();
        }

        private void AttachButtonClickEvent(GameObject panel)
        {
            if (panel == null) return;
            var btn = panel.GetComponent<Button>() ?? panel.AddComponent<Button>();
            if (btn.targetGraphic == null)
            {
                var img = panel.GetComponent<Image>()
                          ?? panel.GetComponentInChildren<Image>(true);
                if (img != null) btn.targetGraphic = img;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener((UnityAction)(() =>
            {
                panel.SetActive(true);
                panel.transform.SetAsLastSibling();
                MelonLogger.Msg("Bank app opened.");
            }));
        }

        private Texture2D CreatePersistentTexture(byte[] data)
        {
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data))
                {
                    tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    return tex;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg("Texture error: " + e);
            }
            return null;
        }

        private void OnSelectAmount(int amt) => UpdateSelectedAmount(amt);

        private void OnWithdrawPressed()
        {
            if (_selectedAmount <= 0f)
            {
                MelonLogger.Msg("[WITHDRAW] Zero not allowed.");
                return;
            }
            var mm = NetworkSingleton<MoneyManager>.Instance;
            if (mm == null) { MelonLogger.Error("MoneyManager missing"); return; }
            if (mm.onlineBalance >= _selectedAmount)
            {
                mm.CreateOnlineTransaction("ATM Withdrawal", -_selectedAmount, 1f, "ATM out");
                mm.ChangeCashBalance(_selectedAmount, true, true);
                MelonLogger.Msg($"[WITHDRAW] ${_selectedAmount:N0} done");
                UpdateSelectedAmount(0);
                UpdateBalanceText();
            }
            else
            {
                MelonLogger.Msg($"[WITHDRAW] Insufficient. Online:{mm.onlineBalance:N0}");
            }
        }

        private void OnDepositPressed()
        {
            if (_selectedAmount <= 0f)
            {
                MelonLogger.Msg("[DEPOSIT] Zero not allowed.");
                return;
            }
            var mm = NetworkSingleton<MoneyManager>.Instance;
            if (mm == null) { MelonLogger.Error("MoneyManager missing"); return; }
            if (mm.cashBalance >= _selectedAmount)
            {
                mm.CreateOnlineTransaction("ATM Deposit", _selectedAmount, 1f, "ATM in");
                mm.ChangeCashBalance(-_selectedAmount, true, true);
                ATM.WeeklyDepositSum += _selectedAmount;
                MelonLogger.Msg($"[DEPOSIT] ${_selectedAmount:N0} done");
                UpdateSelectedAmount(0);
                UpdateBalanceText();
            }
            else
            {
                MelonLogger.Msg($"[DEPOSIT] Insufficient cash:{mm.cashBalance:N0}");
            }
        }

        private void UpdateSelectedAmount(int amt)
        {
            _selectedAmount = (amt == 0) ? 0f : _selectedAmount + amt;
            if (_selectedAmountText != null)
            {
                _selectedAmountText.text = $"${_selectedAmount:N0}";
            }
        }

        private void SetSelectedAmount(int amt)
        {
            _selectedAmount = amt;
            if (_selectedAmountText != null)
            {
                _selectedAmountText.text = $"${_selectedAmount:N0}";
            }
        }

        private void UpdateBalanceText()
        {
            var mm = NetworkSingleton<MoneyManager>.Instance;
            if (mm == null) return;
            if (_onlineBalanceText != null)
                _onlineBalanceText.text = $"${mm.onlineBalance:N0}";
            if (_cashBalanceText != null)
                _cashBalanceText.text = $"${mm.cashBalance:N0}";
            if (_weeklyAmountText != null)
                _weeklyAmountText.text = $"Weekly: ${ATM.WeeklyDepositSum}/{_weeklyDepositLimit}";

            if (_weeklyProgressFill != null)
            {
                float targetRatio = Mathf.Clamp01(ATM.WeeklyDepositSum / (float)_weeklyDepositLimit);
                float currentRatio = _weeklyProgressFill.anchorMax.x;
                if (Mathf.Abs(targetRatio - currentRatio) > 0.001f)
                    MelonLoader.MelonCoroutines.Start(AnimateProgressBar(currentRatio, targetRatio, 0.5f));
            }

            if (_currentTab == TabType.Deposit)
                UpdateConfirmButtonLayout();
        }

        private IEnumerator AnimateProgressBar(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float v = Mathf.Lerp(from, to, t);
                _weeklyProgressFill.anchorMax = new Vector2(v, 1f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _weeklyProgressFill.anchorMax = new Vector2(to, 1f);
        }

        private int DepositMax() => _weeklyDepositLimit - (int)ATM.WeeklyDepositSum;
    }

    [RegisterTypeInIl2Cpp]
    public class BankingAppComponent : MonoBehaviour
    {
        public BankingAppComponent(IntPtr ptr) : base(ptr) { }
        public void Start() => MelonLogger.Msg("BankingAppComponent: Starting");
    }
}