using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    // Registration half of HSceneProc.CreateCharacter only. The owned engine
    // must separately initialize native IK/hand/face/voice/layer dependencies.
    // Dispose BEFORE destroying the proc: its actor lists must no longer own
    // borrowed scene actors when native teardown runs.
    internal sealed class NativeKksActorBinding : IDisposable
    {
        static readonly FieldInfo FemaleField = RequiredField("lstFemale", typeof(List<ChaControl>));
        static readonly FieldInfo MaleField = RequiredField("male", typeof(ChaControl));
        static readonly PropertyInfo[] RootProperties = new[] { "root", "charaBase", "chaCtrl" }
            .Select(name => typeof(SaveData.CharaData).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(SaveData.CharaData).FullName, name)).ToArray();
        readonly HSceneProc proc;
        readonly HFlag flags;
        readonly Func<bool> isOwned;
        readonly List<Action> restore = new List<Action>();
        readonly ChaControl[] females;
        readonly ChaControl male;
        bool disposed;

        internal NativeKksActorBinding(HSceneProc ownedProc, HFlag ownedFlags, Func<bool> isOwned,
            ChaControl[] females, SaveData.Heroine[] heroines, ChaControl male, SaveData.Player player)
        {
            proc = ownedProc ?? throw new ArgumentNullException(nameof(ownedProc));
            flags = ownedFlags ?? throw new ArgumentNullException(nameof(ownedFlags));
            this.isOwned = isOwned ?? throw new ArgumentNullException(nameof(isOwned));
            this.females = females?.ToArray() ?? throw new ArgumentNullException(nameof(females));
            this.male = male;
            if (!isOwned() || proc.enabled || proc.flags != flags)
                throw new InvalidOperationException("Native actor registration requires the disabled owned proc and its exact flags.");
            if (heroines == null || females.Length == default(int) || females.Length != heroines.Length || player == null || male == null)
                throw new ArgumentException("Native actor and save-data role counts differ.");
            var actors = this.females.Concat(new[] { male }).ToArray();
            if (actors.Any(a => a == null || a.animBody == null || a.fileStatus == null) || actors.Distinct().Count() != actors.Length
                || this.females.Any(a => a.sex == default(byte)) || male.sex != default(byte)
                || heroines.Any(h => h == null) || heroines.Distinct().Count() != heroines.Length)
                throw new InvalidOperationException("Native actor identities, loaded bodies or role sexes are invalid.");
            // Capture every dependency before making the first mutation.
            var oldFemales = FemaleField.GetValue(proc);
            var oldMale = MaleField.GetValue(proc);
            var oldHeroines = flags.lstHeroine;
            var oldPlayer = flags.player;
            restore.Add(() => FemaleField.SetValue(proc, oldFemales));
            restore.Add(() => MaleField.SetValue(proc, oldMale));
            restore.Add(() => flags.lstHeroine = oldHeroines);
            restore.Add(() => flags.player = oldPlayer);
            foreach (var actor in actors) CaptureActor(actor);
            foreach (var heroine in heroines) CaptureRoot(heroine);
            CaptureRoot(player);
            try
            {
                FemaleField.SetValue(proc, this.females.ToList());
                MaleField.SetValue(proc, male);
                flags.lstHeroine = heroines.ToList(); flags.player = player;
                for (int i = default(int); i < heroines.Length; ++i) BindRoot(heroines[i], this.females[i]);
                BindRoot(player, male);
                Validate();
            }
            catch (Exception failure)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Native actor binding and rollback failed", failure, cleanup); }
                throw;
            }
        }

        internal void Validate()
        {
            if (disposed || !isOwned() || proc == null || proc.flags != flags
                || !ReferenceEquals(MaleField.GetValue(proc), male)
                || !((List<ChaControl>)FemaleField.GetValue(proc)).SequenceEqual(females)
                || flags.lstHeroine.Count != females.Length || flags.player.chaCtrl != male)
                throw new InvalidOperationException("Native borrowed actor registration ownership changed.");
            for (int i = default(int); i < females.Length; ++i)
                if (flags.lstHeroine[i].chaCtrl != females[i]) throw new InvalidOperationException("Native heroine actor identity changed.");
        }

        void CaptureRoot(SaveData.CharaData data)
        {
            foreach (var property in RootProperties)
            {
                var setter = property.GetSetMethod(true) ?? throw new MissingMethodException("Native root setter: " + property.Name);
                var value = property.GetValue(data, null);
                restore.Add(() => setter.Invoke(data, new[] { value }));
            }
        }
        static void BindRoot(SaveData.CharaData data, ChaControl actor)
        {
            data.SetRoot(null); // Native SetRoot(non-Base) otherwise retains a stale charaBase.
            data.SetRoot(actor.gameObject);
            if (data.chaCtrl != actor) throw new InvalidOperationException("Native SetRoot resolved a different actor.");
        }
        void CaptureActor(ChaControl actor)
        {
            var root = actor.transform; var parent = root.parent; var sibling = root.GetSiblingIndex();
            var position = root.localPosition; var rotation = root.localRotation; var scale = root.localScale;
            var visible = actor.visibleAll;
            restore.Add(() =>
            {
                if (actor == null) throw new InvalidOperationException("Borrowed native actor was destroyed.");
                root.SetParent(parent, false); root.SetSiblingIndex(sibling);
                root.localPosition = position; root.localRotation = rotation; root.localScale = scale;
            });
            restore.Add(() => actor.visibleAll = visible);
            var status = actor.fileStatus;
            var coordinate = status.coordinateType;
            var clothes = (byte[])status.clothesState.Clone();
            var accessories = (bool[])status.showAccessory.Clone();
            var siru = (byte[])status.siruLv.Clone();
            void RequireCoordinate()
            {
                if (actor.fileStatus.coordinateType != coordinate)
                    throw new InvalidOperationException("Borrowed actor coordinate changed; registration does not reload user clothing.");
            }
            for (int i = default(int); i < clothes.Length; ++i)
            { int slot = i; restore.Add(() => { RequireCoordinate(); actor.SetClothesState(slot, clothes[slot], true); }); }
            for (int i = default(int); i < accessories.Length; ++i)
            { int slot = i; restore.Add(() => { RequireCoordinate(); actor.SetAccessoryState(slot, accessories[slot]); }); }
            for (int i = default(int); i < siru.Length; ++i)
            { int slot = i; restore.Add(() => actor.SetSiruFlags((ChaFileDefine.SiruParts)slot, siru[slot])); }
            // Preserve expanded accessory arrays rather than serializing them to
            // the native twenty-slot card representation. Never write a card.
            foreach (var property in typeof(ChaFileStatus).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != default(int)) continue;
                var value = property.GetValue(status, null);
                if (value is Array array) value = array.Clone();
                else if (value != null && !value.GetType().IsValueType && !(value is string) && !(value is Version))
                    throw new NotSupportedException("Native status snapshot needs an explicit clone: " + property.Name);
                var saved = value;
                restore.Add(() =>
                {
                    if (!ReferenceEquals(actor.fileStatus, status)) throw new InvalidOperationException("Borrowed actor fileStatus identity changed.");
                    RequireCoordinate();
                    property.SetValue(status, saved is Array a ? a.Clone() : saved, null);
                });
            }
        }
        static FieldInfo RequiredField(string name, Type type)
        {
            var field = typeof(HSceneProc).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != type) throw new MissingFieldException(typeof(HSceneProc).FullName, name);
            return field;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var failures = new List<Exception>();
            // Detach proc lists first, even if later actor restoration fails.
            foreach (var action in restore)
                try { action(); } catch (Exception error) { failures.Add(error); }
            restore.Clear();
            if (failures.Count != default(int)) throw new AggregateException("Native borrowed actor restoration failed", failures);
        }
    }
}
