using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Lsj.Util.Win32.Structs;
using Lsj.Util.Win32.Enums;
using static Lsj.Util.Win32.User32;
using static Lsj.Util.Win32.Gdi32;
using static Lsj.Util.Win32.Gdiplus;
using static Lsj.Util.Win32.Structs.BLENDFUNCTION;
using Lsj.Util.Win32.Marshals;

namespace SplashImage
{
    class Program
    {
        /// <summary>
        /// Splash File Name
        /// </summary>
        private const string SplashFile = "SplashScreen.png";

        /// <summary>
        /// Image Width
        /// </summary>
        private const int ImageWidth = 780;

        /// <summary>
        /// Image Height
        /// </summary>
        private const int ImageHeight = 522;

        /// <summary>
        /// Window Class
        /// </summary>
        private const string WindowClass = "Splash Image";

        /// <summary>
        /// Window Name
        /// </summary>
        private const string WindowName = "Splash Image";


        /// <summary>
        /// Window Handle
        /// </summary>
        private static IntPtr _window;

        /// <summary>
        /// Window Rectangle
        /// </summary>
        private static RECT _windowRectangle;

        /// <summary>
        /// Window Size
        /// </summary>
        private static SIZE _windowSize;

        /// <summary>
        /// Screen DC
        /// </summary>
        private static readonly IntPtr _screenDC = GetDC(IntPtr.Zero);

        /// <summary>
        /// Window DC
        /// </summary>
        private static IntPtr _windowDC;

        /// <summary>
        /// Memory DC
        /// </summary>
        private static IntPtr _memoryDC = IntPtr.Zero;

        /// <summary>
        /// GDI+ Splash Image
        /// </summary>
        private static IntPtr _splashImage;

        /// <summary>
        /// GDI+ Graphics
        /// </summary>
        private static IntPtr _graphics;

        /// <summary>
        /// Memory Bitmap For SplashImage
        /// </summary>
        private static IntPtr _memoryBitmap = IntPtr.Zero;

        private static void Main(string[] args)
        {
            var hInstance = Process.GetCurrentProcess().Handle;

            using var marshal = new StringToIntPtrMarshaler(WindowClass);
            var wndclass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = ClassStyles.CS_DBLCLKS,
                lpfnWndProc = WindowProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = LoadIcon(hInstance, SystemIcons.IDI_APPLICATION),
                hCursor = LoadCursor(IntPtr.Zero, SystemCursors.IDC_ARROW),
                hbrBackground = (IntPtr)BackgroundColors.COLOR_WINDOW,
                lpszMenuName = IntPtr.Zero,
                lpszClassName = marshal.GetPtr(),
            };

            if (RegisterClassEx(ref wndclass) != 0)
            {
                _window = CreateWindowEx(WindowStylesEx.WS_EX_LAYERED, WindowClass, WindowName, WindowStyles.WS_POPUP, CW_USEDEFAULT, CW_USEDEFAULT,
                    CW_USEDEFAULT, CW_USEDEFAULT, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
                if (_window != IntPtr.Zero)
                {
                    _windowDC = GetDC(_window);

                    var dpi = GetDeviceCaps(_screenDC, DeviceCapIndexes.LOGPIXELSX);
                    SetPositionAndSize(dpi);
                    ShowWindow(_window, ShowWindowCommands.SW_SHOWNORMAL);

                    var startupInput = new GdiplusStartupInput
                    {
                        GdiplusVersion = 1,
                        DebugEventCallback = IntPtr.Zero,
                        SuppressBackgroundThread = false,
                        SuppressExternalCodecs = false,
                    };
                    if (GdiplusStartup(out var token, ref startupInput, out _) == GpStatus.Ok && GdipLoadImageFromFile(SplashFile, out _splashImage) == GpStatus.Ok)
                    {
                        DrawImage();
                        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0)
                        {
                            TranslateMessage(ref msg);
                            DispatchMessage(ref msg);
                        }

                        GdiplusShutdown(token);
                    }
                    else
                    {
                        //TODO: throw GDI+ Error
                    }
                    DeleteObject(_memoryBitmap);
                    DeleteDC(_memoryDC);
                    ReleaseDC(_window, _windowDC);
                    ReleaseDC(IntPtr.Zero, _screenDC);
                }
                else
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Set Window Position And Size
        /// </summary>
        /// <param name="dpi"></param>
        private static void SetPositionAndSize(int dpi)
        {
            var screenWidth = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SystemMetric.SM_CYSCREEN);
            var windowWidth = ImageWidth * dpi / 96;
            var windowHeight = ImageHeight * dpi / 96;
            SetWindowPos(_window, HWND_TOPMOST, (screenWidth - windowWidth) / 2, (screenHeight - windowHeight) / 2, windowWidth, windowHeight, 0);
            GetWindowRect(_window, out _windowRectangle);
            _windowSize = new SIZE { cx = _windowRectangle.right - _windowRectangle.left, cy = _windowRectangle.bottom - _windowRectangle.top };
        }

        /// <summary>
        /// Draw Image
        /// </summary>
        private static void DrawImage()
        {
            if (_memoryDC != IntPtr.Zero)
            {
                DeleteDC(_memoryDC);
            }
            if (_memoryBitmap != IntPtr.Zero)
            {
                DeleteObject(_memoryBitmap);
            }

            _memoryDC = CreateCompatibleDC(_windowDC);
            _memoryBitmap = CreateCompatibleBitmap(_windowDC, _windowSize.cx, _windowSize.cy);
            SelectObject(_memoryDC, _memoryBitmap);
            if (GdipCreateFromHDC(_memoryDC, out _graphics) == GpStatus.Ok && GdipDrawImageRectI(_graphics, _splashImage, 0, 0, _windowSize.cx, _windowSize.cy) == GpStatus.Ok)
            {
                var ptSrc = new POINT
                {
                    x = 0,
                    y = 0,
                };
                var ptDes = new POINT
                {
                    x = _windowRectangle.left,
                    y = _windowRectangle.top,
                };
                var blendFunction = new BLENDFUNCTION
                {
                    AlphaFormat = AC_SRC_ALPHA,
                    BlendFlags = 0,
                    BlendOp = AC_SRC_OVER,
                    SourceConstantAlpha = 255,
                };
                if (UpdateLayeredWindow(_window, _screenDC, ref ptDes, ref _windowSize, _memoryDC, ref ptSrc, 0, ref blendFunction, UpdateLayeredWindowFlags.ULW_ALPHA))
                {
                    return;
                }
                else
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        /// <summary>
        /// Window Proc
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private static IntPtr WindowProc(IntPtr hWnd, WindowsMessages msg, UIntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WindowsMessages.WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
                case WindowsMessages.WM_DPICHANGED:
                    SetPositionAndSize((int)(wParam.ToUInt32() >> 16));
                    DrawImage();
                    return IntPtr.Zero;
                default:
                    return DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }
    }
}
