namespace GtrackWeb.Helpers;

/// <summary>
/// Tiny fluent builder for SQL parameter dictionaries so service code reads
/// cleanly, e.g. <c>Params.New("buyerId", id).Add("sign", 1)</c>.
/// </summary>
public sealed class Params : Dictionary<string, object?>
{
    public static Params New(string name, object? value) => new Params().Add(name, value);

    public new Params Add(string name, object? value)
    {
        base[name] = value;
        return this;
    }
}
