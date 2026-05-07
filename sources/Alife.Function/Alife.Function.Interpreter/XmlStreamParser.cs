using System.Text;

namespace Alife.Function.Interpreter;

public class XmlStreamParser
{
    public IReadOnlyList<string> TagStack => tagStack;
    public IReadOnlyDictionary<string, string> TagParameters => parsedAttributes;
    public Func<Task>? TagOpened { get; set; }
    public Func<Task>? TagClosed { get; set; }
    public Func<Task>? TagShotted { get; set; }
    public Func<Task>? TagReset { get; set; }
    public Func<char, Task>? ContentGot { get; set; }
    public event Action<string, Exception>? Error;

    public async Task Feed(char ch)
    {
        if (isAnnotation)
        {
            switch (ch)
            {
                case '>':
                    if (annotationBuffer.ToString().EndsWith("--"))
                        ClearAnnotation();
                    break;
                default:
                    annotationBuffer.Append(ch);
                    break;
            }

            return;
        }

        if (isCharEscaping)
        {
            switch (ch)
            {
                case ';':
                    escapingBuffer.Append(ch);
                    await FlashEscaping();
                    break;
                case '"' or '<':
                    await FlashEscaping();
                    await Feed(ch);
                    break;
                default:
                    escapingBuffer.Append(ch);
                    break;
            }

            return;
        }

        if (ch == '&')
        {
            escapingBuffer.Append(ch);
            isCharEscaping = true;
            return;
        }

        if (isTagParsing == false)
        {
            if (ch == '<')
            {
                isTagParsing = true;
            }
            else
            {
                await HandleContentChar(ch);
            }

            return;
        }

        // 如果在原样输出模式下，我们需要判断是否是闭标签的开始
        if (IsVerbatimMode)
        {
            // 在原样输出模式中，我们只关心是否出现了类似 </TAGNAME> 的结构
            if (ch == '/')
            {
                tagMode = 1; // 标记为闭标签尝试
                return;
            }

            // 继续收集标签名以供比对
            if (ch == '>')
            {
                FlushTagOrAttributeName();
                if (tagMode == 1 && currentTagName == tagStack.Last())
                {
                    // 匹配成功，退出原样模式
                    await FlashTag();
                }
                else
                {
                    // 匹配失败，把收到的内容作为普通内容吐出去
                    await HandleContentChar('<');
                    if (tagMode == 1) await HandleContentChar('/');
                    if (currentTagName != null)
                    {
                        foreach (var c in currentTagName) await HandleContentChar(c);
                    }

                    foreach (var c in tagBuffer.ToString()) await HandleContentChar(c);

                    ClearTag();
                }

                return;
            }

            if (ch == ' ' || ch == '=')
            {
                // 在原样模式的闭标签尝试中遇到空格，基本可以判定不是我们要找的闭标签
                await HandleContentChar('<');
                if (tagMode == 1) await HandleContentChar('/');
                foreach (var c in tagBuffer.ToString()) await HandleContentChar(c);
                await HandleContentChar(ch);
                ClearTag();
                return;
            }

            HandleTagChar(ch);
            return;
        }

        if (isValueParsing)
        {
            switch (ch)
            {
                case '"':
                    FlashAttributeValue();
                    break;
                default:
                    HandleTagChar(ch);
                    break;
            }

            return;
        }

        switch (ch)
        {
            case ' ':
            case '=':
                FlushTagOrAttributeName();
                break;
            case '/':
                FlushTagOrAttributeName();
                tagMode = currentTagName == null ? 1 : 2;
                break;
            case '>':
                FlushTagOrAttributeName();
                await FlashTag();
                break;
            case '"':
                if (currentTagAttributeName != null)
                    isValueParsing = true;
                break;
            case '!':
                ClearTag();
                ClearEscaping();
                isAnnotation = true;
                break;
            default:
                HandleTagChar(ch);
                break;
        }
    }

    public async Task Feed(string text)
    {
        foreach (char ch in text)
            await Feed(ch);
    }

    public async Task Flush()
    {
        while (tagStack.Count != 0)
        {
            if (TagClosed != null)
                await TagClosed.Invoke();
            tagStack.RemoveAt(tagStack.Count - 1);
        }

        ClearAnnotation();
        ClearEscaping();
        ClearTag();
    }

    public XmlStreamParser(string safeArea = "", params string[] verbatimTags)
    {
        this.safeArea = safeArea;
        this.verbatimTags = new HashSet<string>(verbatimTags.Select(t => t.ToLower()));
    }

    bool IsVerbatimMode => tagStack.Count > 0 && verbatimTags.Contains(tagStack.Last());

    //注释状态
    bool isAnnotation;
    readonly StringBuilder annotationBuffer = new();

    //转义状态
    bool isCharEscaping;
    readonly StringBuilder escapingBuffer = new();

