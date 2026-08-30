using Cpp2IL.Core.Extensions;
using Il2CppInterop.Runtime.Injection;
using Object = UnityEngine.Object;

namespace DHMO.Roles.Neutral;

public class JournalistShot : MonoBehaviour
{
    private GamePlayer? selectedPlayer = null;
    
    SpriteRenderer flashRenderer = null!;
    SpriteRenderer frameRenderer = null!;
    public SpriteRenderer centerRenderer = null!;
    BoxCollider2D collider = null!;

    public bool isVert;
    public void ToggleDirection() => isVert = !isVert;
    static JournalistShot() => ClassInjector.RegisterTypeInIl2Cpp<JournalistShot>();

    private bool focus;

    public void SetLayer(int layer)
    {
        gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.layer = layer));
    }

    public void SetPlayer(GamePlayer? player)
    {
        selectedPlayer = player;
    }
    
    public void Awake()
    {
        frameRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
        flashRenderer = transform.GetChild(1).GetComponent<SpriteRenderer>();
        centerRenderer = transform.GetChild(3).GetComponent<SpriteRenderer>();
        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = frameRenderer.size;

        SetLayer(LayerExpansion.GetDefaultLayer());

        focus = true;
    }

    public void SetUpButton(Action action)
    {
        var button = gameObject.SetUpButton();
        button.OnClick.AddListener(() =>
        {
            if (!focus) return;
            action.Invoke(); GameObject.Destroy(button);
        });
    }

    public void Update()
    {
        if (!focus) return;
        if (selectedPlayer == null) return;

        var mouseInfo = PlayerModInfo.LocalMouseInfo;

        var dis = Mathn.Min(mouseInfo.distance, 2.4f + Mathn.Abs(Mathn.Cos(mouseInfo.angle)) * 1.7f);
        
        var anchorPos = selectedPlayer.Position;
        var mouseDir = new VVector2(Mathn.Cos(mouseInfo.angle), Mathn.Sin(mouseInfo.angle));

        float halfW = transform.localScale.x * frameRenderer.size.x * 0.5f;
        float halfH = transform.localScale.y * frameRenderer.size.y * 0.5f;
        float halfSize = Mathn.Min(halfW, halfH);

        const float maxPresenceRatio = 0.8f;
        float maxOffset = halfSize * maxPresenceRatio;

        float offsetMag = Mathn.Min(mouseInfo.distance * 0.45f, maxOffset);
        var offset = mouseDir * offsetMag;

        var targetPos = anchorPos + offset;

        transform.localPosition -= (transform.localPosition - targetPos.AsUnityVector3(-10f)) * (FastMethods.GetDeltaTimeFast() * 8.6f);

        var scale = transform.localScale.x;
        float targetP = dis switch
        {
            < 2.1f => 0f,
            > 3.5f => 1f,
            _ => (dis - 2.1f) / (3.5f - 2.1f)
        };
        float targetScale = Mathn.Lerp(Journalist.WideAngleFinderSizeOption, Journalist.TelephotoFinderSizeOption, targetP);

        scale -= (scale - targetScale) * FastMethods.GetDeltaTimeFast() * 5.4f;
        transform.localScale = new(scale, scale, 1f);

        if (!Input.GetMouseButton(1))
            transform.eulerAngles = new VVector3(0, 0, mouseInfo.angle * 180f / Mathn.PI + (isVert ? 90f : 0f));
    }

    public void TakePicture(GamePlayer myPlayer, GamePlayer selectedPlayer, Action<bool>? callback = null)
    {
        focus = false;

        var scale = transform.localScale;
        
        GameObject camObj = new("CamObj");
        camObj.transform.SetParent(transform);
        camObj.transform.localScale = new VVector3(1, 1);
        camObj.transform.localPosition = new VVector3(0f, 0f, -10f);
        camObj.transform.localEulerAngles = new VVector3(0, 0, 0);
        
        //zを名前テキストより奥へ
        var pos = camObj.transform.position;
        pos.z = -0.4f;
        camObj.transform.position = pos;
        centerRenderer.gameObject.SetActive(false);

        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = transform.localScale.y * frameRenderer.size.y * 0.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 0b10000000000001101100000001;
        cam.nearClipPlane = -100;
        cam.farClipPlane = 100;
        cam.enabled = true;
        RenderTexture rt = new((int)(frameRenderer.size.x * 100f * scale.x), (int)(frameRenderer.size.y * 100f * scale.y), 16);
        rt.Create();
        cam.targetTexture = rt;

        foreach (var usable in AmongUsLLImpl.ShipStatusInstance.GetComponentsInChildren<IUsable>()) usable.SetOutline(false, false);
        if (collider.OverlapPoint(selectedPlayer.Position))
        {
            selectedPlayer.Unbox().AddOutfit(new OutfitCandidate(NebulaGameManager.Instance!.UnknownOutfit, "ShotComm", 10000, false));
        }
        
        //一時的に影を無視して描画させる
        using(var ignoreShadow = AmongUsUtil.IgnoreShadow(false)) cam.Render();

        RenderTexture.active = cam.targetTexture;
        Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, false);
        texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2D.Apply(false, false);

        //画像を保存する
        if(ClientOption.GetValue(ClientOption.ClientOptionType.OutputPaparazzoPhoto) == 1){
            File.WriteAllBytesAsync(NebulaManager.GetPicturePath("_Original", out _), texture2D.EncodeToPNG());
            VVector2 vec1 = new VVector2(rt.width, rt.height).Rotate(transform.localEulerAngles.z), vec2 = new VVector2(rt.width, -rt.height).Rotate(transform.localEulerAngles.z);
            var rotatedWidth = (int)(Mathn.Max(Mathn.Abs(vec1.x), Mathn.Abs(vec2.x)) + 0.8f);
            var rotatedHeight = (int)(Mathn.Max(Mathn.Abs(vec1.y), Mathn.Abs(vec2.y)) + 0.8f);
            var renderer = UnityHelper.CreateSpriteRenderer("TempImage", null, VVector3.Zero, 30);
            renderer.sprite = texture2D.ToSprite(100f);
            renderer.transform.localEulerAngles = transform.localEulerAngles;
            var rotatedTexture = UnityHelper.TakeCustomPicture(null, new(0f,0f,-1f), rotatedWidth, rotatedHeight, rotatedHeight * 0.5f * 0.01f, 1 << 30, VColor.Clear.ToUnityColor(), false, false);
            File.WriteAllBytesAsync(NebulaManager.GetPicturePath("_Rotated", out _), rotatedTexture.EncodeToPNG());
            GameObject.Destroy(renderer.gameObject);
            GameObject.Destroy(rotatedTexture);
        }

        var sprite = texture2D.ToSprite(100f);

        cam.targetTexture = null;
        RenderTexture.active = null;
        GameObject.Destroy(rt);
        GameObject.Destroy(camObj);

        centerRenderer.gameObject.SetActive(true);
        centerRenderer.transform.localPosition = new(0f, 0f, 0.1f);
        centerRenderer.transform.localScale = new(1f / scale.x, 1f / scale.y, 0.1f);
        centerRenderer.sprite = sprite;
        centerRenderer.material = VanillaAsset.GetHighlightMaterial();
        
        selectedPlayer.Unbox().RemoveOutfit("ShotComm");

        NebulaAsset.PlaySE(NebulaAudioClip.Camera);
        
        //UIレイヤー上の表示に変換
        SetLayer(LayerExpansion.GetUILayer());

        var pictureScaler = UnityHelper.CreateObject("PictureScaler", AmongUsLLImpl.HudManagerBridge.MyTransform, VVector3.Zero);
        transform.SetParent(pictureScaler.transform, true);
        var pictureScalerObj = pictureScaler.ModGameObject();
        pictureScalerObj.LocalScale = NebulaGameManager.Instance!.WideCamera.ViewerTransform.LocalScale;
        pictureScalerObj.LocalEulerAngles = NebulaGameManager.Instance.WideCamera.ViewerTransform.LocalEulerAngles;

        //UI変換後のスケーラ
        IEnumerator CoScale()
        {
            float t = 5f;
            while (t > 0f)
            {
                pictureScalerObj.LocalScale -= (pictureScalerObj.LocalScale - VVector3.One).Delta(2f, 0.02f);
                pictureScalerObj.LocalEulerAngles -= (pictureScalerObj.LocalEulerAngles - VVector3.Zero).Delta(2f, 0.2f);
                transform.localPosition -= (transform.localPosition - new UVector3(0f, 0f, -10f)).Delta(8f, 0.2f);
                t -= Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator CoFlash()
        {
            flashRenderer.color = VColor.White.ToUnityColor();
            float a = 1f;
            while (a > 0f)
            {
                a -= Time.deltaTime * 1.4f;
                a = Mathn.Clamp01(a);
                flashRenderer.color = VColor.White.AlphaMultiplied(a).ToUnityColor();
                yield return null;
            }
            flashRenderer.gameObject.SetActive(false);

            //成功時
            callback?.Invoke(true);

            if (myPlayer.Role is Journalist.Instance journalist)
                journalist.Shot = (pictureScalerObj.GetUnityTransform(), this);
            
            NebulaManager.Instance.StartCoroutine(CoScale().WrapToIl2Cpp());
        }

        StartCoroutine(CoFlash().WrapToIl2Cpp());
    }
}


