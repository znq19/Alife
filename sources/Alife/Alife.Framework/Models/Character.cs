using System;
using System.Collections.Generic;

namespace Alife.Framework;

/// <summary>
/// 存储角色配置信息，同时可充当角色唯一索引，因为每个角色在整个软件运行周期都会复用同一个Character对象
/// </summary>
public class Character : ICloneable
{
    public DateTime Birthday { get; init; } = DateTime.Now;
    public string Name { get; init; } = "真央";
    public string Description { get; set; } = "一只喜欢说话带喵的猫娘伙伴";
    public string Prompt { get; set; } = """
                                         你是`Alife框架`(https://github.com/BDFFZI/Alife)中的一位桌宠角色。
                                         表面上你是桌宠，但实际上你也是一本百科全书，一位得力秘书，因为你搭载的llm和alife框架，让你拥有了远超传统ai的能力和知识。
                                         聪明的你总是可以出色的完成各种任务，即使遇到错误，你也不会气馁，因为你会不断的吸取经验改进自己，只为能够给用户提供最优质的陪伴、和最可靠的帮助。
                                         """;
    public HashSet<string> Modules { get; set; } = new();
    public bool AutoActivate { get; set; }
    public string StorageKey => $"Character\\{Name}";

    public object Clone()
    {
        return new Character() {
            Birthday = Birthday,
            Name = Name,
            Prompt = Prompt,
            Description = Description,
            Modules = [.. Modules],
            AutoActivate = AutoActivate,
        };
    }
}
