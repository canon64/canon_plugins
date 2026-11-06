using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private void SaveRoomActorsAndPreferences(HotelRoomSaveBatch batch)
        {
            foreach (bool female in new[] { true, false })
            {
                string item = female ? "女性衣装・外見" : "男性衣装・外見";
                batch.Item(item, female ? "appearance.female" : "appearance.male", () =>
                {
                    var actor = _hSession.RoomActors().FirstOrDefault(a => (a.sex != default(byte)) == female);
                    if (actor == null) throw new InvalidOperationException("保存対象のキャラが見つかりません。");
                    return HotelRoomAppearanceRuntime.Encode(actor);
                }, card => _roomStateStore.SaveActor(_roomSource, female, card), card =>
                {
                    var status = MessagePack.MessagePackSerializer.Deserialize<ChaFileStatus>(HotelRoomAppearanceRuntime.ReadStatusBytes(card));
                    return "cardChars=" + card.Length + " coordinate=" + status.coordinateType + " clothes=" + string.Join(",", status.clothesState)
                        + " tears=" + status.tearsLv + " tongue=" + status.tongueState;
                });
            }
            HotelRoomActorEffects.Save(_hSession, _roomStateStore, batch, _roomSource);
            foreach (string key in _roomControls.preferences ?? Array.Empty<string>())
            {
                batch.Item("H設定:" + key, "appearance.preferences[key=" + key + "]", () =>
                {
                    var field = typeof(HFlag).GetField(key, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || !(field.FieldType.IsPrimitive || field.FieldType.IsEnum))
                        throw new InvalidOperationException("保存対象のH設定フィールドがありません。");
                    if (_hSession.RoomFlags == null) throw new InvalidOperationException("H設定を取得できません。");
                    return new HotelRoomPreference { key = key, value = Convert.ToString(field.GetValue(_hSession.RoomFlags), CultureInfo.InvariantCulture) };
                }, value => _roomStateStore.SavePreference(_roomSource, value), value => value.value);
            }
        }
    }
}
