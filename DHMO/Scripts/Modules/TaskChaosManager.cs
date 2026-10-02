namespace DHMO.Modules;

public class TaskChaosManager : AbstractModule<IGameModeStandard>, IGameOperator
{
    public static TaskChaosManager Instance { get; private set; } = null!;

    static TaskChaosManager()
    {
        DIManager.Instance.RegisterModule(() => new ChatSystem());
    }
    
    private TaskChaosManager()
    {
        Instance = this;
    }
}