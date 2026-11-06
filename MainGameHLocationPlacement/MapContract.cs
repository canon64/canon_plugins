using System;
using System.Reflection;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    internal static class MapContract
    {
        internal static void Check(Type type, Type actor, Type baseMap)
        {
            void Field(string name, Type expected)
            { if (!(R.Member(type, name) is FieldInfo field) || field.FieldType != expected || field.IsStatic) throw new MissingMemberException("マップ接続の型が違います: " + name); }
            foreach (string name in new[] { "_roomOverlaySceneName", "_roomOverlayBundleName", "_roomOverlayDisplayName", "_hPlaceKey" }) Field(name, typeof(string));
            foreach (string name in new[] { "_roomOverlayLoading", "_sessionStarted" }) Field(name, typeof(bool));
            foreach (string name in new[] { "_roomOverlayPositionRoot", "_vamHotelPivot" }) Field(name, typeof(GameObject));
            Field("_hPlaceRoot", typeof(Transform)); Field("_femaleChara", actor);
            var source = R.Member(type, "LastReservedMap") as PropertyInfo;
            if (source == null || source.PropertyType != baseMap || source.GetGetMethod(true) == null) throw new MissingMemberException("LastReservedMap");
            foreach (string name in new[] { "_hPlaceActive", "_hPlaceDraft" })
            {
                var field = R.Member(type, name) as FieldInfo;
                if (field == null) throw new MissingMemberException(name);
                foreach (string member in new[] { "Id", "Name", "MapKey" })
                    if (!(R.Member(field.FieldType, member) is FieldInfo value) || value.FieldType != typeof(string)) throw new MissingMemberException(name + "." + member);
            }
            Type handler = typeof(Action<,,,>).MakeGenericType(actor, typeof(long), typeof(string), typeof(string));
            foreach (string name in new[] { "HLocationMoving", "HLocationMoved" })
            {
                var evt = type.GetEvent(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (evt?.EventHandlerType != handler || evt.GetAddMethod()?.IsStatic != true || evt.GetRemoveMethod()?.IsStatic != true) throw new MissingMemberException(name);
            }
        }
    }
}
