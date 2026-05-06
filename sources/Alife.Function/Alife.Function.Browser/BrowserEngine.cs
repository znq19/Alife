using Alife.Basic;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace Alife.Function.Browser;

/// <summary>
/// AI 可控浏览器引擎。
/// 只提供最基本的浏览器操控原语：导航、截图、点击、打字、滚动。
/// AI 通过「看截图 → 决定操作 → 看截图」的循环来完成一切任务。
/// </summary>
public class BrowserEngine : IDisposable
{
    readonly WebViewWorker worker = new();

    /// <summary>
    /// 导航到指定 URL 并等待页面加载完成
    /// </summary>
    public async Task<(bool Success, int StatusCode)> NavigateAsync(string url)
    {
        return await worker.EnqueueAsync(async webView =>
        {
            TaskCompletionSource<(bool, int)> tcs = new();
            void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                => tcs.TrySetResult((e.IsSuccess, e.HttpStatusCode));

            webView.CoreWebView2.NavigationCompleted += Handler;
            webView.Source = new Uri(url);

            // 智能探测：轮询页面状态，如果已经有内容了就提前返回
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++) // 最多等 10 秒
                {
                    await Task.Delay(500);
                    if (tcs.Task.IsCompleted) return;

                    try
                    {
                        // 检查是否已经可以交互，或者 body 已经有文字内容了
                        string check = await webView.CoreWebView2.ExecuteScriptAsync(
                            "(function() { return { ready: document.readyState !== 'loading', hasContent: document.body && document.body.innerText.length > 50 }; })()");
                        if (check.Contains("\"ready\":true") && check.Contains("\"hasContent\":true"))
                        {
                            tcs.TrySetResult((true, 200));
                            return;
                        }
                    }
                    catch { }
                }
                tcs.TrySetResult((true, 200)); // 最终保底
            });

            var result = await tcs.Task;
            webView.CoreWebView2.NavigationCompleted -= Handler;
            return result;
        });
    }


    /// <summary>
    /// 观察当前页面，返回精简的页面结构信息：
    /// 标题、URL、可见文本、所有可交互元素及其 CSS 选择器
    /// </summary>
    public async Task<string> ObserveAsync()
    {
        return await ExecuteScriptAsync(@"
        (function() {
            const info = {
                title: document.title,
                url: location.href,
                text: document.body.innerText.substring(0, 3000),
                interactiveElements: []
            };

            // 收集所有可交互元素，附带可用的 CSS 选择器
            const elements = document.querySelectorAll('a, button, input, textarea, select, [role=button], [onclick]');
            const seen = new Set();
            elements.forEach((el, i) => {
                // 构建一个尽可能精确的选择器
                let selector = '';
                if (el.id) selector = '#' + el.id;
                else if (el.name) selector = el.tagName.toLowerCase() + '[name=""' + el.name + '""]';
                else if (el.className && typeof el.className === 'string') {
                    const cls = el.className.trim().split(/\s+/).filter(c => c.length > 0 && c.length < 30).slice(0, 2).join('.');
                    if (cls) selector = el.tagName.toLowerCase() + '.' + cls;
                }
                if (!selector) selector = el.tagName.toLowerCase() + ':nth-of-type(' + (Array.from(el.parentElement?.children || []).filter(c => c.tagName === el.tagName).indexOf(el) + 1) + ')';

                // 跳过重复选择器
                if (seen.has(selector)) return;
                seen.add(selector);

                const text = (el.innerText || el.value || el.placeholder || el.title || el.alt || '').substring(0, 60).trim();
                const type = el.type || el.tagName.toLowerCase();
                const href = el.href || '';

                info.interactiveElements.push({ selector, type, text, href: href.substring(0, 150) });
            });

            // 限制数量防止 token 爆炸
            info.interactiveElements = info.interactiveElements.slice(0, 40);
            return JSON.stringify(info, null, 2);
        })()");
    }

    /// <summary>
    /// 点击页面上匹配 CSS 选择器的元素
    /// </summary>
    public async Task<string> ClickAsync(string selector)
    {
        string result = await ExecuteScriptAsync($@"
        (function() {{
            const el = document.querySelector('{selector.Replace("'", "\\'")}');
            if (!el) return 'ERROR: 未找到匹配元素: {selector}';
            el.scrollIntoView({{block:'center'}});
            const text = (el.innerText || el.tagName).substring(0, 50);
            el.click();
            return 'SUCCESS: 已点击 ' + text;
        }})()", 300);
        
        return result ?? "ACTION: 点击操作已发出（页面可能正在跳转）";
    }

    /// <summary>
    /// 在页面上匹配 CSS 选择器的输入框中输入文字
    /// </summary>
    public async Task<string> TypeAsync(string selector, string text, bool submit = false)
    {
        string escapedText = text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n");
        string submitAction = submit ? "el.form?.submit() || el.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',keyCode:13,bubbles:true}));" : "";
        return await ExecuteScriptAsync($@"
        (function() {{
            const el = document.querySelector('{selector.Replace("'", "\\'")}');
            if (!el) return 'ERROR: 未找到输入框: {selector}';
            el.scrollIntoView({{block:'center'}});
            el.focus();
            el.value = '{escapedText}';
            el.dispatchEvent(new Event('input', {{bubbles:true}}));
            el.dispatchEvent(new Event('change', {{bubbles:true}}));
            {submitAction}
            return '已输入: {escapedText}';
        }})()", submit ? 1500 : 500);
    }

    /// <summary>
    /// 滚动页面
    /// </summary>
    public async Task<string> ScrollAsync(string direction, int pixels = 500)
    {
        int scrollY = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? -pixels : pixels;
        return await ExecuteScriptAsync($@"
        (function() {{
            window.scrollBy(0, {scrollY});
            return '已滚动 {direction} {pixels}px，当前位置: ' + window.scrollY + '/' + document.body.scrollHeight;
        }})()", 500);
    }

    /// <summary>
    /// 在当前页面执行 JavaScript 并返回结果（内部工具方法）
    /// 它会自动处理 WebView2 的二次 JSON 转义，返回正常的字符串。
    /// </summary>
    public async Task<string> ExecuteScriptAsync(string script, int delayMs = 0)
    {
        string rawRes = await worker.EnqueueAsync(async webView =>
        {
            string res = await webView.CoreWebView2.ExecuteScriptAsync(script);
            if (delayMs > 0) await Task.Delay(delayMs);
            return res;
        });

        // WebView2 返回的是 JSON 序列化后的字符串（带引号且内部已转义）
        // 我们需要反序列化一次来拿到原本的字符串内容
        try 
        {
            return JsonSerializer.Deserialize<string>(rawRes) ?? rawRes;
        }
        catch 
        {
            return rawRes;
        }
    }


    /// <summary>
    /// 通过 HttpClient 下载文件到本地
    /// </summary>
    public static async Task DownloadFileAsync(string url, string savePath)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        var bytes = await client.GetByteArrayAsync(url);

        string? dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(savePath, bytes);
    }

    public void Dispose() => worker.Dispose();
}
