using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Illusion.Game.Elements.EasyLoader;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksActorPresentation : IDisposable
    {
        private readonly KksNativeConversationSession _session;
        private readonly ChaControl _actor;
        private readonly int _personality;
        private readonly Action<string> _log;
        private readonly List<Action> _restore = new List<Action>();
        private readonly Dictionary<string, Manager.Game.Expression> _expressions;
        private readonly MotionOverride _motion = new MotionOverride();
        private readonly IKMotion _ik = new IKMotion();
        private readonly YureMotion _yure = new YureMotion();
        private AssetBundleData _defaultController;
        private AssetBundleData _defaultIkAsset;
        private MotionIKData _defaultIkData;
        private bool _disposed, _nativeMotionApplied;
        private Action _restoreExternalFace;
        private readonly bool _conversationCameraLook;
        internal ChaControl Actor => _actor;
        internal KksActorPresentation(KksNativeConversationSession session, Action<string> log)
            : this(session, session?.Heroine ?? throw new ArgumentNullException(nameof(session)), log, true) { }
        internal KksActorPresentation(KksNativeConversationSession session, SaveData.CharaData data, Action<string> log, bool initializeNativeBody)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session)); _log = log;
            _conversationCameraLook = initializeNativeBody;
            if (data == null || data.chaCtrl == null) throw new InvalidOperationException("KKS actor presentation requires a loaded character.");
            _actor = data.chaCtrl; _personality = data is SaveData.Heroine heroine ? heroine.FixCharaIDOrPersonality : data.charFile.parameter.personality;
            var actor = _actor; var body = actor.animBody; var status = actor.fileStatus;
            body.Update(session.Settings.zero);
            var eyebrows = status.eyebrowPtn; var eyes = status.eyesPtn; var mouth = status.mouthPtn;
            var eyebrowsOpen = status.eyebrowOpenMax; var eyesOpen = status.eyesOpenMax; var mouthOpen = status.mouthOpenMax;
            var blink = status.eyesBlink; var eyesLook = status.eyesLookPtn; var neckLook = status.neckLookPtn;
            var neckRate = actor.neckLookCtrl == null ? session.Settings.one : actor.neckLookCtrl.rate;
            var blush = status.hohoAkaRate; var highlight = status.hideEyesHighlight; var tears = status.tearsLv; var mask = status.disableMouthShapeMask;
            _restore.Add(() =>
            {
                if (actor == null) return;
                actor.ChangeEyebrowPtn(eyebrows); actor.ChangeEyesPtn(eyes); actor.ChangeMouthPtn(mouth);
                actor.ChangeEyebrowOpenMax(eyebrowsOpen); actor.ChangeEyesOpenMax(eyesOpen); actor.ChangeMouthOpenMax(mouthOpen);
                actor.ChangeEyesBlinkFlag(blink); actor.ChangeLookEyesPtn(eyesLook); actor.ChangeLookNeckPtn(neckLook, neckRate);
                actor.ChangeHohoAkaRate(blush); actor.HideEyeHighlight(highlight); actor.fileStatus.tearsLv = tears; actor.DisableShapeMouth(mask);
            });
            var controller = body.runtimeAnimatorController; var speed = body.speed; var events = body.fireEvents;
            var rootMotion = body.applyRootMotion; var culling = body.cullingMode;
            var states = Enumerable.Range(session.Settings.zero, body.layerCount).Select(layer => body.GetCurrentAnimatorStateInfo(layer)).ToArray();
            var weights = Enumerable.Range(session.Settings.zero, body.layerCount).Select(layer => body.GetLayerWeight(layer)).ToArray();
            var bones = body.GetComponentsInChildren<Transform>(true).Select(bone =>
            {
                var position = bone.localPosition; var rotation = bone.localRotation; var scale = bone.localScale;
                return (Action)(() => { if (bone != null) { bone.localPosition = position; bone.localRotation = rotation; bone.localScale = scale; } });
            }).ToArray();
            _restore.Add(() =>
            {
                if (body == null) return;
                body.fireEvents = false; body.runtimeAnimatorController = controller; body.applyRootMotion = rootMotion; body.cullingMode = culling; body.speed = speed; body.Rebind();
                foreach (var restore in bones) restore();
                for (var layer = session.Settings.zero; layer < states.Length; layer++)
                {
                    body.SetLayerWeight(layer, weights[layer]);
                    if (body.HasState(layer, states[layer].fullPathHash)) body.Play(states[layer].fullPathHash, layer, states[layer].normalizedTime);
                }
                body.Update(session.Settings.zero); body.fireEvents = events;
            });
            CaptureIk(actor);
            CaptureYure(actor);
            CaptureLookTargets(actor);
            try
            {
                _expressions = Manager.Game.ExpCharaListLoadData(data.voiceNo);
                if (initializeNativeBody) { InitializeNativeBody(); FocusConversationCamera(); }
            }
            catch (Exception exception)
            {
                try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(exception, cleanup); }
                throw;
            }
        }
        private void InitializeNativeBody()
        {
            var actor = _actor; var settings = _session.Settings;
            if (string.IsNullOrEmpty(settings.advDefaultControllerBundle) || string.IsNullOrEmpty(settings.advDefaultControllerAsset)
                || string.IsNullOrEmpty(settings.advInitialPose) || string.IsNullOrEmpty(settings.advInitialExpression))
                throw new InvalidOperationException("KKS ADV initial presentation settings are missing.");
            _defaultController = new AssetBundleData(settings.advDefaultControllerBundle, settings.advDefaultControllerAsset);
            var controller = _defaultController.GetAsset<RuntimeAnimatorController>();
            if (controller == null) throw new InvalidOperationException("KKS ADV default AnimatorController is unavailable.");
            actor.animBody.runtimeAnimatorController = controller;
            Apply(new KksDialogueProgram.Cue(new KksConversationLayout.Row
            {
                pose = settings.advInitialPose, facialExpression = settings.advInitialExpression,
                eyeLook = actor.fileStatus.eyesLookPtn, neckLook = actor.fileStatus.neckLookPtn
            }, true));
            actor.animBody.Update(settings.zero);
            actor.ChangeLookNeckPtn(settings.advInitialNeckPattern, settings.zero);
        }
        internal Manager.Game.Expression FindExpression(string name) => _expressions.TryGetValue(name, out var expression) ? expression : null;
        internal void CaptureExternalFace()
        {
            if (_restoreExternalFace != null) return;
            var status = _actor.fileStatus;
            var brows = status.eyebrowPtn; var eyes = status.eyesPtn; var mouth = status.mouthPtn;
            var browsOpen = status.eyebrowOpenMax; var eyesOpen = status.eyesOpenMax; var mouthOpen = status.mouthOpenMax;
            var blink = status.eyesBlink; var blush = status.hohoAkaRate; var tears = status.tearsLv;
            var highlight = status.hideEyesHighlight; var mask = status.disableMouthShapeMask;
            _restoreExternalFace = () =>
            {
                if (_actor == null) return;
                _actor.ChangeEyebrowPtn(brows); _actor.ChangeEyesPtn(eyes); _actor.ChangeMouthPtn(mouth);
                _actor.ChangeEyebrowOpenMax(browsOpen); _actor.ChangeEyesOpenMax(eyesOpen); _actor.ChangeMouthOpenMax(mouthOpen);
                _actor.ChangeEyesBlinkFlag(blink); _actor.ChangeHohoAkaRate(blush); _actor.HideEyeHighlight(highlight);
                _actor.fileStatus.tearsLv = tears; _actor.DisableShapeMouth(mask);
            };
        }
        internal void RestoreExternalFace()
        { var restore = _restoreExternalFace; _restoreExternalFace = null; restore?.Invoke(); }
        internal void FocusConversationCamera()
        {
            if (_disposed || !_conversationCameraLook || _actor == null || Camera.main == null) return;
            var camera = Camera.main.transform; var settings = _session.Settings;
            var eyes = _actor.eyeLookCtrl?.eyeLookScript?.eyeTypeStates;
            var neck = _actor.neckLookCtrl?.neckLookScript?.neckTypeStates;
            // Read the native character's pattern table instead of assuming a pattern number.
            var eyePattern = eyes == null ? -settings.one : Array.FindIndex(eyes, state => state.lookType == EYE_LOOK_TYPE.TARGET);
            var neckPattern = neck == null ? -settings.one : Array.FindIndex(neck, state => state.lookType == NECK_LOOK_TYPE_VER2.TARGET);
            if (eyePattern >= settings.zero)
            {
                _actor.ChangeLookEyesTarget(settings.zero, camera);
                _actor.ChangeLookEyesPtn(eyePattern);
            }
            if (neckPattern >= settings.zero)
            {
                _actor.ChangeLookNeckTarget(settings.zero, camera);
                _actor.ChangeLookNeckPtn(neckPattern, settings.one);
            }
        }
        internal void PrepareNativeEventCgMotion()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksActorPresentation));
            if (_actor == null) throw new InvalidOperationException("KKS ADV actor disappeared before EventCG playback.");
            _nativeMotionApplied = true;
        }
        internal void ApplyNativeDefaultMotion()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksActorPresentation));
            var settings = _session.Settings;
            if (_actor == null || _actor.animBody == null) throw new InvalidOperationException("KKS ADV actor disappeared before default motion.");
            if (_defaultController == null) _defaultController = new AssetBundleData(settings.advDefaultControllerBundle, settings.advDefaultControllerAsset);
            var controller = _defaultController.GetAsset<RuntimeAnimatorController>();
            if (controller == null) throw new InvalidOperationException("KKS ADV default controller is unavailable.");
            if (_defaultIkData == null)
            {
                _defaultIkAsset = new AssetBundleData(settings.advDefaultIkBundle, settings.advDefaultIkAsset);
                var data = _defaultIkAsset.GetAsset<TextAsset>();
                if (data == null) throw new InvalidOperationException("KKS ADV default IK data is unavailable.");
                _defaultIkData = new MotionIKData(data);
            }
            _actor.animBody.runtimeAnimatorController = controller; _actor.animBody.Play(settings.advDefaultState);
            var motion = new MotionIK(_actor, _defaultIkData.Copy()); motion.Calc(settings.advDefaultState);
            if (_ik.motionIK != null) motion.SetPartners(_ik.motionIK.partners);
            _motion.state = settings.advDefaultState; _ik.Create(_actor, motion);
        }
        private void CaptureLookTargets(ChaControl actor)
        {
            var status = actor.fileStatus;
            var eyesType = status.eyesTargetType; var eyesRate = status.eyesTargetRate; var eyesAngle = status.eyesTargetAngle; var eyesRange = status.eyesTargetRange;
            var neckType = status.neckTargetType; var neckRate = status.neckTargetRate; var neckAngle = status.neckTargetAngle; var neckRange = status.neckTargetRange;
            var eyes = actor.eyeLookCtrl == null ? null : actor.eyeLookCtrl.target;
            var neck = actor.neckLookCtrl == null ? null : actor.neckLookCtrl.target;
            var nodes = new[] { actor.objEyesLookTarget, actor.objEyesLookTargetP, actor.objNeckLookTarget, actor.objNeckLookTargetP }
                .Where(node => node != null).Select(node =>
                {
                    var transform = node.transform; var position = transform.localPosition; var rotation = transform.localRotation; var scale = transform.localScale;
                    return (Action)(() => { if (transform != null) { transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale; } });
                }).ToArray();
            _restore.Add(() =>
            {
                if (actor == null) return;
                status.eyesTargetType = eyesType; status.eyesTargetRate = eyesRate; status.eyesTargetAngle = eyesAngle; status.eyesTargetRange = eyesRange;
                status.neckTargetType = neckType; status.neckTargetRate = neckRate; status.neckTargetAngle = neckAngle; status.neckTargetRange = neckRange;
                if (actor.eyeLookCtrl != null) actor.eyeLookCtrl.target = eyes;
                if (actor.neckLookCtrl != null) actor.neckLookCtrl.target = neck;
                foreach (var restore in nodes) restore();
            });
        }
        internal void ApplyNativeExpression(ADV.Commands.Base.Expression.Data expression)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksActorPresentation));
            if (_actor == null) throw new InvalidOperationException("KKS ADV actor disappeared before expression playback.");
            (expression ?? throw new ArgumentNullException(nameof(expression))).Change(_actor);
        }
        internal void ApplyNativeMotion(ADV.Commands.Base.Motion.Data motion, ADV.Info.Anime.Play options)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksActorPresentation));
            if (motion == null || options == null) throw new ArgumentNullException(motion == null ? nameof(motion) : nameof(options));
            var actor = _actor;
            if (actor == null || actor.animBody == null) throw new InvalidOperationException("KKS ADV actor disappeared before motion playback.");
            _nativeMotionApplied = true;
            if (_motion.Setting(actor.animBody, motion.assetBundleName, motion.assetName,
                motion.overrideAssetBundleName, motion.overrideAssetName, motion.stateName, true))
            {
                _motion.isCrossFade = false; _motion.layers = motion.layerNo;
                _motion.transitionDuration = options.transitionDuration; _motion.normalizedTime = options.normalizedTime;
                _motion.Play(actor.animBody);
            }
            _ik.Setting(actor, motion.ikAssetBundleName, motion.ikAssetName, motion.stateName, false);
            _yure.Setting(actor, motion.shakeAssetBundleName, motion.shakeAssetName, motion.stateName, false);
        }
        private void CaptureYure(ChaControl actor)
        {
            // YureCtrl writes these body-mask cells and DynamicBone_Ver02.enabled.
            var mask = actor.fileStatus.disableBustShapeMask;
            if (mask != null)
            {
                var saved = (bool[,])mask.Clone();
                _restore.Add(() =>
                {
                    if (actor == null || !_nativeMotionApplied) return;
                    for (var side = _session.Settings.zero; side < saved.GetLength(_session.Settings.zero); side++)
                        for (var shape = _session.Settings.zero; shape < saved.GetLength(_session.Settings.one); shape++)
                            actor.DisableShapeBodyID(side, shape, saved[side, shape]);
                });
            }
            foreach (var behaviour in actor.GetComponentsInChildren<Behaviour>(true).Where(component => component != null && component.GetType().Name == "DynamicBone_Ver02"))
            {
                var enabled = behaviour.enabled;
                _restore.Add(() => { if (_nativeMotionApplied && behaviour != null) behaviour.enabled = enabled; });
            }
        }
        internal void Apply(KksDialogueProgram.Cue cue)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KksActorPresentation));
            if (cue.Kind != KksDialogueProgram.RowKind.HeroineText) return;
            var actor = _actor; var row = cue.Source; var p = _session.Settings;
            actor.ChangeLookEyesPtn(row.eyeLook); actor.ChangeLookNeckPtn(row.neckLook);
            if (cue.Motion && !string.IsNullOrEmpty(row.pose))
            {
                var pose = _session.Assist.GetPoseInfo(_personality, row.pose);
                if (pose == null) _log?.Invoke("KKS_CONVERSATION_NATIVE_POSE_UNDEFINED personality=" + _personality + " pose=" + row.pose);
                else
                {
                    if (pose.Length <= p.poseStateIndex) throw new InvalidOperationException("KKSの会話ポーズ定義が不完全です：" + row.pose);
                    var state = pose[p.poseStateIndex]; var animator = actor.animBody;
                    animator.fireEvents = true; animator.applyRootMotion = false; animator.speed = p.one;
                    if (_motion.Setting(animator, pose[p.poseBundleIndex], pose[p.poseControllerIndex], null, null, state, true))
                    {
                        _motion.isCrossFade = false; _motion.normalizedTime = p.zero; _motion.Play(animator);
                    }
                    _ik.Setting(actor, pose.Length > p.poseIkBundleIndex ? pose[p.poseIkBundleIndex] : null,
                        pose.Length > p.poseIkAssetIndex ? pose[p.poseIkAssetIndex] : null, state, false);
                }
            }
            if (!string.IsNullOrEmpty(row.facialExpression))
            {
                if (!_expressions.TryGetValue(row.facialExpression, out var expression))
                {
                    // Native ADV.Expression also attempts a literal pattern when the named dictionary has no entry.
                    _log?.Invoke("KKS_CONVERSATION_NATIVE_EXPRESSION_LITERAL_FALLBACK personality=" + _personality + " expression=" + row.facialExpression);
                    expression = new Manager.Game.Expression(new[] { row.facialExpression }) { isChangeSkip = true };
                }
                expression.Change(actor);
            }
            FocusConversationCamera();
        }
        private void CaptureIk(ChaControl actor)
        {
            // MotionIK mutates these existing native objects once when a pose changes.
            foreach (var component in actor.GetComponentsInChildren<Component>(true).Where(component => component != null))
            {
                var names = new List<string>();
                for (var type = component.GetType(); type != null; type = type.BaseType) names.Add(type.FullName);
                if (names.Any(name => name == "Illusion.Component.Correct.BaseData" || name == "Illusion.Component.Correct.Process.BaseProcess")
                    || names.Any(name => name == "Illusion.Component.Correct.FrameCorrect" || name == "Illusion.Component.Correct.IKCorrect"))
                {
                    if (component is Behaviour behaviour)
                    {
                        var enabled = behaviour.enabled;
                        _restore.Add(() => { if (behaviour != null) behaviour.enabled = enabled; });
                    }
                    CaptureFields(component);
                    if (Member(component, "list") is IEnumerable entries) foreach (var entry in entries) CaptureFields(entry);
                }
                if (names.Contains("RootMotion.FinalIK.FullBodyBipedIK"))
                {
                    var behaviour = (Behaviour)component; var enabled = behaviour.enabled;
                    _restore.Add(() => { if (behaviour != null) behaviour.enabled = enabled; });
                    var solver = Member(component, "solver");
                    // FinalIK's private indexes are caches built by Initiate. Restoring
                    // pre-init (-1) indexes after conversation initialization corrupts
                    // ResetOffset, even though the KKS bone hierarchy never changed.
                    foreach (var name in new[] { "leftHandEffector", "rightHandEffector", "leftFootEffector", "rightFootEffector" })
                        _restore.Add(KksIkParameterSnapshot.Capture(Member(solver, name)));
                    foreach (var name in new[] { "leftArmChain", "rightArmChain", "leftLegChain", "rightLegChain" })
                        _restore.Add(KksIkParameterSnapshot.Capture(Member(Member(solver, name), "bendConstraint")));
                }
            }
        }
        private static object Member(object instance, string name)
        {
            if (instance == null) return null;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return instance.GetType().GetField(name, flags)?.GetValue(instance) ?? instance.GetType().GetProperty(name, flags)?.GetValue(instance, null);
        }
        private void CaptureFields(object instance)
        {
            if (instance == null) return;
            for (var type = instance.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(Behaviour) && type != typeof(UnityEngine.Object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsInitOnly && (field.FieldType.IsValueType || field.FieldType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)))
                    {
                        var value = field.GetValue(instance);
                        _restore.Add(() => { if (!(instance is UnityEngine.Object obj) || obj != null) field.SetValue(instance, value); });
                    }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            try { _ik.motionIK?.Reset(); } catch (Exception exception) { errors.Add(exception); }
            try { _ik.motionIK?.Release(); } catch (Exception exception) { errors.Add(exception); }
            try { _yure.yureCtrl?.Release(); } catch (Exception exception) { errors.Add(exception); }
            // Restore IK fields before evaluating the original controller, then restore the original face.
            foreach (var restore in _restore.AsEnumerable().Reverse()) try { restore(); } catch (Exception exception) { errors.Add(exception); }
            _restore.Clear();
            try { _motion.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            try { _ik.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            try { _yure.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            try { _defaultController?.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            try { _defaultIkAsset?.UnloadBundle(); } catch (Exception exception) { errors.Add(exception); }
            if (errors.Any()) throw new AggregateException("KKS conversation actor restoration failed.", errors);
        }
    }
}
