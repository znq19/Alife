using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace SearchTest
{
    public partial class MainWindow : Window
    {
        private bool _isInitialized;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();
                _isInitialized = true;
                Log("WebView2 Initialized.");
            }
            catch (Exception ex)
            {
                Log($"WebView2 Initialization failed: {ex.Message}");
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            string query = "2026年5月2日 热门趣事";
            Log($"Searching for: {query}...");

            string url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&setmkt=zh-CN";
            await NavigateAndProcess(url, async () =>
            {
                // Wait a bit for results to load
                await Task.Delay(2000);

                string script = @"
                    (function() {
                        const results = [];
                        // 1. 尝试通用的搜索结果容器
                        document.querySelectorAll('li.b_algo, .b_ans, .b_gsc').forEach(item => {
                            // 2. 更加宽泛地寻找标题链接
                            const titleEl = item.querySelector('h2 a, h3 a, .b_algo h2, a[h]');
                            if (!titleEl) return;

                            const title = titleEl.innerText.trim();
                            const url = titleEl.href || (titleEl.querySelector('a') ? titleEl.querySelector('a').href : '');
                            
                            // 3. 寻找描述文字
                            const snippetEl = item.querySelector('.b_caption p, .b_lineclamp2, .b_algo_snippet, .ftr_st');
                            const snippet = snippetEl ? snippetEl.innerText.trim() : '';

                            if (title && url && url.startsWith('http')) {
                                results.push({ title, url, snippet });
                            }
                        });
                        return JSON.stringify(results, null, 2);
                    })();";

                string json = await WebView.ExecuteScriptAsync(script);
                // ExecuteScriptAsync returns a JSON-encoded string, need to unescape
                string results = JsonSerializer.Deserialize<string>(json);
                Log("Search Results:\n" + results);
            });
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            string url = UrlBox.Text;
            Log($"Fetching content from: {url}...");

            await NavigateAndProcess(url, async () =>
            {
                await Task.Delay(2000); // Wait for rendering

                string script = @"
                    (function() {
                        const body = document.body.cloneNode(true);
                        ['script', 'style', 'nav', 'footer', 'header', 'iframe', 'noscript'].forEach(tag => {
                            body.querySelectorAll(tag).forEach(el => el.remove());
                        });
                        return body.innerText.replace(/\n\s*\n/g, '\n').trim();
                    })();";

                string json = await WebView.ExecuteScriptAsync(script);
                string content = JsonSerializer.Deserialize<string>(json);
                Log("Webpage Content (Extracted):\n" + (content.Length > 1000 ? content.Substring(0, 1000) + "..." : content));
            });
        }

        private async Task NavigateAndProcess(string url, Func<Task> process)
        {
            TaskCompletionSource<bool> tcs = new();
            void Handler(object sender, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
            
            WebView.CoreWebView2.NavigationCompleted += Handler;
            try
            {
                WebView.Source = new Uri(url);
                bool success = await tcs.Task;
                if (success)
                {
                    await process();
                }
                else
                {
                    Log("Navigation failed.");
                }
            }
            finally
            {
                WebView.CoreWebView2.NavigationCompleted -= Handler;
            }
        }

        private void Log(string message)
        {
            OutputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n\n");
            OutputBox.ScrollToEnd();
        }
    }
}
