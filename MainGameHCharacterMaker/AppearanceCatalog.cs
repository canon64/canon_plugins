using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameHCharacterMaker
{
    internal sealed class AppearanceColor
    {
        internal string Key, Name;
        internal Func<ChaControl, Color> Read;
        internal Action<ChaControl, Color> Write;
        internal AppearanceColor(string key, string name, Func<ChaControl, Color> read, Action<ChaControl, Color> write)
        { Key = key; Name = name; Read = read; Write = write; }
    }
    internal sealed class AppearancePart
    {
        internal string Key, Name;
        internal int Page;
        internal ChaListDefine.CategoryNo Category;
        internal Func<ChaControl, int> Read;
        internal Action<ChaControl, int> Write;
        internal readonly List<AppearanceColor> Colors = new List<AppearanceColor>();
        internal AppearancePart(int page, string key, string name, ChaListDefine.CategoryNo category,
            Func<ChaControl, int> read, Action<ChaControl, int> write)
        { Page = page; Key = key; Name = name; Category = category; Read = read; Write = write; }
    }
    internal static class AppearanceCatalog
    {
        internal static List<AppearancePart> Create()
        {
            var list = new List<AppearancePart>();
            var hairCategories = new[] { ChaListDefine.CategoryNo.bo_hair_b, ChaListDefine.CategoryNo.bo_hair_f,
                ChaListDefine.CategoryNo.bo_hair_s, ChaListDefine.CategoryNo.bo_hair_o };
            var hairNames = new[] { "後ろ髪", "前髪", "横髪", "オプション髪" };
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                var part = new AppearancePart(2, "hair." + i, hairNames[i], hairCategories[i],
                    c => c.fileHair.parts[slot].id, (c, v) => {
                        c.ChangeHairNoAsync(slot, v, true);
                        if (c.fileHair.parts[slot].id != v) throw new InvalidOperationException("髪の読み込みに失敗しました。");
                    });
                part.Colors.Add(new AppearanceColor(part.Key + ".base", "基本色", c => c.fileHair.parts[slot].baseColor,
                    (c, v) => { c.fileHair.parts[slot].baseColor = v; c.ChangeSettingHairColor(slot, true, false, false); }));
                part.Colors.Add(new AppearanceColor(part.Key + ".start", "根元の色", c => c.fileHair.parts[slot].startColor,
                    (c, v) => { c.fileHair.parts[slot].startColor = v; c.ChangeSettingHairColor(slot, false, true, false); }));
                part.Colors.Add(new AppearanceColor(part.Key + ".end", "毛先の色", c => c.fileHair.parts[slot].endColor,
                    (c, v) => { c.fileHair.parts[slot].endColor = v; c.ChangeSettingHairColor(slot, false, false, true); }));
                list.Add(part);
            }
            for (int i = 0; i < 2; i++)
            {
                int side = i;
                var part = new AppearancePart(3, "eye." + i, i == 0 ? "左の瞳" : "右の瞳", ChaListDefine.CategoryNo.mt_eye,
                    c => c.fileFace.pupil[side].id, (c, v) => {
                        c.fileFace.pupil[side].id = v;
                        Require(c.ChangeSettingEye((byte)side, true, false, true));
                    });
                part.Colors.Add(new AppearanceColor(part.Key + ".base", "瞳の色", c => c.fileFace.pupil[side].baseColor,
                    (c, v) => { c.fileFace.pupil[side].baseColor = v; Require(c.ChangeSettingEye((byte)side, false, false, true)); }));
                list.Add(part);
            }
            Add(list, 3, "brow", "眉", ChaListDefine.CategoryNo.mt_eyebrow,
                c => c.fileFace.eyebrowId, (c,v) => c.fileFace.eyebrowId=v, c=>Require(c.ChangeSettingEyebrow()),
                c=>c.fileFace.eyebrowColor, (c,v)=> { c.fileFace.eyebrowColor=v; c.ChangeSettingEyebrowColor(); });
            Add(list, 3, "highlight.up", "上ハイライト", ChaListDefine.CategoryNo.mt_eye_hi_up,
                c=>c.fileFace.hlUpId, (c,v)=>c.fileFace.hlUpId=v, c=>Require(c.ChangeSettingEyeHiUp()),
                c=>c.fileFace.hlUpColor, (c,v)=> { c.fileFace.hlUpColor=v; c.ChangeSettingEyeHiUpColor(); });
            Add(list, 3, "highlight.down", "下ハイライト", ChaListDefine.CategoryNo.mt_eye_hi_down,
                c=>c.fileFace.hlDownId, (c,v)=>c.fileFace.hlDownId=v, c=>Require(c.ChangeSettingEyeHiDown()),
                c=>c.fileFace.hlDownColor, (c,v)=> { c.fileFace.hlDownColor=v; c.ChangeSettingEyeHiDownColor(); });
            Add(list, 3, "eyeline.up", "上アイライン", ChaListDefine.CategoryNo.mt_eyeline_up,
                c=>c.fileFace.eyelineUpId, (c,v)=>c.fileFace.eyelineUpId=v, c=>Require(c.ChangeSettingEyelineUp()),
                c=>c.fileFace.eyelineColor, (c,v)=> { c.fileFace.eyelineColor=v; c.ChangeSettingEyelineColor(); }, "eyeline.color");
            Add(list, 3, "eyeline.down", "下アイライン", ChaListDefine.CategoryNo.mt_eyeline_down,
                c=>c.fileFace.eyelineDownId, (c,v)=>c.fileFace.eyelineDownId=v, c=>Require(c.ChangeSettingEyelineDown()),
                c=>c.fileFace.eyelineColor, (c,v)=> { c.fileFace.eyelineColor=v; c.ChangeSettingEyelineColor(); }, "eyeline.color");
            Add(list, 4, "shadow", "アイシャドウ", ChaListDefine.CategoryNo.mt_eyeshadow,
                c=>Makeup(c).eyeshadowId, (c,v)=>Makeup(c).eyeshadowId=v, c=>Require(c.ChangeSettingEyeShadow()),
                c=>Makeup(c).eyeshadowColor, (c,v)=> { Makeup(c).eyeshadowColor=v; c.ChangeSettingEyeShadowColor(); });
            Add(list, 4, "cheek", "チーク", ChaListDefine.CategoryNo.mt_cheek,
                c=>Makeup(c).cheekId, (c,v)=>Makeup(c).cheekId=v, RebuildFace,
                c=>Makeup(c).cheekColor, (c,v)=> { Makeup(c).cheekColor=v; RebuildFace(c); });
            Add(list, 4, "lip", "口紅", ChaListDefine.CategoryNo.mt_lip,
                c=>Makeup(c).lipId, (c,v)=>Makeup(c).lipId=v, c=>Require(c.ChangeSettingLip()),
                c=>Makeup(c).lipColor, (c,v)=> { Makeup(c).lipColor=v; c.ChangeSettingLipColor(); });
            Add(list, 4, "lipline", "唇の輪郭", ChaListDefine.CategoryNo.mt_lipline,
                c=>c.fileFace.lipLineId, (c,v)=>c.fileFace.lipLineId=v, c=>Require(c.ChangeSettingLip()),
                c=>c.fileFace.lipLineColor, (c,v)=> { c.fileFace.lipLineColor=v; RebuildFace(c); });
            Add(list, 4, "mole", "ほくろ", ChaListDefine.CategoryNo.mt_mole,
                c=>c.fileFace.moleId, (c,v)=>c.fileFace.moleId=v, RebuildFace,
                c=>c.fileFace.moleColor, (c,v)=> { c.fileFace.moleColor=v; RebuildFace(c); });
            Add(list, 4, "nose", "鼻の陰影", ChaListDefine.CategoryNo.mt_nose,
                c=>c.fileFace.noseId, (c,v)=>c.fileFace.noseId=v, c=>Require(c.ChangeSettingNose()));
            for (int i=0; i<2; i++)
            {
                int slot=i;
                Add(list, 4, "face.paint."+i, "顔ペイント "+(i+1), ChaListDefine.CategoryNo.mt_face_paint,
                    c=>Makeup(c).paintId[slot], (c,v)=>Makeup(c).paintId[slot]=v, RebuildFace,
                    c=>Makeup(c).paintColor[slot], (c,v)=> { Makeup(c).paintColor[slot]=v; RebuildFace(c); });
                Add(list, 5, "body.paint."+i, "体ペイント "+(i+1), ChaListDefine.CategoryNo.mt_body_paint,
                    c=>c.fileBody.paintId[slot], (c,v)=>c.fileBody.paintId[slot]=v, RebuildBody,
                    c=>c.fileBody.paintColor[slot], (c,v)=> { c.fileBody.paintColor[slot]=v; RebuildBody(c); });
            }
            Add(list, 5, "face.detail", "顔の肌質", ChaListDefine.CategoryNo.mt_face_detail,
                c=>c.fileFace.detailId, (c,v)=>c.fileFace.detailId=v, c=>Require(c.ChangeSettingFaceDetail()));
            Add(list, 5, "body.detail", "体の肌質", ChaListDefine.CategoryNo.mt_body_detail,
                c=>c.fileBody.detailId, (c,v)=>c.fileBody.detailId=v, c=>Require(c.ChangeSettingBodyDetail()));
            Add(list, 5, "sunburn", "日焼け", ChaListDefine.CategoryNo.mt_sunburn,
                c=>c.fileBody.sunburnId, (c,v)=>c.fileBody.sunburnId=v, RebuildBody,
                c=>c.fileBody.sunburnColor, (c,v)=> { c.fileBody.sunburnColor=v; RebuildBody(c); });
            Add(list, 5, "nip", "乳首", ChaListDefine.CategoryNo.mt_nip,
                c=>c.fileBody.nipId, (c,v)=>c.fileBody.nipId=v, c=>Require(c.ChangeSettingNip()),
                c=>c.fileBody.nipColor, (c,v)=> { c.fileBody.nipColor=v; c.ChangeSettingNipColor(); });
            Add(list, 5, "underhair", "アンダーヘア", ChaListDefine.CategoryNo.mt_underhair,
                c=>c.fileBody.underhairId, (c,v)=>c.fileBody.underhairId=v, c=>Require(c.ChangeSettingUnderhair()),
                c=>c.fileBody.underhairColor, (c,v)=> { c.fileBody.underhairColor=v; c.ChangeSettingUnderhairColor(); });
            var skin = new AppearancePart(5, "skin.color", "肌の色", default, null, null);
            skin.Colors.Add(new AppearanceColor("skin.main", "肌の基本色", c=>c.fileBody.skinMainColor,
                (c,v)=> { c.fileBody.skinMainColor=v; RebuildSkin(c); }));
            skin.Colors.Add(new AppearanceColor("skin.sub", "肌の陰影色", c=>c.fileBody.skinSubColor,
                (c,v)=> { c.fileBody.skinSubColor=v; RebuildSkin(c); }));
            skin.Colors.Add(new AppearanceColor("nail.color", "爪の色", c=>c.fileBody.nailColor,
                (c,v)=> { c.fileBody.nailColor=v; RebuildBody(c); }));
            list.Add(skin);
            return list;
        }
        private static void Add(List<AppearancePart> list, int page, string key, string name, ChaListDefine.CategoryNo category,
            Func<ChaControl,int> read, Action<ChaControl,int> set, Action<ChaControl> refresh,
            Func<ChaControl,Color> readColor=null, Action<ChaControl,Color> writeColor=null, string colorKey=null)
        {
            var part = new AppearancePart(page,key,name,category,read,(c,v)=> { set(c,v); refresh(c); });
            if (readColor!=null) part.Colors.Add(new AppearanceColor(colorKey ?? key+".color", "色・濃さ",readColor,writeColor));
            list.Add(part);
        }
        private static void Require(bool result)
        { if (!result) throw new InvalidOperationException("パーツの描画更新に失敗しました。取消で元に戻せます。"); }
        internal static ChaFileMakeup Makeup(ChaControl c)
            => c.nowCoordinate != null && c.nowCoordinate.enableMakeup ? c.nowCoordinate.makeup : c.fileFace.baseMakeup;
        private static void RebuildFace(ChaControl c)
        {
            c.AddUpdateCMFaceTexFlags(true,true,true,true,true,true,true);
            c.AddUpdateCMFaceColorFlags(true,true,true,true,true,true,true);
            c.AddUpdateCMFaceLayoutFlags(true,true,true);
            Require(c.CreateFaceTexture());
        }
        private static void RebuildBody(ChaControl c)
        {
            c.AddUpdateCMBodyTexFlags(false,false,true,true,true);
            c.AddUpdateCMBodyColorFlags(false,false,true,true,true,true);
            Require(c.CreateBodyTexture());
        }
        private static void RebuildSkin(ChaControl c)
        {
            c.AddUpdateCMBodyTexFlags(true,true,true,true,true);
            c.AddUpdateCMBodyColorFlags(true,true,true,true,true,true);
            c.AddUpdateCMBodyLayoutFlags(true,true);
            Require(c.CreateBodyTexture()); RebuildFace(c);
        }
    }
}
