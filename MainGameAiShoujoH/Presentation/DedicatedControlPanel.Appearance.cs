using System;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    public sealed partial class DedicatedControlPanel
    {
        private bool appearanceOpen, sceneControlsOpen;
        private string appearanceActor;
        private HLightState lightState;
        private HCameraState cameraState;
        private Vector3 cameraPositionOrigin;
        private bool TryUi(Action action)
        { try { action(); error = null; return true; } catch (Exception ex) { error = ex.Message; context.ReportFailure(ex.Message); return false; } }
        private bool EditControlsAvailable => !progression.Engine.WaitingForStartVoice && (!progression.Engine.State.Contains("Orgasm") || progression.Engine.State.EndsWith("_A", StringComparison.Ordinal));
        private void DrawAppearance()
        {
            if (GUILayout.Button((appearanceOpen ? "▼ " : "▶ ") + "衣服・アクセサリ・汁", GUILayout.ExpandWidth(false))) appearanceOpen = !appearanceOpen;
            if (!appearanceOpen) return;
            GUILayout.BeginVertical(GUI.skin.box);
            foreach (var actor in context.Request.Participants)
                if (GUILayout.Toggle(appearanceActor == actor.Identity, actor.Character.fileParam.fullname + " (" + actor.Role + ")", "Button")) appearanceActor = actor.Identity;
            var selected = context.Request.Participants.FirstOrDefault(p => p.Identity == appearanceActor) ?? context.Request.Participants.First(p => p.Role == "female");
            appearanceActor = selected.Identity;
            bool enabled = GUI.enabled; GUI.enabled = enabled && EditControlsAvailable;
            var character = selected.Character;
            if (selected.Role != "male")
            {
                GUILayout.BeginHorizontal();
                foreach (int state in new[] { 0, 1, 2 })
                    if (GUILayout.Button(state == 0 ? "全着衣" : state == 1 ? "全半脱ぎ" : "全脱衣")) { int value = state; TryUi(() => presentation.AllClothes(selected.Identity, value)); }
                GUILayout.EndHorizontal();
                bool perPart = GUI.enabled; GUI.enabled = perPart && (!selected.IsPlayer || context.Request.Settings.PlayerClothesEnabled);
                var labels = new[] { "上", "下", "ブラ", "下着", "手袋", "ストッキング", "靴下", "内履き", "外履き" };
                for (int part = 0; part < character.fileStatus.clothesState.Length; part++)
                {
                    if (!character.IsClothes(part)) continue; int index = part;
                    if (GUILayout.Button((part < labels.Length ? labels[part] : part.ToString()) + ": " + character.fileStatus.clothesState[part] + " → 次")) TryUi(() => presentation.NextClothes(selected.Identity, index));
                }
                GUI.enabled = perPart;
            }
            bool accessories = GUI.enabled; GUI.enabled = accessories && (!(selected.IsPlayer || selected.Role == "male") || context.Request.Settings.PlayerAccessoriesEnabled);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("アクセ全表示")) TryUi(() => presentation.AllAccessories(selected.Identity, true));
            if (GUILayout.Button("アクセ全非表示")) TryUi(() => presentation.AllAccessories(selected.Identity, false));
            GUILayout.EndHorizontal();
            for (int part = 0; part < Math.Min(character.fileStatus.showAccessory.Length, character.objAccessory.Length); part++)
            {
                if (character.objAccessory[part] == null) continue;
                bool visible = GUILayout.Toggle(character.fileStatus.showAccessory[part], "アクセ " + (part + 1) + ": " + character.objAccessory[part].name);
                if (visible != character.fileStatus.showAccessory[part]) { int index = part; TryUi(() => presentation.SetAccessory(selected.Identity, index, visible)); }
            }
            GUI.enabled = accessories;
            if (selected.Role != "male" && GUILayout.Button("汁を全0 / 全2")) TryUi(() => presentation.ToggleSiru(selected.Identity));
            GUI.enabled = enabled; GUILayout.EndVertical();
        }
        private void ClearNumbers(string prefix)
        { foreach (var key in numberText.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) numberText.Remove(key); }
        private void DrawSceneControls()
        {
            if (GUILayout.Button((sceneControlsOpen ? "▼ " : "▶ ") + "照明・視点", GUILayout.ExpandWidth(false)))
            {
                sceneControlsOpen = !sceneControlsOpen;
                if (sceneControlsOpen) TryUi(() => { lightState = context.Request.SceneControls?.ReadLight?.Invoke(); cameraState = context.Request.SceneControls?.ReadCamera?.Invoke(); cameraPositionOrigin = cameraState?.Position ?? Vector3.zero; ClearNumbers("光"); ClearNumbers("視点"); });
            }
            if (!sceneControlsOpen) return;
            GUILayout.BeginVertical(GUI.skin.box);
            if (lightState == null) GUILayout.Label("専用照明は現在無効です。");
            else
            {
                var next = lightState.Copy(); var color = next.Color; var angles = next.LocalRotation.eulerAngles;
                color.r = Quantity("光 赤", color.r, 0, 1); color.g = Quantity("光 緑", color.g, 0, 1); color.b = Quantity("光 青", color.b, 0, 1);
                next.Intensity = Quantity("光 強さ", next.Intensity, 0, 8);
                angles.x = Quantity("光 縦角", angles.x, 0, 360); angles.y = Quantity("光 横角", angles.y, 0, 360);
                next.Color = color; bool rotationChanged = angles != lightState.LocalRotation.eulerAngles; next.LocalRotation = rotationChanged ? Quaternion.Euler(angles) : lightState.LocalRotation;
                if (next.Color != lightState.Color || Math.Abs(next.Intensity - lightState.Intensity) > .00001f || rotationChanged)
                    if (TryUi(() => context.Request.SceneControls.ApplyLight(next))) lightState = next;
                if (GUILayout.Button("照明リセット")) TryUi(() => { context.Request.SceneControls.ResetLight(); lightState = context.Request.SceneControls.ReadLight(); ClearNumbers("光"); });
            }
            if (cameraState != null)
            {
                var next = new HCameraState { Position = cameraState.Position, Rotation = cameraState.Rotation, FieldOfView = cameraState.FieldOfView }; var position = next.Position; var angles = next.Rotation.eulerAngles;
                position.x = Quantity("視点 X", position.x, cameraPositionOrigin.x - 10, cameraPositionOrigin.x + 10); position.y = Quantity("視点 Y", position.y, cameraPositionOrigin.y - 10, cameraPositionOrigin.y + 10); position.z = Quantity("視点 Z", position.z, cameraPositionOrigin.z - 10, cameraPositionOrigin.z + 10);
                angles.x = Quantity("視点 縦角", angles.x, 0, 360); angles.y = Quantity("視点 横角", angles.y, 0, 360); next.FieldOfView = Quantity("視点 画角", next.FieldOfView, 1, 179);
                next.Position = position; bool rotationChanged = angles != cameraState.Rotation.eulerAngles; next.Rotation = rotationChanged ? Quaternion.Euler(angles) : cameraState.Rotation;
                if (next.Position != cameraState.Position || rotationChanged || Math.Abs(next.FieldOfView - cameraState.FieldOfView) > .00001f)
                    if (TryUi(() => context.Request.SceneControls.ApplyCamera(next))) cameraState = next;
                if (GUILayout.Button("視点リセット")) TryUi(() => { context.Request.SceneControls.ResetCamera(); cameraState = context.Request.SceneControls.ReadCamera(); cameraPositionOrigin = cameraState?.Position ?? Vector3.zero; ClearNumbers("視点"); });
            }
            GUILayout.EndVertical();
        }
    }
}
