using System;
using System.Text.Json;
using Alife.Function.DeskPet;

namespace Alife.DeskPet;

public interface IPetModule : IDisposable
{
    string? CssCode => null;
    string? HtmlCode => null;
    string? JsCode => null;
    bool HandleIpc(IpcCommand cmd) => false;
}
