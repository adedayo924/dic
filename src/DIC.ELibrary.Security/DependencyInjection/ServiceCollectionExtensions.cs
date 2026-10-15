using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Security.Cryptography;
using DIC.ELibrary.Security.Keys;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.Security.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibrarySecurity(this IServiceCollection services)
    {
        services.AddSingleton<ProtectedKeyStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IContentCipher, AesGcmContentCipher>();
        services.AddSingleton<IPackageSigner, RsaPackageSigner>();
        services.AddSingleton<IKeyManager, RsaKeyManager>();
        return services;
    }
}
