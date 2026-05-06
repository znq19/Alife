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

    public void Dispose()
    {
        _engine.Dispose();
    }
}
