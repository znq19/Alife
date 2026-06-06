namespace Alife.Function.Vision;

public class MiniCPMVisionModelConfig
{
    /// <summary>
    /// 视觉token降采样模式：16x（更快，默认）或 4x（保留更多细节）。
    /// </summary>
    public string DownsampleMode { get; set; } = "16x";
}
