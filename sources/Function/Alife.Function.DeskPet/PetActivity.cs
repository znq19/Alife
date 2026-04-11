namespace Alife.Function.DeskPet;

/// <summary>
/// 负责桌宠的自主业务行为逻辑 (灵魂)
/// </summary>
public class PetActivity
{
    public PetActivity(PetProcess process, PetBridge bridge, PetModelMetadata metadata, IPetWindow window)
    {
        this.process = process;
        this.bridge = bridge;
        this.metadata = metadata;
        this.window = window;

        detector = new MotionDetector();
        tracker = new MouseTracker();

        //客户端输入
        this.process.InputReceived += OnCommandReceived;
        this.process.ListenInput();

        //桌宠端输入
        this.bridge.OnHit += areas => {
            HandleMouseHit(areas);
            process.SendOutput(new HitEvent(areas));
        };
        this.bridge.OnChat += text => process.SendOutput(new ChatEvent(text));
        this.bridge.OnReady += () => {
            this.bridge.LoadModel(this.metadata.ModelPath);
            HandleInteraction("startup");
            process.SendOutput(new ReadyEvent());
        };
        this.bridge.OnDragRequest += () => process.SendOutput(new DragRequestEvent());

        //运行交互检测
        detector.Shaked += () => {
            HandleInteraction("shake");
            this.process.SendOutput(new ShakeEvent());
        };
        detector.Moved += () => {
            HandleInteraction("move");
            this.process.SendOutput(new MoveEvent());
        };
        detector.MouseShaked += () => HandleInteraction("random");

        //鼠标位置跟踪
        tracker.MouseMoved += (x, y) => HandleMouseMove(x, y);
        tracker.Start();

        //自动重置视线功能
        HandleFocusReset();
    }

    readonly PetProcess process;
    readonly PetBridge bridge;
    readonly IPetWindow window;
    readonly PetModelMetadata metadata;

    readonly MotionDetector detector;
    readonly MouseTracker tracker;

    long lastMouseMoveTime; //上次鼠标移动时间
    int comboCount; //短时间累计点击数量
    long lastHitTime; //上次点击交互时间

    void HandleMouseMove(int x, int y)
    {
        lastMouseMoveTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        (double ScaleX, double ScaleY) dpi = window.GetDpi();
        (double Left, double Top, double Width, double Height) layout = window.GetLayout();

        double logicalMouseX = x / dpi.ScaleX;
        double logicalMouseY = y / dpi.ScaleY;
        double centerX = layout.Left + layout.Width / 2;
        double centerY = layout.Top + layout.Height / 2;

        //传递鼠标事件给运动交互侦测器
        detector.ReportMouseLocation(logicalMouseX, logicalMouseY, centerX, centerY);
        detector.ReportLocation(layout.Left, layout.Top);

        //传递给桌宠看鼠标
        double nx = (logicalMouseX - centerX) / (layout.Width / 2);
        double ny = (logicalMouseY - centerY) / (layout.Height / 2);
        bridge.SetFocus(nx, ny);
    }
    void HandleMouseHit(List<string> areas)
    {
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (now - lastHitTime < 2500) comboCount++;
        else comboCount = 1;
        lastHitTime = now;

        //连击交互
        if (comboCount >= 5 && comboCount % 5 == 0)
        {
            HandleInteraction("combo");
            return;
        }

        //普通点击交互
        string category = "random";
        if (areas.Any(a => a.Contains("Head", StringComparison.OrdinalIgnoreCase))) category = "head";
        else if (areas.Any(a => a.Contains("Body", StringComparison.OrdinalIgnoreCase))) category = "body";
        HandleInteraction(category);
    }
    void HandleInteraction(string type)
    {
        if (metadata.Interactions.TryGetValue(type, out List<InteractionItem>? pool) == false || pool.Count == 0)
            return;

        InteractionItem item = pool[Random.Shared.Next(pool.Count)];
        if (string.IsNullOrEmpty(item.Exp) == false) bridge.PlayExpression(item.Exp);
        if (item.Mtn != null) bridge.PlayMotion(item.Mtn.Group, item.Mtn.Index);
        if (string.IsNullOrEmpty(item.Text) == false)
        {
            bridge.ShowBubble(item.Text);
            process.SendOutput(new PokeEvent($"(交互: {type}) {item.Text}"));
        }
    }
    async void HandleFocusReset()
    {
        try
        {
            while (true)
            {
                await Task.Delay(500);
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastMouseMoveTime > 3000)
                {
                    bridge.SetFocus(0, 0);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    void OnCommandReceived(IpcCommand cmd)
    {
        switch (cmd)
        {
            case WindowMoveCommand moveCmd:
                window.ProgrammaticMove(moveCmd.X, moveCmd.Y, moveCmd.Duration);
                break;
            case GetPositionCommand:
                (double Left, double Top, double Width, double Height) layout = window.GetLayout();
                (double ScaleX, double ScaleY) dpi = window.GetDpi();
                process.SendOutput(new PositionEvent((layout.Left + layout.Width / 2) * dpi.ScaleX, (layout.Top + layout.Height / 2) * dpi.ScaleY));
                break;
            case BubbleCommand b: bridge.ShowBubble(b.Text); break;
            case PlayExpressionCommand e: bridge.PlayExpression(e.Id); break;
            case MotionCommand m: bridge.PlayMotion(m.Group, m.Index); break;
            case HideBubbleCommand: bridge.HideBubble(); break;
        }
    }
}
