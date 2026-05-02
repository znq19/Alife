using HtmlAgilityPack;

namespace SearchConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string query = args.Length > 0 ? args[0] : "AI 最新新闻";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            string url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&setmkt=zh-CN";

            try
            {
                string html = await client.GetStringAsync(url);
                Console.WriteLine(html);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
