using GameCore;
using Nebula.Roles.Assignment;
using UnityObject = UnityEngine.Object;

namespace DHMO.Core;

public static class APICompat
{
    public static T DontDestroy<T>(this T obj) where T : UnityObject
    {
        obj.hideFlags |= HideFlags.HideAndDontSave;

        return obj.DontDestroyOnLoad();
    }

    public static T DontUnload<T>(this T obj) where T : UnityObject
    {
        obj.hideFlags |= HideFlags.DontUnloadUnusedAsset;

        return obj;
    }

    public static T DontDestroyOnLoad<T>(this T obj) where T : UnityObject
    {
        UnityObject.DontDestroyOnLoad(obj);

        return obj;
    }

    public static void Destroy(this UnityObject obj)
    {
        UnityObject.Destroy(obj);
    }

    public static void DestroyImmediate(this UnityObject obj)
    {
        UnityObject.DestroyImmediate(obj);
    }

    static public GamePlayer ToGamePlayer(this PlayerControl player) => GamePlayer.GetPlayer(player.PlayerId)!;
    public static IEnumerable<(byte playerId, List<DefinedModifier> role)>? GetPlayersModifier(this IRoleTable table)
    {
        var grouped = (table as RoleTable)?.modifiers
    .GroupBy(x => x.playerId)
    .ToDictionary(g => g.Key, g => g.Select(x => x.modifier).ToList());
        if (grouped is null) return null;
        return PlayerControl.AllPlayerControls.GetFastEnumerator().Select(p=>p.PlayerId)
            .Select(playerId => ValueTuple.Create(
                playerId,
                grouped.ContainsKey(playerId) ? grouped[playerId] : []
            ))
            .ToList();
    }
    public static IEnumerable<T> ToEnumerable<T>(this IEnumerator<T> enumerator)
    {
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }
    public static void AddValueV2(this Dictionary<byte, int> self, byte target, int num)
    {
        if (self.TryGetValue(target, out var last))
            self[target] = last + num;
        else
            self[target] = num;
    }
    public static KeyValuePair<byte, int> MaxPairV2(this Dictionary<byte, int> self, out bool tie)
    {
        tie = true;
        KeyValuePair<byte, int> result = new(PlayerVoteArea.SkippedVote, 0);
        foreach (KeyValuePair<byte, int> keyValuePair in self)
        {
            if (keyValuePair.Value > result.Value)
            {
                result = keyValuePair;
                tie = false;
            }
            else if (keyValuePair.Value == result.Value)
            {
                tie = true;
            }
        }
        return result;
    }

    static public FieldInfo? GetPrivateFieldInfo(this object instance,string fieldname)
    {
        return instance.GetType().GetField(fieldname, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public T? GetPrivateField<T>(this object instance, string fieldname)
    {
        return (T?)instance.GetPrivateFieldInfo(fieldname)?.GetValue(instance);
    }
    static public void SetPrivateField(this object instance, string fieldname,object value)
    {
        instance.GetPrivateFieldInfo(fieldname)?.SetValue(instance,value);
    }
    static public MethodInfo? GetPrivateMethodInfo(this object instance, string method)
    {
        if (instance is Type)
        {
            return (instance as Type)!.GetPrivateMethodInfoType(method);
        }
        return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateMethodInfoType(this Type type, string method)
    {
        return type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateStaticMethodInfo(this object instance, string method)
    {
        return instance.GetType().GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
    }
    static public MethodInfo? GetPrivateStaticMethodInfoType(this Type type, string method)
    {
        return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
    }
    static public T? CallPrivateMethod<T>(this object instance, string method,params object[] param)
    {
        return (T?)instance.GetPrivateMethodInfo(method)?.Invoke(instance,param);
    }
    static public T? CallPrivateStaticMethod<T>(this object instance, string method,params object[] param)
    {
        return (T?)instance.GetPrivateStaticMethodInfo(method)?.Invoke(instance, param);
    }
    static public Type? GetPrivateChildType(this Type t,string name)
    {
        return t.GetNestedType(name, BindingFlags.NonPublic);
    }
}