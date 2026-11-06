using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MainGameHLocationPlacement
{
    internal static class R
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, MemberInfo> Members = new Dictionary<string, MemberInfo>();
        internal static MemberInfo Member(Type type, string name)
        {
            string key = type.AssemblyQualifiedName + "/" + name;
            if (Members.TryGetValue(key, out var member)) return member;
            for (var current = type; current != null; current = current.BaseType)
            {
                member = (MemberInfo)current.GetField(name, Flags | BindingFlags.DeclaredOnly) ?? current.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (member != null) { Members.Add(key, member); return member; }
            }
            throw new MissingMemberException(type.FullName, name);
        }
        internal static object Get(object value, string name)
        {
            if (value == null) throw new InvalidOperationException("接続対象がありません: " + name);
            var member = Member(value.GetType(), name);
            return member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value, null);
        }
        internal static T Get<T>(object value, string name) => (T)Get(value, name);
        internal static void Set(object value, string name, object data)
        {
            var member = Member(value.GetType(), name);
            if (member is FieldInfo field) field.SetValue(value, data); else ((PropertyInfo)member).SetValue(value, data, null);
        }
        internal static MethodInfo Method(Type type, string name, params Type[] parameters)
            => type.GetMethod(name, Flags, null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);
        internal static object Call(object value, string name, params object[] args)
        {
            var type = value as Type ?? value.GetType();
            var methods = type.GetMethods(Flags).Where(m => m.Name == name && m.GetParameters().Length >= args.Length
                && m.GetParameters().Skip(args.Length).All(p => p.IsOptional)).ToArray();
            var method = methods.Single(m => m.GetParameters().Take(args.Length).Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
            var supplied = method.GetParameters().Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray();
            try { return method.Invoke(value is Type ? null : value, supplied); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
    }
}
