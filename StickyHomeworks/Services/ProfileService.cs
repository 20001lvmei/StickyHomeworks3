using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Models;

namespace StickyHomeworks.Services;

public class ProfileService : IHostedService, INotifyPropertyChanged
{
    private const string ProfileFileName = "Profile.json";

    /// <summary>
    /// 保存延迟。编辑富文本时会连续触发变更，延迟合并写入可以避免频繁写盘。
    /// </summary>
    private const int SaveDelayMilliseconds = 400;

    private readonly object _saveLock = new();
    private readonly DispatcherTimer _saveTimer;

    private Profile _profile = new();

    public event EventHandler? ProfileSaved;

    public ProfileService(IHostApplicationLifetime applicationLifetime)
    {
        AppStorage.EnsureDataDirectory();
        AppStorage.MigrateLegacyFile(ProfileFileName);

        // 在 UI 线程上做防抖保存，避免后台线程序列化时与界面修改集合冲突
        _saveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(SaveDelayMilliseconds),
            DispatcherPriority.Background,
            OnSaveTimerTick,
            Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _saveTimer.Stop();

        Profile = AppStorage.Load<Profile>(ProfileFileName) ?? new Profile();

        // 进程/宿主停止时确保未落盘的内容被写入
        applicationLifetime.ApplicationStopping.Register(Flush);
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        Flush();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Flush();
        return Task.CompletedTask;
    }

    public Profile Profile
    {
        get => _profile;
        set
        {
            if (ReferenceEquals(value, _profile))
            {
                return;
            }

            Detach(_profile);
            _profile = value;
            Attach(_profile);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 把已经过期的作业移动到归档中，返回被归档的作业。
    /// 与旧版本直接删除不同，归档的作业会随 Profile 一起保存，重启后依然可以恢复。
    /// </summary>
    public List<Homework> ArchiveOutdated(bool enabled = true)
    {
        var archived = new List<Homework>();
        if (!enabled)
        {
            return archived;
        }

        var today = DateTime.Today;
        foreach (var homework in Profile.Homeworks
                     .Where(i => !i.IsArchiveExempt && i.DueTime.Date < today)
                     .ToList())
        {
            if (Profile.Homeworks.Remove(homework))
            {
                Profile.ArchivedHomeworks.Add(homework);
                archived.Add(homework);
            }
        }

        if (archived.Count > 0)
        {
            ScheduleSave();
        }

        return archived;
    }

    /// <summary>
    /// 把所有归档的作业恢复到列表中，返回恢复的数量。
    /// 恢复的作业会标记为豁免自动归档，避免下次启动又被归档。
    /// </summary>
    public int RestoreArchived()
    {
        var items = Profile.ArchivedHomeworks.ToList();
        foreach (var item in items)
        {
            Profile.ArchivedHomeworks.Remove(item);
            if (!Profile.Homeworks.Contains(item))
            {
                item.IsArchiveExempt = true;
                Profile.Homeworks.Add(item);
            }
        }

        if (items.Count > 0)
        {
            ScheduleSave();
        }

        return items.Count;
    }

    /// <summary>
    /// 请求保存（防抖）。真正的写入会合并到稍后执行。
    /// </summary>
    public void SaveProfile() => ScheduleSave();

    /// <summary>
    /// 立即把当前数据写入磁盘。用于退出、关机等必须保证数据完整的场景。
    /// </summary>
    public void Flush()
    {
        lock (_saveLock)
        {
            if (_saveTimer.Dispatcher.CheckAccess())
            {
                _saveTimer.Stop();
            }

            try
            {
                AppStorage.Save(ProfileFileName, Profile);
                ProfileSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // 保存失败不应该让程序崩溃（例如磁盘满、文件被占用）
                Debug.WriteLine($"[ProfileService] 保存失败：{ex.Message}");
            }
        }
    }

    private void ScheduleSave()
    {
        if (!_saveTimer.Dispatcher.CheckAccess())
        {
            _saveTimer.Dispatcher.BeginInvoke(new Action(ScheduleSave));
            return;
        }

        // 重新开始计时，合并短时间内的连续修改
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Attach(Profile profile)
    {
        profile.PropertyChanged += OnTrackedChanged;
        profile.Homeworks.CollectionChanged += OnTrackedCollectionChanged;
        profile.ArchivedHomeworks.CollectionChanged += OnTrackedCollectionChanged;

        foreach (var homework in profile.Homeworks)
        {
            homework.PropertyChanged += OnTrackedChanged;
        }

        foreach (var homework in profile.ArchivedHomeworks)
        {
            homework.PropertyChanged += OnTrackedChanged;
        }
    }

    private void Detach(Profile profile)
    {
        profile.PropertyChanged -= OnTrackedChanged;
        profile.Homeworks.CollectionChanged -= OnTrackedCollectionChanged;
        profile.ArchivedHomeworks.CollectionChanged -= OnTrackedCollectionChanged;

        foreach (var homework in profile.Homeworks)
        {
            homework.PropertyChanged -= OnTrackedChanged;
        }

        foreach (var homework in profile.ArchivedHomeworks)
        {
            homework.PropertyChanged -= OnTrackedChanged;
        }
    }

    private void OnTrackedChanged(object? sender, PropertyChangedEventArgs e) => ScheduleSave();

    private void OnTrackedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<Homework>())
            {
                item.PropertyChanged -= OnTrackedChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<Homework>())
            {
                item.PropertyChanged -= OnTrackedChanged;
                item.PropertyChanged += OnTrackedChanged;
            }
        }

        // 作业内容、截止日期等改动都会走到这里，保证不需要手动保存也不会丢数据
        ScheduleSave();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
