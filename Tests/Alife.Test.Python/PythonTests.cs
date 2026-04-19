using Alife.Basic;
using Alife.Implement;

namespace Alife.Test.Python;

public class PythonTests
{
    [SetUp]
    public void Setup() { }

    [Test]
    public async Task TestExecutePython()
    {
        string filePath = $"{AlifePath.StorageFolderPath}/pythonScript.py";
        Console.WriteLine(await PythonService.Python(filePath));
    }
}
