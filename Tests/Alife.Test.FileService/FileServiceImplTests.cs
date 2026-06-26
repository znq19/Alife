using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alife.Function.FileService;

namespace Alife.Test.FileService;

[TestFixture]
public class FileServiceImplTests
{
    string tempDir = null!;
    FileServiceImpl impl = null!;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"alife_test_fs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        impl = new FileServiceImpl { TempFolderPath = tempDir };
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(tempDir, true); } catch { }
    }

    string WriteTestFile(string name, string content)
    {
        string path = Path.Combine(tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Test]
    public async Task ReadAsync_Directory_ReturnsEntries()
    {
        string subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        WriteTestFile("a.txt", "");
        WriteTestFile("sub/b.txt", "");

        FileReadResult result = await impl.ReadAsync(tempDir, null, null);

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Content, Does.Contain("a.txt"));
        Assert.That(result.Content, Does.Contain("sub/"));
    }

    [Test]
    public async Task ReadAsync_FileExists_ReturnsContent()
    {
        string path = WriteTestFile("test.txt", "line1\nline2\nline3");

        FileReadResult result = await impl.ReadAsync(path, null, null);

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Content, Does.Contain("line2"));
        Assert.That(result.TempFilePath, Is.Null);
    }

    [Test]
    public async Task ReadAsync_FileNotExists_ReturnsError()
    {
        FileReadResult result = await impl.ReadAsync("nonexistent.txt", null, null);

        Assert.That(result.Error, Is.EqualTo("文件不存在"));
    }

    [Test]
    public async Task ReadAsync_WithOffset_StartsFromCorrectLine()
    {
        string path = WriteTestFile("test.txt", "line1\nline2\nline3\nline4");

        FileReadResult result = await impl.ReadAsync(path, 3, null);

        Assert.That(result.Content, Does.StartWith("3: line3"));
        Assert.That(result.Content, Does.Not.Contain("1: line1"));
    }

    [Test]
    public async Task ReadAsync_OffsetExceedsLength_ReturnsError()
    {
        string path = WriteTestFile("test.txt", "line1");

        FileReadResult result = await impl.ReadAsync(path, 10, null);

        Assert.That(result.Error, Is.EqualTo("起始行号超出文件范围"));
    }

    [Test]
    public async Task ReadAsync_LargeContent_WritesTempFile()
    {
        string large = new('x', 60000);
        string path = WriteTestFile("large.txt", large);

        FileReadResult result = await impl.ReadAsync(path, null, null);

        Assert.That(result.TempFilePath, Is.Not.Null);
        Assert.That(File.Exists(result.TempFilePath), Is.True);
    }

    [Test]
    public async Task WriteAsync_CreatesFile()
    {
        string path = Path.Combine(tempDir, "new.txt");

        await impl.WriteAsync(path, "hello world");

        Assert.That(File.Exists(path), Is.True);
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("hello world"));
    }

    [Test]
    public async Task WriteAsync_CreatesDirectory()
    {
        string path = Path.Combine(tempDir, "sub", "nested.txt");

        await impl.WriteAsync(path, "test");

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task EditAsync_ReplacesText()
    {
        string path = WriteTestFile("edit.txt", "hello world foo");

        await impl.EditAsync(path, "foo", "bar", false);

        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("hello world bar"));
    }

    [Test]
    public async Task EditAsync_ReplaceAll_ReplacesAll()
    {
        string path = WriteTestFile("edit.txt", "foo hello foo world foo");

        await impl.EditAsync(path, "foo", "bar", true);

        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("bar hello bar world bar"));
    }

    [Test]
    public void EditAsync_FileNotExists_Throws()
    {
        Assert.That(async () => await impl.EditAsync("nope.txt", "a", "b", false),
            Throws.InstanceOf<FileNotFoundException>());
    }

    [Test]
    public void EditAsync_NotFound_Throws()
    {
        string path = WriteTestFile("edit.txt", "hello");

        Assert.That(async () => await impl.EditAsync(path, "xyz", "b", false),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void EditAsync_MultipleMatches_Throws()
    {
        string path = WriteTestFile("edit.txt", "foo foo foo");

        Assert.That(async () => await impl.EditAsync(path, "foo", "bar", false),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Glob_ReturnsMatchingFiles()
    {
        WriteTestFile("a.cs", "");
        WriteTestFile("b.cs", "");
        WriteTestFile("c.txt", "");
        Directory.CreateDirectory(Path.Combine(tempDir, "sub"));
        File.WriteAllText(Path.Combine(tempDir, "sub", "d.cs"), "");

        string[] files = impl.Glob(tempDir, "**/*.cs");

        Assert.That(files.Length, Is.EqualTo(3));
        Assert.That(files, Has.Member("a.cs"));
        Assert.That(files, Has.Member("b.cs"));
        Assert.That(files, Has.Member(Path.Combine("sub", "d.cs")));
    }

    [Test]
    public void Glob_ReturnsDirectories()
    {
        Directory.CreateDirectory(Path.Combine(tempDir, "PluginsA"));
        Directory.CreateDirectory(Path.Combine(tempDir, "PluginsB"));
        WriteTestFile("readme.txt", "");

        string[] files = impl.Glob(tempDir, "*");

        Assert.That(files, Does.Contain("PluginsA/"));
        Assert.That(files, Does.Contain("PluginsB/"));
        Assert.That(files, Does.Contain("readme.txt"));
        Assert.That(files.Length, Is.EqualTo(3));
    }

    [Test]
    public void Glob_NoMatch_ReturnsEmpty()
    {
        string[] files = impl.Glob(tempDir, "*.xyz");

        Assert.That(files, Is.Empty);
    }

    [Test]
    public void Glob_DirectoryNotExists_Throws()
    {
        Assert.That(() => impl.Glob("nonexistent", "*"), Throws.InstanceOf<DirectoryNotFoundException>());
    }

    [Test]
    public void Grep_ReturnsMatches()
    {
        WriteTestFile("a.txt", "hello world\nfoo bar");
        WriteTestFile("b.txt", "goodbye world\nnothing");

        GrepResult result = impl.Grep(tempDir, "world");

        Assert.That(result.Matches.Count, Is.EqualTo(2));
        Assert.That(result.Matches[0], Does.Contain("hello world"));
        Assert.That(result.Matches[1], Does.Contain("goodbye world"));
        Assert.That(result.Truncated, Is.False);
    }

    [Test]
    public void Grep_NoMatch_ReturnsEmpty()
    {
        WriteTestFile("a.txt", "hello");

        GrepResult result = impl.Grep(tempDir, "xyz");

        Assert.That(result.Matches, Is.Empty);
    }

    [Test]
    public void Grep_WithFilter_OnlySearchesMatchingFiles()
    {
        WriteTestFile("a.cs", "hello world");
        WriteTestFile("a.txt", "hello world");

        GrepResult result = impl.Grep(tempDir, "world", "*.cs");

        Assert.That(result.Matches.Count, Is.EqualTo(1));
        Assert.That(result.Matches[0], Does.StartWith("a.cs"));
    }

    [Test]
    public void Grep_Truncated_WhenExceedsLimit()
    {
        // 每个文件60行，10个文件 = 600行 > 500 触发截断
        for (int i = 0; i < 10; i++)
        {
            string content = string.Join("\n", Enumerable.Range(0, 60).Select(j => $"match line {j}"));
            WriteTestFile($"file{i}.txt", content);
        }

        GrepResult result = impl.Grep(tempDir, "match");

        Assert.That(result.Truncated, Is.True);
        Assert.That(result.Matches.Count, Is.EqualTo(FileServiceImpl.MaxGrepResults));
    }

    [Test]
    public void Grep_DirectoryNotExists_Throws()
    {
        Assert.That(() => impl.Grep("nonexistent", "pattern"), Throws.InstanceOf<DirectoryNotFoundException>());
    }
}
