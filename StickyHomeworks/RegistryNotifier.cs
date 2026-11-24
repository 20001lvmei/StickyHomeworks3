using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassIsland;

/// <summary>
/// 监听注册表项变化（ClassIsland 联动功能使用）。
/// 旧实现用阻塞的 RegNotifyChangeKeyValue 死循环占用一个线程且无法退出，
/// 这里改为异步通知 + 事件等待，停止时线程可以立即结束。
/// </summary>
public class RegistryNotifier : IDisposable
{
    [DllImport("advapi32.dll", EntryPoint = "RegNotifyChangeKeyValue", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, int dwNotifyFilter,
        IntPtr hEvent, bool fAsynchronous);

    [DllImport("advapi32.dll", EntryPoint = "RegOpenKey", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKey(uint hKey, string lpSubKey, ref IntPtr phkResult);

    [DllImport("advapi32.dll", EntryPoint = "RegCloseKey", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    public const uint HKEY_CLASSES_ROOT = 0x80000000;
    public const uint HKEY_CURRENT_USER = 0x80000001;
    public const uint HKEY_LOCAL_MACHINE = 0x80000002;
    public const uint HKEY_USERS = 0x80000003;
    public const uint HKEY_PERFORMANCE_DATA = 0x80000004;
    public const uint HKEY_CURRENT_CONFIG = 0x80000005;

    private const int REG_NOTIFY_CHANGE_NAME = 0x1;
    private const int REG_NOTIFY_CHANGE_ATTRIBUTES = 0x2;
    private const int REG_NOTIFY_CHANGE_LAST_SET = 0x4;
    private const int REG_NOTIFY_CHANGE_SECURITY = 0x8;

    private const int NotifyFilter = REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_ATTRIBUTES |
                                     REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_CHANGE_SECURITY;

    private readonly ManualResetEvent _stopEvent = new(false);
    private readonly object _syncRoot = new();

    private IntPtr _keyHandle = IntPtr.Zero;
    private Thread? _worker;

    public delegate void RegistryKeyUpdatedHandler();

    public event RegistryKeyUpdatedHandler? RegistryKeyUpdated;

    public RegistryNotifier(uint root, string path)
    {
        var handle = IntPtr.Zero;
        var result = RegOpenKey(root, path, ref handle);
        if (result != 0)
        {
            Debug.WriteLine($"[RegistryNotifier] 打开注册表项失败：{path}，错误码 {result}");
        }

        _keyHandle = handle;
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_worker != null || _keyHandle == IntPtr.Zero)
            {
                return;
            }

            _stopEvent.Reset();
            _worker = new Thread(UpdateLoop)
            {
                IsBackground = true,
                Name = "RegistryNotifier"
            };
            _worker.Start();
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _stopEvent.Set();
            _worker?.Join(TimeSpan.FromSeconds(1));
            _worker = null;
        }
    }

    private void UpdateLoop()
    {
        using var changeEvent = new ManualResetEvent(false);
        var handles = new WaitHandle[] { changeEvent, _stopEvent };

        while (!_stopEvent.WaitOne(0))
        {
            changeEvent.Reset();

            var result = RegNotifyChangeKeyValue(_keyHandle, true, NotifyFilter,
                changeEvent.SafeWaitHandle.DangerousGetHandle(), true);

            if (result != 0)
            {
                Debug.WriteLine($"[RegistryNotifier] 注册表通知失败，错误码 {result}");
                if (_stopEvent.WaitOne(TimeSpan.FromSeconds(1)))
                {
                    return;
                }

                continue;
            }

            var index = WaitHandle.WaitAny(handles);
            if (index == 1)
            {
                return;
            }

            Debug.WriteLine("[RegistryNotifier] 注册表已更新。");
            RegistryKeyUpdated?.Invoke();
        }
    }

    public void Dispose()
    {
        Stop();

        lock (_syncRoot)
        {
            if (_keyHandle != IntPtr.Zero)
            {
                RegCloseKey(_keyHandle);
                _keyHandle = IntPtr.Zero;
            }
        }

        _stopEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
