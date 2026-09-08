namespace Common;

/// <summary>
/// Gemini 키. 개인 프로젝트라 환경변수 대신 소스에 둔다.
/// <para>
/// 테스트와 Runner가 같은 키를 쓰도록 한 곳에만 적는다. 키를 바꿀 일이 생기면 이 줄만 고친다.
/// 저장소가 공개되면 이 키도 같이 공개된다는 점은 알고 쓴다.
/// </para>
/// </summary>
public static class ApiKey
{
    public const string Gemini = "AQ.Ab8RN6Lr0pUVMpIW9HlA-q25iJbKWg07SExdnrziPrj1FURubQ";
}
