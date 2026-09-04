using Common;

namespace LLMTests;

/// <summary>
/// 검산이 실제로 성립하는지 정답 22건으로 확인한다.
/// 정답이 전부 통과해야 이 검산을 운영 응답에 쓸 수 있다. API를 호출하지 않는다.
/// </summary>
public class ReceiptCheckTests
{
    public static TheoryData<string> Answers => [.. TestBase.Of("Starbucks").Cases()];

    [Theory]
    [MemberData(nameof(Answers))]
    public void 정답은_전부_산수가_맞는다(string receiptId)
    {
        string json = File.ReadAllText(TestBase.Of("Starbucks").AnswerPath(receiptId));

        var problems = ReceiptCheck.Verify(json);

        Assert.True(problems.Count == 0, $"{receiptId}{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void 상품_금액이_모자라면_잡아낸다()
    {
        // P0042412의 실제 실패: 44,850 자리에 몫 10,850만 들어가 34,000이 빈다.
        string json = File.ReadAllText(TestBase.Of("Starbucks").AnswerPath("260517-164718136-P0042412"))
            .Replace("\"productPrice\": 44850", "\"productPrice\": 10850");

        var problems = ReceiptCheck.Verify(json);

        Assert.Contains(problems, p => p.Contains("결제금액이 항목과 안 맞음"));
        Assert.Contains(problems, p => p.Contains("34000"));
    }

    [Fact]
    public void 쿠폰을_빠뜨리면_결제금액에서_잡힌다()
    {
        string json = File.ReadAllText(TestBase.Of("Starbucks").AnswerPath("260517-224843885-H0120915"))
            .Replace("\"optionPrice\": -3000", "\"optionPrice\": 0");

        var problems = ReceiptCheck.Verify(json);

        Assert.Contains(problems, p => p.Contains("결제금액이 항목과 안 맞음"));
    }

    [Fact]
    public void 결제수단_합이_어긋나면_잡아낸다()
    {
        string json = File.ReadAllText(TestBase.Of("Starbucks").AnswerPath("22350"))
            .Replace("\"paymentPrice\": 6500", "\"paymentPrice\": 6400");

        var problems = ReceiptCheck.Verify(json);

        Assert.Contains(problems, p => p.Contains("결제수단 합이 안 맞음"));
    }

    [Fact]
    public void 깨진_JSON은_파싱_실패로_보고한다()
    {
        Assert.Contains(ReceiptCheck.Verify("{ \"totalOrderPrice\": "), p => p.Contains("파싱 실패"));
    }
}