    //解析状态
    bool isTagParsing;
    readonly StringBuilder tagBuffer = new();
    string? currentTagName;
    string? currentTagAttributeName;
    bool isValueParsing;
    readonly Dictionary<string, string> parsedAttributes = new();
    readonly HashSet<string> verbatimTags;

    /// 0：开标签；1：闭标签；2：自闭合标签
    int tagMode;

    readonly List<string> tagStack = new();
    readonly string safeArea;


    async Task HandleContentChar(char ch)
    {
        if (ContentGot != null && tagStack.Contains(safeArea) == false)
            await ContentGot.Invoke(ch);
    }

    void HandleTagChar(char ch)
    {
        tagBuffer.Append(ch);
    }

    async Task FlashEscaping()
    {
        isCharEscaping = false;
        string content = escapingBuffer.ToString();
        escapingBuffer.Clear();

        char? escaping = content switch
        {
            "&#34;" or "&quot;" => '"',
            "&#38;" or "&amp;" => '&',
            "&#60;" or "&lt;" => '<',
            "&#62;" or "&gt;" => '>',
            "&#160;" or "&nbsp;" => ' ',
            _ => null
        };

        if (escaping != null)
        {
            if (isTagParsing)
                HandleTagChar(escaping.Value);
            else
                await HandleContentChar(escaping.Value);
        }
        else
        {
            if (isTagParsing)
            {
                foreach (char ch in content)
                    HandleTagChar(ch);
            }
            else
            {
                foreach (char ch in content)
                    await HandleContentChar(ch);
            }
        }
    }

    string ExtractTagContent()
    {
        string content = tagBuffer.ToString();
        tagBuffer.Clear();
        return content;
    }

    /// <summary>
    /// 仅用于触发名称输入完成
    /// </summary>
    void FlushTagOrAttributeName()
    {
        if (currentTagName == null) //正在解析名称
        {
            if (tagBuffer.Length != 0)
                currentTagName = ExtractTagContent().ToLower();
        }
        else if (currentTagAttributeName == null) //正在解析属性名
        {
            if (tagBuffer.Length != 0)
                currentTagAttributeName = ExtractTagContent().ToLower();
        }
    }

    void FlashAttributeValue()
    {
        if (currentTagAttributeName == null)
            throw new Exception("缺少属性名！请检查调用顺序。");

        string currentTagAttributeValue = ExtractTagContent();
        parsedAttributes[currentTagAttributeName] = currentTagAttributeValue;
        currentTagAttributeName = null;
        isValueParsing = false;
    }

    async Task FlashTag()
    {
        if (currentTagName != null)
        {
            switch (tagMode)
            {
                case 0:
                    if (tagStack.Contains(safeArea) == false)
                    {
                        tagStack.Add(currentTagName);
                        if (TagOpened != null)
                            await TagOpened.Invoke();
                    }

                    break;
                case 1:
                    if (tagStack.Contains(currentTagName) == false)
                    {
                        Error?.Invoke(currentTagName, new Exception($"检测到无效的孤儿闭标签：{currentTagName}"));
                        break; //无效的孤儿闭标签（未触发事件和入栈，直接无视即可）
                    }

                    while (tagStack.Last() != currentTagName)
                    {
                        //移除无效的孤儿开标签（因为入栈且调用过函数，所以要回调）
                        if (TagReset != null)
                            await TagReset.Invoke();
                        tagStack.RemoveAt(tagStack.Count - 1);
                    }

                    if (TagClosed != null)
                        await TagClosed.Invoke();
                    tagStack.RemoveAt(tagStack.Count - 1);
                    break;
                case 2:
                    if (tagStack.Contains(safeArea) == false)
                    {
                        tagStack.Add(currentTagName);
                        if (TagShotted != null && tagStack.Contains(safeArea) == false)
                            await TagShotted.Invoke();
                        tagStack.RemoveAt(tagStack.Count - 1);
                    }

                    break;
            }
        }

        isTagParsing = false;
        currentTagName = null;
        currentTagAttributeName = null;
        if (tagStack.Count == 0) //TODO 缺少正确的Xml参数环境，目前等于不清除，虽然确保闭标签时也能拿到参数，但可能污染其他标签。
            parsedAttributes.Clear();
        tagMode = 0;
    }

    void ClearTag()
    {
        isTagParsing = false;
        tagBuffer.Clear();
        currentTagName = null;
        currentTagAttributeName = null;
        isValueParsing = false;
        parsedAttributes.Clear();
        tagMode = 0;
    }

    void ClearEscaping()
    {
        isCharEscaping = false;
        escapingBuffer.Clear();
    }

    void ClearAnnotation()
    {
        isAnnotation = false;
        annotationBuffer.Clear();
    }
}