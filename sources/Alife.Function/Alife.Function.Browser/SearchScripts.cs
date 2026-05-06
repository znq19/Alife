namespace Alife.Function.Browser;

/// <summary>
/// 搜索引擎 JavaScript 解析脚本集合
/// </summary>
public static class SearchScripts
{
    public static string Google => @"
        (function() {
            const results = [];
            document.querySelectorAll('div.g').forEach(item => {
                const titleEl = item.querySelector('h3');
                const linkEl = item.querySelector('a');
                const snippetEl = item.querySelector('div.VwiC3b, div.IsZvec, span.aCOpRe');
                if (titleEl && linkEl && linkEl.href.startsWith('http')) {
                    results.push({
                        title: titleEl.innerText.trim(),
                        url: linkEl.href,
                        snippet: snippetEl ? snippetEl.innerText.trim() : ''
                    });
                }
            });
            if (results.length === 0) {
                return JSON.stringify([{
                    _isDiagnostic: true,
                    title: document.title,
                    text: document.body.innerText.substring(0, 300).replace(/\s+/g, ' '),
                    hasCaptcha: /CAPTCHA|verify|human|robot|验证/i.test(document.body.innerText)
                }]);
            }
            return JSON.stringify(results);
        })()";

    public static string Bing => @"
        (function() {
            const results = [];
            document.querySelectorAll('li.b_algo, div.b_algo').forEach(item => {
                const titleEl = item.querySelector('h2 a, h3 a');
                if (!titleEl) return;
                const title = titleEl.innerText.trim();
                const url = titleEl.href;
                const snippetEl = item.querySelector('.b_caption p, .b_lineclamp2, .b_algo_snippet');
                const snippet = snippetEl ? snippetEl.innerText.trim() : '';
                if (title && url && url.startsWith('http')) results.push({ title, url, snippet });
            });
            if (results.length === 0) {
                return JSON.stringify([{
                    _isDiagnostic: true,
                    title: document.title,
                    text: document.body.innerText.substring(0, 300).replace(/\s+/g, ' '),
                    hasCaptcha: /CAPTCHA|verify|human|robot|人机验证/i.test(document.body.innerText)
                }]);
            }
            return JSON.stringify(results);
        })()";

    public static string Baidu => @"
        (function() {
            const results = [];
            document.querySelectorAll('div.result, div.result-op, .result.c-container').forEach(item => {
                const titleEl = item.querySelector('h3.t a, h3 a');
                if (!titleEl) return;
                const title = titleEl.innerText.trim();
                const url = titleEl.href;
                const snippetEl = item.querySelector('.c-abstract, .content-right_8Zs8j, .c-span18');
                const snippet = snippetEl ? snippetEl.innerText.trim() : '';
                if (title && url && url.startsWith('http')) results.push({ title, url, snippet });
            });
            return JSON.stringify(results);
        })()";

    public static string GetScript(string engineName) => engineName switch
    {
        "Google" => Google,
        "Bing" => Bing,
        "Baidu" => Baidu,
        _ => Google
    };

    public static string GetSearchUrl(string engineName, string query) => engineName switch
    {
        "Google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
        "Bing" => $"https://cn.bing.com/search?q={Uri.EscapeDataString(query)}",
        "Baidu" => $"https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}",
        _ => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
    };

    public static readonly string[] EngineNames = ["Google", "Bing", "Baidu"];
}
