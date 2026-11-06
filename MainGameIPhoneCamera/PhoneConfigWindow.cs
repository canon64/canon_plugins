using System;

namespace MainGameIPhoneCamera
{
    internal sealed class PhoneConfigWindow
    {
        internal const string Query = "MainGameIPhoneCamera";
        private object _manager;
        private string _previousSearch;
        private bool _previousVisible;
        internal void Open(object manager)
        {
            if (manager == null) throw new InvalidOperationException("F1のコンフィグマネージャーが読み込まれていません");
            var visible = manager.GetType().GetProperty("DisplayingWindow");
            var search = manager.GetType().GetProperty("SearchString");
            if (visible == null || search == null) throw new InvalidOperationException("設定画面の表示・検索APIがありません");
            if (_manager == null)
            {
                _manager = manager; _previousSearch = (string)search.GetValue(manager, null); _previousVisible = (bool)visible.GetValue(manager, null);
            }
            // The installed manager builds _allSettings only when first shown.
            // SearchString immediately filters that list, so setting it first throws on a cold open.
            visible.SetValue(manager, true, null);
            search.SetValue(manager, Query, null);
        }
        internal void Close()
        {
            object manager = _manager; _manager = null;
            if (manager == null) return;
            var search = manager.GetType().GetProperty("SearchString");
            if ((string)search.GetValue(manager, null) != Query) return;
            search.SetValue(manager, _previousSearch, null);
            manager.GetType().GetProperty("DisplayingWindow").SetValue(manager, _previousVisible, null);
        }
    }
}
