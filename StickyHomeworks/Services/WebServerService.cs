using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using StickyHomeworks.Models;

namespace StickyHomeworks.Services;

/// <summary>
/// 内置的网页版：在本机用浏览器查看、添加、修改、删除作业。
/// 用 TcpListener 自己实现极简 HTTP（不依赖 http.sys，无需管理员权限，也不影响单文件发布）。
/// </summary>
public class WebServerService : IDisposable
{
    private TcpListener? _listener;

    private CancellationTokenSource? _cts;

    private ProfileService? _profileService;

    public bool IsRunning => _listener != null;

    /// <summary>
    /// 启动网页服务。返回监听地址（失败时返回 null）。
    /// </summary>
    public string? Start(ProfileService profileService, int port)
    {
        Stop();
        _profileService = profileService;

        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _listener = listener;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
            Debug.WriteLine($"[Web] 已启动：http://127.0.0.1:{port}/");
            return $"http://127.0.0.1:{port}/";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Web] 启动失败：{ex.Message}");
            Stop();
            return null;
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // 忽略
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // 忽略
        }

        _listener = null;
        _cts = null;
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Debug.WriteLine($"[Web] 接受连接失败：{ex.Message}");
                await Task.Delay(200, token).ConfigureAwait(false);
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client), token);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var request = await HttpRequest.ReadAsync(stream).ConfigureAwait(false);
                if (request == null)
                {
                    return;
                }

                var response = Route(request);
                await response.WriteAsync(stream).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Web] 处理连接失败：{ex.Message}");
        }
    }

    private HttpResponse Route(HttpRequest request)
    {
        var path = request.Path;
        try
        {
            if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponse.Html(IndexHtml);
            }

            if (request.Method == "GET" && path.Equals("/api/homeworks", StringComparison.OrdinalIgnoreCase))
            {
                return HttpResponse.Json(JsonSerializer.Serialize(ListHomeworks()));
            }

            if (request.Method == "POST" && path.Equals("/api/homeworks", StringComparison.OrdinalIgnoreCase))
            {
                return AddOrUpdate(request.Body, null);
            }

            if (request.Method == "PUT" && path.StartsWith("/api/homeworks/", StringComparison.OrdinalIgnoreCase))
            {
                return AddOrUpdate(request.Body, path["/api/homeworks/".Length..]);
            }

            if (request.Method == "DELETE" && path.StartsWith("/api/homeworks/", StringComparison.OrdinalIgnoreCase))
            {
                return Delete(path["/api/homeworks/".Length..]);
            }

            return HttpResponse.Json("{\"error\":\"not found\"}", 404);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Web] 路由失败：{ex}");
            return HttpResponse.Json(JsonSerializer.Serialize(new { error = ex.Message }), 500);
        }
    }

    private List<object> ListHomeworks()
    {
        var result = new List<object>();
        var profile = _profileService?.Profile;
        if (profile == null)
        {
            return result;
        }

        var homeworks = RunOnUi(() => profile.Homeworks.ToList()) ?? new List<Homework>();
        foreach (var homework in homeworks)
        {
            result.Add(new
            {
                id = homework.Id.ToString("N"),
                subject = homework.Subject,
                text = RichTextBoxHelper.GetPlainText(homework.Content).Trim(),
                dueTime = homework.DueTime.ToString("yyyy-MM-dd"),
                tags = homework.Tags.ToList()
            });
        }

        return result;
    }

    private HttpResponse AddOrUpdate(string body, string? id)
    {
        var payload = JsonSerializer.Deserialize<HomeworkPayload>(body);
        if (payload == null || string.IsNullOrWhiteSpace(payload.Text))
        {
            return HttpResponse.Json("{\"error\":\"内容不能为空\"}", 400);
        }

        var profile = _profileService?.Profile;
        if (profile == null)
        {
            return HttpResponse.Json("{\"error\":\"数据未加载\"}", 500);
        }

        var dueTime = DateTime.TryParse(payload.DueTime, out var parsed) ? parsed.Date : DateTime.Today;

        RunOnUi(() =>
        {
            Homework? homework = null;
            if (!string.IsNullOrEmpty(id) && Guid.TryParseExact(id, "N", out var guid))
            {
                homework = profile.Homeworks.FirstOrDefault(h => h.Id == guid);
            }

            if (homework == null)
            {
                homework = new Homework();
                profile.Homeworks.Add(homework);
            }

            homework.Subject = string.IsNullOrWhiteSpace(payload.Subject) ? "其它" : payload.Subject!;
            homework.DueTime = dueTime;
            homework.Content = RichTextBoxHelper.CreateDocumentFromPlainText(payload.Text!);
            return true;
        });

        _profileService?.SaveProfile();
        return HttpResponse.Json("{\"ok\":true}");
    }

    private HttpResponse Delete(string id)
    {
        var profile = _profileService?.Profile;
        if (profile == null || !Guid.TryParseExact(id, "N", out var guid))
        {
            return HttpResponse.Json("{\"error\":\"找不到这条作业\"}", 404);
        }

        var removed = RunOnUi(() =>
        {
            var homework = profile.Homeworks.FirstOrDefault(h => h.Id == guid);
            if (homework == null)
            {
                return false;
            }

            profile.Homeworks.Remove(homework);
            return true;
        });

        _profileService?.SaveProfile();
        return HttpResponse.Json(removed == true ? "{\"ok\":true}" : "{\"error\":\"not found\"}", removed == true ? 200 : 404);
    }

    private T? RunOnUi<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.Invoke(action);
    }

    /// <summary>
    /// 极简 HTTP 请求/响应。
    /// </summary>
    private sealed class HttpRequest
    {
        public string Method { get; private init; } = "GET";

        public string Path { get; private init; } = "/";

        public string Body { get; private init; } = "";

        public static async Task<HttpRequest?> ReadAsync(NetworkStream stream)
        {
            var buffer = new List<byte>();
            var chunk = new byte[4096];
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(chunk).ConfigureAwait(false);
                if (read <= 0)
                {
                    return null;
                }

                buffer.AddRange(chunk.Take(read));
                headerEnd = IndexOfHeaderEnd(buffer);
                if (buffer.Count > 64 * 1024)
                {
                    return null;
                }
            }

            var headerText = Encoding.UTF8.GetString(buffer.Take(headerEnd).ToArray());
            var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return null;
            }

            var parts = lines[0].Split(' ');
            if (parts.Length < 2)
            {
                return null;
            }

            var contentLength = 0;
            foreach (var line in lines.Skip(1))
            {
                var index = line.IndexOf(':');
                if (index > 0 && line[..index].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line[(index + 1)..].Trim(), out contentLength);
                }
            }

            var bodyStart = headerEnd + 4;
            var bodyBytes = buffer.Skip(bodyStart).ToList();
            while (bodyBytes.Count < contentLength)
            {
                var read = await stream.ReadAsync(chunk).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                bodyBytes.AddRange(chunk.Take(read));
            }

            var body = contentLength > 0
                ? Encoding.UTF8.GetString(bodyBytes.Take(contentLength).ToArray())
                : "";

            return new HttpRequest
            {
                Method = parts[0].ToUpperInvariant(),
                Path = parts[1].Split('?')[0],
                Body = body
            };
        }

        private static int IndexOfHeaderEnd(List<byte> buffer)
        {
            for (var i = 3; i < buffer.Count; i++)
            {
                if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
                {
                    return i - 3;
                }
            }

            return -1;
        }
    }

    private sealed class HttpResponse
    {
        private string ContentType { get; init; } = "text/plain; charset=utf-8";

        private int StatusCode { get; init; } = 200;

        private string Body { get; init; } = "";

        public static HttpResponse Html(string html) =>
            new() { ContentType = "text/html; charset=utf-8", Body = html };

        public static HttpResponse Json(string json, int statusCode = 200) =>
            new() { ContentType = "application/json; charset=utf-8", Body = json, StatusCode = statusCode };

        public async Task WriteAsync(NetworkStream stream)
        {
            var bytes = Encoding.UTF8.GetBytes(Body);
            var header = $"HTTP/1.1 {StatusCode} {StatusText(StatusCode)}\r\n" +
                         $"Content-Type: {ContentType}\r\n" +
                         $"Content-Length: {bytes.Length}\r\n" +
                         "Cache-Control: no-store\r\n" +
                         "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes).ConfigureAwait(false);
            await stream.WriteAsync(bytes).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static string StatusText(int code) => code switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };
    }

    private sealed class HomeworkPayload
    {
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("dueTime")]
        public string? DueTime { get; set; }
    }

    /// <summary>
    /// 内置页面（单文件，无外部依赖）。
    /// </summary>
    private const string IndexHtml = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>StickyHomeworks3 网页版</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body { margin: 0; font-family: "Microsoft YaHei UI", system-ui, sans-serif; background: #f6f8fc; color: #1e293b; }
  header { background: #2563eb; color: #fff; padding: 14px 18px; display: flex; align-items: center; gap: 12px; }
  header h1 { font-size: 17px; margin: 0; font-weight: 600; }
  header .hint { font-size: 12px; opacity: .85; margin-left: auto; }
  main { max-width: 900px; margin: 0 auto; padding: 16px; }
  .card { background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 14px; margin-bottom: 12px; }
  .row { display: flex; gap: 8px; align-items: center; }
  input, select, textarea, button { font: inherit; }
  input[type=text], input[type=date], select, textarea {
    border: 1px solid #cbd5e1; border-radius: 8px; padding: 8px 10px; background: #fff; color: inherit; width: 100%;
  }
  textarea { min-height: 64px; resize: vertical; }
  button { border: 0; border-radius: 8px; padding: 8px 14px; background: #2563eb; color: #fff; cursor: pointer; white-space: nowrap; }
  button.ghost { background: #eff6ff; color: #1d4ed8; }
  button.danger { background: #fee2e2; color: #b91c1c; }
  button:active { transform: translateY(1px); }
  ul { list-style: none; margin: 0; padding: 0; }
  li { border-bottom: 1px solid #eef2f7; padding: 12px 0; }
  li:last-child { border-bottom: 0; }
  .meta { display: flex; gap: 8px; align-items: center; font-size: 12px; color: #64748b; margin-bottom: 6px; flex-wrap: wrap; }
  .subject { background: #e0e7ff; color: #3730a3; border-radius: 999px; padding: 2px 10px; font-size: 12px; }
  .tag { background: #e2e8f0; color: #334155; border-radius: 999px; padding: 2px 8px; font-size: 12px; }
  .text { font-size: 15px; white-space: pre-wrap; word-break: break-word; }
  .empty { color: #94a3b8; text-align: center; padding: 24px 0; }
  .ops { display: flex; gap: 6px; margin-top: 8px; }
  .grid { display: grid; grid-template-columns: 120px 1fr 150px; gap: 8px; }
  @media (max-width: 640px) { .grid { grid-template-columns: 1fr; } .ops { flex-wrap: wrap; } }
</style>
</head>
<body>
<header>
  <h1>StickyHomeworks3 · 网页版</h1>
  <span class="hint" id="hint">加载中…</span>
</header>
<main>
  <div class="card">
    <div class="grid">
      <input id="subject" type="text" placeholder="科目" list="subjects" />
      <input id="text" type="text" placeholder="作业内容" />
      <input id="due" type="date" />
    </div>
    <div class="row" style="margin-top:8px; justify-content:flex-end;">
      <button id="add">添加作业</button>
    </div>
    <datalist id="subjects"></datalist>
  </div>

  <div class="card">
    <ul id="list"></ul>
    <div class="empty" id="empty" style="display:none;">还没有作业</div>
  </div>
</main>
<script>
const $ = (id) => document.getElementById(id);
let editing = null;

function showHint(text) { $('hint').textContent = text; }

async function load() {
  try {
    const res = await fetch('/api/homeworks', { cache: 'no-store' });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const items = await res.json();
    render(items);
    showHint('共 ' + items.length + ' 条 · ' + new Date().toLocaleTimeString());
  } catch (e) {
    showHint('读取失败：' + e.message);
  }
}

function render(items) {
  const list = $('list');
  list.innerHTML = '';
  $('empty').style.display = items.length ? 'none' : 'block';
  const subjects = new Set();
  for (const it of items) {
    if (it.subject) subjects.add(it.subject);
    const li = document.createElement('li');
    const meta = document.createElement('div');
    meta.className = 'meta';
    meta.innerHTML = '<span class="subject"></span><span class="due"></span>';
    meta.querySelector('.subject').textContent = it.subject || '其它';
    meta.querySelector('.due').textContent = it.dueTime || '';
    for (const t of it.tags || []) {
      const tag = document.createElement('span');
      tag.className = 'tag';
      tag.textContent = t;
      meta.appendChild(tag);
    }

    const text = document.createElement('div');
    text.className = 'text';
    if (editing === it.id) {
      text.appendChild(buildEditor(it));
    } else {
      text.textContent = it.text || '（空）';
    }

    const ops = document.createElement('div');
    ops.className = 'ops';
    if (editing === it.id) {
      const save = document.createElement('button');
      save.textContent = '保存';
      save.onclick = () => saveItem(it.id);
      const cancel = document.createElement('button');
      cancel.className = 'ghost';
      cancel.textContent = '取消';
      cancel.onclick = () => { editing = null; load(); };
      ops.append(save, cancel);
    } else {
      const edit = document.createElement('button');
      edit.className = 'ghost';
      edit.textContent = '编辑';
      edit.onclick = () => { editing = it.id; load(); };
      const del = document.createElement('button');
      del.className = 'danger';
      del.textContent = '删除';
      del.onclick = () => removeItem(it.id);
      ops.append(edit, del);
    }

    li.append(meta, text, ops);
    list.appendChild(li);
  }

  const dl = $('subjects');
  dl.innerHTML = '';
  for (const s of subjects) {
    const opt = document.createElement('option');
    opt.value = s;
    dl.appendChild(opt);
  }
}

function buildEditor(item) {
  const wrap = document.createElement('div');
  wrap.style.cssText = 'display:grid; gap:6px;';
  const subject = document.createElement('input');
  subject.type = 'text'; subject.value = item.subject || ''; subject.placeholder = '科目';
  const text = document.createElement('textarea');
  text.value = item.text || ''; text.placeholder = '作业内容';
  const due = document.createElement('input');
  due.type = 'date'; due.value = item.dueTime || '';
  wrap.append(subject, text, due);
  wrap.dataset.role = 'editor';
  return wrap;
}

async function saveItem(id) {
  const editor = document.querySelector('li [data-role=editor]');
  if (!editor) return;
  const [subject, text, due] = editor.querySelectorAll('input,textarea');
  await fetch('/api/homeworks/' + id, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ subject: subject.value, text: text.value, dueTime: due.value })
  });
  editing = null;
  load();
}

async function removeItem(id) {
  if (!confirm('删除这条作业？')) return;
  await fetch('/api/homeworks/' + id, { method: 'DELETE' });
  load();
}

$('add').onclick = async () => {
  const subject = $('subject').value.trim();
  const text = $('text').value.trim();
  const due = $('due').value;
  if (!text) { alert('先写点内容吧'); return; }
  await fetch('/api/homeworks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ subject, text, dueTime: due })
  });
  $('text').value = '';
  load();
};

$('due').value = new Date().toISOString().slice(0, 10);
load();
setInterval(() => { if (!editing) load(); }, 5000);
</script>
</body>
</html>
""";

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
