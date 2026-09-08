using Common;

namespace LLMTests;

/// 벤더 폴더에서 케이스를 읽는 규칙을 고정한다.
public class VendorTests : IDisposable
{
    readonly string _dir;

    public VendorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "VendorTests", Path.GetRandomFileName(), "Starbucks");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_dir)!, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void 정답이_있는_영수증만_케이스가_된다()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "A"));
        Directory.CreateDirectory(Path.Combine(_dir, "B"));   // 정답이 없다
        File.WriteAllText(Path.Combine(_dir, "A", "answer.json"), "{}");

        Assert.Equal(["A"], new Vendor(_dir).Cases());
    }

    [Fact]
    public void 케이스_폴더가_없으면_빈_목록이다()
    {
        Assert.Empty(new Vendor(_dir).Cases());
    }

    [Fact]
    public void 실제_벤더의_프롬프트와_케이스를_찾는다()
    {
        var vendor = TestBase.Of("Starbucks");

        Assert.True(File.Exists(vendor.SystemPromptPath), vendor.SystemPromptPath);
        Assert.True(File.Exists(vendor.UserPromptPath), vendor.UserPromptPath);
        Assert.Equal(22, vendor.Cases().Count);
    }
}
