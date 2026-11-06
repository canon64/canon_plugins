using System;

namespace MainGameAiShoujoMode.Furniture
{
    // Pure layout: page buttons replace an inner scrolling list and use the available window.
    internal sealed class FurnitureGrid
    {
        internal int Columns,Rows;
        internal int PageSize=>Columns*Rows;
        internal static FurnitureGrid Fit(float width,float height,float tileWidth,float tileHeight,int one)
        {
            if(one<=default(int)||tileWidth<=default(float)||tileHeight<=default(float))throw new ArgumentException("家具一覧の寸法が不正です。");
            return new FurnitureGrid{Columns=Math.Max(one,(int)Math.Floor(width/tileWidth)),Rows=Math.Max(one,(int)Math.Floor(height/tileHeight))};
        }
    }
}
