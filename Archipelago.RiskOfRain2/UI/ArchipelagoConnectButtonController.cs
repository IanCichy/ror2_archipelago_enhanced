using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Archipelago.RiskOfRain2.UI;

/// <summary>
/// Controls the Archipelago connection UI elements and handles user interactions for connecting to an Archipelago
/// server within the character select screen.
/// </summary>
/// <remarks>This controller manages the display and behavior of the Archipelago connection panel, including input
/// fields for connection details and buttons for connecting or minimizing the panel. It exposes delegates for handling
/// changes to connection parameters and button clicks, allowing other components to respond to user input. The
/// controller is intended to be used in multiplayer or single-player lobbies where Archipelago integration is
/// available.</remarks>
public class ArchipelagoConnectButtonController : MonoBehaviour
{
    public static CharacterSelectController contr { get; private set; }
    public GameObject connectPanel;
    public string assetName = "ConnectPanel";
    public string bundleName = "connectbundle";
    public GameObject chat;
    public GameObject ConnectPanel;
    public GameObject MinimizePanel;
    private string minimizeText = "-";
    private static bool isConnected = false;
    private TMP_FontAsset font;

    public delegate string SlotChanged(string newValue);
    public static SlotChanged OnSlotChanged;
    public delegate string PasswordChanged(string newValue);
    public static PasswordChanged OnPasswordChanged;
    public delegate string UrlChanged(string newValue);
    public static UrlChanged OnUrlChanged;
    public delegate string PortChanged(string newValue);
    public static PortChanged OnPortChanged;
    public delegate void ConnectClicked();
    public static ConnectClicked OnConnectClick;
    public static ConnectClicked OnButtonClick;
    public void Start()
    {
        connectPanel = AssetBundleHelper.LoadPrefab("ConnectCanvas");
        On.RoR2.UI.CharacterSelectController.Update += CharacterSelectController_Update;
    }

    public void OnLoadDone(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> obj)
    {
        if (obj.Result == null)
        {
            Log.LogDebug("error obj is null");
        }
        else
        {
            Log.LogDebug($"obj.Result {obj.Result}");
        }
    }

    public void Awake()
    {
        On.RoR2.UI.CharacterSelectController.Awake += CharacterSelectController_Awake;
        OnButtonClick += ButtonPressed;
    }

    private void CharacterSelectController_Update(On.RoR2.UI.CharacterSelectController.orig_Update orig, CharacterSelectController self)
    {
        orig(self);
        contr = self;

        if (chat != null && chat.gameObject.activeSelf == false)
        {
            chat.gameObject.SetActive(true);
        }
    }

    //Hook for when the lobby is entered
    //Only show for the Host or Single Player
    internal void CharacterSelectController_Awake(On.RoR2.UI.CharacterSelectController.orig_Awake orig, CharacterSelectController self)
    {
        orig(self);
        contr = self;
        var isHost = NetworkServer.active && RoR2Application.isInMultiPlayer;
        var isSinglePlayer = RoR2Application.isInSinglePlayer;
        Log.LogDebug($"Is the Host: {isHost} Is in Single Player {isSinglePlayer}");
        chat = contr.transform.Find("SafeArea/ChatboxPanel/").gameObject;
        if (isHost || isSinglePlayer)
        {
            CreateButton();
            CreateFields();
            CreateMinimizeButton();
            Log.LogDebug("Character Controller Awake()");
            ConnectPanel = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel").gameObject;
        }
    }

    //Create button for the lobby to connect to Archipelago
    private void CreateButton()
    {
        var readyButton = contr.transform.Find("SafeArea/ReadyPanel/ReadyButton");
        font = readyButton.GetComponentInChildren<TextMeshProUGUI>().font;
        var readyPanel = contr.transform.Find("SafeArea");
        var baseHoverOutlineSprite = readyButton.Find("HoverOutlineImage").gameObject;

        var cb = Instantiate(connectPanel);
        cb.AddComponent<MPEventSystemLocator>();
        cb.AddComponent<HGGamepadInputEvent>();
        cb.transform.SetParent(readyPanel, false);
        cb.transform.localPosition = new Vector3(125, 0, 0);
        cb.transform.localScale = Vector3.one;
        RectTransform rectTransform = cb.GetComponent<RectTransform>();
        var button = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/Button/").gameObject;
        var outline = Instantiate(baseHoverOutlineSprite);
        outline.transform.SetParent(button.transform, false);
        button.AddComponent<HGButton>();
        button.GetComponent<HGButton>().imageOnHover = outline.GetComponent<Image>();
        button.GetComponent<HGButton>().showImageOnHover = true;
        button.AddComponent<HGGamepadInputEvent>();
        button.GetComponent<Image>().sprite = readyButton.gameObject.GetComponent<Image>().sprite;
        button.GetComponent<HGButton>().onClick.AddListener(() => OnConnectClick?.Invoke());

        button.GetComponentInChildren<TextMeshProUGUI>().font = font;
    }

