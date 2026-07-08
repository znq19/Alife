using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.DeskPet;

namespace Alife.DeskPet;

public class ExpressionModule : IPetModule
{
    readonly PetBridge bridge;
    readonly PetModelMetadata metadata;
    CancellationTokenSource? cancellationTokenSource;
    long? lastTime;

    public ExpressionModule(PetBridge bridge, PetModelMetadata metadata)
    {
        this.bridge = bridge;
        this.metadata = metadata;
        cancellationTokenSource = new CancellationTokenSource();
        _ = AutoRevertLoop(cancellationTokenSource.Token);
    }

    public string JsCode => @"
messageBus.on('expression', (msg) => model.expression(msg.id));
messageBus.on('motion', (msg) => model.motion(msg.group, msg.index, PIXI.live2d.MotionPriority.FORCE));
";

    public void Dispose() { cancellationTokenSource?.Cancel(); cancellationTokenSource?.Dispose(); }

    public void Play(string? id)
    {
        lastTime = Now();
        bridge.SendMessage("expression", new { id });
    }

    public void PlayMotion(string group, int index)
    {
        bridge.SendMessage("motion", new { group, index });
    }

    public bool HandleIpc(IpcCommand cmd)
    {
        if (cmd is PlayExpressionCommand e) { Play(e.Id); return true; }
        if (cmd is MotionCommand m) { PlayMotion(m.Group, m.Index); return true; }
        return false;
    }

    async Task AutoRevertLoop(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(200, ct);
                if (lastTime != null && Now() - lastTime > 3000 && metadata.Expressions.Count > 0)
                { Play(metadata.Expressions.First()); lastTime = null; }
            }
        }
        catch (OperationCanceledException) { }
    }

    static long Now() => DateTimeOffset.Now.ToUnixTimeMilliseconds();
}
