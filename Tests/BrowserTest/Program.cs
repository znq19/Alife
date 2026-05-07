using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.Browser;

namespace BrowserTest
{
    class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing BrowserEngine...");
            using var browser = new BrowserEngine();
            
            string path = Path.GetFullPath("test.html");
            string url = "file:///" + path.Replace("\\", "/");
            Console.WriteLine($"Navigating to: {url}");
            
            var result = await browser.NavigateAsync(url);
            Console.WriteLine($"Navigation Result: Success={result.Success}, StatusCode={result.StatusCode}");
            
            Console.WriteLine("Observing page...");
            string observeResult = await browser.ObserveAsync();
            Console.WriteLine("Observation completed.");
            
            string clickResult = await browser.ClickAsync("[data-alife-id=\"79\"]");
            Console.WriteLine($"Click [79] result: {clickResult}");
            
            Console.WriteLine("Waiting 2 seconds to observe navigation...");
            await Task.Delay(2000);
            
            string finalObserve = await browser.ObserveAsync();
            Console.WriteLine($"Final page title: {finalObserve}");
        }
    }
}
