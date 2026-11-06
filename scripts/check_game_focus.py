"""Read whether the specified game PID owns the foreground window. Never changes focus."""
import ctypes
import sys

game_pid = int(sys.argv[1])
api = ctypes.WinDLL('user32', use_last_error=True)
api.GetForegroundWindow.restype = ctypes.c_void_p
api.GetWindowThreadProcessId.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong)]
foreground_pid = ctypes.c_ulong()
api.GetWindowThreadProcessId(api.GetForegroundWindow(), ctypes.byref(foreground_pid))
print(f'GameIsForeground={foreground_pid.value == game_pid}')
