using AmongUs.Data;

namespace DHMO.Modules;

public class ChatSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static IConfigurationHolder ChatOptions = NebulaAPI.Configurations.Holder("options.chat", [ConfigurationTab.Settings], [Virial.Game.GameModes.FreePlay, Virial.Game.GameModes.Standard]);
    static internal readonly IntegerConfiguration NumOfChatHistory = NebulaAPI.Configurations.Configuration("options.chat.numOfchatHistory", (20, 200, 10), 100);
    static internal readonly IntegerConfiguration NumOfFreeChatMaxChar = NebulaAPI.Configurations.Configuration("options.chat.numOffreeChatMaxChar", (100, 300, 25), 100, () => DataManager.Settings.Multiplayer.ChatMode != InnerNet.QuickChatModes.QuickChatOnly);

    public static ChatSystem Instance { get; private set; } = null!;

    static ChatSystem()
    {
        DIManager.Instance.RegisterModule(() => new ChatSystem());

        ChatOptions.AppendConfiguration(NumOfChatHistory);
        ChatOptions.AppendConfiguration(NumOfFreeChatMaxChar);
    }

    public ChatSystem()
    {
        Instance = this;
        ModSingleton<ChatSystem>.Instance = this;
    }

    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    public static ChatController ChatController => AmongUsLLImpl.HudManagerBridge.Chat;
    public ModGameObject? SearchButton;
    public bool IsSearch = false;
}