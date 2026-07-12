using System;
using System.Threading;
using System.Threading.Tasks;
using Alife.Framework;
using Alife.Function.PythonPipe;
using Microsoft.Extensions.Logging;

namespace Alife.Function.Vision.MiniCPM;

public partial class MiniCPMVisionModel
{
    static PythonPipeProcess? pythonPipe;

    static async Task TryInitializedAsync(ILogger<MiniCPMVisionModel> logger, MiniCPMVisionModelConfig? config)
    {
        if (pythonPipe != null) return;

        const string ModelId = "OpenBMB/MiniCPM-V-4.6";
        string modelPath = AIModelUtility.AIModelUtility.EnsureModelExisting(ModelId);
        string precision = config?.Precision ?? "int4";
        pythonPipe = new PythonPipeProcess("minicpm_v", PythonCode);
        pythonPipe.OnStderr += line => logger.LogWarning(line);
        await pythonPipe.StartAsync();
        await pythonPipe.InvokeAsync<string>("init", modelPath, precision);
    }

    static readonly string PythonCode =
        """
        import sys, json, torch
        from PIL import Image
        from transformers import AutoModelForImageTextToText, AutoProcessor

        device = torch.device('cuda')
        model = None
        processor = None

        def init(model_path, precision):
            global model, processor
            load_kwargs = {
                "device_map": "auto",
                "attn_implementation": "sdpa",
            }
            if precision == "int4":
                from transformers import BitsAndBytesConfig
                load_kwargs["quantization_config"] = BitsAndBytesConfig(
                    load_in_4bit=True,
                    bnb_4bit_compute_dtype=torch.bfloat16,
                    bnb_4bit_use_double_quant=True,
                    bnb_4bit_quant_type="nf4"
                )
            else:
                load_kwargs["dtype"] = torch.bfloat16
            model = AutoModelForImageTextToText.from_pretrained(model_path, **load_kwargs)
            processor = AutoProcessor.from_pretrained(model_path)
            return "ready"

        def query(image_path, question, max_new_tokens):
            image = Image.open(image_path).convert("RGB")
            messages = [
                {
                    "role": "user",
                    "content": [
                        {"type": "image", "image": image},
                        {"type": "text", "text": question},
                    ],
                }
            ]
            inputs = processor.apply_chat_template(
                messages,
                add_generation_prompt=True,
                tokenize=True,
                return_dict=True,
                return_tensors="pt",
            ).to(model.device, dtype=model.dtype)
            with torch.no_grad():
                generated_ids = model.generate(**inputs, max_new_tokens=max_new_tokens)
                generated_ids_trimmed = [
                    out_ids[len(in_ids):] for in_ids, out_ids in zip(inputs.input_ids, generated_ids)
                ]
                res = processor.batch_decode(
                    generated_ids_trimmed, skip_special_tokens=True, clean_up_tokenization_spaces=False
                )
            del inputs, generated_ids
            torch.cuda.empty_cache()
            return res[0].strip()
        """;
}

[Module("MiniCPM视觉分析", "基于MiniCPM-V 4.6的轻量本地视觉分析引擎",
    defaultCategory: "Alife 官方/模型接入/视觉模型",
    EditorUI = typeof(MiniCPMVisionModelUI))]
public partial class MiniCPMVisionModel(
    ILogger<MiniCPMVisionModel> logger
) : IVisionModel,
    ISystemEvent,
    IConfigurable<MiniCPMVisionModelConfig>
{
    public MiniCPMVisionModelConfig? Configuration { get; set; }

    public async Task<string> QueryAsync(string imagePath, string question, int maxResponseTokens,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await pythonPipe!.InvokeAsync<string>("query",
                new { image_path = imagePath, question, max_new_tokens = maxResponseTokens });
        }
        catch (Exception ex)
        {
            return $"调用失败：{ex}";
        }
    }

    public async Task AwakeAsync(AwakeContext context)
    {
        await TryInitializedAsync(logger, Configuration);
    }
}
