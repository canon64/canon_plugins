using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MainGameDesktopDisplay
{
    internal interface IDesktopMouse
    {
        void RefreshBounds();
        void Move(int x, int y);
        void Button(bool right, bool down);
        void Scroll(int horizontal, int vertical);
        void ReleaseAll();
    }
    internal sealed class WindowsDesktopMouse : IDesktopMouse
    {
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
        { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input
        { internal uint Type; internal MouseInput Mouse; }
        private bool _left, _right;
        private readonly Input[] _inputs = new Input[1];
        private int _originX, _originY;
        private double _scaleX, _scaleY;
        internal static readonly int InputSize = Marshal.SizeOf(typeof(Input));
        [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
        public void RefreshBounds()
        {
            _originX = GetSystemMetrics(76); _originY = GetSystemMetrics(77);
            _scaleX = 65535d / Math.Max(1, GetSystemMetrics(78) - 1);
            _scaleY = 65535d / Math.Max(1, GetSystemMetrics(79) - 1);
        }
        internal static int Normalize(int point, int origin, int size)
        {
            if (size <= 1) return 0;
            return (int)Math.Round(Math.Max(0d, Math.Min(1d, ((double)point - origin) / (size - 1))) * 65535d);
        }
        public void Move(int x, int y)
        {
            if (!GetCursorPos(out var current)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (current.X == x && current.Y == y) return;
            Send(new MouseInput { X = (int)Math.Round(Math.Max(0, Math.Min(65535, (x - (double)_originX) * _scaleX))),
                Y = (int)Math.Round(Math.Max(0, Math.Min(65535, (y - (double)_originY) * _scaleY))), Flags = 0xC001 });
        }
        public void Button(bool right, bool down)
        {
            if ((right ? _right : _left) == down) return;
            Send(new MouseInput { Flags = right ? (down ? 8u : 16u) : (down ? 2u : 4u) });
            if (right) _right = down; else _left = down;
        }
        public void Scroll(int horizontal, int vertical)
        {
            if (vertical != 0) Send(new MouseInput { Flags = 0x800, Data = unchecked((uint)(vertical * 120)) });
            if (horizontal != 0) Send(new MouseInput { Flags = 0x1000, Data = unchecked((uint)(horizontal * 120)) });
        }
        public void ReleaseAll()
        { try { Button(false, false); } finally { Button(true, false); } }
        private void Send(MouseInput mouse)
        {
            _inputs[0].Mouse = mouse;
            if (SendInput(1, _inputs, InputSize) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windowsへのマウス入力を送信できません。");
        }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", SetLastError = true)] static extern bool GetCursorPos(out Point point);
    }
}
