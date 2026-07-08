using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Alife.DeskPet;

public class PetBridge : IDisposable
{
    readonly WebView2 webView;

    public event Action<string, JsonElement>? OnMessage;

    public PetBridge(WebView2 webView)
    {
        this.webView = webView;
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    public void Dispose() { }

    public void SendMessage(string type, object? payload = null)
    {
        JsonObject json = new() { ["type"] = type };
        if (payload != null)
        {
            string payloadJson = JsonSerializer.Serialize(payload);
            JsonObject payloadObj = JsonNode.Parse(payloadJson)!.AsObject();
            foreach (KeyValuePair<string, JsonNode?> kvp in payloadObj)
                json[kvp.Key] = kvp.Value?.DeepClone();
        }
        webView.CoreWebView2.PostWebMessageAsJson(json.ToJsonString());
    }

    public async Task LoadModel(string url)
    {
        TaskCompletionSource tcs = new();
        void Handler(string type, JsonElement _)
        {
            if (type == "loaded")
            {
                OnMessage -= Handler;
                tcs.SetResult();
            }
        }
        OnMessage += Handler;
        SendMessage("load", new { url });
        await tcs.Task;
    }

    void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("type", out JsonElement typeProp)) return;
            string? type = typeProp.GetString();
            if (type == null) return;
            OnMessage?.Invoke(type, root);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PetBridge Error: {ex.Message}");
        }
    }
}
