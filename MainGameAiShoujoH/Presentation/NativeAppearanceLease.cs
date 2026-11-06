using System;
using System.Collections.Generic;
using MainGameAiShoujoH.Contracts;

namespace MainGameAiShoujoH.Presentation
{
    internal sealed class NativeAppearanceLease : IDisposable
    {
        private sealed class Saved
        {
            internal Participant Participant; internal byte[] Clothes, Siru; internal bool[] Accessories;
            internal bool ChangedClothes, ChangedAccessories, ChangedSiru;
        }
        private readonly Dictionary<string, Saved> actors = new Dictionary<string, Saved>(StringComparer.Ordinal);
        private readonly SessionContext context;
        private bool completed, disposed;
        internal NativeAppearanceLease(SessionContext context)
        {
            this.context = context;
            foreach (var participant in context.Request.Participants)
            {
                var status = participant.Character.fileStatus;
                actors.Add(participant.Identity, new Saved { Participant = participant, Clothes = (byte[])status.clothesState.Clone(), Accessories = (bool[])status.showAccessory.Clone(), Siru = (byte[])status.siruLv.Clone() });
            }
        }
        private Saved Actor(string identity)
        {
            if (disposed || context.CanInteract == null || !context.CanInteract()) throw new InvalidOperationException("Native H appearance input is inactive.");
            if (!actors.TryGetValue(identity, out var actor)) throw new ArgumentException("Unknown session participant."); return actor;
        }
        internal void NextClothes(string identity, int part)
        {
            var saved = Actor(identity); var actor = saved.Participant.Character;
            if (saved.Participant.Role == "male" || part < 0 || part >= saved.Clothes.Length || !actor.IsClothes(part)) throw new ArgumentException("Clothing slot is unavailable.");
            if (saved.Participant.IsPlayer && !context.Request.Settings.PlayerClothesEnabled) throw new InvalidOperationException("専用H設定で主人公の個別衣服操作は無効です。");
            saved.ChangedClothes = true; actor.SetClothesStateNext(part);
        }
        internal void AllClothes(string identity, int state)
        {
            var saved = Actor(identity); if (saved.Participant.Role == "male" || state < 0 || state > 2) throw new ArgumentException("Clothing action is unavailable.");
            saved.ChangedClothes = true;
            for (int i = 0; i < saved.Clothes.Length; i++) if (saved.Participant.Character.IsClothes(i)) saved.Participant.Character.SetClothesState(i, (byte)state);
        }
        internal void Accessory(string identity, int slot, bool visible)
        {
            var saved = Actor(identity); if (slot < 0 || slot >= saved.Accessories.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            CheckAccessories(saved); saved.ChangedAccessories = true; saved.Participant.Character.SetAccessoryState(slot, visible);
        }
        internal void AllAccessories(string identity, bool visible)
        {
            var saved = Actor(identity); CheckAccessories(saved); saved.ChangedAccessories = true;
            for (int i = 0; i < saved.Accessories.Length; i++) saved.Participant.Character.SetAccessoryState(i, visible);
        }
        private void CheckAccessories(Saved saved)
        {
            if ((saved.Participant.IsPlayer || saved.Participant.Role == "male") && !context.Request.Settings.PlayerAccessoriesEnabled)
                throw new InvalidOperationException("専用H設定で主人公/男性のアクセサリ操作は無効です。");
        }
        internal void ToggleSiru(string identity)
        {
            var saved = Actor(identity); var actor = saved.Participant.Character;
            if (!actor.visibleAll || actor.objBody == null) return;
            bool has = false; foreach (var value in actor.fileStatus.siruLv) has |= value != 0;
            saved.ChangedSiru = true;
            for (int i = 0; i < saved.Siru.Length; i++) actor.SetSiruFlags((ChaFileDefine.SiruParts)i, (byte)(has ? 0 : 2));
        }
        internal void Complete() { completed = true; }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var saved in actors.Values)
            {
                var actor = saved.Participant.Character; if (actor == null) continue;
                void Restore(Action action) { try { action(); } catch (Exception ex) { context.ReportFailure("AI H appearance restore: " + ex.Message); } }
                if (completed) continue;
                if (saved.ChangedClothes) for (int i = 0; i < saved.Clothes.Length; i++) { int slot = i; Restore(() => actor.SetClothesState(slot, saved.Clothes[slot])); }
                if (saved.ChangedAccessories) for (int i = 0; i < saved.Accessories.Length; i++) { int slot = i; Restore(() => actor.SetAccessoryState(slot, saved.Accessories[slot])); }
                if (saved.ChangedSiru) for (int i = 0; i < saved.Siru.Length; i++) { int slot = i; Restore(() => actor.SetSiruFlags((ChaFileDefine.SiruParts)slot, saved.Siru[slot])); }
            }
            actors.Clear();
        }
    }
}
