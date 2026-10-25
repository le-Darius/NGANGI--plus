namespace Ngangi.Core.Services;

public sealed class OperationResult
{
    public List<string> Errors { get; } = new();
    public int? Id { get; set; }
    public bool Ok => Errors.Count == 0;
    public string Message => string.Join("\n", Errors);

    public static OperationResult Success(int? id = null) => new() { Id = id };

    public static OperationResult Fail(params string[] errors)
    {
        var r = new OperationResult();
        r.Errors.AddRange(errors);
        return r;
    }
}
