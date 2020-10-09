using Chaincase.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Chaincase
{
    public static class XamarinExtensions
    {
        public static void ConfigureCommonXamarinServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IHsmStorage, XamarinHsmStorage>();
        }
    }
}