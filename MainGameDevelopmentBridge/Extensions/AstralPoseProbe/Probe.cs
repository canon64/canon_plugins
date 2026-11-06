using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameDevelopmentBridge;

public sealed class AstralPoseProbe : IDevelopmentCommand
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public string Name => "astral-pose-probe";
    readonly List<Tuple<IList, object, int>> removed = new List<Tuple<IList, object, int>>();
    static object Get(object target, string name)
    {
        var type = target as Type ?? target.GetType();
        var field = type.GetField(name, Flags);
        return field != null ? field.GetValue(target is Type ? null : target)
            : type.GetProperty(name, Flags).GetValue(target is Type ? null : target, null);
    }
    static object Pose(object pose) => pose == null ? null : new { id = Get(pose, "id"), mode = Get(pose, "mode").ToString(), name = Get(pose, "nameAnimation") };
    static object[] Bench(object proc, string field)
    {
        var lists = (IEnumerable)Get(proc, field);
        return lists.Cast<IEnumerable>().Where(list => list != null).SelectMany(list => list.Cast<object>())
            .Where(p => p != null && Convert.ToString(Get(p, "nameAnimation")).Contains("ベンチフェラ")).Select(Pose).ToArray();
    }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var type = ReflectionCommands.ResolveType("AstralMode.Plugin");
        var plugin = Get(type, "Instance");
        var proc = Get(type, "_hSceneProc");
        string action = ReflectionCommands.Text(arguments, "action", "sample");
        if (action == "mask-filtered-bench")
        {
            if (Get(plugin, "_state").ToString() != "Normal" || removed.Count != 0)
                throw new InvalidOperationException("Mask requires a normal, unmasked H session");
            foreach (IList list in (IEnumerable)Get(proc, "lstUseAnimInfo"))
                if (list != null)
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (Convert.ToString(Get(list[i], "nameAnimation")).Contains("ベンチフェラ"))
                        { removed.Add(Tuple.Create(list, list[i], i)); list.RemoveAt(i); }
        }
        if (action == "restore-filtered-bench") Dispose();
        if (action == "enter" || action == "exit")
        {
            if (plugin == null || proc == null) throw new InvalidOperationException("Astral H session is not ready");
            string state = Get(plugin, "_state").ToString();
            if (state != (action == "enter" ? "Normal" : "Astral")) throw new InvalidOperationException("Unexpected state: " + state);
            var routine = type.GetMethod(action == "enter" ? "EnterAstralCoroutine" : "ExitAstralCoroutine", Flags).Invoke(plugin, null);
            type.GetMethod("StartCoroutine", new[] { typeof(IEnumerator) }).Invoke(plugin, new[] { routine });
        }
        var flags = proc == null ? null : Get(proc, "flags");
        return new {
            state = plugin == null ? null : Get(plugin, "_state").ToString(),
            hProc = proc != null,
            pose = flags == null ? null : Pose(Get(flags, "nowAnimationInfo")),
            pending = flags == null ? null : Pose(Get(flags, "selectAnimationListInfo")),
            voiceWait = flags == null ? null : Get(flags, "voiceWait"),
            motion = flags == null ? null : Get(flags, "nowAnimStateName"),
            filteredBench = proc == null ? null : Bench(proc, "lstUseAnimInfo"),
            allBench = proc == null ? null : Bench(proc, "lstAnimInfo")
        };
    }
    public void Dispose()
    {
        for (int i = removed.Count - 1; i >= 0; i--)
        {
            var entry = removed[i];
            entry.Item1.Insert(Math.Min(entry.Item3, entry.Item1.Count), entry.Item2);
        }
        removed.Clear();
    }
}
