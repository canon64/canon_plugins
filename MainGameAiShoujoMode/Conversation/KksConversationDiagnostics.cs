using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace MainGameAiShoujoMode.Conversation
{
    // Snapshot at explicit conversation boundaries; never inspect the other plugin each frame.
    internal static class KksConversationDiagnostics
    {
        internal static void KsPlugContext(string phase, Action<string> log)
        {
            if (log == null) return;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "ksPlug");
                if (assembly == null) return;
                var core = assembly.GetType("ksPlug.Core", throwOnError: true);
                var dealer = Read(core, null, "dealer");
                if (dealer == null) { log("KKS_CONVERSATION_KSPLUG_CONTEXT phase=" + phase + " dealer=missing"); return; }
                var type = dealer.GetType(); var info = Read(type, dealer, "eventInfo");
                var actor = Read(type, dealer, "EventCha") as UnityEngine.Object;
                log("KKS_CONVERSATION_KSPLUG_CONTEXT phase=" + phase + " mvid=" + assembly.ManifestModule.ModuleVersionId
                    + " context=" + Read(type, dealer, "Ctx") + " inStory=" + Read(type, dealer, "InStory")
                    + " eventActor=" + (actor != null) + " eventInfo=" + (info != null)
                    + " saves=" + Count(info, "saves") + " actors=" + Count(info, "chas"));
            }
            catch (Exception exception) { log("KKS_CONVERSATION_KSPLUG_DIAGNOSTIC_ERROR phase=" + phase + " " + exception.GetBaseException().Message); }
        }
        private static string Count(object value, string name)
        {
            if (value == null) return "missing";
            var list = Read(value.GetType(), value, name) as ICollection;
            return list == null ? "missing" : list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        private static object Read(Type type, object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(instance);
            var property = type.GetProperty(name, flags);
            if (property != null) return property.GetValue(instance, null);
            throw new MissingMemberException(type.FullName, name);
        }
    }
}
