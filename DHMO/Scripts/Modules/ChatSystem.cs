using AmongUs.Data;
using Virial.Events.Configurations;

namespace DHMO.Modules;

public class ChatSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static IConfigurationHolder ChatOptions = NebulaAPI.Configurations.Holder("options.chat", [ConfigurationTab.Settings], [Virial.Game.GameModes.FreePlay, Virial.Game.GameModes.Standard]);
    static internal readonly IntegerConfiguration NumOfChatHistory = NebulaAPI.Configurations.Configuration("options.chat.numOfchatHistory", (20, 100, 10), 50);
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
    public FreeChatInputField? SearchField;
    public ModGameObject? SearchButton;
    public bool IsSearch = false;

    void OnConfigurationChanged(SharableEntryUpdateEvent ev)
    {
        var pool = ChatController.chatBubblePool;

        IntegerConfigurationImpl integerConfigurationImpl = (IntegerConfigurationImpl)ChatSystem.NumOfChatHistory;
        if (!pool.AsBoolFast() || ev.SharableEntry.Id != integerConfigurationImpl.val.Id) return;

        int count = ChatSystem.NumOfChatHistory - pool.poolSize;
        pool.poolSize = ChatSystem.NumOfChatHistory;
        NebulaManager.Instance.StartCoroutine(CoChangeSize(pool, count).WrapToIl2Cpp());
    }

    static IEnumerator CoChangeSize(ObjectPoolBehavior pool, int count)
    {
        if (count == 0) yield break;

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                lock (pool.inactiveChildren)
                {
                    pool.CreateOneInactive(pool.Prefab);
                }
                yield return null;
            }
        }
        else
        {
            int removeCount = -count;

            var difference = pool.NotInUse - removeCount;
            if (difference <= 0)
            {
                for (int i = 0; i < difference; i++)
                    pool.Reclaim(pool.activeChildren[0]);

                AmongUsLLImpl.HudManagerBridge.Chat.AlignAllBubbles();
            }

            List<PoolableBehavior> toRemove = [];

            for (int i = 0; i < removeCount; i++)
            {
                int lastIdx = pool.inactiveChildren.Count - 1;
                var p = pool.inactiveChildren[lastIdx];
                toRemove.Add(p);

                lock (pool.inactiveChildren)
                {
                    pool.inactiveChildren.RemoveAt(lastIdx);
                }
            }

            toRemove.Do(p => p.gameObject.Destroy());
            yield return null;
        }

        DLog.Log($"ObjectPoolSize is changed to {pool.poolSize} now.(Acive: {pool.InUse}, Inactive: {pool.NotInUse})");
    }
}