using System.Security.Cryptography;
using System.Text;

namespace ClearSkies.Api.Http
{
    public interface IEtagService
    {
        string ComputeWeak(string input);
    }

    public sealed class EtagService : IEtagService
    {
        public string ComputeWeak(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
            var hex = Convert.ToHexString(bytes);
            return $"W/\"{hex}\""; // RFC: weak ETag
        }
    }
}
