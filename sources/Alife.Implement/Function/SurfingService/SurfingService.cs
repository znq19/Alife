using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Browser;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;

namespace Alife.Implement.Function;

[Plugin("网上冲浪", "让 AI 像人一样操控浏览器：打开网页、观察页面、点击、打字、滚动。")]
[Description(@"你拥有一个真实的、用户可见的浏览器窗口。通过以下函数来操控它：
- navigate: 打开网址
- observe: 观察当前页面内容和可交互元素（返回标题、文本和按钮选择器）
- click: 点击页面上的元素
- type: 在输入框中打字
- scroll: 滚动页面
- download: 下载文件

重要规则：
1. **遇到障碍别放弃**：如果你遇到了验证码 (CAPTCHA)、滑动验证、登录墙或者需要扫码，不要直接报错或放弃任务。
2. **求助主人**：你应该通过对话告诉主人：'主人，我遇到了验证码，请在浏览器窗口中帮我处理一下'。
3. **等待与继续**：在主人处理完后，你可以重新调用 observe 来确认状态并继续执行剩下的任务。")]
public class SurfingService(FunctionService functionService)
    : InteractivePlugin<SurfingService>, IDisposable
{
    readonly BrowserEngine browser = new();

    [XmlFunction("navigate")]
    [Description("在浏览器中打开指定网址。成功后会自动返回页面观察结果，无需再次调用 observe。")]
    public async Task Navigate(XmlExecutorContext context,
        [Description("要打开的网址")] string url)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        var result = await browser.NavigateAsync(url);
        if (result.Success)
        {
            string observation = await browser.ObserveAsync();
            Poke($"[Navigate] 已打开: {url}\n[Auto-Observe] 页面内容：\n{observation}");
        }
        else
        {
            Poke($"[Navigate] 加载失败 (HTTP {result.StatusCode})");
        }
    }


    [XmlFunction("observe")]
    [Description("观察当前页面：返回标题、URL、正文文本以及所有可交互元素的选择器。")]
    public async Task Observe(XmlExecutorContext context)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ObserveAsync();
        Poke($"[Observe] 页面状态：\n{result}");
    }

    [XmlFunction("click")]
    [Description("点击页面上的元素。")]
    public async Task Click(XmlExecutorContext context,
        [Description("CSS 选择器")] string selector)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ClickAsync(selector);
        Poke($"[Click] {result}");
    }

    [XmlFunction("type")]
    [Description("在输入框中打字。")]
    public async Task Type(XmlExecutorContext context,
        [Description("CSS 选择器")] string selector,
        [Description("要输入的文字")] string text,
        [Description("是否提交（回车），默认 true")] bool submit = true)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.TypeAsync(selector, text, submit);
        Poke($"[Type] {result}");
    }

    [XmlFunction("scroll")]
    [Description("滚动页面。")]
    public async Task Scroll(XmlExecutorContext context,
        [Description("方向：up 或 down")] string direction,
        [Description("距离（像素），默认 500")] int pixels = 500)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        string result = await browser.ScrollAsync(direction, pixels);
        Poke($"[Scroll] {result}");
    }

    [XmlFunction("download")]
    [Description("下载文件到本地。")]
    public async Task Download(XmlExecutorContext context,
        [Description("下载链接")] string url,
        [Description("本地绝对路径")] string path)
    {
        if (context.CallMode != CallMode.OneShot)
            throw new Exception("请使用自闭合标签调用。");

        await BrowserEngine.DownloadFileAsync(url, path);
        Poke($"[Download] 文件已下载至：{path}");
    }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        functionService.RegisterHandler(this);
    }

    public void Dispose() => browser.Dispose();
}