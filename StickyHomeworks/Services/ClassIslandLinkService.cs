using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace StickyHomeworks.Services;

/// <summary>
/// 当前处于什么时间状态（相对于 ClassIsland 的课表）。
/// </summary>
public enum ClassIslandTimeState
{
    /// <summary>读不到课表。</summary>
    Unknown,

    /// <summary>正在上课。</summary>
    InClass,

    /// <summary>课间 / 放学 / 没有课。</summary>
    Break
}

/// <summary>
/// 读取 ClassIsland 的课表配置，判断当前是上课还是课间。
/// 只读文件，不依赖 ClassIsland 是否在运行。
/// </summary>
public class ClassIslandLinkService
{
    private string? _cachedDataDirectory;

    /// <summary>
    /// 猜出 ClassIsland 的数据目录（便携版是 exe 同级的 data，安装版是 %LOCALAPPDATA%\ClassIsland）。
    /// </summary>
    public static string? ResolveDataDirectory(string? configured)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured.Trim());
        }

        // 正在运行的 ClassIsland：取它 exe 同级的 data 目录
        try
        {
            foreach (var process in Process.GetProcessesByName("ClassIsland"))
            {
                try
                {
                    var exe = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exe))
                    {
                        var data = Path.Combine(Path.GetDirectoryName(exe)!, "data");
                        candidates.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ClassIslandLink] 读取进程路径失败：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassIslandLink] 枚举 ClassIsland 进程失败：{ex.Message}");
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClassIsland"));
        candidates.Add(@"D:\dj\classisland\data");
        candidates.Add(@"C:\Program Files\ClassIsland\data");
        candidates.Add(@"C:\Program Files (x86)\ClassIsland\data");

        foreach (var candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Settings.json")))
                {
                    return candidate;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClassIslandLink] 检查目录失败：{ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// 判断 now 时刻是上课还是课间。
    /// </summary>
    public ClassIslandTimeState GetState(string? configuredDirectory, DateTime now)
    {
        try
        {
            var directory = ResolveDataDirectory(configuredDirectory) ?? _cachedDataDirectory;
            if (directory == null)
            {
                return ClassIslandTimeState.Unknown;
            }

            _cachedDataDirectory = directory;

            var settingsPath = Path.Combine(directory, "Settings.json");
            var profileName = "Default.json";
            if (File.Exists(settingsPath))
            {
                using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (settings.RootElement.TryGetProperty("SelectedProfile", out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    profileName = value.GetString()!;
                }
            }

            var profilePath = Path.Combine(directory, "Profiles", profileName);
            if (!File.Exists(profilePath))
            {
                return ClassIslandTimeState.Unknown;
            }

            using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
            var root = profile.RootElement;

            if (!root.TryGetProperty("ClassPlans", out var plans) ||
                !root.TryGetProperty("TimeLayouts", out var layouts))
            {
                return ClassIslandTimeState.Unknown;
            }

            var plan = FindPlanFor(plans, now);
            if (plan == null)
            {
                return ClassIslandTimeState.Break;
            }

            if (!plan.Value.TryGetProperty("TimeLayoutId", out var layoutIdElement) ||
                layoutIdElement.ValueKind != JsonValueKind.String)
            {
                return ClassIslandTimeState.Unknown;
            }

            var layoutId = layoutIdElement.GetString();
            if (string.IsNullOrEmpty(layoutId) ||
                !layouts.TryGetProperty(layoutId, out var layout) ||
                !layout.TryGetProperty("Layouts", out var points) ||
                points.ValueKind != JsonValueKind.Array)
            {
                return ClassIslandTimeState.Unknown;
            }

            var current = now.TimeOfDay;
            foreach (var point in points.EnumerateArray())
            {
                if (!TryGetTime(point, "StartTime", out var start) || !TryGetTime(point, "EndTime", out var end))
                {
                    continue;
                }

                if (current < start || current >= end)
                {
                    continue;
                }

                // ClassIsland 的 TimeType：0 = 上课，其它 = 课间/其它
                var type = point.TryGetProperty("TimeType", out var typeElement) ? typeElement.GetInt32() : -1;
                return type == 0 ? ClassIslandTimeState.InClass : ClassIslandTimeState.Break;
            }

            // 不在任何时间段里（早于上课或已经放学）
            return ClassIslandTimeState.Break;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassIslandLink] 读取课表失败：{ex.Message}");
            return ClassIslandTimeState.Unknown;
        }
    }

    /// <summary>
    /// 取现在能显示出信息的说明文字（用于设置页展示与自检）。
    /// </summary>
    public string GetDescription(string? configuredDirectory, DateTime now)
    {
        var directory = ResolveDataDirectory(configuredDirectory);
        if (directory == null)
        {
            return "没找到 ClassIsland 数据目录";
        }

        var state = GetState(configuredDirectory, now);
        var text = state switch
        {
            ClassIslandTimeState.InClass => "上课中（窗口应隐藏）",
            ClassIslandTimeState.Break => "课间/课余（窗口应显示）",
            _ => "读取课表失败"
        };
        return $"{text}｜{directory}";
    }

    private static JsonElement? FindPlanFor(JsonElement plans, DateTime now)
    {
        // ClassIsland 的 WeekDay：周日 = 0，周一 = 1 … 周六 = 6
        var weekDay = now.DayOfWeek == DayOfWeek.Sunday ? 0 : (int)now.DayOfWeek;

        JsonElement? weeklyMatch = null;
        JsonElement? oddEvenMatch = null;

        foreach (var plan in plans.EnumerateObject())
        {
            var value = plan.Value;
            if (!value.TryGetProperty("TimeRule", out var rule))
            {
                continue;
            }

            var day = rule.TryGetProperty("WeekDay", out var dayElement) ? dayElement.GetInt32() : -1;
            if (day != weekDay)
            {
                continue;
            }

            var div = rule.TryGetProperty("WeekCountDiv", out var divElement) ? divElement.GetInt32() : 0;
            var total = rule.TryGetProperty("WeekCountDivTotal", out var totalElement) ? totalElement.GetInt32() : 2;

            if (div == 0 || total <= 1)
            {
                weeklyMatch ??= value;
                continue;
            }

            // 单双周：单周号对应 div = 1，双周号对应 div = 2
            var weekIndex = GetWeekIndex(now);
            var parity = weekIndex % 2 == 1 ? 1 : 2;
            if (div == parity)
            {
                oddEvenMatch = value;
            }
        }

        return oddEvenMatch ?? weeklyMatch;
    }

    private static int GetWeekIndex(DateTime now)
    {
        try
        {
            var start = new DateTime(now.Year, 9, 1);
            var days = (now.Date - start.Date).Days;
            return days < 0 ? 1 : days / 7 + 1;
        }
        catch
        {
            return 1;
        }
    }

    private static bool TryGetTime(JsonElement point, string name, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (!point.TryGetProperty(name, out var element))
        {
            return false;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TimeSpan.TryParse(text, out time);
    }
}
