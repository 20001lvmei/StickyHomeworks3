using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
//using static Windows.Win32.PInvoke;


namespace StickyHomeworks.Services;

public class WindowFocusObserverService
{
    // 由原生窗口焦点钩子触发（见下方 SetWinEventHook 调用）
#pragma warning disable CS0067
    public event EventHandler? FocusChanged;
#pragma warning restore CS0067

    public WindowFocusObserverService()
    {
        //SetWinEventHook(
        //    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
        //    HMODULE.Null, (hook, @event, hwnd, idObject, child, thread, time) =>
        //    {
        //        FocusChanged?.Invoke(this, EventArgs.Empty);
        //    },
        //    0, 0,
        //    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }
}