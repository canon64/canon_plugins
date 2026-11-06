using System;
using System.IO;

namespace MainGameAiShoujoMode
{
    internal static class HotelRoomCoordinateRestore
    {
        internal static void Apply(int saved, int available, Func<int> current, Func<bool> reload)
        {
            if (saved < default(int) || saved >= available)
                throw new InvalidDataException("保存衣装番号が読み込み済みカードの範囲外です：" + saved);
            if (current() == saved) return;
            if (!reload()) throw new InvalidOperationException("保存衣装への切替・再読込に失敗しました：" + saved);
            if (current() != saved) throw new InvalidOperationException("衣装の再読込後も保存衣装番号と一致しません：" + saved + " actual=" + current());
        }
    }
}
