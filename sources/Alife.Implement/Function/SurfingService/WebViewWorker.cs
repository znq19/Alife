using Microsoft.Web.WebView2.WinForms;

/// <summary>
/// 在独立的 STA 线程中管理 WebView2，以支持后台静默执行浏览器任务
/// </summary>
public class WebViewWorker : IDisposable
{
    public Task<T> EnqueueAsync<T>(Func<WebView2, Task<T>> action)
    {
        TaskCompletionSource<T> tcs = new();

        jobs.Add(async () =>
        {
            try
            {
                if (initialized == false || webView == null)
                    throw new InvalidOperationException("WebView2 引擎尚未就绪或未安装。");

                T result = await action(webView);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
    
    readonly System.Collections.Concurrent.BlockingCollection<Func<Task>> jobs = new();
    WebView2? webView;
    Form? form;
    bool initialized;

    public WebViewWorker()
    {
        var thread = new Thread(RunLoop);
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    public void Dispose()
    {
        jobs.CompleteAdding();
        if (form is { IsDisposed: false })
        {
            form.Invoke((Action)(() => form.Close()));
        }
    }

    void RunLoop()
    {
        form = new Form
        {
            Width = 1024,
            Height = 768,
            WindowState = FormWindowState.Minimized,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Opacity = 0 // 完全透明隐藏
        };

        webView = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(webView);

        form.Load += async (s, e) =>
        {
            try
            {
                // 初始化 Edge 核心
                await webView.EnsureCoreWebView2Async();
                initialized = true;

                // 开启后台消费者循环
                _ = Task.Run(() =>
                {
                    foreach (Func<Task> job in jobs.GetConsumingEnumerable())
                    {
                        if (form.IsDisposed) break;
                        try
                        {
                            // 将任务通过 Invoke 封送到 STA 线程执行
                            Task task = form.Invoke(job);
                            // 阻塞等待该任务完全结束，保证浏览器任务串行
                            task.Wait();
                        }
                        catch
                        {
                            // 忽略单个任务崩溃，保证队列继续运行
                        }
                    }
                });
            }
            catch
            {
                // 浏览器核心初始化失败
            }
        };

        // 启动消息循环
        Application.Run(form);
    }
}