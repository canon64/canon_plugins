using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CoordCatalog
{
    public static class ImageComposer
    {
        private const int CellPadding = 2;

        public static void Compose(
            List<(UnityEngine.Texture2D tex, string name, int index)> items,
            string outputPath)
        {
            if (items.Count == 0) return;

            float fontSize = Mathf.Clamp(CoordCatalogPlugin.LabelFontSize.Value, 8, 96);
            int labelHeight = (int)(fontSize * 1.33f + 20f);

            int n = items.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            int rows = Mathf.CeilToInt((float)n / cols);

            // 出力キャンバスサイズ固定、セル・サムネイルはそこから逆算
            int canvasW = Mathf.Clamp(CoordCatalogPlugin.OutputWidth.Value, 320, 7680);
            int canvasH = Mathf.Clamp(CoordCatalogPlugin.OutputHeight.Value, 240, 4320);

            int cellW = canvasW / cols;
            int cellH = canvasH / rows;
            int thumbW = cellW - CellPadding;
            int thumbH = cellH - labelHeight - CellPadding;

            using (var canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format24bppRgb))
            using (var g = System.Drawing.Graphics.FromImage(canvas))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.FromArgb(20, 20, 20));

                using (var labelFont = new Font("MS Gothic", fontSize, FontStyle.Regular, GraphicsUnit.Point))
                using (var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(200, 10, 10, 10)))
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        var (tex, name, idx) = items[i];
                        int col = i % cols;
                        int row = i / cols;
                        int x = col * cellW;
                        int y = row * cellH;

                        // アスペクト比を維持してサムネイルを枠内に収める（レターボックス）
                        using (var bmp = ToBitmap(tex))
                        {
                            float srcAspect = (float)bmp.Width / bmp.Height;
                            float dstAspect = (float)thumbW / thumbH;
                            int drawW, drawH, drawX, drawY;
                            if (srcAspect > dstAspect)
                            {
                                drawW = thumbW;
                                drawH = (int)(thumbW / srcAspect);
                                drawX = x;
                                drawY = y + (thumbH - drawH) / 2;
                            }
                            else
                            {
                                drawH = thumbH;
                                drawW = (int)(thumbH * srcAspect);
                                drawX = x + (thumbW - drawW) / 2;
                                drawY = y;
                            }
                            g.DrawImage(bmp, drawX, drawY, drawW, drawH);
                        }

                        // ラベル背景
                        g.FillRectangle(bgBrush, x, y + thumbH, cellW, labelHeight);

                        // テキスト描画
                        DrawLabel(g, labelFont, name, idx, x, y + thumbH, cellW, labelHeight);
                    }
                }

                canvas.Save(outputPath, ImageFormat.Png);
            }
        }

        private static void DrawLabel(
            System.Drawing.Graphics g, Font font,
            string name, int index,
            int x, int y, int width, int labelHeight)
        {
            bool showName = CoordCatalogPlugin.ShowCoordName.Value;
            bool showIdx = CoordCatalogPlugin.ShowIndex.Value;

            if (!showName && !showIdx) return;

            var brush = System.Drawing.Brushes.White;
            var rect = new RectangleF(x + 6f, y + 6f, width - 12f, labelHeight - 8f);
            var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };

            // 1行表示: インデックス+名前 or どちらか片方
            string text;
            if (showName && showIdx)
                text = $"[{index}] {name}";
            else if (showIdx)
                text = $"[{index}]";
            else
                text = name;

            g.DrawString(text, font, brush, rect, fmt);

            fmt.Dispose();
        }

        private static Bitmap ToBitmap(UnityEngine.Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            var bitmapData = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb);

            // Texture2D は左下原点、Bitmap は左上原点なのでY軸反転
            var pixels = tex.GetPixels32();
            int stride = bitmapData.Stride;
            var buf = new byte[stride * h];

            for (int py = 0; py < h; py++)
            {
                int srcRow = (h - 1 - py);
                int dstOffset = py * stride;
                for (int px = 0; px < w; px++)
                {
                    var p = pixels[srcRow * w + px];
                    int o = dstOffset + px * 4;
                    buf[o + 0] = p.b;
                    buf[o + 1] = p.g;
                    buf[o + 2] = p.r;
                    buf[o + 3] = 255;
                }
            }

            Marshal.Copy(buf, 0, bitmapData.Scan0, buf.Length);
            bmp.UnlockBits(bitmapData);
            return bmp;
        }
    }
}
