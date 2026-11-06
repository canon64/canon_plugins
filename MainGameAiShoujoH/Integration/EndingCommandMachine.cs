using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MainGameAiShoujoH.Integration
{
    public sealed class EndingRow { public int hash, version; public bool multi; public string command; public string[] args; }
    public sealed class EndingScript { public int personality; public string name, sourceBundle, sourceSha256, rawSha256; public EndingRow[] rows; }
    public sealed class EndingCatalog { public string sourceSha256, enumSha256; public EndingScript[] scripts; }
    public sealed class EndingCue
    {
        public string Speaker, Text;
        public readonly List<string[]> Voices = new List<string[]>(), Expressions = new List<string[]>(), Motions = new List<string[]>();
    }
    // The saved category10 scripts use exactly these 18 commands. Progress advances only
    // through script execution or a presentation completion; no Unity frame callback owns it.
    public sealed class EndingCommandMachine : IDisposable
    {
        public interface IHost
        {
            void Present(EndingCue cue, Action completed, Action<Exception> failed);
            void ActorCommand(string command, string[] arguments);
        }
        readonly IHost host;
        readonly Func<int, int, int> random;
        readonly Dictionary<string, object> vars;
        EndingScript script;
        EndingCue cue = new EndingCue();
        Action completed; Action<Exception> failed;
        int line; bool pumping, waiting, disposed; object generation;
        public IReadOnlyDictionary<string, object> Variables => vars;
        public EndingCommandMachine(IHost host, IDictionary<string, object> variables, Func<int, int, int> random)
        { this.host = host ?? throw new ArgumentNullException(nameof(host)); vars = new Dictionary<string, object>(variables); this.random = random ?? throw new ArgumentNullException(nameof(random)); }
        public void Start(EndingScript value, Action done, Action<Exception> error)
        {
            if (disposed || script != null) throw new InvalidOperationException("Ending machine is unavailable or already running.");
            script = value ?? throw new ArgumentNullException(nameof(value)); completed = done; failed = error; generation = new object(); line = 0; Pump();
        }
        object Value(string text)
        {
            if (vars.TryGetValue(text, out var value)) return value;
            if (bool.TryParse(text, out bool boolean)) return boolean;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) return integer;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) return number;
            return text;
        }
        static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
        void Jump(string target)
        {
            target = Convert.ToString(Value(target), CultureInfo.InvariantCulture);
            var candidates = Enumerable.Range(0, script.rows.Length).Where(i => script.rows[i].command == "Tag" && script.rows[i].args.FirstOrDefault() == target).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("Ending tag is missing: " + target);
            // Native TextScenario.SearchTag scans commandPacks from its first row.
            line = candidates[0] + 1;
        }
        void Pump()
        {
            if (pumping || disposed || waiting || script == null) return;
            pumping = true;
            try
            {
                int budget = checked(script.rows.Length * 64);
                while (!disposed && script != null && !waiting)
                {
                    if (--budget < 0) throw new InvalidOperationException("Ending script has a non-progressing control loop.");
                    if (line >= script.rows.Length) throw new InvalidOperationException("Ending script ended without Close/Receive.");
                    var row = script.rows[line++]; var a = row.args ?? Array.Empty<string>();
                    switch (row.command)
                    {
                        case "Tag": break;
                        case "VAR":
                            string text = a[random(2, a.Length)]; int depth = 0;
                            while (text.StartsWith("*", StringComparison.Ordinal)) { ++depth; text = text.Substring(1); }
                            object v = text; while (depth-- > 0) v = vars[Convert.ToString(v, CultureInfo.InvariantCulture)];
                            var type = Type.GetType(a[0], true); vars[a[1]] = Convert.ChangeType(v, type, CultureInfo.InvariantCulture); break;
                        case "RandomVar":
                            // Saved native RandomVar adds one to the integer upper bound.
                            vars[a[1]] = random(Convert.ToInt32(Value(a[2])), checked(Convert.ToInt32(Value(a[3])) + 1)); break;
                        case "Calc":
                            double left = vars.TryGetValue(a[0], out var old) ? Number(old) : 0;
                            double right = Number(Value(a[2])); int op = int.Parse(a[1], CultureInfo.InvariantCulture);
                            double answer = op == 0 ? right : op == 1 ? left + right : op == 2 ? left - right : op == 3 ? left * right : op == 4 ? left / right : throw new NotSupportedException("Ending Calc operator " + op);
                            vars[a[0]] = old is int ? (object)checked((int)answer) : (float)answer; break;
                        case "IF":
                            object l = Value(a[0]), r = Value(a[2]); int compare = int.Parse(a[1], CultureInfo.InvariantCulture);
                            bool match = compare == 0 ? string.Equals(Convert.ToString(l), Convert.ToString(r), StringComparison.OrdinalIgnoreCase)
                                : compare == 1 ? !string.Equals(Convert.ToString(l), Convert.ToString(r), StringComparison.OrdinalIgnoreCase)
                                : compare == 2 ? Number(l) > Number(r) : compare == 3 ? Number(l) < Number(r)
                                : compare == 4 ? Number(l) >= Number(r) : compare == 5 ? Number(l) <= Number(r) : vars.ContainsKey(a[0]);
                            string branch = match ? a[3] : a[4]; if (!string.IsNullOrEmpty(branch)) Jump(branch); break;
                        case "Switch":
                            string key = Convert.ToString(Value(a[0]), CultureInfo.InvariantCulture);
                            foreach (string item in a.Skip(1)) { var pair = item.Split(','); if (pair[0] == key) { Jump(pair[1]); break; } } break;
                        case "Jump": Jump(a[0]); break;
                        case "Prob": Jump(random(0, 100) < int.Parse(a[0], CultureInfo.InvariantCulture) ? a[1] : a[2]); break;
                        case "Probs":
                            int roll = random(0, 100), cumulative = 0; bool chosen = false;
                            foreach (string item in a) { var pair = item.Split(','); cumulative += int.Parse(pair[0], CultureInfo.InvariantCulture); if (roll < cumulative) { Jump(pair[1]); chosen = true; break; } }
                            if (!chosen) throw new InvalidOperationException("Ending Probs table does not cover its random value."); break;
                        case "Voice": cue.Voices.Add(a); break;
                        case "Expression": cue.Expressions.Add(a); break;
                        case "CharaMotion": case "CharaExpression": case "CharaLookEyes": case "CharaLookNeck": case "CharaVoiceStop": host.ActorCommand(row.command, a); break;
                        case "Text":
                            cue.Speaker = a[0]; cue.Text = a[1]; var presenting = cue; cue = new EndingCue();
                            waiting = true; object token = generation; bool accepted = false;
                            host.Present(presenting, () => { if (accepted || disposed || !ReferenceEquals(token, generation)) return; accepted = true; waiting = false; Pump(); }, ex => { if (!accepted && !disposed && ReferenceEquals(token, generation)) { accepted = true; Fail(ex); } });
                            break;
                        case "Close":
                            var done = completed; script = null; generation = null; completed = null; failed = null; done?.Invoke(); break;
                        default: throw new NotSupportedException("Ending command has no implementation: " + row.command);
                    }
                }
            }
            catch (Exception ex) { Fail(ex); }
            finally { pumping = false; }
            // A presentation may complete synchronously; the while loop already continues it.
        }
        void Fail(Exception error) { var callback = failed; script = null; generation = null; completed = null; failed = null; waiting = false; callback?.Invoke(error); }
        public void Dispose() { disposed = true; generation = null; script = null; completed = null; failed = null; waiting = false; }
    }
}
