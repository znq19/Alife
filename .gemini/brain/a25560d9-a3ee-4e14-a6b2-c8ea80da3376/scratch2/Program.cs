using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

class Program {
    static async Task Main() {
        var builder = Kernel.CreateBuilder();
        builder.AddBertOnnxEmbeddingGenerator("model", "vocab");
        var kernel = builder.Build();
        var generator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        
        var result = await generator.GenerateAsync(new[] { "test" });
        var embedding = result.First().Vector.ToArray();
    }
}
