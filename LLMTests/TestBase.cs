using Common;
using System.Runtime.CompilerServices;

namespace LLMTests;

public abstract class TestBase
{
    protected const string APIKEY = "AQ.Ab8RN6Lr0pUVMpIW9HlA-q25iJbKWg07SExdnrziPrj1FURubQ";
    protected readonly ITestOutputHelper _output;

    /// 호출 설정은 Runner와 공유한다. 테스트와 러너의 점수가 같은 뜻이어야 한다.
    protected readonly ReceiptExtractor _extractor = new(APIKEY);

    public TestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// 벤더 폴더 이름. 하위 클래스가 지정한다. ("Starbucks" -> LLMTests/Starbucks/)
    protected abstract string VendorName { get; }

    /// 그 벤더의 프롬프트·정답·영수증 이미지가 전부 이 폴더 아래에 있다.
    protected Vendor Data => Of(VendorName);

    /// [MemberData]로 케이스를 뽑을 때 쓴다. 정답 파일을 추가하면 테스트가 저절로 늘어난다.
    public static Vendor Of(string vendorName) => new(Path.Combine(ProjectDir(), vendorName));

    /// 응답 원본과 차이 목록을 출력 폴더에 남긴다. 프롬프트를 고칠 때 이 파일들만 보면 된다.
    protected string Save(string receiptId, string actual, ReceiptDiff.Report report)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Diffs", VendorName);
        Directory.CreateDirectory(dir);

        System.IO.File.WriteAllText(Path.Combine(dir, $"{receiptId}.actual.json"), actual);

        string diffPath = Path.Combine(dir, $"{receiptId}.diff.txt");
        string body = report.ToString();
        if (body.Length == 0) System.IO.File.Delete(diffPath);
        else System.IO.File.WriteAllText(diffPath, body);

        return dir;
    }

    /// 프롬프트·정답·이미지를 모두 출력 폴더 복사본이 아니라 소스 폴더에서 읽는다.
    private static string ProjectDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
