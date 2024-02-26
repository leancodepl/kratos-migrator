using System.Buffers.Binary;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ory.Client.Api;

class Migrator
{
    private readonly IdentityApi identityApi;
    private readonly UserManager<User> userManager;

    private readonly ILogger<Migrator> logger;

    public Migrator(
        IdentityApi identityApi,
        UserManager<User> userManager,
        ILogger<Migrator> logger
    )
    {
        this.identityApi = identityApi;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task RunAsync()
    {
        await foreach (var user in userManager.Users.AsAsyncEnumerable())
        {
            logger.LogInformation(
                "Trying to import identity {Email} with old ID {Id}",
                user.Email,
                user.Id
            );
            var body = new Ory.Client.Model.ClientCreateIdentityBody(
                // The schema that the user will be created with, needs to be adjusted.
                schemaId: "preset://email",
                // This is the mapping of all the user properties from the ASP.NET Core Identity to Kratos traits.
                // It should be adjusted to your needs.
                traits: new Dictionary<string, object> { ["email"] = user.Email ?? "", },
                // This maps user addresses (e-mail) to Kratos verifiable addresses - adjust it to your needs.
                verifiableAddresses:
                [
                    new(
                        via: "email",
                        value: user.Email ?? "",
                        verified: user.EmailConfirmed,
                        status: user.EmailConfirmed ? "completed" : "pending"
                    )
                ],
                metadataAdmin: new { imported_id = user.Id, }
            );
            // Here, we map ASP.NET Core Identity password to Kratos-compatible one.
            if (user.PasswordHash is { } ph)
            {
                body.Credentials = new(password: new(new(ReencodePasswordHash(ph))));
            }

            // Here, one can add OIDC data to `body.Credentials.Oidc` if needed.

            try
            {
                var response = await identityApi.CreateIdentityAsync(body);
                logger.LogInformation(
                    "Imported identity {Id} as user with e-mail {Email}",
                    response.Id,
                    user.Email
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import identity {Email}, skipping", user.Email);
            }
        }
    }

    private static string ReencodePasswordHash(string passwordHash)
    {
        var bytes = Convert.FromBase64String(passwordHash);
        var decoded = bytes[0] switch
        {
            0x00 => DecodeV2(bytes),
            0x01 => DecodeV3(bytes),
            _ => throw new NotSupportedException()
        };
        return Encode(decoded);
    }

    private static string Encode(Password password)
    {
        return $"$pbkdf2-{password.ShaVersion.ToLowerInvariant()}$i={password.IterationCount},l={password.Subkey.Length}${Base64Encode(password.Salt)}${Base64Encode(password.Subkey)}";
    }

    private static Password DecodeV2(byte[] passwordHash)
    {
        var salt = passwordHash[1..17];
        var subkey = passwordHash[17..];
        return new Password(salt, subkey, "sha1", 1000);
    }

    private static Password DecodeV3(byte[] passwordHash)
    {
        var prf = (KeyDerivationPrf)BinaryPrimitives.ReadUInt32BigEndian(passwordHash[1..]);
        var iterCount = BinaryPrimitives.ReadUInt32BigEndian(passwordHash[5..]);
        var saltLength = (int)BinaryPrimitives.ReadUInt32BigEndian(passwordHash[9..]);
        var salt = passwordHash[13..(13 + saltLength)];
        var subkey = passwordHash[(13 + saltLength)..];
        return new Password(salt, subkey, ToName(prf), iterCount);
    }

    private static string Base64Encode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=');
    }

    private static string ToName(KeyDerivationPrf prf)
    {
        return prf switch
        {
            KeyDerivationPrf.HMACSHA1 => "sha1",
            KeyDerivationPrf.HMACSHA256 => "sha256",
            KeyDerivationPrf.HMACSHA512 => "sha512",
            _ => throw new NotSupportedException(),
        };
    }

    private record Password(byte[] Salt, byte[] Subkey, string ShaVersion, uint IterationCount);
}
