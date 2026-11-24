using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using StickyHomeworks.Models;

namespace StickyHomeworks.ViewModels;

public class MainViewModel : ObservableRecipient
{
    private bool _isDrawerOpened = false;
    private Homework? _selectedHomework;
    private Homework _editingHomework = new()
    {
        Subject = "其它"
    };
    private bool _isTagEditingPopupOpened = false;
    private bool _isCreatingMode = false;
    private bool _isUnlocked = false;
    private bool _isExpanded = true;
    private bool _isClosing = false;
    private bool _isWorking = false;
    private SnackbarMessageQueue _snackbarMessageQueue = new();
    private Control? _selectedListBoxItem;
    private bool _isUpdatingHomeworkSubject = false;
    private List<Homework> _archivedHomeworks = new();
    private bool _hasArchivedHomeworks = false;
    private ObservableCollection<BackupEntry> _backups = new();
    private BackupEntry? _selectedBackup;

    public Control? SelectedListBoxItem
    {
        get => _selectedListBoxItem;
        set
        {
            if (Equals(value, _selectedListBoxItem)) return;
            _selectedListBoxItem = value;
            OnPropertyChanged();
        }
    }

    public bool IsDrawerOpened
    {
        get => _isDrawerOpened;
        set
        {
            if (value == _isDrawerOpened) return;
            _isDrawerOpened = value;
            OnPropertyChanged();
        }
    }

    public Homework? SelectedHomework
    {
        get => _selectedHomework;
        set
        {
            if (Equals(value, _selectedHomework)) return;
            _selectedHomework = value;
            OnPropertyChanged();
        }
    }

    public Homework EditingHomework
    {
        get => _editingHomework;
        set
        {
            if (Equals(value, _editingHomework)) return;
            _editingHomework = value;
            OnPropertyChanged();
        }
    }

    public bool IsTagEditingPopupOpened
    {
        get => _isTagEditingPopupOpened;
        set
        {
            if (value == _isTagEditingPopupOpened) return;
            _isTagEditingPopupOpened = value;
            OnPropertyChanged();
        }
    }

    public bool IsCreatingMode
    {
        get => _isCreatingMode;
        set
        {
            if (value == _isCreatingMode) return;
            _isCreatingMode = value;
            OnPropertyChanged();
        }
    }

    public bool IsUnlocked
    {
        get => _isUnlocked;
        set
        {
            if (value == _isUnlocked) return;
            _isUnlocked = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsClosing
    {
        get => _isClosing;
        set
        {
            if (value == _isClosing) return;
            _isClosing = value;
            OnPropertyChanged();
        }
    }

    public bool IsWorking
    {
        get => _isWorking;
        set
        {
            if (value == _isWorking) return;
            _isWorking = value;
            OnPropertyChanged();
        }
    }

    public SnackbarMessageQueue SnackbarMessageQueue
    {
        get => _snackbarMessageQueue;
        set
        {
            if (Equals(value, _snackbarMessageQueue)) return;
            _snackbarMessageQueue = value;
            OnPropertyChanged();
        }
    }

    public bool IsUpdatingHomeworkSubject
    {
        get => _isUpdatingHomeworkSubject;
        set
        {
            if (value == _isUpdatingHomeworkSubject) return;
            _isUpdatingHomeworkSubject = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 本次启动时被归档的过期作业。归档的作业会保存到 Profile.json，重启后依然可以通过“恢复已归档的作业”找回。
    /// </summary>
    public List<Homework> ArchivedHomeworks
    {
        get => _archivedHomeworks;
        set
        {
            if (Equals(value, _archivedHomeworks)) return;
            _archivedHomeworks = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ArchivedHomeworkCount));
        }
    }

    public int ArchivedHomeworkCount => ArchivedHomeworks.Count;

    public bool HasArchivedHomeworks
    {
        get => _hasArchivedHomeworks;
        set
        {
            if (value == _hasArchivedHomeworks) return;
            _hasArchivedHomeworks = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 时间机器里的备份列表。
    /// </summary>
    public ObservableCollection<BackupEntry> Backups
    {
        get => _backups;
        set
        {
            if (Equals(value, _backups)) return;
            _backups = value;
            OnPropertyChanged();
        }
    }

    public BackupEntry? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (Equals(value, _selectedBackup)) return;
            _selectedBackup = value;
            OnPropertyChanged();
        }
    }
}