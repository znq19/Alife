using System.ComponentModel;
using System.Text.Json;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace Alife.Implement.Function;

[Plugin("网上冲浪", "让 AI 获取在网络上进行搜索以及扒取网页的能力。")]
public class SurfingService(FunctionService functionService)
    : InteractivePlugin<SurfingService>, IDisposable
{
    [XmlFunction("websearch")]
    [Description("在线搜索。（使用后需等待结果返回）")]
    public async Task WebSearch(XmlExecutorContext context, [Description("你要搜索的关键词")] string query)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("错误的调用方式，应该使用自闭合标签调用。");

        try
        {
            string url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&setmkt=zh-CN";
            string jsonResult = await worker.EnqueueAsync(async webView =>
            {
                TaskCompletionSource<bool> tcs = new();

                void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                    tcs.TrySetResult(e.IsSuccess);

                webView.CoreWebView2.NavigationCompleted += Handler;
                webView.Source = new Uri(url);
                bool success = await tcs.Task;
                webView.CoreWebView2.NavigationCompleted -= Handler;

                if (success == false) return "[]";

                // 等待页面渲染和动态内容加载
                await Task.Delay(2000);

                string script = @"
                    (function() {
                        const results = [];
                        
                        // 1. 尝试基于容器的提取 (国际版或标准排版)
                        document.querySelectorAll('li.b_algo, div.b_algo, .b_ans, .b_gsc').forEach(item => {
                            const titleEl = item.querySelector('h2 a, h3 a, .b_algo h2, a[h]');
                            if (!titleEl) return;
                            
                            const title = titleEl.innerText.trim();
                            const url = titleEl.href || (titleEl.querySelector('a') ? titleEl.querySelector('a').href : '');
                            
                            const snippetEl = item.querySelector('.b_caption p, .b_lineclamp2, .b_algo_snippet, .ftr_st');
                            const snippet = snippetEl ? snippetEl.innerText.trim() : '';
                            
                            if (title && url && url.startsWith('http')) {
                                results.push({ title, url, snippet });
                            }
                        });

                        // 2. 扁平结构回退提取 (国内特供版等无标准容器结构)
                        if (results.length === 0) {
                            document.querySelectorAll('h2 a, h3 a').forEach(a => {
                                const title = a.innerText.trim();
                                const url = a.href;
                                if (!title || !url || !url.startsWith('http')) return;
                                
                                // 尝试向上找几层父级来定位可能包含摘要的区块
                                let snippet = '';
                                let container = a.parentElement;
                                for (let i = 0; i < 4; i++) {
                                    if (!container || container.tagName === 'BODY') break;
                                    const descEl = container.querySelector('.b_caption, p, .b_lineclamp2, .c_abstract');
                                    if (descEl && descEl.innerText !== title) {
                                        snippet = descEl.innerText.trim();
                                        break;
                                    }
                                    container = container.parentElement;
                                }
                                results.push({ title, url, snippet });
                            });
                        }

                        // 3. 去重
                        const uniqueResults = [];
                        const urls = new Set();
                        for (const r of results) {
                            if (!urls.has(r.url) && r.title.length > 0) {
                                urls.add(r.url);
                                uniqueResults.push(r);
                            }
                        }

                        return JSON.stringify(uniqueResults, null, 2);
                    })();";

                return await webView.ExecuteScriptAsync(script);
            });

            // ExecuteScriptAsync 返回的是带引号的字符串 JSON.stringify 结果，需解包一次
            string unescaped = JsonSerializer.Deserialize<string>(jsonResult) ?? "[]";

            Poke($"""
                  [{nameof(WebSearch)}] 搜索成功，找到以下结果：
                  ```json
                  {unescaped}
                  ```
                  """);
        }
        catch (Exception ex)
        {
            Poke($"[{nameof(WebSearch)}] 搜索失败: {ex.Message}");
        }
    }

    [XmlFunction("webfetch")]
    [Description("抓取网页。（使用后需等待结果返回）")]
    public async Task WebFetch(XmlExecutorContext context, [Description("要抓取的网页绝对URL，必须以http开头")] string url)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("错误的调用方式，应该使用自闭合标签调用。");

        if (url.StartsWith("http") == false)
        {
            Poke($"[{nameof(WebFetch)}] 错误：无效的 URL ({url})。");
            return;
        }

        try
        {
            string jsonResult = await worker.EnqueueAsync(async webView =>
            {
                TaskCompletionSource<bool> tcs = new();

                void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
                    tcs.TrySetResult(e.IsSuccess);

                webView.CoreWebView2.NavigationCompleted += Handler;
                webView.Source = new Uri(url);
                bool success = await tcs.Task;
                webView.CoreWebView2.NavigationCompleted -= Handler;

                if (success == false) return "\"页面加载失败\"";

                // 等待目标页面渲染
                await Task.Delay(2000);

                string script = @"
                    (function() {
                        const body = document.body.cloneNode(true);
                        
                        // 1. 移除无关标签（脚本、样式、导航、页脚、表单等）
                        ['script', 'style', 'nav', 'footer', 'header', 'iframe', 'noscript', 'svg', 'canvas', 'img', 'aside', 'form', 'button'].forEach(tag => {
                            body.querySelectorAll(tag).forEach(el => el.remove());
                        });
                        
                        // 2. 移除常见的噪音区块（广告、侧边栏、评论、菜单等）
                        const noiseSelectors = [
                            '.ad', '.ads', '.advertisement', '.banner', 
                            '.sidebar', '.side-bar', '.widget', 
                            '.comments', '.comment-list', 
                            '.menu', '.navigation', '.nav-links',
                            '.share', '.social', 
                            '.footer', '.bottom'
                        ];
                        noiseSelectors.forEach(sel => {
                            body.querySelectorAll(sel).forEach(el => el.remove());
                        });
                        
                        // 3. 提取纯文本，将多个连续换行统一替换为双换行 (\n\n)，保持段落感
                        return body.innerText.replace(/\n[ \t]*\n[ \t\n]*/g, '\n\n').trim();
                    })();";

                return await webView.ExecuteScriptAsync(script);
            });

            string content = JsonSerializer.Deserialize<string>(jsonResult) ?? "";

            // 避免内容过长（考虑到Token上限）
            if (content.Length > 8000)
            {
                content = content.Substring(0, 8000) + "\n\n...[内容过长已截断]";
            }

            Poke($"""
                  [{nameof(WebFetch)}] 页面抓取成功：
                  ```text
                  {content}
                  ```
                  """);
        }
        catch (Exception ex)
        {
            Poke($"[{nameof(WebFetch)}] 抓取失败: {ex.Message}");
        }
    }

    readonly WebViewWorker worker = new();

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        functionService.RegisterHandler(this);
    }

    public void Dispose()
    {
        worker?.Dispose();
    }
}