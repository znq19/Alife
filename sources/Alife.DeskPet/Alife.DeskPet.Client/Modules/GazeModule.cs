using System;

namespace Alife.DeskPet;

public class GazeModule : IPetModule
{
    readonly PetBridge bridge;

    public GazeModule(PetBridge bridge) { this.bridge = bridge; }

    public string JsCode => @"
messageBus.on('look', (msg) => model.focus(msg.x, msg.y, msg.instant));
";

    public void Dispose() { }

    public void SetFocus(double x, double y, bool instant = false)
    {
        bridge.SendMessage("look", new { x, y, instant });
    }
}
