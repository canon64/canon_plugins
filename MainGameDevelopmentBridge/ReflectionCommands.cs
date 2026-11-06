using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MainGameDevelopmentBridge
{
    // Extensions run inside KKS on Unity's main thread and can use any installed managed API.
    public interface IDevelopmentCommand : IDisposable
    {
        string Name { get; }
        object Execute(Dictionary<string, object> arguments, DevelopmentContext context);
    }
    public sealed class DevelopmentContext
    {
        public ReflectionCommands Reflection { get; internal set; }
        public string OutputDirectory { get; internal set; }
    }
    public sealed class ReflectionCommands
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private readonly Dictionary<string, object> _objects = new Dictionary<string, object>();
        private readonly Dictionary<string, IDevelopmentCommand> _extensions = new Dictionary<string, IDevelopmentCommand>();
        private readonly BridgeJson _json = new BridgeJson();
        public DevelopmentContext Context { get; } = new DevelopmentContext();
        public ReflectionCommands() { Context.Reflection = this; }
        public static string Text(Dictionary<string, object> request, string key, string fallback = null)
            => request.TryGetValue(key, out var value) ? Convert.ToString(value) : fallback;
        public static Type ResolveType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("type is required");
            return Type.GetType(name, false) ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null)
                ?? throw new TypeLoadException(name);
        }
        public object Resolve(string handle) => _objects.TryGetValue(handle, out var value) ? value : throw new KeyNotFoundException("Unknown or expired handle: " + handle);
        public object Pack(object value)
        {
            if (value == null || value is string || value is decimal || value.GetType().IsPrimitive) return value;
            if (value is Enum) return value.ToString();
            string handle = System.Guid.NewGuid().ToString("N"); _objects.Add(handle, value);
            return new { handle, type = value.GetType().AssemblyQualifiedName };
        }
        private object ConvertArgument(object value, Type type)
        {
            if (value is Dictionary<string, object> reference && reference.ContainsKey("handle")) value = Resolve(Text(reference, "handle"));
            if (value == null || type.IsInstanceOfType(value)) return value;
            if (type == typeof(Type)) return ResolveType(Convert.ToString(value));
            if (type.IsEnum) return Enum.Parse(type, Convert.ToString(value));
            return _json.Deserialize(_json.Serialize(value), type);
        }
        private static MemberInfo Member(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var member = (MemberInfo)current.GetField(name, Flags | BindingFlags.DeclaredOnly) ?? current.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (member != null) return member;
            }
            throw new MissingMemberException(type.FullName, name);
        }
        public object Execute(Dictionary<string, object> request)
        {
            string op = Text(request, "op");
            if (op == "notifications") return NotificationSnapshot.Read();
            if (op == "assemblies") return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName).ToArray();
            if (op == "release") { _objects.Remove(Text(request, "handle")); return true; }
            if (op == "extensions") return _extensions.Keys.ToArray();
            if (op == "load-extension")
            {
                var assembly = Assembly.Load(System.IO.File.ReadAllBytes(Text(request, "path")));
                var providers = assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IDevelopmentCommand).IsAssignableFrom(t)).ToArray();
                if (providers.Length == default(int)) throw new InvalidOperationException("No IDevelopmentCommand implementation");
                foreach (var type in providers)
                {
                    var extension = (IDevelopmentCommand)Activator.CreateInstance(type);
                    if (_extensions.TryGetValue(extension.Name, out var previous)) previous.Dispose();
                    _extensions[extension.Name] = extension;
                }
                return _extensions.Keys.ToArray();
            }
            if (_extensions.TryGetValue(op ?? "", out var command)) return command.Execute(request, Context);
            object target = request.ContainsKey("handle") ? Resolve(Text(request, "handle")) : null;
            Type targetType = target?.GetType() ?? ResolveType(Text(request, "type"));
            if (op == "members") return targetType.GetMembers(Flags).Select(m => new { name = m.Name, kind = m.MemberType.ToString(), signature = m.ToString() }).ToArray();
            if (op == "get" || op == "set")
            {
                var member = Member(targetType, Text(request, "member"));
                var field = member as FieldInfo; var property = member as PropertyInfo;
                if (op == "get") return Pack(field != null ? field.GetValue(target) : property.GetValue(target, null));
                object converted = ConvertArgument(request["value"], field?.FieldType ?? property.PropertyType);
                if (field != null) field.SetValue(target, converted); else property.SetValue(target, converted, null);
                return true;
            }
            var arguments = request.TryGetValue("args", out var args) ? ((IEnumerable)args).Cast<object>().ToArray() : Array.Empty<object>();
            var parameterTypes = request.TryGetValue("parameterTypes", out var types) ? ((IEnumerable)types).Cast<object>().Select(v => ResolveType(Convert.ToString(v))).ToArray() : null;
            MethodBase[] candidates = op == "new" ? targetType.GetConstructors(Flags).Cast<MethodBase>().ToArray()
                : targetType.GetMethods(Flags).Where(m => m.Name == Text(request, "member") && !m.ContainsGenericParameters).Cast<MethodBase>().ToArray();
            candidates = candidates.Where(m => m.GetParameters().Length == arguments.Length && (parameterTypes == null || m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes))).ToArray();
            if (op != "invoke" && op != "new") throw new ArgumentException("Unknown operation: " + op);
            if (candidates.Length != Convert.ToInt32(true)) throw new AmbiguousMatchException("Specify exact parameterTypes; matching methods=" + candidates.Length);
            var method = candidates.Single();
            var values = method.GetParameters().Select((p, index) => ConvertArgument(arguments[index], p.ParameterType)).ToArray();
            return Pack(method is ConstructorInfo constructor ? constructor.Invoke(values) : ((MethodInfo)method).Invoke(target, values));
        }
        public void Clear()
        {
            foreach (var command in _extensions.Values) { try { command.Dispose(); } catch { } }
            _extensions.Clear(); _objects.Clear();
        }
    }
}
