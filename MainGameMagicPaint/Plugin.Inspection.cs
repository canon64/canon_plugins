using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MainGameMagicPaint
{
    public sealed partial class Plugin
    {
        readonly StrokeInspection _inspection = new StrokeInspection();
        readonly List<InspectionActor> _inspectionActors = new List<InspectionActor>();
        Coroutine _inspectionTimeout, _inspectionCapture;
        bool _inspectionPending, _inspectionPreviousLogging;
        string _inspectionDirectory;
        string _inspectionResult = "検査は停止中", _inspectionEndState;
        sealed class InspectionActor
        {
            internal PaintActor Actor;
            internal int FaceBefore, BodyBefore;
            internal bool Held, Contact, Guide, Trigger;
        }
        bool InspectionRunning => _inspection.Active || _inspectionPending;

        void ArmInspection()
        {
            if (!CanAct || _busy || Selected == null || InspectionRunning) return;
            if (_trigger) { _inspectionResult = "トリガーを離してから検査を開始してください"; return; }
            _inspectionActors.Clear();
            _inspectionPreviousLogging = _logging.Value;
            _logging.Value = true; SaveConfig();
            _inspection.Arm(); ObserveInspectionActor(Selected);
            _inspectionResult = "次の一筆を待機中。保持して描き、トリガーかグリップを離すと画像を保存します";
            _inspectionTimeout = StartCoroutine(InspectionTimeout());
        }
        IEnumerator InspectionTimeout()
        {
            yield return new WaitForSecondsRealtime(5);
            _inspectionTimeout = null;
            if (_inspection.Timeout(5)) RequestInspectionCapture("有効なトリガー入力なし・5秒経過");
        }
        void InspectionInput(bool accepted)
        {
            if (!_logging.Value || !_inspection.Active) return;
            _inspection.Input(accepted);
            if (accepted)
            {
                if (_inspectionTimeout != null) StopCoroutine(_inspectionTimeout);
                _inspectionTimeout = null;
                _inspectionResult = "入力受信済み。トリガーかグリップを離した時に画像を保存します";
            }
        }
        void ObserveInspectionActor(PaintActor actor)
        {
            if (!_logging.Value || !_inspection.Active || actor == null) return;
            foreach (var entry in _inspectionActors) if (entry.Actor == actor) return;
            _inspectionActors.Add(new InspectionActor { Actor = actor,
                FaceBefore = actor.FaceBuffer?.InkPixels ?? 0, BodyBefore = actor.BodyBuffer?.InkPixels ?? 0,
                Held = _vrPaint.Held, Contact = _contact, Guide = _sprayReady, Trigger = _trigger });
        }
        void RequestInspectionCapture(string reason)
        {
            if (!_logging.Value || !CanAct || !_inspection.Finish()) return;
            if (_inspectionTimeout != null) StopCoroutine(_inspectionTimeout);
            _inspectionTimeout = null;
            // Snapshot event state before grip cleanup resets it. Read existing textures at frame end.
            _inspectionEndState = "reason=" + reason + "\nrawTriggerReceived=" + _inspection.RawInput +
                "\nacceptedTriggerReceived=" + _inspection.Started + "\ntool=" + _tool +
                "\nheldAtEnd=" + _vrPaint.Held + "\ncontactAtEnd=" + _contact +
                "\nguideAtEnd=" + _sprayReady + "\ntriggerAtEnd=" + _trigger + "\nstatus=" + _status;
            _inspectionPending = true;
            _inspectionResult = "一筆終了。描き込み先の画像を保存中";
            _inspectionCapture = StartCoroutine(CaptureInspectionAtFrameEnd());
        }
        IEnumerator CaptureInspectionAtFrameEnd()
        {
            // One yield, never a per-frame loop. Native texture/property updates may finish later in this frame.
            yield return new WaitForEndOfFrame();
            _inspectionCapture = null;
            if (!_inspectionPending || !_logging.Value || !CanAct) { CancelInspection(); yield break; }
            try
            {
                string directory = Path.Combine(SaveDirectory, "Diagnostics", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + System.Guid.NewGuid().ToString("N").Substring(0,6));
                _inspectionDirectory = directory;
                Directory.CreateDirectory(directory);
                var report = new StringBuilder(); report.AppendLine(_inspectionEndState);
                for (int i = 0; i < _inspectionActors.Count; i++)
                {
                    var entry = _inspectionActors[i]; var actor = entry.Actor;
                    string member = Path.Combine(directory, "actor-" + (i+1)); Directory.CreateDirectory(member);
                    report.AppendLine("actor-"+(i+1)+" name="+(actor.Actor != null ? actor.Actor.chaFile.parameter.fullname : "キャラ消失"));
                    report.AppendLine("firstObserved held="+entry.Held+" contact="+entry.Contact+" guide="+entry.Guide+" trigger="+entry.Trigger);
                    report.AppendLine("before faceInkPixels="+entry.FaceBefore+" bodyInkPixels="+entry.BodyBefore);
                    CaptureActorImages(actor, member, report);
                }
                report.AppendLine("paintTargets=native-face-and-body-only; occlusion=disabled; other-meshes-and-colliders=ignored");
                File.WriteAllText(Path.Combine(directory,"state.txt"), report.ToString(), new UTF8Encoding(false));
                _inspectionResult = "画像保存成功：" + directory;
            }
            catch (Exception ex)
            {
                _inspectionResult = "画像保存失敗：" + ex.Message + " 保存先：" + _inspectionDirectory;
                if (_logging.Value && !string.IsNullOrEmpty(_inspectionDirectory))
                    try { File.WriteAllText(Path.Combine(_inspectionDirectory,"capture-error.txt"),_inspectionEndState+"\n"+ex,new UTF8Encoding(false)); } catch { }
                Fail("一筆検査の保存失敗", ex);
            }
            finally { FinishInspectionResources(); }
        }
        void CaptureActorImages(PaintActor actor, string directory, StringBuilder report)
        {
            // Do not call EndStroke/Flush/Rebind/Compose here: inspect what exists, including lost assignments.
            foreach (bool face in new[] { true, false })
            {
                string name = face ? "face" : "body";
                var buffer = face ? actor.FaceBuffer : actor.BodyBuffer;
                var surface = face ? actor.Face : actor.Body;
                CaptureReceiverState(actor,face,report);
                if (buffer == null) report.AppendLine(name + ": CPUインク画像は未作成（PNGなし）");
                else
                {
                    report.AppendLine(name + " CPU inkPixels=" + buffer.InkPixels);
                    File.WriteAllBytes(Path.Combine(directory,name+"-cpu-ink.png"),Encode(buffer));
                }
                if (surface == null) report.AppendLine(name + ": GPUインク・合成画像は未作成（PNGなし）");
                else
                {
                    try { surface.CaptureImages(directory,name,report); }
                    catch(Exception ex) { report.AppendLine(name+" GPU画像取得失敗="+ex.GetType().Name+": "+ex.Message); throw; }
                }
            }
        }
        void CaptureReceiverState(PaintActor actor,bool face,StringBuilder report)
        {
            if(actor.Actor==null)return;
            var renderer=face?actor.Actor.rendFace:actor.Actor.rendBody;
            if(renderer==null){report.AppendLine("receiver="+(face?"face":"body")+" missing");return;}
            var probe=actor.Probes.Find(p=>p.Renderer==renderer);
            report.AppendLine("receiver="+renderer.name+" enabled="+renderer.enabled+" active="+renderer.gameObject.activeInHierarchy+" forceRenderingOff="+renderer.forceRenderingOff+" probe="+(probe!=null));
            if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh!=null)
                report.AppendLine("sourceMesh="+skin.sharedMesh.name+" readable="+skin.sharedMesh.isReadable+" vertices="+skin.sharedMesh.vertexCount+" bounds="+renderer.bounds);
            try
            {
                if(probe?.Index==null||probe.Vertices==null){report.AppendLine("receiver geometry has not been sampled while held");return;}
                report.AppendLine("geometry vertices="+probe.Vertices.Length+" uv="+probe.Uv.Length+" triangles="+probe.Triangles.Length/3);
                if(_model!=null)
                {
                    report.AppendLine("tip="+_model.Tip.position+" rendererBoundsDistance="+Mathf.Sqrt(renderer.bounds.SqrDistance(_model.Tip.position)));
                    report.AppendLine("cachedSkinContact="+probe.Index.Near(MeshProbe.Num(_model.Tip.position),.03f,out _,out _)+" cachedSkinRay="+probe.Index.Ray(MeshProbe.Num(_model.Tip.position),MeshProbe.Num(_model.Visual.forward),.30f,out _,out _));
                }
            }
            catch(Exception ex){report.AppendLine("receiverGeometryError="+ex.GetType().Name+": "+ex.Message);}
        }
        void FinishInspectionResources()
        {
            _inspectionPending = false; _inspection.Cancel(); _inspectionActors.Clear();
            if (_logging.Value) { _logging.Value = _inspectionPreviousLogging; SaveConfig(); }
        }
        void CancelInspection()
        {
            if (!InspectionRunning) return;
            if (_inspectionTimeout != null) StopCoroutine(_inspectionTimeout);
            if (_inspectionCapture != null) StopCoroutine(_inspectionCapture);
            _inspectionTimeout = _inspectionCapture = null;
            _inspectionResult = "検査をキャンセルしました";
            FinishInspectionResources();
        }
    }
}
