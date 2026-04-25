using System.Text;
using Alife.Framework;
using Newtonsoft.Json.Linq;

namespace Alife.Implement;

/// <summary>
/// 一个特殊的 HttpClient 处理器，用于拦截 DeepSeek 的原始流，
/// 将其中的 reasoning_content 字段伪装成 content 字段，
/// 从而让旧版本的 Semantic Kernel / OpenAI SDK 能够读取到思考过程。
/// </summary>
public class DeepSeekReasoningHandler : DelegatingHandler
{
    public DeepSeekReasoningHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // 仅对流式传输进行处理
        if (response.IsSuccessStatusCode &&
            response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            response.Content = new StreamContent(new ReasoningStreamWrapper(stream));
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        }

        return response;
    }

    class ReasoningStreamWrapper : Stream
    {
        readonly Stream innerStream;
        readonly StreamReader reader;
        readonly MemoryStream outputBuffer = new();

        public ReasoningStreamWrapper(Stream innerStream)
        {
            this.innerStream = innerStream;
            this.reader = new StreamReader(innerStream, Encoding.UTF8);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // 如果缓冲区空了，读取下一行并处理
            if (outputBuffer.Position >= outputBuffer.Length)
            {
                outputBuffer.SetLength(0);
                outputBuffer.Position = 0;

                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) return 0;

                string processedLine = ProcessLine(line) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(processedLine);
                outputBuffer.Write(bytes);
                outputBuffer.Position = 0;
            }

            int count = await outputBuffer.ReadAsync(buffer, cancellationToken);
            return count;
        }

        private string ProcessLine(string line)
        {
            if (!line.StartsWith("data: ")) return line;

            string jsonPart = line.Substring(6).Trim();
            if (string.IsNullOrWhiteSpace(jsonPart) || jsonPart == "[DONE]") return line;

            try
            {
                JObject obj = JObject.Parse(jsonPart);
                JToken? delta = obj["choices"]?[0]?["delta"];
                if (delta is JObject deltaObj)
                {
                    JToken? reasoning = deltaObj["reasoning_content"];
                    if (reasoning != null && reasoning.Type != JTokenType.Null)
                    {
                        // 发现思考内容，将其转移到 content 并加上前缀
                        deltaObj["content"] = $"{ChatBot.ThinkContentPrefix}{reasoning}";
                        deltaObj.Remove("reasoning_content");
                    }
                }
                return "data: " + obj.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                // 如果解析失败，原样返回，避免破坏流
                return line;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => throw new NotSupportedException(); }
        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                reader.Dispose();
                innerStream.Dispose();
                outputBuffer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
