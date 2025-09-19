using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace ClearSkies.Tests
{
    public static class TestServiceSetup
    {
        public static void ConfigureTestServices(IServiceCollection services)
        {
            // Remove all existing IMemoryCache registrations
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMemoryCache));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add a singleton MemoryCache with TrackStatistics = true
            var shared = new MemoryCache(new MemoryCacheOptions { TrackStatistics = true });
            services.AddSingleton<IMemoryCache>(shared);

            // Add IHttpContextAccessor
            services.AddHttpContextAccessor();
        }
    }
}
