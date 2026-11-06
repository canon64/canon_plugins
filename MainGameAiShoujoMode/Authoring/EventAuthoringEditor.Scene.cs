using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Life;
using UnityEngine;

namespace MainGameAiShoujoMode.Authoring
{
    internal sealed partial class EventAuthoringEditor
    {
        private AiLifeAnimationClip[] animationChoices = Array.Empty<AiLifeAnimationClip>();
        private void DrawAnimations(EventNode node)
        {
            GUILayout.Box("この会話で使うアニメ");
            foreach (var motion in node.animations ?? Enumerable.Empty<MotionCue>())
            {
                Reference("動かす人物", motion.characterId, Characters, v => motion.characterId = v, "人物を選択");
                DrawMotion(motion);
                if (Button("このアニメを外す")) Enqueue(() => draft.Change(p => node.animations.Remove(motion)));
            }
            if (Button("＋人物のアニメ")) Enqueue(() => draft.Change(p => { if (node.animations == null) node.animations = new List<MotionCue>(); node.animations.Add(new MotionCue { characterId = node.characterId }); }));
            if (Item.sceneMode == "freeH")
            {
                GUILayout.Label("フリーHの体位：" + (node.hAnimation?.name ?? "前の体位を継続"));
                if (Button("体位一覧から選ぶ")) Enqueue(() =>
                {
                    var choices = freeHBridge.Animations();
                    if (choices.Length == default(int)) throw new InvalidOperationException("このイベントをフリーHで試し再生してから、編集を開いてください。実際の参加者で使える体位を選べます。");
                    referenceChoices = choices.Select((c, i) => new KeyValuePair<string, string>(i.ToString(), c.name)).ToArray(); referenceSearch = ""; referencePage = default(int); referenceSelected = null;
                    referencePicked = id => { var selection = choices[int.Parse(id)]; draft.Change(p => node.hAnimation = new HAnimationCue { id = selection.id, mode = selection.mode, name = selection.name, bundle = selection.bundle, asset = selection.asset }); };
                });
                if (node.hAnimation != null && Button("体位指定を外す")) Enqueue(() => draft.Change(p => node.hAnimation = null));
            }
        }
        private void DrawMotion(MotionCue motion)
        {
            GUILayout.Label("選択中：" + (string.IsNullOrEmpty(motion.asset) ? "アニメを選んでください" : motion.asset));
            if (Button("生活アニメから選ぶ")) Enqueue(() =>
            {
                if (animationChoices.Length == default(int)) animationChoices = AiLifeAnimationCatalog.Load(content)?.clips.Where(c => c.status == "converted").ToArray() ?? Array.Empty<AiLifeAnimationClip>();
                referenceChoices = animationChoices.Select(c => new KeyValuePair<string, string>(c.id, c.sourceName + " / " + c.sourceBundle)).ToArray(); referenceSearch = ""; referencePage = default(int); referenceSelected = null;
                referencePicked = id => { var clip = animationChoices.Single(c => c.id == id); draft.Change(p => { motion.source = "clip"; motion.bundle = Path.Combine(content, "LifeAnimations", clip.bundle); motion.asset = clip.asset; }); };
                if (referenceChoices.Length == default(int)) { referencePicked = null; throw new InvalidOperationException("変換済み生活アニメがありません。ゲーム内Controllerと動作名でも指定できます。"); }
            });
            Select("アニメの指定方法", motion.source, new[] { "clip", "controller" }, new[] { "動作クリップ", "ゲーム内Controller" }, v => motion.source = v);
            Field("アニメBundle", motion.bundle, v => motion.bundle = v); Field("アニメ資産名", motion.asset, v => motion.asset = v);
            if (motion.source == "controller") Field("Controller内の動作名", motion.state, v => motion.state = v);
            Number("再生速度", motion.speed, v => motion.speed = v, AuthoringDefaults.Current.minPitch, AuthoringDefaults.Current.maxPitch);
            Number("開始位置（秒）", motion.startSeconds, v => motion.startSeconds = v, default(int), AuthoringDefaults.Current.maxWaitSeconds);
            Number("切替にかける秒数", motion.blendSeconds, v => motion.blendSeconds = v, default(int), AuthoringDefaults.Current.maxCameraSeconds);
            bool loop = GUILayout.Toggle(motion.loop, "繰り返す（クリップ形式）"); if (loop != motion.loop) Enqueue(() => draft.Change(p => motion.loop = loop));
            if (Button("このアニメを試す")) Enqueue(() => { if (!runner.Active && !cameraOnly) { playing = AuthoringJson.Copy(draft.Project); stage.Begin(playing); cameraOnly = true; } stage.Perform(motion); });
        }
        private void DrawItems(AuthoringEvent item)
        {
            GUILayout.Box("アイテム・家具の配置");
            foreach (var placement in item.items ?? Enumerable.Empty<ItemPlacement>())
            {
                Field("アイテム名", placement.name, v => placement.name = v);
                Select("使う実物", placement.source, new[] { "map", "prefab" }, new[] { "マップ内の家具・小物", "素材から追加" }, v => placement.source = v);
                if (placement.source == "map")
                {
                    GUILayout.Label("選択中：" + placement.path);
                    if (Button("マップの実物から選ぶ")) Enqueue(() =>
                    {
                        referenceChoices = stage.MapItems().ToArray(); referenceSearch = ""; referencePage = default(int); referenceSelected = placement.path;
                        referencePicked = id => draft.Change(p => { placement.path = id; var target = stage.MapRoot.transform.Find(id); placement.name = target.name; placement.position = AuthoringStage.Point(target.position); placement.rotation = AuthoringStage.Point(target.eulerAngles); });
                    });
                }
                else
                {
                    Field("アイテムBundle", placement.bundle, v => placement.bundle = v); Field("アイテム資産名", placement.asset, v => placement.asset = v);
                    if (Button("素材ファイルから選ぶ")) Enqueue(() => OpenFiles(Path.Combine(BepInEx.Paths.GameRootPath, "abdata"), ".unity3d", path => SelectPrefab(path, placement)));
                }
                PointFields("アイテム位置", placement.position); PointFields("アイテム角度", placement.rotation);
                bool show = GUILayout.Toggle(placement.visible, "アイテムを表示する"); if (show != placement.visible) Enqueue(() => draft.Change(p => placement.visible = show));
                if (Button("この配置を試す")) Enqueue(() => stage.PlaceItems(new[] { placement }));
                if (Button("アイテムを削除")) Enqueue(() => draft.Change(p => item.items.Remove(placement)));
            }
            if (Button("＋アイテム・家具")) Enqueue(() => draft.Change(p => { if (item.items == null) item.items = new List<ItemPlacement>(); item.items.Add(new ItemPlacement()); }));
        }
        private void SelectPrefab(string path, ItemPlacement item)
        {
            var bundle = AssetBundle.LoadFromFile(path); if (bundle == null) throw new IOException("この素材Bundleを読めません。既に使用中の素材はBundle名で直接指定できます。");
            string[] assets; try { assets = bundle.GetAllAssetNames().Where(a => a.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray(); } finally { bundle.Unload(false); }
            var abdata = Path.Combine(BepInEx.Paths.GameRootPath, "abdata") + Path.DirectorySeparatorChar;
            if (!path.StartsWith(abdata, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("追加アイテムは導入済みabdata内の素材から選んでください。");
            referenceChoices = assets.Select(a => new KeyValuePair<string, string>(a, Path.GetFileNameWithoutExtension(a))).ToArray(); referenceSearch = ""; referencePage = default(int); referenceSelected = null;
            referencePicked = id => draft.Change(p => { item.bundle = path.Substring(abdata.Length).Replace('\\', '/'); item.asset = Path.GetFileNameWithoutExtension(id); item.name = item.asset; });
        }
    }
}
