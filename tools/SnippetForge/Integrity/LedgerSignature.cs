using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SnippetForge.Integrity;

/// <summary>Résultat d'une vérification de signature.</summary>
public enum SignatureState
{
    /// <summary>Aucune clé publique déclarée : la signature n'est pas exigée.</summary>
    NotConfigured = 0,

    /// <summary>Clé publique déclarée, mais le registre n'est pas signé.</summary>
    Missing = 1,

    /// <summary>Signature présente mais invalide : le registre a été modifié.</summary>
    Invalid = 2,

    /// <summary>Signature valide.</summary>
    Valid = 3,
}

/// <summary>
/// Signature du registre d'empreintes par une paire de clés RSA.
///
/// **Ce que cela apporte.** Le registre d'empreintes seul détecte l'accident et le
/// dépôt manuel, mais pas un adversaire : qui peut réécrire un artefact peut aussi
/// réécrire l'empreinte correspondante. La signature ferme cette porte — falsifier
/// l'ensemble exige désormais la clé privée.
///
/// **Ce que cela n'apporte pas.** Ce n'est pas une signature NuGet reconnue par
/// l'écosystème : celle-là exige un certificat délivré par une autorité, et se
/// vérifie par <c>dotnet nuget verify</c> chez tous les consommateurs. Ici la
/// confiance repose sur une clé que vous gérez vous-même, vérifiée par cet outil
/// seul. C'est adapté à un feed d'équipe, pas à une distribution publique.
///
/// La clé privée n'est **jamais** stockée dans la bibliothèque : son emplacement est
/// désigné par une variable d'environnement.
/// </summary>
public static class LedgerSignature
{
    /// <summary>Variable d'environnement désignant le fichier de clé privée.</summary>
    public const string PrivateKeyVariable = "MICROFORGE_SIGNING_KEY";

    /// <summary>Nom du fichier de clé publique, à la racine de la bibliothèque.</summary>
    public const string PublicKeyFileName = "signing-key.pub";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Chemin du fichier de signature.</summary>
    public static string SignaturePath(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Path.Combine(root.RegistryDir, "artifacts.sig");
    }

    /// <summary>Chemin de la clé publique.</summary>
    public static string PublicKeyPath(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Path.Combine(root.Path, PublicKeyFileName);
    }

    /// <summary>
    /// Crée une paire de clés : la publique dans la bibliothèque, la privée à
    /// l'emplacement demandé — hors du dépôt, et à protéger comme un secret.
    /// </summary>
    /// <returns>Le chemin de la clé privée écrite.</returns>
    /// <exception cref="InvalidOperationException">Si une clé publique existe déjà.</exception>
    public static string CreateKeyPair(ForgeRoot root, string privateKeyPath, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);

        if (File.Exists(PublicKeyPath(root)) && !overwrite)
        {
            throw new InvalidOperationException(
                "Une clé publique existe déjà. Remplacer la paire invaliderait toutes les " +
                "signatures antérieures : utiliser --force en connaissance de cause.");
        }

        using var rsa = RSA.Create(3072);

        var directory = Path.GetDirectoryName(Path.GetFullPath(privateKeyPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(PublicKeyPath(root), rsa.ExportRSAPublicKeyPem());

        return Path.GetFullPath(privateKeyPath);
    }

    /// <summary>
    /// Signe le contenu du registre d'empreintes. La clé privée est lue depuis le
    /// fichier désigné par <see cref="PrivateKeyVariable"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la clé privée est introuvable.</exception>
    public static void Sign(ForgeRoot root, ArtifactLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(ledger);

        var keyPath = Environment.GetEnvironmentVariable(PrivateKeyVariable);
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            throw new InvalidOperationException(
                $"Clé privée introuvable. Définir {PrivateKeyVariable} vers le fichier de clé " +
                "(créé par « forge sign --init <chemin> »).");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));

        var payload = Canonical(ledger);
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Directory.CreateDirectory(root.RegistryDir);
        File.WriteAllText(
            SignaturePath(root),
            JsonSerializer.Serialize(
                new SignatureDocument("RSA-3072/SHA-256", Convert.ToBase64String(signature), DateTime.UtcNow),
                JsonOptions));
    }

    /// <summary>Vérifie la signature du registre d'empreintes.</summary>
    public static SignatureState Verify(ForgeRoot root, ArtifactLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(ledger);

        var publicKeyPath = PublicKeyPath(root);
        if (!File.Exists(publicKeyPath))
        {
            return SignatureState.NotConfigured;
        }

        var signaturePath = SignaturePath(root);
        if (!File.Exists(signaturePath))
        {
            return SignatureState.Missing;
        }

        try
        {
            var document = JsonSerializer.Deserialize<SignatureDocument>(File.ReadAllText(signaturePath), JsonOptions);
            if (document is null || string.IsNullOrWhiteSpace(document.Signature))
            {
                return SignatureState.Invalid;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(publicKeyPath));

            return rsa.VerifyData(
                Canonical(ledger),
                Convert.FromBase64String(document.Signature),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)
                ? SignatureState.Valid
                : SignatureState.Invalid;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or CryptographicException)
        {
            return SignatureState.Invalid;
        }
    }

    /// <summary>Explication lisible d'un état de signature.</summary>
    public static string Describe(SignatureState state) => state switch
    {
        SignatureState.Valid => "Signature valide : le registre d'empreintes n'a pas été modifié.",
        SignatureState.Missing =>
            "Une clé publique est déclarée mais le registre n'est pas signé. " +
            "Signer avec « forge sign », ou retirer la clé publique si la signature n'est plus voulue.",
        SignatureState.Invalid =>
            "Signature invalide : le registre d'empreintes a été modifié depuis sa signature. " +
            "Restaurer depuis une sauvegarde, ou resigner après avoir vérifié l'origine des artefacts.",
        _ => "Aucune signature configurée. Les empreintes seules détectent l'accident, pas un adversaire.",
    };

    /// <summary>
    /// Représentation canonique du registre : les empreintes triées, une par ligne.
    /// L'ordre du dictionnaire ne doit pas influer sur la signature.
    /// </summary>
    private static byte[] Canonical(ArtifactLedger ledger) =>
        Encoding.UTF8.GetBytes(string.Join('\n', ledger.Hashes
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}:{kv.Value}")));

    private sealed record SignatureDocument(string Algorithm, string Signature, DateTime SignedUtc);
}
