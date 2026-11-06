using System;
using System.Collections.Generic;
using UnityEngine;
namespace MainGameFaceSweatControl
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<ChaControl, Action> _sessionRestore = new Dictionary<ChaControl, Action>();
        private void RememberFaceState(ChaControl cha)
        {
            if (_sessionRestore.ContainsKey(cha) || cha.fileStatus == null) return;
            var status = cha.fileStatus;
            int part = (int)ChaFileDefine.SiruParts.SiruKao;
            int sweat = status.siruLv != null && part < status.siruLv.Length ? status.siruLv[part] : 0;
            float cheek = status.hohoAkaRate, gloss = status.skinTuyaRate;
            _sessionRestore.Add(cha, () => {
                if (cha == null) return;
                SetFaceSweat(cha, sweat); SetCheek(cha, cheek);
                status.skinTuyaRate = gloss; cha.ChangeSettingSkinGlossPower();
            });
        }
        private void RestoreSessionFace()
        {
            foreach (var restore in _sessionRestore.Values)
                try { restore(); } catch (Exception ex) { Logger.LogWarning(ex.Message); }
            _sessionRestore.Clear();
        }
    }
}
