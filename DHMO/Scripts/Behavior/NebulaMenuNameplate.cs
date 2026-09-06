using Il2CppInterop.Runtime.Injection;

namespace DHMO.Behaviour;

public class NebulaMenuNameplate : MonoBehaviour
{
    public SpriteRenderer AdaptiveRenderer;

    static NebulaMenuNameplate()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaMenuNameplate>();
    }

    public void Awake()
    {
        var panel = GetComponent<ShapeshifterPanel>();
        AdaptiveRenderer = Instantiate(panel.Background, panel.Background.transform);
        AdaptiveRenderer.GetComponent<PassiveButton>().enabled = false;
        if (MeetingHud.Instance.AsBoolFast())
        {
            //ゲーム内だとマスク不要
            AdaptiveRenderer.material = HatManager.Instance.PlayerMaterial;
            AdaptiveRenderer.maskInteraction = SpriteMaskInteraction.None;
        }
        else
        {
            //ゲーム外だとマスク
            AdaptiveRenderer.material = HatManager.Instance.MaskedPlayerMaterial;
            AdaptiveRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
        AdaptiveRenderer.sprite = null;

        UpdateColor();
    }

    public void UpdateColor()
    {
        var panel = GetComponent<ShapeshifterPanel>();
        PlayerMaterial.SetColors(panel.PlayerIcon.ColorId, AdaptiveRenderer);
    }
}