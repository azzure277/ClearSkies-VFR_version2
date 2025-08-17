namespace ClearSkies.Domain.Diagnostics
{
    /// <summary>
    /// Simple scoped stamp for recording cache result (HIT or MISS).
    /// </summary>
    public interface ICacheStamp
    {
        string? Result { get; set; }
    }

    public sealed class CacheStamp : ICacheStamp
    {
        public string? Result { get; set; }
    }
}
