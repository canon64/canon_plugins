using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private Infrastructure.ClothingQueryServer _clothingServer;
        private readonly Dictionary<string, ClothingSnapshot> _clothingSnapshots = new Dictionary<string, ClothingSnapshot>();
        private string _clothingFailureNotification;

        private void NotifyClothingFailure(string reason)
        {
            _clothingFailureNotification = MainGameNotifications.NotificationApi.Publish(GUID,
                "衣装情報の取得に失敗", reason + "\nHuman_2_KKSへ衣装JSONを渡せませんでした。",
                MainGameNotifications.NotificationSeverity.Failure, _clothingFailureNotification);
        }

        private ChaControl ResolveClothingActor(int main)
        {
            if (!SessionGate.Allowed) return null;
            var females = SessionGate.Females;
            if (females.Count > 0) return females[Math.Max(0, Math.Min(females.Count - 1, main))];
            return SessionGate.Actor;
        }

        private static string ResolveClothingCoordinateName(ChaControl actor, int index)
        {
            if (index >= 0 && index < CoordTypeJapaneseNames.Length) return CoordTypeJapaneseNames[index];
            var controller = actor.gameObject.GetComponents<UnityEngine.MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "MoreOutfitsController");
            var method = controller?.GetType().GetMethod("GetCoodinateName", BindingFlags.Public | BindingFlags.Instance);
            string name = method?.Invoke(controller, new object[] { index }) as string;
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Coordinate name missing");
            return name;
        }

        private void StartClothingServer(string pipeName, int epoch)
        {
            var context = SynchronizationContext.Current;
            if (context == null) throw new InvalidOperationException("Unity synchronization context unavailable");
            var dispatcher = new Infrastructure.ClothingQueryDispatcher(context, line =>
            {
                if (!SessionGate.Allowed || SessionGate.Epoch != epoch) throw new InvalidOperationException("Session expired");
                int main = ClothingJson.ReadMain(line);
                main = main < 0 ? Settings.TargetMainIndex : main;
                var snapshot = CaptureClothing(main);
                string reply = ClothingJson.Serialize(snapshot);
                // Store only a successfully serialized snapshot, without a timer.
                if (_clothingSnapshots.Count >= 128) _clothingSnapshots.Remove(_clothingSnapshots.Keys.First());
                _clothingSnapshots.Add(snapshot.Token, snapshot);
                return reply;
            });
            _clothingServer = new Infrastructure.ClothingQueryServer(pipeName + "_clothing", dispatcher.Query,
                error => context.Post(_ =>
            {
                if (SessionGate.Allowed && SessionGate.Epoch == epoch)
                    NotifyClothingFailure(error.Message);
            }, null));
            _clothingServer.Start();
        }

        private void StopClothingServer()
        {
            _clothingServer?.Dispose();
            _clothingServer = null;
            _clothingSnapshots.Clear();
        }

        private ClothingSnapshot CaptureClothing(int main)
        {
            ChaControl actor = ResolveClothingActor(main);
            if (actor == null || actor.nowCoordinate == null || actor.fileStatus == null) throw new InvalidOperationException("No actor");
            int coordinateIndex = TryGetCurrentCoordinateIndex(actor) ?? throw new InvalidOperationException("Coordinate type missing");
            var snapshot = new ClothingSnapshot { Token = Guid.NewGuid().ToString("N"), Main = main, Epoch = SessionGate.Epoch,
                Actor = actor, Coordinate = actor.nowCoordinate, CoordinateIndex = coordinateIndex,
                CoordinateName = ResolveClothingCoordinateName(actor, coordinateIndex) };
            for (int i = 0; i < ClothingSnapshot.Parts.Length; i++)
            {
                int id = actor.nowCoordinate.clothes.parts[i].id;
                if (!actor.IsClothes(i))
                {
                    // Missing metadata for an existing garment must not become 'unequipped'.
                    if (actor.objClothes[i] != null && actor.infoClothes[i] == null) throw new InvalidOperationException("Clothing metadata missing");
                    snapshot.Add(i, id, false, null, null);
                    continue;
                }
                string name = actor.infoClothes[i].GetInfo(ChaListDefine.KeyType.Name);
                string state;
                if (string.IsNullOrWhiteSpace(name) || !actor.GetClothesStateKind(i).TryGetValue(actor.fileStatus.clothesState[i], out state))
                    throw new InvalidOperationException("Clothing name or state missing");
                snapshot.Add(i, id, true, name, state);
            }
            foreach (int slot in FindGlassesAccessorySlots(actor))
            {
                bool visible;
                if (actor.infoAccessory == null || slot >= actor.infoAccessory.Length || actor.infoAccessory[slot] == null
                    || !TryGetAccessoryShowState(actor, slot, out visible))
                    throw new InvalidOperationException("Accessory metadata or visibility missing");
                snapshot.AddAccessory(actor.infoAccessory[slot].GetInfo(ChaListDefine.KeyType.Name), visible);
            }
            return snapshot;
        }

        private ClothingSnapshot ValidClothingSnapshot(string token, int main)
        {
            ClothingSnapshot saved;
            if (!SessionGate.Allowed || string.IsNullOrEmpty(token) || !_clothingSnapshots.TryGetValue(token, out saved)
                || saved.Epoch != SessionGate.Epoch || saved.Main != main) return null;
            ChaControl actor = ResolveClothingActor(main);
            if (actor == null || !ReferenceEquals(saved.Actor, actor) || !ReferenceEquals(saved.Coordinate, actor.nowCoordinate)) return null;
            if (TryGetCurrentCoordinateIndex(actor) != saved.CoordinateIndex) return null;
            var ids = new int[saved.Ids.Length];
            var names = new string[saved.Names.Length];
            for (int i = 0; i < saved.Ids.Length; i++)
            {
                ids[i] = actor.nowCoordinate.clothes.parts[i].id;
                names[i] = actor.IsClothes(i) ? actor.infoClothes[i]?.GetInfo(ChaListDefine.KeyType.Name) : null;
            }
            return saved.MatchesIdentity(SessionGate.Epoch, main, actor, actor.nowCoordinate, ids, names) ? saved : null;
        }
    }
}