public class Journalist : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.journalist", new(218, 165, 32), TeamRevealType.OnlyMe);
    
    private Journalist() : base("journalist", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [MiniCamCoolDownOption, RequiredBuzzOption, RequiredPhotosOption, TelephotoFinderSizeOption, WideAngleFinderSizeOption, VentConfiguration])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
    }
    
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);
    
    static private FloatConfiguration MiniCamCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.journalist.miniCamCoolDown", (5f, 30f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration RequiredBuzzOption = NebulaAPI.Configurations.Configuration("options.role.journalist.requiredBuzz", (10f, 20f, 0.5f), 12f);
    static private IntegerConfiguration RequiredPhotosOption = NebulaAPI.Configurations.Configuration("options.role.journalist.requiredPhotos", (1, 5), 3);
    static internal FloatConfiguration TelephotoFinderSizeOption = NebulaAPI.Configurations.Configuration("options.role.journalist.telephotoFinderSize", (0.25f, 0.75f, 0.125f), 0.625f, FloatConfigurationDecorator.Ratio);
    static internal FloatConfiguration WideAngleFinderSizeOption = NebulaAPI.Configurations.Configuration("options.role.journalist.wideAngleFinderSize", (0.5f, 1.25f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.journalist.vent", true);

    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;

    static public readonly Journalist MyRole = new();

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public static GameEnd? JournalistTeamWin = NebulaAPI.Preprocessor?.CreateEnd("journalist", MyRole.RoleColor);
        
        public override DefinedRole Role => MyRole;
        public (Transform holder, JournalistShot shot)? Shot { get; internal set; } = null;
        private HudContent? shotsHolder = null;
        public GamePlayer? SelectedPlayer { get; private set; } = null;

        private float buzz = 0f;
        private int photos = 0;

        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                JournalistShot? lastFinder = null;

                shotsHolder = HudContent.InstantiateContent("Pictures", true, true, false, true);
                this.BindGameObject(shotsHolder.gameObject);

                var miniCamButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer,
                    Virial.Compat.VirtualKeyInput.Ability, MiniCamCoolDownOption, "journalist.miniCam",
                    SpectatorsAbility.spectatorChangeSprite,
                    _ => MyPlayer.CanMove && Shot == null
                );
                miniCamButton.OnClick = (button) =>
                {
                    if (AmongUsLLImpl.HudManagerInstance.PlayerCam.Target == MyPlayer.VanillaPlayer)
                    {
                        var menu = AbstractPlayerMenuMinigame.Create<SelectPlayerMenu>();
                        
                        menu.Begin((p) =>
                        {
                            if (p.IsDead || GamePlayer.AllPlayers.Any(p =>
                                    p.Role is Pelican.Instance pelican &&
                                    pelican.DevouredPlayerMask.Test(SelectedPlayer)))
                            {
                                return;
                            }

                            SelectedPlayer = p;
                            AmongUsUtil.SetCamTarget(SelectedPlayer.VanillaPlayer, true);
                            menu.CloseInternal();
                        }, (p) => p.DeadRound == null || (ModSingleton<DGameManager>.Instance.CurrentRound - p.DeadRound) == 1);
                    }
                    else
                    {
                        AmongUsUtil.SetCamTarget(MyPlayer.VanillaPlayer);
                    }

                    miniCamButton.StartCoolDown();
                };

                var shotButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer,
                        Virial.Compat.VirtualKeyInput.SecondaryAbility, "journalist.camera", 0f, "journalist.shot",
                        Paparazzo.cameraButtonSprite,
                        _ => lastFinder.AsBoolFast() && Shot == null && SelectedPlayer != null,
                        _ => AmongUsLLImpl.HudManagerInstance.PlayerCam.Target == SelectedPlayer?.VanillaPlayer
                    ).BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "journalist.toggle", true)
                    .SetAsMouseClickButton();
                shotButton.OnClick = (button) =>
                {
                    Object.Destroy(lastFinder!.GetComponent<PassiveButton>());

                    IEnumerator CoTakePicture(JournalistShot finder)
                    {
                        yield return new WaitForEndOfFrame();

                        if (SelectedPlayer == null) yield break;
                        finder.TakePicture(MyPlayer, SelectedPlayer);
                        RpcAddPhoto.Invoke(MyPlayer);
                    }

                    NebulaManager.Instance.StartCoroutine(CoTakePicture(lastFinder).WrapToIl2Cpp());
                    lastFinder = null;
                    AmongUsUtil.SetCamTarget(MyPlayer.VanillaPlayer);
                };
                ButtonEffect.SetAidAction(shotButton, this, null, MyPlayer, () =>
                {
                    if (lastFinder.AsBoolFast(out var finder)) finder.ToggleDirection();
                });

                void DestroyFinder()
                {
                    if (lastFinder.AsBoolFast(out var finder)) Object.Destroy(finder.gameObject);
                    AmongUsUtil.SetCamTarget(MyPlayer.VanillaPlayer);
                    lastFinder = null;
                }

                GameOperatorManager.Instance?.RegisterReleasedAction(DestroyFinder, this);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
                {
                    var camTarget = AmongUsLLImpl.HudManagerInstance.PlayerCam.Target == SelectedPlayer?.VanillaPlayer;
                    bool predicate = MyPlayer is { IsDead: false, CanMove: true } && camTarget &&
                                     !AmongUsUtil.InMeeting;

                    if (lastFinder == null && predicate && !(shotButton.CoolDownTimer?.IsProgressing ?? true))
                    {
                        lastFinder = Object.Instantiate(NebulaAsset.PaparazzoShot, null).AddComponent<JournalistShot>();
                        lastFinder.SetPlayer(SelectedPlayer);
                        lastFinder.gameObject.layer = LayerExpansion.GetUILayer();
                        lastFinder.transform.localScale = VVector3.Zero;

                        var pos = MyPlayer.VanillaPlayer.transform.localPosition;
                        pos.z = -10f;
                        lastFinder.transform.localPosition = pos;
                    }

                    if (lastFinder != null && (!predicate || GamePlayer.AllPlayers.Any(p =>
                            p.Role is Pelican.Instance pelican && pelican.DevouredPlayerMask.Test(SelectedPlayer))))
                        DestroyFinder();
                }, this);
            }
        }

        [OnlyMyPlayer]
        void OnStatusUpdate(JournalistStatusUpdate ev)
        {
            if (ev.Correct) return;
            if (ev.NoGuessed) buzz += 0.5f;
            else buzz++;
        }

        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (MyPlayer.IsDead) return;
            if (buzz >= RequiredBuzzOption && photos >= RequiredPhotosOption && JournalistTeamWin != null)
            {
                NebulaAPI.CurrentGame?.RequestGameEnd(JournalistTeamWin, BitMasks.AsPlayer(MyPlayer));
            }
        }

        [Local]
        void LocalHudUpdate(GameHudUpdateEvent ev)
        {
            if (shotsHolder == null || Shot == null) return;
            var shot = Shot.Value;

            if (shot.holder.transform.parent != shotsHolder!.transform)
                shot.holder.transform.SetParent(shotsHolder.transform, true);

            var scale = shot.shot.transform.localScale.x;
            scale -= (scale - 0.2f) * Time.deltaTime * 3.6f;
            shot.shot.transform.localScale = new(scale, scale, 1f);

            var diffPos = shot.holder.transform.localPosition - new UVector3(-0.3f, -0.25f, -10f);
            shot.holder.transform.localPosition -= diffPos * Mathn.Min(1f, Time.deltaTime) * 6.4f;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyPlayer.IsDead || Shot == null) return;
            var shot = Shot.Value;

            bool shareFlag = false;
            float timer = 20f;

            var hourglass = UnityHelper.CreateObject<SpriteRenderer>("Hourglass", shotsHolder!.transform,
                new VVector3(0.6f, -0.25f, -10f));
            hourglass.sprite = Paparazzo.Instance.hourGlassSprite.GetSprite();
            var hourText = Object.Instantiate(AmongUsLLImpl.HudManagerBridge.KillButton.cooldownTimerText,
                hourglass.transform);
            hourText.text = Mathn.CeilToInt(timer).ToString();
            hourText.transform.localScale = new VVector3(0.5f, 0.5f, 1f);
            hourText.gameObject.SetActive(true);


            IEnumerator CoWaitSharing()
            {
                while (!shareFlag && timer > 0f && MeetingHudExtension.CanShowPhotos)
                {
                    timer -= Time.deltaTime;
                    hourText.text = Mathf.CeilToInt(timer).ToString();

                    yield return null;
                }

                if (!shot.shot.AsBoolFast()) yield break;
                if (shot.shot.gameObject.TryGetComponent<PassiveButton>(out var button))
                    Object.Destroy(button);

                if (hourglass.AsBoolFast()) Object.Destroy(hourglass.gameObject);
            }

            NebulaManager.Instance.StartCoroutine(CoWaitSharing().WrapToIl2Cpp());

            var button = shot.shot.gameObject.SetUpButton(true);
            button.OnMouseOut.AddListener(() => { AmongUsUtil.SetHighlight(shot.shot.centerRenderer, false); });
            button.OnMouseOver.AddListener(() => { AmongUsUtil.SetHighlight(shot.shot.centerRenderer, true); });
            button.OnClick.AddListener(() =>
            {
                if (MyPlayer.IsDead || SelectedPlayer == null) return;

                RpcSharePicture.Invoke((shot.shot.centerRenderer.transform.localScale.x,
                    shot.shot.transform.localEulerAngles.z,
                    shot.shot.centerRenderer.sprite.texture.EncodeToJPG(60).ToArray(),
                    [MyPlayer.PlayerId, SelectedPlayer.PlayerId]));
                shareFlag = true;
            });
        }
        
        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            var text = Language.Translate("role.journalist.taskText");

            var detail = Language.Translate("role.journalist.taskTextBuzz")
                .Replace("%CB%", buzz.ToString())
                .Replace("%GB%", RequiredBuzzOption.GetValue().ToString());

            if (RequiredPhotosOption > 1)
            {
                detail = Language.Translate("role.journalist.taskTextPhoto")
                    .Replace("%CP%", photos.ToString())
                    .Replace("%GP%", RequiredPhotosOption.GetValue().ToString()) + ", " + detail;
            }

            ev.AppendText(text.Replace("%DETAIL%", detail));
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            Shot?.shot.gameObject.Destroy();
            Shot = null;
        }

        public static readonly Dictionary<int, (float scale, float angle, int length, byte[]?[] bytes)> storedTexture = [];

        public static readonly DivisibleRemoteProcess<(float, float, byte[], byte[]), (int id, float scale, float angle, int length, int index, byte[] bytes, byte[] playerId)> RpcSharePicture = new("JournalistSharePicture",
                (message) =>
                {
                    int id = System.Random.Shared.Next(100000);
                    
                    List<(byte[], int)> arrays = [];
                    int proceed = 0;
                    int index = 0;
                    while (proceed < message.Item3.Length)
                    {
                        int last = proceed;
                        proceed = Mathn.Min(proceed + 500, message.Item3.Length);

                        arrays.Add((message.Item3.SubArray(last, proceed - last), index));
                        index++;
                    }

                    return arrays
                        .Select(array => (id, message.Item1, message.Item2, arrays.Count, array.Item2, array.Item1, message.Item4))
                        .GetEnumerator();
                },
                (writer, divided) =>
                {
                    writer.Write(divided.id);
                    writer.Write(divided.scale);
                    writer.Write(divided.angle);
                    writer.Write(divided.length);
                    writer.Write(divided.index);
                    writer.WriteBytesAndSize(divided.bytes);
                    writer.WriteBytesAndSize(divided.playerId);
                },
                (reader) => (reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadInt32(),
                    reader.ReadInt32(), reader.ReadBytesAndSize(), reader.ReadBytesAndSize()),
                
                (divided, _) =>
                {
                    if (!storedTexture.ContainsKey(divided.id))
                        storedTexture[divided.id] = (divided.scale, divided.angle, divided.length,
                            new byte[]?[divided.length]);

                    var stored = storedTexture[divided.id];
                    stored.bytes[divided.index] = divided.bytes;
                    if (stored.bytes.All(b => b != null))
                    {
                        var obj = Object.Instantiate(NebulaAsset.PaparazzoShot, null);
                        AddRightContent(obj);
                        var renderer = obj.transform.GetChild(3).GetComponent<SpriteRenderer>();
                        List<byte> data = [];
                        foreach (var b in stored.bytes) data.AddRange(b!);
                        Texture2D texture = new(1, 1);
                        texture.LoadImage(data.ToArray());
                        renderer.sprite = texture.ToSprite(100f);
                        renderer.transform.localPosition = new(0f, 0f, 0.1f);
                        renderer.transform.localScale = VVector3.One * stored.scale;
                        storedTexture.Remove(divided.id);
                        obj.transform.localEulerAngles = new(0, 0, stored.angle);
                        obj.ForEachChild((Il2CppSystem.Action<GameObject>)(o => o.layer = LayerExpansion.GetUILayer()));

                        byte journalistByte = divided.playerId[0];
                        byte selectedByte = divided.playerId[1];

                        bool cannotClick = GamePlayer.LocalPlayer!.IsDead ||
                                           GamePlayer.LocalPlayer.PlayerId == journalistByte;
                        bool isGuessed = false;

                        var collider2D = obj.AddComponent<BoxCollider2D>();
                        collider2D.enabled = !cannotClick;
                        collider2D.size = new VVector2(2f, 2f);

                        obj.AddComponent<ScriptBehaviour>().DestroyHandler += () =>
                        {
                             if (GamePlayer.LocalPlayer.IsDead || GamePlayer.LocalPlayer.PlayerId == journalistByte) return;
                             if (!isGuessed) RpcGuessPlayer?.Invoke((journalistByte, false, true));
                        };
                        
                        var button = obj.SetUpButton();
                        button.OnClick.AddListener(() =>
                        {
                            if (isGuessed || GamePlayer.LocalPlayer.IsDead) return;
                            if (Minigame.Instance.AsBoolFast(out var minigame)) minigame.CloseInternal(); 
                            
                            var menu = AbstractPlayerMenuMinigame.Create<SelectPlayerMenu>();
                            
                            menu.Begin((p) =>
                            {
                                bool correct = p.PlayerId == selectedByte;
                                AmongUsUtil.PlayQuickFlash(correct ? VColor.Green : VColor.Red);

                                RpcGuessPlayer?.Invoke((journalistByte, correct, false));
                                isGuessed = true;
                                collider2D.enabled = false;
                                menu.CloseInternal();
                            }, (p) => p.DeadRound == null || (ModSingleton<DGameManager>.Instance.CurrentRound - p.DeadRound) == 1, true);
                        });
                        button.SetLocalizedOverlay("ui.journalist.photoInfo");
                        
                        IEnumerator CoShow()
                        {
                            NebulaAsset.PlaySE(NebulaAudioClip.PaparazzoDisclose, volume: 1f);

                            float scale = 0f;
                            while (scale < 0.4f)
                            {
                                scale -= (scale - 0.4f) * Time.deltaTime * 4f;
                                if (obj.AsBoolFast()) obj.transform.localScale = VVector3.One * scale;
                                yield return null;
                            }

                            if (obj.AsBoolFast()) obj.transform.localScale = VVector3.One * 0.4f;
                        }

                        NebulaManager.Instance.StartCoroutine(CoShow().WrapToIl2Cpp());
                        
                        if (MeetingHud.Instance.AsBoolFast()) MeetingHud.Instance.ResetPlayerState();
                    }
                });
        
        public static void AddRightContent(GameObject obj)
        {
            obj.transform.SetParent(MeetingHud.Instance.transform);
            obj.layer = LayerExpansion.GetUILayer();
            obj.transform.localPosition = new VVector3(4.6f, 1.4f, -40f);
        }
        
        private static readonly RemoteProcess<GamePlayer> RpcAddPhoto = new("JournalistAddPhoto", (message, _) =>
        {
            if (message.Role is Journalist.Instance journalist)
                journalist.photos++;
        });
        
        private static readonly RemoteProcess<(byte journalist, bool correct, bool noGuessed)> RpcGuessPlayer = new("JournalistGuessPlayer", (message, _) =>
        {
            var player = GamePlayer.GetPlayer(message.journalist);
            if (player == null) return;
            
            GameOperatorManager.Instance?.Run(new JournalistStatusUpdate(player, message.correct, message.noGuessed));
        });
        
        public class JournalistStatusUpdate : Virial.Events.Player.AbstractPlayerEvent
        {
            public Virial.Game.Player Journalist { get; init; }
            public bool Correct { get; init; }
            public bool NoGuessed { get; init; }

            public JournalistStatusUpdate(Virial.Game.Player journalist, bool correct, bool noGuessed) : base(journalist)
            {
                this.Journalist = journalist;
                this.Correct = correct;
                this.NoGuessed = noGuessed;
            }
        }
    }
}