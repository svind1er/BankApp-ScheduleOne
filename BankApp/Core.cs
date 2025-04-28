using BankApp;
using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using MelonLoader;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(Core), "BankApp", "2.0.3", "svindler, Lalisa")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonColor(0, 255, 0, 249)]
[assembly: MelonAuthorColor(0, 234, 0, 255)]

namespace BankApp;

public class Core : MelonMod
{
    const string TEMPLATE_APP_NAME = "Messages";
    const string APP_OBJECT_NAME = "BankingApp";
    const string APP_LABEL_TEXT = "Bank";

    readonly string iconFileName = "BankAppIcon.png";
    readonly string iconUrl = "https://i.imgur.com/hsVNqRx.png";
    bool _appCreated;

    bool _appSetupAttempted;
    GameObject _bankingAppPanel;
    Text _cashBalanceText;

    Button _confirmButton;

    TabType _currentTab = TabType.Deposit;
    GameObject _depositTab;

    MelonPreferences_Entry<bool> _disableWeeklyDepositLimit;
    bool _iconModified;

    float _lastProgressRatio;
    Button _maxButton;

    Text _onlineBalanceText;
    Button _resetButton;

    float _selectedAmount;
    Text _selectedAmountText;
    float _timeSinceLastReset;
    Text _weeklyAmountText;
    readonly int _weeklyDepositLimit = (int)ATM.WEEKLY_DEPOSIT_LIMIT;
    MelonPreferences_Entry<int> _WeeklyDepositResetInterval;
    RectTransform _weeklyProgressFill;
    GameObject _withdrawTab;
    int[] amounts = new[] { 1, 5, 10, 25, 50, 100, 500, 1000 };

    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("BankingApp: Initializing mod...");
        EnsureUserDataFolderExists();
        loadPreferences();
        MelonCoroutines.Start(EnsureIconFileExists());
        ClassInjector.RegisterTypeInIl2Cpp<BankingAppComponent>();

