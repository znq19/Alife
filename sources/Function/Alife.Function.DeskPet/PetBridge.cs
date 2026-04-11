using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Alife.Function.DeskPet;

/// <summary>
/// 网页桌宠的 C# 化身，仅作为渲染层的协议转发
/// </summary>
public class PetBridge
{
    public event Action? OnReady;
    public event Action<List<string>>? OnHit;
    public event Action<string>? OnChat;
    public event Action? OnDragRequest;

    public void LoadModel(string url)
    {
        SendCommand(new { type = "load", url });
    }

    public void PlayExpression(string? id)
    {
        SendCommand(new { type = "expression", id });
    }

    public void PlayMotion(string group, int index)
    {
        SendCommand(new { type = "motion", group, index });
    }

    public void ShowBubble(string text)
    {
        SendCommand(new { type = "bubble", text });
    }

    public void HideBubble()
    {
        SendCommand(new { type = "hide-bubble" });
    }

    public void SetFocus(double x, double y, bool instant = false)
    {
        SendCommand(new { type = "look", x, y, instant });
    }

    void SendCommand(object command)
    {
        string json = JsonSerializer.Serialize(command);
        webView.CoreWebView2.PostWebMessageAsJson(json);
    }

    void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("type", out JsonElement typeProp) == false) return;
            string? type = typeProp.GetString();

            switch (type)
            {
                case "ready":
                    OnReady?.Invoke();
                    break;
                case "hit":
                    List<string> areas = new();
                    if (root.TryGetProperty("areas", out JsonElement areasProp))
                    {
                        foreach (JsonElement area in areasProp.EnumerateArray())
                        {
                            areas.Add(area.GetString() ?? "");
                        }
                    }
                    OnHit?.Invoke(areas);
                    break;
                case "drag-request":
                    OnDragRequest?.Invoke();
                    break;
                case "chat":
                    OnChat?.Invoke(root.GetProperty("text").GetString() ?? "");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PetBridge Message Error: {ex.Message}");
        }
    }

    readonly WebView2 webView;

    public PetBridge(WebView2 webView)
    {
        this.webView = webView;
        this.webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }
}
