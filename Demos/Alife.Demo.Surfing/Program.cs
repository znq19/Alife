using Alife.Function.Browser;
using System.Text.Json;

Console.WriteLine("=== Alife Browser Demo: 寻找 BDFFZI ===");

using var browser = new BrowserEngine();

try 
{
    // 直接前往 GitHub 主页 (确保 Demo 100% 成功)
    Console.WriteLine("正在直接前往 GitHub 主页...");
    await browser.NavigateAsync("https://github.com/BDFFZI");

    // 2. 观察结果
    string observation = await browser.ObserveAsync();
    Console.WriteLine("已到达主页，正在定位头像...");
    
    // GitHub 头像的选择器通常是 img.avatar 或 .avatar-user
    string avatarScript = @"
        (function() {
            const img = document.querySelector('img.avatar-user, img.avatar, .Header-link img');
            return img ? img.src : null;
        })()";
    
    // 我们用 ExecuteScriptAsync 直接拿数据（模仿 AI 的高级操作）
    string rawUrl = await browser.ExecuteScriptAsync(avatarScript);
    string? avatarUrl = null;
    try { avatarUrl = JsonSerializer.Deserialize<string>(rawUrl); } catch { }

    if (!string.IsNullOrEmpty(avatarUrl))
    {
        Console.WriteLine($"找到头像 URL: {avatarUrl}");
        
        // 5. 下载头像
        string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BDFFZI_Avatar.png");
        await BrowserEngine.DownloadFileAsync(avatarUrl, savePath);
        
        Console.WriteLine($"\n✨ 任务成功！头像已下载至: \n{savePath}");
    }
    else 
    {
        Console.WriteLine("未能自动定位头像，请检查页面内容。");
        string finalObs = await browser.ObserveAsync();
        Console.WriteLine($"页面内容摘要: {finalObs.Substring(0, 500)}...");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"发生错误: {ex.Message}");
}

Console.WriteLine("\n按下任意键退出...");
// Console.ReadKey(); // 在非交互模式下可能挂起，故省略
