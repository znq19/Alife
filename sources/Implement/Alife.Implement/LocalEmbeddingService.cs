#pragma warning disable SKEXP0070

using Alife.Basic;
using Alife.Framework;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

[Plugin("本地文本嵌入", "基于 ONNX BERT 模型的本地文本向量化服务，为记忆框架等功能提供离线语义嵌入能力。", launchOrder: -100)]
public class LocalEmbeddingService : Plugin
{
    public override Task AwakeAsync(AwakeContext context)
    {
        string modelPath = Path.Combine(AlifePath.ModelsFolderPath, "all-minilm-l6-v2", "model.onnx");
        string vocabPath = Path.Combine(AlifePath.ModelsFolderPath, "all-minilm-l6-v2", "vocab.txt");

        if (File.Exists(modelPath) == false)
            throw new FileNotFoundException($"找不到嵌入模型，请将 model.onnx 放置到：{modelPath}");
        if (File.Exists(vocabPath) == false)
            throw new FileNotFoundException($"找不到词表文件，请将 vocab.txt 放置到：{vocabPath}");

        context.kernelBuilder.AddBertOnnxTextEmbeddingGeneration(modelPath, vocabPath);
        return Task.CompletedTask;
    }
}
