using System;
using System.Linq;
using MainGameAiShoujoH.Results;
using MainGameAiShoujoMode.Life;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeSession
    {
        private float ReadDedicatedHParameter(string participant, int parameter)
        {
            var state = _life.Store.Read().residents.Single(r => r.id == participant);
            var tags = _settings.T("dedicatedH.resultTags").Split(',');
            if (parameter >= _settings.I("dedicatedH.desireOffset"))
            {
                int desire = parameter - _settings.I("dedicatedH.desireOffset");
                if (desire >= _settings.I("dedicatedH.desireCount")) throw new ArgumentException("原本欲求keyの範囲外です。");
                return state.nativeDesires != null && state.nativeDesires.TryGetValue(desire, out var value) ? value : default(float);
            }
            switch (tags[parameter])
            {
                case "temperature": return state.temperature;
                case "mood": return DedicatedNativeMood(participant);
                case "fullness": return state.fullness;
                case "health": return state.health;
                case "vitality": return state.nativeVitality ?? _life.Rules.maximum;
                case "motivation": return state.motivation;
                case "erotic": return state.erotic;
                case "morality": return state.nativeMorality ?? _settings.I("dedicatedH.moralityDefault");
                default: throw new ArgumentException("未定義の原本状態keyです。");
            }
        }
        private static float DedicatedNativeMood(string participant) => MainGameAiShoujoH.Plugin.ReadResidentProfile(participant).Mood;
        private ResultReceipt ApplyDedicatedHResult(ResultDelta delta)
        {
            if (delta == null || string.IsNullOrWhiteSpace(delta.OperationId)) throw new ArgumentException("H結果の操作IDがありません。");
            var resident = _conversation?.Resident;
            var active = MainGameAiShoujoH.Plugin.ReadState();
            if (resident == null || active?.Participants == null || !active.Participants.Any(p => p.StartsWith(delta.ParticipantId + ":", StringComparison.Ordinal)))
                return new ResultReceipt { Error = "対象AI住人の同一性を確認できません。" };
            MainGameAiShoujoH.Plugin.ApplyResidentVoiceResult(delta);
            var world = _life.Store.Read();
            float amount = delta.Assignment ? delta.AssignedValue ?? delta.Amount : delta.Amount;
            if (float.IsNaN(amount) || float.IsInfinity(amount)) throw new ArgumentException("H結果が有限値ではありません。");
            string intent = delta.ParticipantId + ":" + delta.Parameter + ":" + amount.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + (delta.Assignment ? ":set" : "");
            var previous = world.operations.SingleOrDefault(r => r.request.id == delta.OperationId);
            if (previous != null)
            {
                if (previous.request.connectionIntent != intent) throw new InvalidOperationException("H結果の操作IDが別の結果に再利用されました。");
                return new ResultReceipt { Applied = true, Saved = true };
            }
            var state = world.residents.Single(r => r.id == delta.ParticipantId);
            int flavorGrowth = default(int);
            float Add(float value) => Math.Max(_life.Rules.minimum, Math.Min(_life.Rules.maximum, (delta.Assignment ? default(float) : value) + amount));
            var tags = _settings.T("dedicatedH.resultTags").Split(',');
            string tag = delta.Parameter >= default(int) && delta.Parameter < tags.Length ? tags[delta.Parameter] : "unsupported";
            switch (tag)
            {
                case "temperature": state.temperature = Add(state.temperature); break;
                case "mood": AiNativeHResultState.SetNativeMood(state, MainGameAiShoujoH.Plugin.ReadResidentProfile(delta.ParticipantId).Mood); break;
                case "fullness": state.fullness = Add(state.fullness); break;
                case "health": state.health = Add(state.health); break;
                case "vitality": state.nativeVitality = Add(state.nativeVitality ?? _life.Rules.maximum); break;
                case "morality": state.nativeMorality = Add(state.nativeMorality ?? _settings.I("dedicatedH.moralityDefault")); break;
                case "motivation": state.motivation = Add(state.motivation); break;
                case "erotic": state.erotic = Add(state.erotic); break;
                default:
                    int desire = delta.Parameter - _settings.I("dedicatedH.desireOffset");
                    if (desire >= default(int) && desire < _settings.I("dedicatedH.desireCount"))
                    {
                        if (state.nativeDesires == null) state.nativeDesires = new System.Collections.Generic.Dictionary<int, float>();
                        // Native SetParamator uses SetDesire(sum), rather than adding to the old desire.
                        state.nativeDesires[desire] = Math.Max(_life.Rules.minimum, Math.Min(_life.Rules.maximum, amount));
                        break;
                    }
                    int index = delta.Parameter - Array.IndexOf(tags, "flavor0");
                    if (!tag.StartsWith("flavor", StringComparison.Ordinal) || index < default(int) || index >= state.flavor.Length)
                        return new ResultReceipt { Error = "AI住人に未接続の原本HTag: " + delta.Parameter };
                    state.flavor[index] = checked((int)Math.Max(default(long), Math.Min(_life.Rules.flavorMaximum, (long)state.flavor[index] + delta.Amount)));
                    state.totalFlavor = checked((int)Math.Max(default(long), (long)state.totalFlavor + delta.Amount));
                    state.flavorAdditionAmount = checked((int)Math.Max(default(long), (long)state.flavorAdditionAmount + delta.Amount));
                    flavorGrowth = delta.Amount;
                    break;
            }
            _life.Store.Commit(world, new AiLifeStateStore.Request { id = delta.OperationId, action = "state", resident = state,
                connectionKey = "dedicated-h", connectionIntent = intent, growthDelta = flavorGrowth, growthMaximum = _life.Rules.flavorMaximum }, out _, out _);
            // Read back the durable operation rather than treating the in-memory mutation as a save.
            bool saved = _life.Store.Read().operations.Any(r => r.request.id == delta.OperationId && r.request.connectionIntent == intent);
            if (resident.ResidentId == delta.ParticipantId) resident.Refresh();
            return new ResultReceipt { Applied = saved, Saved = saved, Error = saved ? null : "H結果を保存後に確認できません。" };
        }
    }
}
