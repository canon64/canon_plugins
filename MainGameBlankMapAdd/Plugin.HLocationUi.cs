using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using MainGameUiInputCapture;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly string[] _hPlaceEditNumbers=new string[9];
        private bool _hPlaceEditValid=true;
        private readonly float[] _hPlaceSliderCenters=new float[9];
        private readonly float[] _hPlaceSliderWidths={10f,45f,10f};
        private readonly string[] _hPlaceWidthInputs={"10","45","10"};
        private int _hPlaceSliderHotControl;
        private Action _hPlaceUiAction;
        private void QueueHPlaceUi(Action action)
        {
            int generation=_hPlaceGeneration;string key=_hPlaceKey;Transform root=_hPlaceRoot;
            _hPlaceUiAction=()=>{if(generation==_hPlaceGeneration&&HPlaceContext(out string current,out Transform currentRoot)&&current==key&&currentRoot==root)action();};
        }
        private void ApplyPendingHPlaceUi()
        {
            if(Event.current.type!=EventType.Layout||_hPlaceUiAction==null)return;
            var action=_hPlaceUiAction;_hPlaceUiAction=null;action();
        }
        private void ReloadHPlaceThumbnails()
        {
            foreach(var texture in _hPlaceImages.Values)if(texture!=null)Destroy(texture);
            _hPlaceImages.Clear();
            if(_hLocations==null||_hPlaceKey==null)return;
            foreach(var preset in _hLocations.Presets.Where(p=>HLocationMapIdentity.Same(p.MapKey,_hPlaceKey))){
                if(string.IsNullOrEmpty(preset.Image))continue;
                Texture2D source=null,small=null;RenderTexture render=null;var active=RenderTexture.active;
                try{
                    string path=Path.Combine(HPlaceImageDir,preset.Image);if(!File.Exists(path))continue;
                    source=new Texture2D(2,2,TextureFormat.RGB24,false);
                    if(!source.LoadImage(File.ReadAllBytes(path)))continue;
                    int height=Mathf.Max(1,Mathf.RoundToInt(256f*source.height/source.width));
                    render=RenderTexture.GetTemporary(256,height,0,RenderTextureFormat.ARGB32);
                    Graphics.Blit(source,render);RenderTexture.active=render;
                    small=new Texture2D(256,height,TextureFormat.RGB24,false);
                    small.ReadPixels(new Rect(0,0,256,height),0,0);small.Apply();_hPlaceImages[preset.Id]=small;small=null;
                }catch(Exception){ /* A missing preview never invalidates its saved placement. */ }
                finally{RenderTexture.active=active;if(render!=null)RenderTexture.ReleaseTemporary(render);if(source!=null)Destroy(source);if(small!=null)Destroy(small);}
            }
        }
        private void DrawHLocationPresets(float width)
        {
            if(!HPlaceContext(out string key,out Transform root)){
                GUILayout.BeginVertical("box");GUILayout.Label("H場所プリセット");GUILayout.Label(_hPlaceStatus??"マップの読み込み完了後に使えます。");GUILayout.EndVertical();return;
            }
            BindHPlaceContext(key,root);
            if(_hPlaceDraft!=null){DrawHLocationEditor();return;}
            DrawCategoryLocationControls();
            GUILayout.BeginVertical("box");
            GUILayout.Label("H場所プリセット");
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!_hPlaceBusy;
            if(GUILayout.Button("ここをHの場所にする",GUILayout.Width(210f),GUILayout.Height(30f)))QueueHPlaceUi(()=>BeginHPlaceCapture());
            GUILayout.Label("注視点へキャラ一式を配置 → 正面合わせ → 中心設定 → UIなし撮影・自動保存");
            if(!string.IsNullOrEmpty(_hPlaceStatus))GUILayout.Label(_hPlaceStatus);
            if(_hPlaceBusy)GUILayout.Label("撮影・保存中です…");
            var presets=_hLocations.Presets.Where(p=>HLocationMapIdentity.Same(p.MapKey,key)).ToArray();
            if(presets.Length==0)GUILayout.Label("まだH場所がありません。カメラで場所を決めて、上のボタンを押してください。");
            const float cardWidth=220f;
            int columns=Mathf.Max(1,Mathf.FloorToInt((width-20f)/(cardWidth+8f)));
            for(int i=0;i<presets.Length;i++){
                if(i%columns==0)GUILayout.BeginHorizontal();
                var preset=presets[i];GUILayout.BeginVertical("box",GUILayout.Width(cardWidth));
                Rect image=GUILayoutUtility.GetRect(cardWidth-12f,124f,GUILayout.Width(cardWidth-12f),GUILayout.Height(124f));
                if(GUI.Button(image,GUIContent.none))QueueHPlaceUi(()=>RecallHPlace(preset.Id));
                if(_hPlaceImages.TryGetValue(preset.Id,out var texture)&&texture!=null)GUI.DrawTexture(image,texture,ScaleMode.ScaleToFit);
                else GUI.Label(image,preset.Legacy?"旧地点・画像なし\n押すと呼び出して撮影":"画像なし\n押して呼び出す");
                GUILayout.Label((_hPlaceActive?.Id==preset.Id?"● ":"")+preset.Name,GUILayout.Width(cardWidth-12f));
                if(GUILayout.Button("編集",GUILayout.Width(72f)))QueueHPlaceUi(()=>BeginHPlaceEdit(preset.Id));
                bool cardEnabled=GUI.enabled;GUI.enabled=cardEnabled&&!preset.Legacy;
                if(GUILayout.Button(preset.IsDefault?"デフォルト解除":"デフォルトにする",GUILayout.Width(152f)))QueueHPlaceUi(()=>SetHPlaceDefault(preset.Id,!preset.IsDefault));
                GUI.enabled=cardEnabled;
                if(preset.IsDefault)GUILayout.Label("★ 次回の開始場所");
                else if(preset.Legacy)GUILayout.Label("撮影・保存後にデフォルトを指定できます。");
                GUILayout.EndVertical();
                if(i%columns==columns-1||i==presets.Length-1)GUILayout.EndHorizontal();
            }
            GUI.enabled=enabled;GUILayout.EndVertical();
        }
        private void BeginHPlaceEdit(string id)
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if(_hPlaceBusy||!HPlaceContext(out string key,out Transform root))return;
            if(!AdoptHPlaceCamera()){_hPlaceStatus="別の機能がカメラを使用中です。";return;}
            BindHPlaceContext(key,root);var preset=_hLocations.Presets.SingleOrDefault(p=>p.Id==id&&HLocationMapIdentity.Same(p.MapKey,key));if(preset==null)return;
            HPlaceSnapshot before=null;
            try{
                before=SnapshotHPlace();var draft=preset.Clone();
                if(draft.Legacy){draft.Rotation=HA((Quaternion.Inverse(root.rotation)*_femaleChara.transform.rotation).eulerAngles);SaveHPlaceCamera(draft,root,before.Camera);draft.Legacy=false;}
                ApplyHPlace(draft,root,false);_hPlaceEditBefore=before;_hPlaceDraft=draft;_hPlaceEditValid=true;SyncHPlaceEditNumbers();
                for(int group=0;group<3;group++){RecenterHPlaceSliders(group);_hPlaceWidthInputs[group]=_hPlaceSliderWidths[group].ToString("R",CultureInfo.InvariantCulture);}
                _hPlaceStatus=null;
            }catch(Exception ex){RestoreHPlace(before);_hPlaceStatus="編集を開けません："+ex.Message;}
        }
        private void SyncHPlaceEditNumbers()
        {
            if(_hPlaceDraft==null)return;
            for(int i=0;i<9;i++)_hPlaceEditNumbers[i]=(i<3?_hPlaceDraft.Position[i]:i<6?_hPlaceDraft.Rotation[i-3]:_hPlaceDraft.Center[i-6]).ToString("R",CultureInfo.InvariantCulture);
        }
        private void SaveHPlaceEdit()
        {
            if(_hPlaceBusy||_hPlaceDraft==null||!_hPlaceEditValid||!HPlaceContext(out string key,out Transform root)||!HLocationMapIdentity.Same(key,_hPlaceDraft.MapKey))return;
            try{
                var preset=_hPlaceDraft.Clone();preset.Name=(preset.Name??"").Trim();preset.Validate();
                var document=_hLocations.Clone();int index=document.Presets.FindIndex(p=>p.Id==preset.Id&&HLocationMapIdentity.Same(p.MapKey,key));
                if(index<0)throw new InvalidOperationException("編集対象のH場所がありません。");
                document.Presets[index]=preset;SaveHLocationDocument(document);
                _hLocations=document;_hPlaceOriginal=_hPlaceOriginal??_hPlaceEditBefore;_hPlaceActive=preset.Clone();_hPlaceActive.MapKey=key;
                ReleaseHPlaceSliderCapture();_hPlaceDraft=null;_hPlaceEditBefore=null;
                _hPlaceStatus="「"+preset.Name+"」の配置を保存しました。画像は変更していません。";
            }catch(Exception ex){_hPlaceStatus="配置を保存できません："+ex.Message;}
        }
        private void CloseHPlaceEdit()
        {
            if(_hPlaceBusy)return;EndCategoryGizmo();ReleaseHPlaceSliderCapture();RestoreHPlace(_hPlaceEditBefore);_hPlaceDraft=null;_hPlaceEditBefore=null;_hPlaceStatus="編集を閉じました。変更は保存していません。";
        }
        private float HPlaceEditValue(int index)=>index<3?_hPlaceDraft.Position[index]:index<6?_hPlaceDraft.Rotation[index-3]:_hPlaceDraft.Center[index-6];
        private void SetHPlaceTypedNumber(int index,string value)
        {
            _hPlaceEditNumbers[index]=value;
            if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float number)&&!float.IsNaN(number)&&!float.IsInfinity(number))_hPlaceSliderCenters[index]=number;
        }
        private void RecenterHPlaceSliders(int group)
        {
            for(int axis=0;axis<3;axis++)_hPlaceSliderCenters[group*3+axis]=HPlaceEditValue(group*3+axis);
        }
        private bool SetHPlaceSliderWidth(int group,float width)
        {
            if(float.IsNaN(width)||float.IsInfinity(width)||width<=0)return false;
            for(int axis=0;axis<3;axis++){
                float value=HPlaceEditValue(group*3+axis);
                if(double.IsInfinity((double)value+width)||(double)value+width>float.MaxValue||(double)value-width<float.MinValue||value+width==value-width)return false;
            }
            _hPlaceSliderWidths[group]=width;RecenterHPlaceSliders(group);return true;
        }
        private void ReleaseHPlaceSliderCapture()
        {
            if(_hPlaceSliderHotControl!=0&&GUIUtility.hotControl==_hPlaceSliderHotControl)GUIUtility.hotControl=0;
            _hPlaceSliderHotControl=0;
            UiInputCaptureApi.Sync("MainGameBlankMapAdd.PlaybackBar","h-location-slider",false);
        }
        private void SyncHPlaceSliderCapture()
        {
            if(_hPlaceSliderHotControl==0)return;
            if(!Input.GetMouseButton(0)||GUIUtility.hotControl!=_hPlaceSliderHotControl){ReleaseHPlaceSliderCapture();return;}
            UiInputCaptureApi.Sync("MainGameBlankMapAdd.PlaybackBar","h-location-slider",true);
        }
        private void DrawHPlaceSliderWidth(int group)
        {
            float[] choices=group==1?new[]{5f,45f,180f}:new[]{1f,10f,1000f};
            string[] labels=group==1?new[]{"微調整 ±5°","通常 ±45°","粗調整 ±180°"}:new[]{"微調整 ±1","通常 ±10","粗調整 ±1000"};
            int selected=Array.IndexOf(choices,_hPlaceSliderWidths[group]);
            int next=GUILayout.Toolbar(selected,labels,GUILayout.Width(350f),GUILayout.Height(28f));
            if(next!=selected&&next>=0&&SetHPlaceSliderWidth(group,choices[next]))_hPlaceWidthInputs[group]=choices[next].ToString("R",CultureInfo.InvariantCulture);
            GUILayout.BeginHorizontal();GUILayout.Label("調整幅 ±",GUILayout.Width(75f));
            string input=GUILayout.TextField(_hPlaceWidthInputs[group],GUILayout.Width(90f));
            if(input!=_hPlaceWidthInputs[group]){
                _hPlaceWidthInputs[group]=input;
                if(float.TryParse(input,NumberStyles.Float,CultureInfo.InvariantCulture,out float width))SetHPlaceSliderWidth(group,width);
            }
            if(GUILayout.Button("現在値の周辺を調整",GUILayout.Width(168f),GUILayout.Height(28f)))RecenterHPlaceSliders(group);
            GUILayout.EndHorizontal();
        }
        private void DrawHLocationEditor()
        {
            if(_hPlaceDraft==null)return;
            if (_categoryEditing) {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("移動",GUILayout.Width(75f)))_categoryGizmo?.SetMode(MainGameTransformGizmo.GizmoMode.Move);
                if(GUILayout.Button("回転",GUILayout.Width(75f)))_categoryGizmo?.SetMode(MainGameTransformGizmo.GizmoMode.Rotate);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginVertical("box");GUILayout.Label(_categoryEditing?"体位の場所を調整：「"+_hPlaceDraft.Name+"」":"H場所の編集：「"+_hPlaceDraft.Name+"」");
            if(_categoryEditing)GUILayout.Label("この体位だけの位置・向きを保存します。未保存の体位は共通の初期位置を使います。");
            else {
                GUILayout.Label("下の画像は保存済みの見本です。UIを隠して視点を調整し、マップタブへ戻って撮影できます。編集中の内容は保持します。");
                if(_hPlaceImages.TryGetValue(_hPlaceDraft.Id,out var image)&&image!=null){Rect preview=GUILayoutUtility.GetRect(256f,144f,GUILayout.Width(256f),GUILayout.Height(144f));GUI.DrawTexture(preview,image,ScaleMode.ScaleToFit);}
            }
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!_hPlaceBusy;
            if(!_categoryEditing){GUILayout.BeginHorizontal();GUILayout.Label("名前",GUILayout.Width(75f));_hPlaceDraft.Name=GUILayout.TextField(_hPlaceDraft.Name??"",80,GUILayout.Width(220f));GUILayout.EndHorizontal();}
            GUILayout.Label("マップ基準の座標・角度（度）。数値変更は仮表示です。");
            bool changed=false;
            for(int i=0;i<(_categoryEditing?6:9);i++){
                if(i%3==0){GUILayout.BeginVertical("box");GUILayout.Label(i<3?"配置座標（マップ単位）":i<6?(_categoryEditing?"角度（度・キャラの基準位置で回転）":"角度（度・保存した中心で回転）"):"回転中心（マップ単位）");DrawHPlaceSliderWidth(i/3);}
                GUILayout.BeginHorizontal();GUILayout.Label("XYZ"[i%3].ToString(),GUILayout.Width(22f));
                string value=GUILayout.TextField(_hPlaceEditNumbers[i]??"",GUILayout.Width(110f),GUILayout.Height(28f));
                if(value!=_hPlaceEditNumbers[i]){
                    SetHPlaceTypedNumber(i,value);changed=true;
                }
                float current=HPlaceEditValue(i),range=_hPlaceSliderWidths[i/3];
                float minimum=_hPlaceSliderCenters[i]-range,maximum=_hPlaceSliderCenters[i]+range;
                // Draw an out-of-range dependent coordinate without silently clamping its value.
                float shown=Mathf.Clamp(current,minimum,maximum);
                int previousHot=GUIUtility.hotControl;
                float slid=GUILayout.HorizontalSlider(shown,minimum,maximum,GUILayout.Width(220f),GUILayout.Height(28f));
                if(GUIUtility.hotControl!=0&&GUIUtility.hotControl!=previousHot){_hPlaceSliderHotControl=GUIUtility.hotControl;UiInputCaptureApi.Sync("MainGameBlankMapAdd.PlaybackBar","h-location-slider",true);}
                if(slid!=shown){_hPlaceEditNumbers[i]=slid.ToString("R",CultureInfo.InvariantCulture);changed=true;}
                GUILayout.EndHorizontal();
                if(i%3==2)GUILayout.EndVertical();
            }
            SyncHPlaceSliderCapture();
            if(changed)PreviewHPlaceEdit();
            GUILayout.Label(_hPlaceEditValid?"":"数値を正しく入力してください。");
            GUILayout.Label(_hPlaceStatus??"");
            GUILayout.BeginHorizontal();GUI.enabled=enabled&&!_hPlaceBusy&&_hPlaceEditValid&&!string.IsNullOrWhiteSpace(_hPlaceDraft.Name);
            if(GUILayout.Button(_categoryEditing?"この場所を覚える":"決定",GUILayout.Width(_categoryEditing?155f:90f)))QueueHPlaceUi(_categoryEditing?(Action)SaveCategoryLocation:SaveHPlaceEdit);
            if(!_categoryEditing&&GUILayout.Button("画像を撮って保存",GUILayout.Width(160f)))QueueHPlaceUi(()=>BeginHPlaceCapture(_hPlaceDraft,useCurrentCamera:true));
            GUI.enabled=enabled&&!_hPlaceBusy;
            if(GUILayout.Button(_categoryEditing?"キャンセル":"閉じる",GUILayout.Width(100f)))QueueHPlaceUi(CloseHPlaceEdit);
            if(!_categoryEditing&&GUILayout.Button("削除",GUILayout.Width(70f)))QueueHPlaceUi(DeleteHPlaceEdit);
            GUILayout.EndHorizontal();GUI.enabled=enabled;GUILayout.EndVertical();
            if(_categoryEditing){
                GUILayout.Label("「この場所を覚える」でこのマップ・タグの配置を保存。「キャンセル」で編集前に戻ります。");
                GUILayout.Label("UIを隠しても編集中の内容を保持します。体位を変更すると未保存の編集を取り消します。");
            }else{
                GUILayout.Label("視点は自由に動かせます。決定：名前・配置だけ保存。画像を撮って保存：現在の構図も撮り直して保存。");
                GUILayout.Label("UIを隠すだけでは編集は終了しません。この欄の「閉じる」は変更を破棄して編集前へ戻します。");
            }
        }
        private void PreviewHPlaceEdit()
        {
            if(_hPlaceDraft==null||!HPlaceContext(out string key,out Transform root)||!HLocationMapIdentity.Same(key,_hPlaceDraft.MapKey))return;
            float[] values=new float[9];_hPlaceEditValid=true;
            for(int i=0;i<9;i++)if(!float.TryParse(_hPlaceEditNumbers[i],NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||float.IsNaN(values[i])||float.IsInfinity(values[i])){_hPlaceEditValid=false;return;}
            var next=_hPlaceDraft.Clone();Vector3 oldRotation=HV(next.Rotation);
            next.Position=values.Take(3).ToArray();next.Rotation=values.Skip(3).Take(3).ToArray();next.Center=values.Skip(6).Take(3).ToArray();
            if(!_categoryEditing&&oldRotation!=HV(next.Rotation)){
                Quaternion delta=root.rotation*Quaternion.Euler(HV(next.Rotation))*Quaternion.Inverse(Quaternion.Euler(oldRotation))*Quaternion.Inverse(root.rotation);
                next.Position=HA(root.InverseTransformPoint(HLocationGeometry.RotateAround(root.TransformPoint(HV(next.Position)),root.TransformPoint(HV(next.Center)),delta)));
            }
            try{
                ApplyHPlace(next,root,false);_hPlaceDraft=next;
                SyncCategoryGizmo();
                if(oldRotation!=HV(next.Rotation))for(int axis=0;axis<3;axis++)_hPlaceEditNumbers[axis]=next.Position[axis].ToString("R",CultureInfo.InvariantCulture);
                _hPlaceStatus=null;
            }
            catch(Exception ex){_hPlaceStatus="仮表示に失敗しました："+ex.Message;_hPlaceEditValid=false;}
        }
        private void DeleteHPlaceEdit()
        {
            if(_hPlaceBusy||_hPlaceDraft==null||!HPlaceContext(out string key,out _)||!HLocationMapIdentity.Same(key,_hPlaceDraft.MapKey))return;
            try{
                var document=_hLocations.Clone();document.Presets.RemoveAll(p=>p.Id==_hPlaceDraft.Id&&HLocationMapIdentity.Same(p.MapKey,key));
                SaveHLocationDocument(document);string id=_hPlaceDraft.Id;CloseHPlaceEdit();_hLocations=document;
                if(_hPlaceActive?.Id==id)_hPlaceActive=null;ReloadHPlaceThumbnails();_hPlaceStatus="H場所を削除しました。";
            }catch(Exception ex){_hPlaceStatus="削除できません："+ex.Message;}
        }
    }
}
