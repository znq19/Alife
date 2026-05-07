using Alife.Function.Browser;
using Xunit;

namespace Alife.Test.Browser;

public class BrowserEngineTests : IDisposable
{
    private readonly BrowserEngine _engine;

    public BrowserEngineTests()
    {
        _engine = new BrowserEngine();
    }

    [Fact]
    public async Task Navigate_ShouldReturnSuccess()
    {
        var result = await _engine.NavigateAsync("https://www.example.com");

        Assert.True(result.Success, "导航失败");
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Observe_ShouldReturnStructuredJson()
    {
        await _engine.NavigateAsync("https://www.example.com");

        string json = await _engine.ObserveAsync();

        Assert.NotNull(json);
        Assert.Contains("title", json);
        Assert.Contains("Example Domain", json);
        Assert.Contains("interactiveElements", json);
    }

    [Fact]
    public async Task Click_ShouldReturnFeedback()
    {
        await _engine.NavigateAsync("https://www.example.com");

        string result = await _engine.ClickAsync("h1");

        Assert.Contains("已点击", result);
    }

    [Fact]
    public async Task Type_ShouldWorkOnSearchEngine()
    {
        await _engine.NavigateAsync("https://www.google.com");

        string result = await _engine.TypeAsync("textarea[name=q], input[name=q]", "hello world", submit: false);

        Assert.Contains("已输入", result);
    }

    [Fact]
    public async Task SearchWorkflow_ShouldFindGithubRepo()
    {
        // 1. 导航到搜索引擎
        await _engine.NavigateAsync("https://www.bing.com");

        // 2. 搜索 "GitHub Alife-Pet" (假设这是你的项目关键词)
        // Bing 的搜索框选择器通常是 #sb_form_q 或 input[name=q]
        string typeResult = await _engine.TypeAsync("input[name=q]", "GitHub Alife-Pet", submit: true);
        Assert.Contains("已输入", typeResult);

        // 3. 等待并观察搜索结果
        await Task.Delay(3000); 
        string observation = await _engine.ObserveAsync();
        Assert.Contains("Alife-Pet", observation); // 只要页面里出现了关键词，说明搜索成功了

        // 4. 点击第一个 GitHub 链接
        string clickResult = await _engine.ClickAsync("a[href*='github.com']");
        // 只要不报错就行，因为跳转时可能返回 null 或 ACTION 提示
        Assert.DoesNotContain("ERROR", clickResult); 

        // 5. 验证是否成功跳转到 GitHub
        await Task.Delay(4000); // 跳转比较慢，多等会儿
        string finalPage = await _engine.ObserveAsync();
        Assert.Contains("GitHub", finalPage);
    }

    [Fact]
    public async Task MidiClouds_SearchAndExtractData()
    {
        // 1. 打开 MIDIClouds 主页
        var nav = await _engine.NavigateAsync("https://www.midiclouds.com/");
        Assert.True(nav.Success, "导航到 MIDIClouds 失败");

        // 2. 搜索「极乐净土」
        // 使用 subagent 发现的精准选择器 #KeyStrs
        await _engine.TypeAsync("#KeyStrs", "极乐净土", submit: false);
        
        // 3. 点击搜索按钮 (div.pCls)
        string searchClick = await _engine.ClickAsync("div.pCls");
        Console.WriteLine($"=== 搜索按钮点击结果 === {searchClick}");
        Console.WriteLine($"=== 搜索后 URL === {await _engine.ExecuteScriptAsync("location.href")}");

        // 4. 点击第一个搜索结果 (使用更精准的 td.fmTD a)
        string resultClick = await _engine.ClickAsync("td.fmTD a");
        Console.WriteLine($"=== 结果链接点击结果 === {resultClick}");
        Console.WriteLine($"=== 点击后 URL === {await _engine.ExecuteScriptAsync("location.href")}");

        // 5. 用 JS 提取 data-d 和 data-f
        string jsResult = await _engine.ExecuteScriptAsync(@"
        (function() {
            var els = document.querySelectorAll('[data-d],[data-f]');
            var results = [];
            els.forEach(function(el) {
                results.push({
                    tag: el.tagName,
                    dataD: el.getAttribute('data-d'),
                    dataF: el.getAttribute('data-f'),
                    text: (el.innerText || '').substring(0, 80)
                });
            });
            return JSON.stringify(results, null, 2);
        })()");

        Console.WriteLine($"=== 抓取结果 ===\n{jsResult}");
        Assert.NotNull(jsResult);
        Assert.Contains("data", jsResult.ToLower());
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