    //Listeners for the fields to save Archipelago connection info
    private void CreateFields()
    {
        var inputSlotName = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/InputSlotName/").gameObject;
        inputSlotName.GetComponent<TMP_InputField>().onValueChanged.AddListener((string value) => { OnSlotChanged?.Invoke(value); });
        inputSlotName.GetComponent<TMP_InputField>().text = ArchipelagoPlugin.apSlotName;
        var inputPassword = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/InputPassword/").gameObject;
        inputPassword.GetComponent<TMP_InputField>().onValueChanged.AddListener((string value) => { OnPasswordChanged?.Invoke(value); });
        inputPassword.GetComponent<TMP_InputField>().text = ArchipelagoPlugin.apPassword;
        var inputUrl = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/InputUrl/").gameObject;
        inputUrl.GetComponent<TMP_InputField>().onValueChanged.AddListener((string value) => { OnUrlChanged?.Invoke(value); });
        inputUrl.GetComponent<TMP_InputField>().text = ArchipelagoPlugin.apServerUri;
        var inputPort = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/InputPort/").gameObject;
        inputPort.GetComponent<TMP_InputField>().onValueChanged.AddListener((string value) => { OnPortChanged?.Invoke(value); });
        inputPort.GetComponent<TMP_InputField>().text = string.Concat(ArchipelagoPlugin.apServerPort);
    }

    //Create button info to minimize Archipelago Panel
    private void CreateMinimizeButton()
    {
        var minimizePanel = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Hide");
        var button = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Hide/Button").gameObject;
        button.AddComponent<HGButton>();
        button.AddComponent<HGGamepadInputEvent>();
        minimizePanel.GetComponentInChildren<TextMeshProUGUI>().font = font;
        button.GetComponent<HGButton>().onClick.AddListener(() => OnButtonClick?.Invoke());
        MinimizePanel = minimizePanel.gameObject;
    }

    private void ButtonPressed()
    {
        ConnectPanel.SetActive(!ConnectPanel.activeSelf);
        if (ConnectPanel.activeSelf)
        {
            minimizeText = "-";
        }
        else
        {
            minimizeText = isConnected ? "<color=#00FF00>AP Connected</color> <color=#AAAAAA>[click to expand]</color>" : "Archipelago";
        }
        MinimizePanel.GetComponentInChildren<TextMeshProUGUI>().text = minimizeText;
    }

    public static void ChangeButtonWhenConnected()
    {
        Log.LogDebug("Changing Button after connecting.");
        isConnected = true;
        if (contr != null)
        {
            var button = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/Button").gameObject;
            button.GetComponent<Image>().color = Color.red;
            var text = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/Button/Text (TMP)").gameObject;
            text.GetComponent<TextMeshProUGUI>().text = "Disconnect";

            // Auto-minimize the panel on successful connect
            var panel = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel").gameObject;
            panel.SetActive(false);
            var minimize = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Hide").gameObject;
            minimize.GetComponentInChildren<TextMeshProUGUI>().text = "<color=#00FF00>AP Connected</color> <color=#AAAAAA>[click to expand]</color>";
        }
    }

    public static void ChangeButtonWhenDisconnected()
    {
        Log.LogDebug("Changing Button after disconnecting.");
        isConnected = false;
        if (contr != null)
        {
            var button = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/Button").gameObject;
            button.GetComponent<Image>().color = new Color(0.0745f, 0.2824f, 0.4392f, 1f);
            var text = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel/Button/Text (TMP)").gameObject;
            text.GetComponent<TextMeshProUGUI>().text = "Connect To AP";

            // Update minimize label if panel is currently minimized
            var panel = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Panel").gameObject;
            if (!panel.activeSelf)
            {
                var minimize = contr.transform.Find("SafeArea/ConnectCanvas(Clone)/Hide").gameObject;
                minimize.GetComponentInChildren<TextMeshProUGUI>().text = "Archipelago";
            }
        }
    }
}