        MelonLogger.Msg("BankingApp: Successfully registered BankingAppComponent type");
    }

    void loadPreferences()
    {
        MelonLogger.Msg("Loading preferences...");
        var preferences = MelonPreferences.CreateCategory("BankApp", "Banking App");
        preferences.SetFilePath("UserData/BankApp/BankApp.cfg", true);
        preferences.LoadFromFile();
        _disableWeeklyDepositLimit = preferences.CreateEntry(
            "DisableWeeklyDepositLimit",
            false,
            "Disable Weekly Deposit Limit",
            "Disable the weekly deposit limit for the ATM and BankApp."
        );

        _WeeklyDepositResetInterval = preferences.CreateEntry(
            "WeeklyDepositResetInterval",
            10,
            "Weekly Deposit Reset Interval",
            "The interval in seconds after which the weekly deposit limit resets."
        );
        preferences.SaveToFile();
        MelonLogger.Msg("Preferences loaded.");
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
                Object.DontDestroyOnLoad(holder);
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
        ResetWeeklyDepositSum();
    }

    void EnsureUserDataFolderExists()
    {
        var directoryPath = Path.Combine("UserData", "BankApp");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            MelonLogger.Msg($"Created missing directory at '{directoryPath}'.");
        }
    }

    IEnumerator EnsureIconFileExists()
    {
        var filePath = Path.Combine("UserData/BankApp", iconFileName);

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

    // ATM.DepositLimitEnabled is a const so resetting WeeklyDepositSum
    // is the only way :/
    void ResetWeeklyDepositSum()
    {
        if (!_disableWeeklyDepositLimit.Value)
            return;

        _timeSinceLastReset += Time.deltaTime;
        var resetIntervalSeconds = _WeeklyDepositResetInterval.Value;

        if (_timeSinceLastReset >= resetIntervalSeconds)
        {
            ATM.WeeklyDepositSum = 0f;
            _timeSinceLastReset = 0f;
            UpdateBalanceText();
        }
    }

    void CreateOrEnsureAppAndIcon()
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

            _bankingAppPanel = Object.Instantiate(template.gameObject, appsCanvas.transform);
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
                MelonCoroutines.Start(UpdateAppIconLabelAndSpriteCoroutine(iconGO));
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

    IEnumerator UpdateAppIconLabelAndSpriteCoroutine(GameObject iconGO)
    {
        Transform lbl = null;
        for (var i = 0; i < 10 && lbl == null; i++)
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

    IEnumerator SetActiveIconSprite(Image targetImage)
    {
        var filePath = Path.Combine("UserData/BankApp", iconFileName);
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
    }

    void EnsureAppPanelIsSetup(GameObject appPanel)
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
                        SetSelectedAmount(mm.onlineBalance);
                    else
                        SetSelectedAmount(Mathf.Min(mm.cashBalance, DepositMax()));
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

    void SetActiveTab(TabType tab)
    {
        _currentTab = tab;
        var wImg = _withdrawTab.GetComponent<Image>();
        var dImg = _depositTab.GetComponent<Image>();

        wImg.color = tab == TabType.Withdraw
            ? ColorPalette.TABBUTTON_ACTIVE
            : ColorPalette.TABBUTTON_DISABLED;

        dImg.color = tab == TabType.Deposit
            ? ColorPalette.TABBUTTON_ACTIVE
            : ColorPalette.TABBUTTON_DISABLED;

        var wBtn = _withdrawTab.GetComponent<Button>();
        var dBtn = _depositTab.GetComponent<Button>();

        var wc = wBtn.colors;
        wc.normalColor = wImg.color;
        wc.highlightedColor = tab == TabType.Withdraw
            ? ColorPalette.TABBUTTON_ACTIVE_HOVER
            : ColorPalette.TABBUTTON_DISABLED_HOVER;
        wc.pressedColor = tab == TabType.Withdraw
            ? ColorPalette.TABBUTTON_ACTIVE_HOVER
            : ColorPalette.TABBUTTON_DISABLED_HOVER;
        wc.selectedColor = wc.normalColor;
        wBtn.colors = wc;

        var dc = dBtn.colors;
        dc.normalColor = dImg.color;
        dc.highlightedColor = tab == TabType.Deposit
            ? ColorPalette.TABBUTTON_ACTIVE_HOVER
            : ColorPalette.TABBUTTON_DISABLED_HOVER;
        dc.pressedColor = tab == TabType.Deposit
            ? ColorPalette.TABBUTTON_ACTIVE_HOVER
            : ColorPalette.TABBUTTON_DISABLED_HOVER;
        dc.selectedColor = dc.normalColor;
        dBtn.colors = dc;

        wBtn.OnDeselect(null);
        dBtn.OnDeselect(null);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        UpdateUIForCurrentTab();
        UpdateConfirmButtonLayout();
    }

    void UpdateUIForCurrentTab()
    {
        if (_currentTab == TabType.Withdraw)
            _selectedAmountText.text = $"${_selectedAmount:N0}";
        else
            _selectedAmountText.text = $"${_selectedAmount:N0}";
        UpdateSelectedAmount(0);
    }

    void UpdateConfirmButtonLayout()
    {
        if (_confirmButton == null) return;

        var txt = _confirmButton.GetComponentInChildren<Text>();
        var mm = NetworkSingleton<MoneyManager>.Instance;
        if (mm == null) return;

        if (_currentTab == TabType.Withdraw)
        {
            txt.text = "Withdraw";
            var canWithdraw = _selectedAmount > 0 &&
                              !Mathf.Approximately(_selectedAmount, 0) &&
                              mm.onlineBalance >= _selectedAmount;

            _confirmButton.GetComponent<Image>().color = canWithdraw ? ColorPalette.CONFIRMBUTTON_DEFAULT : Color.gray;

            var colors = _confirmButton.colors;
            colors.normalColor = canWithdraw
                ? ColorPalette.CONFIRMBUTTON_DEFAULT
                : Color.gray;
            colors.highlightedColor = canWithdraw
                ? ColorPalette.CONFIRMBUTTON_HOVERED
                : Color.gray;
            colors.pressedColor = canWithdraw
                ? new Color32(164, 37, 37, 255)
                : Color.gray;
            colors.selectedColor = colors.normalColor;
            _confirmButton.colors = colors;

            _confirmButton.interactable = canWithdraw;
        }
        else
        {
            txt.text = "Deposit";

            var depositMax = DepositMax();
            var canDeposit = _selectedAmount > 0
                             && _selectedAmount <= depositMax
                             && _selectedAmount <= mm.cashBalance;

            _confirmButton.GetComponent<Image>().color = canDeposit
                ? ColorPalette.CONFIRMBUTTON_DEFAULT
                : Color.gray;

            var colors = _confirmButton.colors;
            colors.normalColor = canDeposit
                ? ColorPalette.CONFIRMBUTTON_DEFAULT
                : Color.gray;
            colors.highlightedColor = canDeposit
                ? ColorPalette.CONFIRMBUTTON_HOVERED
                : Color.gray;
            colors.pressedColor = canDeposit
                ? new Color32(3, 89, 64, 255)
                : Color.gray;
            colors.selectedColor = colors.normalColor;
            _confirmButton.colors = colors;

            _confirmButton.interactable = canDeposit;
        }
    }

    void OnConfirmPressed()
    {
        if (_currentTab == TabType.Withdraw) OnWithdrawPressed();
        else OnDepositPressed();
    }

    void AttachButtonClickEvent(GameObject panel)
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

    Texture2D CreatePersistentTexture(byte[] data)
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

    void OnSelectAmount(int amt)
    {
        UpdateSelectedAmount(amt);
    }

    void OnWithdrawPressed()
    {
        if (_selectedAmount <= 0f)
        {
            MelonLogger.Msg("[WITHDRAW] Zero not allowed.");
            return;
        }

        var mm = NetworkSingleton<MoneyManager>.Instance;
        if (mm == null)
        {
            MelonLogger.Error("MoneyManager missing");
            return;
        }

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
            MelonLogger.Msg(
                $"[WITHDRAW] Attempted withdrawal: {_selectedAmount:N0} | Insufficient Online Balance:{mm.onlineBalance:N0}");
        }
    }

    void OnDepositPressed()
    {
        if (_selectedAmount <= 0f)
        {
            MelonLogger.Msg("[DEPOSIT] Zero not allowed.");
            return;
        }

        var remaining = DepositMax();
        if (_selectedAmount > remaining)
        {
            MelonLogger.Msg($"[DEPOSIT] Exceeds weekly limit. You can only deposit up to ${remaining:N0} this week.");
            return;
        }

        var mm = NetworkSingleton<MoneyManager>.Instance;
        if (mm == null)
        {
            MelonLogger.Error("MoneyManager missing");
            return;
        }

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
            MelonLogger.Msg(
                $"[DEPOSIT] Attempted deposit: {_selectedAmount:N0} | Insufficient cash:{mm.cashBalance:N0}");
        }
    }

    void UpdateSelectedAmount(int amt)
    {
        _selectedAmount = amt == 0
            ? 0f
            : _selectedAmount + amt;

        if (_currentTab == TabType.Deposit)
            _selectedAmount = Mathf.Min(_selectedAmount, DepositMax());

        if (_selectedAmountText != null)
            _selectedAmountText.text = $"${_selectedAmount:N0}";

        UpdateConfirmButtonLayout();
    }

    void SetSelectedAmount(float amt)
    {
        _selectedAmount = amt;
        UpdateConfirmButtonLayout();
        if (_selectedAmountText != null) _selectedAmountText.text = $"${_selectedAmount:N0}";
    }

    void UpdateBalanceText()
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
            var targetRatio = Mathf.Clamp01(ATM.WeeklyDepositSum / _weeklyDepositLimit);
            var currentRatio = _weeklyProgressFill.anchorMax.x;
            if (Mathf.Abs(targetRatio - currentRatio) > 0.001f)
                MelonCoroutines.Start(AnimateProgressBar(currentRatio, targetRatio, 0.5f));
        }

        if (_currentTab == TabType.Deposit)
            UpdateConfirmButtonLayout();
    }

    IEnumerator AnimateProgressBar(float from, float to, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            var t = elapsed / duration;
            var v = Mathf.Lerp(from, to, t);
            _weeklyProgressFill.anchorMax = new Vector2(v, 1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _weeklyProgressFill.anchorMax = new Vector2(to, 1f);
    }

    int DepositMax()
    {
        return _weeklyDepositLimit - (int)ATM.WeeklyDepositSum;
    }

    enum TabType
    {
        Withdraw,
        Deposit
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