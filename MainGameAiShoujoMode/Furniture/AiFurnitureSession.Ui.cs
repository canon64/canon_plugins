using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MainGameTransformGizmo;
using UnityEngine;

namespace MainGameAiShoujoMode.Furniture
{
    internal sealed partial class AiFurnitureSession
    {
        private GameObject _preview;
        private FurnitureItem _previewItem;
        private string _editing,_search="";
        private string _category="すべて";
        private FurnitureItem[] _filtered;
        private bool _show,_categories;
        private int _page;
        private Vector2 _placedScroll;
        private Action _pending,_resumeEdit;
        private TransformGizmo _gizmo;
        private readonly Dictionary<string,string> _fields=new Dictionary<string,string>();
        private readonly Dictionary<string,float> _centers=new Dictionary<string,float>();
        private bool[] _colliderStates;
        private float _rangeScale=System.Math.Sign(int.MaxValue);
        internal bool UsesWindow=>_show;
        private FurnitureGrid _grid;
        private FurnitureItem[] _pageItems=Array.Empty<FurnitureItem>();
        private readonly Dictionary<int,Texture2D> _thumbnails=new Dictionary<int,Texture2D>();
        private string _thumbnailPage;
        private GUIStyle _tileName;
        private GUIStyle _tileHint;
        private float _gridTop;
        private string CategoryName(string id)=>id=="すべて"?id:(_catalog.categories??Array.Empty<FurnitureCategory>()).FirstOrDefault(c=>c.id.ToString(CultureInfo.InvariantCulture)==id)?.name??("分類 "+id);
        internal void Draw()
        {
            if(Event.current.type==EventType.Layout&&_pending!=null){var action=_pending;_pending=null;Run(action);}
            GUILayout.BeginVertical(GUI.skin.box);
            var show=GUILayout.Toggle(_show,"家具配置",GUI.skin.button,GUILayout.ExpandWidth(false));
            if(show!=_show)_pending=()=>{if(!show&&Editing)CancelEdit();_show=show;};
            if(_show)
            {
                if(_status!=null)GUILayout.Label(_status);
                if(!Allowed||_broken)GUILayout.Label("家具を編集できません。表示中の理由とcanon_gateの許可を確認してください。");
                else if(_preview!=null)DrawEdit();
                else
                {
                    GUILayout.BeginHorizontal();GUILayout.Label("検索",GUILayout.ExpandWidth(false));
                    GUI.SetNextControlName("parameter:furniture:search");var search=GUILayout.TextField(_search);if(search!=_search){_search=search;_page=default(int);Filter();}
                    if(GUILayout.Button("分類："+CategoryName(_category),GUILayout.ExpandWidth(false)))_pending=()=>_categories=!_categories;
                    GUILayout.EndHorizontal();
                    if(_categories)
                    {
                        foreach(var category in new[]{"すべて"}.Concat(_catalog.items.Select(i=>i.category.ToString(CultureInfo.InvariantCulture)).Distinct().OrderBy(v=>v)))
                            if(GUILayout.Button(CategoryName(category),GUILayout.ExpandWidth(false)))
                            { _pending=()=>{_category=category;_categories=false;_page=default(int);Filter();}; }
                    }
                    if(_filtered==null)Filter();
                    PrepareGrid();
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button("前のページ",GUILayout.ExpandWidth(false)))_page=Math.Max(default(int),_page-I("math.one"));
                    GUILayout.Label((_page+I("math.one"))+" / "+Math.Max(I("math.one"),(_filtered.Length+_grid.PageSize-I("math.one"))/_grid.PageSize),GUILayout.ExpandWidth(false));
                    if(GUILayout.Button("次のページ",GUILayout.ExpandWidth(false)))_page=Math.Min(Math.Max(default(int),(_filtered.Length-I("math.one"))/_grid.PageSize),_page+I("math.one"));
                    GUILayout.EndHorizontal();
                    if(Event.current.type==EventType.Repaint)_gridTop=GUILayoutUtility.GetLastRect().yMax;
                    DrawFurnitureGrid();
                    if(!_placed.Any())GUILayout.Label("配置済みの家具はありません。");
                    else GUILayout.Label("配置済みの家具");
                    if(_placed.Any())_placedScroll=GUILayout.BeginScrollView(_placedScroll,GUILayout.Height(N("placedHeight")));
                    foreach(var entry in _placed.Values.ToArray())
                    {
                        GUILayout.BeginHorizontal();
                        if(_thumbnails.TryGetValue(entry.Item.id,out var placedIcon))GUILayout.Label(placedIcon,GUILayout.Width(N("placedIconSize")),GUILayout.Height(N("placedIconSize")));
                        GUILayout.Label(entry.Item.name,GUILayout.ExpandWidth(false));
                        var old=GUI.enabled;GUI.enabled=old&&!_reservations.Busy(entry.Data.id);
                        if(GUILayout.Button("移動・回転",GUILayout.ExpandWidth(false)))_pending=()=>BeginEdit(entry.Item,entry.Data.id);
                        if(GUILayout.Button("撤去",GUILayout.ExpandWidth(false)))_pending=()=>Remove(entry.Data.id);
                        GUI.enabled=old;GUILayout.EndHorizontal();
                        foreach(var slot in entry.Item.slots??Array.Empty<FurnitureSlot>())
                        {
                            if(slot.status!="ready"){GUILayout.Label("利用不可："+slot.error);continue;}
                            GUI.enabled=old&&!_reservations.Busy(entry.Data.id)&&_resident()!=null;
                            if(GUILayout.Button("このベッドで女の子を休ませる",GUILayout.ExpandWidth(false)))_pending=()=>QueueBed(Target(entry.Data.id,slot));
                            GUI.enabled=old;
                        }
                    }
                    if(_placed.Any())GUILayout.EndScrollView();
                }
            }
            GUILayout.EndVertical();
            if(_show&&Event.current.type==EventType.Repaint&&!string.IsNullOrWhiteSpace(GUI.tooltip))
            {
                if(_tileHint==null)_tileHint=new GUIStyle(GUI.skin.box){wordWrap=true,alignment=TextAnchor.UpperLeft};
                var width=Math.Min(_host.Session.PanelRect.width-N("gridPadding"),N("tileWidth")*N("math.two"));
                var hint=new GUIContent(GUI.tooltip);var height=_tileHint.CalcHeight(hint,width);var pointer=Event.current.mousePosition;
                var rect=new Rect(Mathf.Clamp(pointer.x+N("tileInset"),N("tileInset"),_host.Session.PanelRect.width-width-N("tileInset")),Mathf.Clamp(pointer.y+N("tileInset"),N("tileInset"),_host.Session.PanelRect.height-height-N("tileInset")),width,height);
                GUI.Box(rect,hint,_tileHint);
            }
        }
        private void PrepareGrid()
        {
            if(Event.current.type!=EventType.Layout&&_grid!=null)return;
            var panel=_host.Session.PanelRect;
            _grid=FurnitureGrid.Fit(panel.width-N("gridPadding"),panel.height-(_gridTop>default(float)?_gridTop+N("gridBottomSpace"):N("gridChromeHeight"))-(_placed.Any()?N("placedHeight"):default(float)),N("tileWidth"),N("tileHeight"),I("math.one"));
            _page=Math.Min(_page,Math.Max(default(int),(_filtered.Length-I("math.one"))/_grid.PageSize));
            _pageItems=_filtered.Skip(_page*_grid.PageSize).Take(_grid.PageSize).ToArray();
            var key=string.Join("|",_pageItems.Select(i=>i.id.ToString(CultureInfo.InvariantCulture)).Concat(_placed.Values.Select(p=>p.Item.id.ToString(CultureInfo.InvariantCulture))));
            if(key==_thumbnailPage)return;
            ClearThumbnails();_thumbnailPage=key;
            foreach(var item in _pageItems.Concat(_placed.Values.Select(p=>p.Item)).GroupBy(i=>i.id).Select(g=>g.First()))
            {
                if(string.IsNullOrWhiteSpace(item.thumbnail)||item.thumbnail!="Thumbnails/"+item.id.ToString(CultureInfo.InvariantCulture)+".png")continue;
                Texture2D image=null;
                try
                {
                    image=new Texture2D(I("math.two"),I("math.two"),TextureFormat.RGBA32,false);
                    if(!image.LoadImage(File.ReadAllBytes(Path.Combine(_content,"Furniture",item.thumbnail)),true))throw new InvalidDataException("画像を読み込めません。");
                    _thumbnails.Add(item.id,image);
                }
                catch{if(image!=null)UnityEngine.Object.Destroy(image);}
            }
        }
        private void ClearThumbnails(){foreach(var texture in _thumbnails.Values)if(texture!=null)UnityEngine.Object.Destroy(texture);_thumbnails.Clear();_thumbnailPage=null;}
        private void DrawFurnitureGrid()
        {
            if(_tileName==null)_tileName=new GUIStyle(GUI.skin.label){alignment=TextAnchor.UpperCenter,wordWrap=true,clipping=TextClipping.Clip};
            for(var row=default(int);row<_grid.Rows;row++)
            {
                GUILayout.BeginHorizontal();
                for(var column=default(int);column<_grid.Columns;column++)
                {
                    var index=row*_grid.Columns+column;
                    var rect=GUILayoutUtility.GetRect(N("tileWidth"),N("tileHeight"),GUILayout.ExpandWidth(true),GUILayout.Height(N("tileHeight")));
                    if(index>=_pageItems.Length)continue;
                    var item=_pageItems[index];var old=GUI.enabled;GUI.enabled=old&&item.Available;
                    if(GUI.Button(rect,new GUIContent("",item.Available?item.name:item.name+"\n設置不可："+item.error)))_pending=()=>BeginEdit(item,null);
                    var imageRect=new Rect(rect.center.x-N("iconSize")*N("math.half"),rect.y+N("tileInset"),N("iconSize"),N("iconSize"));
                    if(_thumbnails.TryGetValue(item.id,out var image))GUI.DrawTexture(imageRect,image,ScaleMode.ScaleToFit,true);
                    else GUI.Label(imageRect,"画像なし",_tileName);
                    GUI.Label(new Rect(rect.x+N("tileInset"),imageRect.yMax,rect.width-N("tileInset")*N("math.two"),rect.yMax-imageRect.yMax),item.name+(item.Available?"":"（設置不可）"),_tileName);
                    GUI.enabled=old;
                }
                GUILayout.EndHorizontal();
            }
        }
        private void Filter(){_filtered=_catalog.items.Where(i=>(_category=="すべて"||i.category.ToString(CultureInfo.InvariantCulture)==_category)&&i.name.IndexOf(_search,StringComparison.OrdinalIgnoreCase)>=default(int)).ToArray();}
        private void Run(Action action){try{action();}catch(Exception error){_status=error.GetBaseException().Message;}}
        private void BeginEdit(FurnitureItem item,string id)
        {
            Require();if(_beds.Any()||_resident()?.Conversing==true||_resident()?.InH==true||_resident()?.State.activity!=null)throw new InvalidOperationException("会話・生活行動を終えてから家具を編集してください。");
            CancelEdit();
            if(id!=null&&_reservations.Busy(id))throw new InvalidOperationException("利用中のベッドは編集できません。");
            var camera=_host.Walker?.ViewCamera??throw new InvalidOperationException("配置に使う視点がありません。");
            try
            {
                _preview=Create(item);_previewItem=item;_editing=id;
                if(id!=null){Pose(_preview,_placed[id].Data);_placed[id].Root.SetActive(false);ChangedLayout();}
                else
                {
                    var direction=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
                    var point=camera.transform.position+direction*N("forward")*SourceScale;
                    _preview.transform.SetPositionAndRotation(point,Quaternion.LookRotation(direction,Vector3.up));
                    if(!Ground(point,out var floor,_preview))throw new InvalidOperationException("配置先の地面がありません。地面のある場所へ視点を向けてください。");
                    _preview.transform.position+=Vector3.up*(floor.point.y-BoundsOf(_preview).min.y);
                }
                var colliders=_preview.GetComponentsInChildren<Collider>(true);_colliderStates=colliders.Select(c=>c.enabled).ToArray();foreach(var collider in colliders)collider.enabled=false;
                if(_actor.ConversationTarget!=null)_resumeEdit=_actor.PauseForConversation(camera.transform);
                _gizmo=TransformGizmoApi.AttachForActivity(_preview,_permit);
                if(_gizmo!=null){_gizmo.SetMode(GizmoMode.Move);_gizmo.SetVisible(true);_gizmo.TransformCommitted+=CaptureFields;TransformGizmoApi.EnableCameraInputCaptureOnDrag(_gizmo);}
                _rangeScale=N("math.one");CaptureFields();_status="仮配置中。確定するまで保存されません。";
            }
            catch{CancelEdit();throw;}
        }
        private void CaptureFields()
        {
            if(_preview==null)return;var pose=Capture(_editing??Guid.NewGuid().ToString("N"),_previewItem,_preview);
            _fields.Clear();_centers.Clear();
            foreach(var pair in new[]{new KeyValuePair<string,Vector3>("位置",Vector(pose.position)),new KeyValuePair<string,Vector3>("角度",Vector(pose.rotation))})
                foreach(var axis in new[]{"X","Y","Z"}){var value=axis=="X"?pair.Value.x:axis=="Y"?pair.Value.y:pair.Value.z;_fields[pair.Key+axis]=value.ToString("R",CultureInfo.InvariantCulture);_centers[pair.Key+axis]=value;}
        }
        private void DrawEdit()
        {
            GUILayout.Label("仮配置："+_previewItem.name);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("移動",GUILayout.ExpandWidth(false)))_gizmo?.SetMode(GizmoMode.Move);
            if(GUILayout.Button("回転",GUILayout.ExpandWidth(false)))_gizmo?.SetMode(GizmoMode.Rotate);
            if(GUILayout.Button("現在位置の周辺へ調整範囲を合わせる",GUILayout.ExpandWidth(false)))CaptureFields();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("細かく調整（範囲を半分）",GUILayout.ExpandWidth(false))){_rangeScale/=N("math.two");CaptureFields();}
            if(GUILayout.Button("大きく調整（範囲を倍）",GUILayout.ExpandWidth(false))){_rangeScale*=N("math.two");CaptureFields();}
            GUILayout.EndHorizontal();
            var changed=false;
            foreach(var kind in new[]{"位置","角度"})foreach(var axis in new[]{"X","Y","Z"})
            {
                var key=kind+axis;GUILayout.BeginHorizontal();GUILayout.Label(key,GUILayout.ExpandWidth(false));
                var validNumber=float.TryParse(_fields[key],NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&!float.IsNaN(value)&&!float.IsInfinity(value);
                var width=(kind=="位置"?N("positionRange"):N("rotationRange"))*_rangeScale;
                var enabled=GUI.enabled;GUI.enabled=enabled&&validNumber;
                var slider=GUILayout.HorizontalSlider(value,_centers[key]-width,_centers[key]+width);
                GUI.enabled=enabled;
                if(validNumber&&slider!=value){_fields[key]=slider.ToString("R",CultureInfo.InvariantCulture);changed=true;}
                GUI.SetNextControlName("parameter:furniture:"+key);var text=GUILayout.TextField(_fields[key],GUILayout.Width(N("numberWidth")));
                if(text!=_fields[key]){_fields[key]=text;changed=true;if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)&&!float.IsNaN(number)&&!float.IsInfinity(number))_centers[key]=number;}
                GUILayout.EndHorizontal();
            }
            if(changed)
            {
                var valid=_fields.All(f=>float.TryParse(f.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&!float.IsNaN(v)&&!float.IsInfinity(v));
                if(valid)
                {
                    float Field(string key)=>float.Parse(_fields[key],CultureInfo.InvariantCulture);
                    Pose(_preview,new FurnitureInstance{position=Value(new Vector3(Field("位置X"),Field("位置Y"),Field("位置Z"))),rotation=Value(new Vector3(Field("角度X"),Field("角度Y"),Field("角度Z")))});
                }
            }
            GUILayout.BeginHorizontal();if(GUILayout.Button("配置を確定",GUILayout.ExpandWidth(false)))_pending=CommitEdit;
            if(GUILayout.Button("取消",GUILayout.ExpandWidth(false)))_pending=CancelEdit;GUILayout.EndHorizontal();
        }
        private void CommitEdit()
        {
            Require();if(_preview==null)return;
            if(_fields.Any(f=>!float.TryParse(f.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)||float.IsNaN(v)||float.IsInfinity(v)))throw new InvalidOperationException("数値入力を確定してください。");
            var error=CheckPlacement(_preview,_previewItem);if(error!=null)throw new InvalidOperationException(error);
            var value=Capture(_editing??Guid.NewGuid().ToString("N"),_previewItem,_preview);var next=FurnitureJson.Copy(_layout);
            next.items=next.items.Where(i=>i.id!=value.id).Concat(new[]{value}).ToArray();_store.Write(next);
            if(_placed.TryGetValue(value.id,out var old)){old.Root.SetActive(false);UnityEngine.Object.Destroy(old.Root);_placed.Remove(value.id);}
            ReleaseGizmo();var placed=new Placed{Data=value,Item=_previewItem,Root=_preview};_placed.Add(value.id,placed);
            var colliders=_preview.GetComponentsInChildren<Collider>(true);for(var index=default(int);index<colliders.Length;index++)colliders[index].enabled=_colliderStates[index];
            AddObstacle(placed);_preview=null;_previewItem=null;_editing=null;_layout=next;ChangedLayout();var resume=_resumeEdit;_resumeEdit=null;resume?.Invoke();_status="家具の配置を保存しました。";
        }
        private void ReleaseGizmo(){if(_gizmo==null)return;_gizmo.TransformCommitted-=CaptureFields;TransformGizmoApi.DisableCameraInputCaptureOnDrag(_gizmo);UnityEngine.Object.Destroy(_gizmo);_gizmo=null;}
        internal void CancelEdit()
        {
            _pending=null;ReleaseGizmo();if(_preview!=null){_preview.SetActive(false);UnityEngine.Object.Destroy(_preview);}_preview=null;_previewItem=null;
            if(_editing!=null&&_placed.TryGetValue(_editing,out var old)){old.Root.SetActive(true);ChangedLayout();}_editing=null;
            var resume=_resumeEdit;_resumeEdit=null;if(Allowed)resume?.Invoke();
        }
        private void Remove(string id)
        {
            Require();if(_reservations.Busy(id))throw new InvalidOperationException("利用中のベッドは撤去できません。");
            var next=FurnitureJson.Copy(_layout);next.items=next.items.Where(i=>i.id!=id).ToArray();_store.Write(next);
            if(_placed.TryGetValue(id,out var old)){old.Root.SetActive(false);UnityEngine.Object.Destroy(old.Root);_placed.Remove(id);}_layout=next;ChangedLayout();_status="家具を撤去して保存しました。";
        }
    }
}
