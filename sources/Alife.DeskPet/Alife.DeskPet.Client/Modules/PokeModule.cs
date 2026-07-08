using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Alife.Function.DeskPet;

namespace Alife.DeskPet;

public class PokeModule : IPetModule
{
    readonly PetBridge bridge;
    readonly PetProcess process;
    readonly PetModelMetadata metadata;
    readonly IServiceProvider services;
    int comboCount;
    long lastHitTime;

    public PokeModule(PetBridge bridge, PetProcess process, PetModelMetadata metadata, IServiceProvider services)
    {
        this.bridge = bridge;
        this.process = process;
        this.metadata = metadata;
        this.services = services;
        bridge.OnMessage += OnBridgeMessage;
    }

    public string JsCode => @"
window.addEventListener('dblclick', async function(e) {
    if (e.target.tagName !== 'CANVAS') return;
    var areas = await model.hitTest(e.clientX, e.clientY);
    if (areas.length > 0) postMessage({type:'poke', areas:areas});
});
window.addEventListener('mousedown', async function(e) {
    if (e.button !== 0 || e.target.tagName !== 'CANVAS') return;
    var areas = await model.hitTest(e.clientX, e.clientY);
    if (!areas || areas.length === 0) {
        window.petDragging = true;
        postMessage({type:'drag_start'});
    }
});
window.addEventListener('mouseup', function(e) {
    if (window.petDragging === true) {
        window.petDragging = false;
        postMessage({type:'drag_end'});
    }
});
";

    public void Dispose()
    {
        bridge.OnMessage -= OnBridgeMessage;
    }

    void OnBridgeMessage(string type, JsonElement data)
    {
        if (type == "poke") HandlePoke(data);
    }

    void HandlePoke(JsonElement data)
    {
        List<string> areas = new();
        if (data.TryGetProperty("areas", out JsonElement areasProp))
        {
            foreach (JsonElement area in areasProp.EnumerateArray())
                areas.Add(area.GetString() ?? "");
        }

        string? category = null;
        if (areas.Any(a => a.Contains("Head", StringComparison.OrdinalIgnoreCase))) category = "head";
        else if (areas.Any(a => a.Contains("Body", StringComparison.OrdinalIgnoreCase))) category = "body";
        if (category == null) return;

        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (now - lastHitTime < 2500) comboCount++;
        else comboCount = 1;
        lastHitTime = now;

        if (comboCount != 0 && comboCount % 3 == 0)
        {
            HandleInteraction("mouse_combo");
            process.SendOutput(new InteractionEvent("桌宠被连续触摸：" + category));
            return;
        }

        HandleInteraction(category);
    }

    void HandleInteraction(string type)
    {
        if (!metadata.Interactions.TryGetValue(type, out List<InteractionItem>? pool) || pool == null || pool.Count == 0) return;
        InteractionItem item = pool[Random.Shared.Next(pool.Count)];
        if (!string.IsNullOrEmpty(item.Text))
            services.GetService<BubbleModule>()?.Show(item.Text);
        if (!string.IsNullOrEmpty(item.Exp))
            services.GetService<ExpressionModule>()?.Play(item.Exp);
        if (item.Mtn != null)
            services.GetService<ExpressionModule>()?.PlayMotion(item.Mtn.Group, item.Mtn.Index);
    }
}
