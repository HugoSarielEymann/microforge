using System.Text;

namespace Micro.Text.TokenTemplate;

/// <summary>Ce qu'a produit le rendu d'un gabarit.</summary>
/// <param name="Text">Le texte rendu.</param>
/// <param name="UsedTokens">Noms de jetons effectivement remplacés, sans doublon, dans l'ordre de rencontre.</param>
/// <param name="MissingTokens">Noms de jetons présents au gabarit mais absents du dictionnaire.</param>
public sealed record TokenTemplateResult(
    string Text,
    IReadOnlyList<string> UsedTokens,
    IReadOnlyList<string> MissingTokens)
{
    /// <summary>Tous les jetons du gabarit ont trouvé leur valeur.</summary>
    public bool IsComplete => MissingTokens.Count == 0;
}

/// <summary>
/// Remplace les jetons nommés d'un gabarit par les valeurs d'un dictionnaire.
/// </summary>
/// <remarks>
/// Fonction pure et déterministe. Le rendu dit aussi <em>ce qu'il a consommé</em> : c'est ce qui
/// permet à l'appelant de savoir quelles valeurs ont déjà servi et quelles autres restent à
/// placer ailleurs — une adresse qui absorbe deux champs et un corps de requête qui reçoit le
/// reste, par exemple.
/// </remarks>
public static class TokenTemplate
{
    /// <summary>Rend un gabarit.</summary>
    /// <param name="template">Gabarit ; nul ou vide rend un texte vide sans échouer.</param>
    /// <param name="values">Valeurs par nom de jeton.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le texte rendu, les jetons consommés et les jetons manquants.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="values"/> est nul.</exception>
    /// <exception cref="ArgumentException">Si les délimiteurs sont invalides.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si la longueur maximale de jeton est invalide.</exception>
    /// <exception cref="FormatException">
    /// Si un jeton est absent du dictionnaire et que les réglages exigent l'échec.
    /// </exception>
    public static TokenTemplateResult Render(
        string? template,
        IReadOnlyDictionary<string, string> values,
        TokenTemplateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        TokenTemplateOptions settings = options ?? TokenTemplateOptions.Default;
        settings.Validate();

        if (string.IsNullOrEmpty(template))
        {
            return new TokenTemplateResult(string.Empty, [], []);
        }

        StringBuilder rendu = new(template.Length);
        List<string> consommes = [];
        List<string> manquants = [];
        HashSet<string> vus = new(settings.NameComparer);
        HashSet<string> signales = new(settings.NameComparer);

        int position = 0;

        while (position < template.Length)
        {
            int ouverture = template.IndexOf(settings.Open, position, StringComparison.Ordinal);

            if (ouverture < 0)
            {
                rendu.Append(template, position, template.Length - position);
                break;
            }

            rendu.Append(template, position, ouverture - position);
            position = ouverture;

            // Délimiteur doublé : on écrit un délimiteur littéral et on avance.
            if (settings.AllowEscape && IsAt(template, ouverture + settings.Open.Length, settings.Open))
            {
                rendu.Append(settings.Open);
                position = ouverture + (settings.Open.Length * 2);
                continue;
            }

            int debutNom = ouverture + settings.Open.Length;
            int fermeture = template.IndexOf(settings.Close, debutNom, StringComparison.Ordinal);
            int longueurNom = fermeture - debutNom;

            // Ni fermeture, ni nom plausible : le délimiteur n'ouvrait rien, il est littéral.
            if (fermeture < 0 || longueurNom <= 0 || longueurNom > settings.MaxTokenLength)
            {
                rendu.Append(settings.Open);
                position = debutNom;
                continue;
            }

            string nom = template.Substring(debutNom, longueurNom);
            position = fermeture + settings.Close.Length;

            if (values.TryGetValue(nom, out string? valeur))
            {
                string insere = valeur ?? string.Empty;
                rendu.Append(settings.Transform is null ? insere : settings.Transform(insere));

                if (vus.Add(nom))
                {
                    consommes.Add(nom);
                }

                continue;
            }

            if (signales.Add(nom))
            {
                manquants.Add(nom);
            }

            switch (settings.OnMissing)
            {
                case MissingTokenBehavior.Fail:
                    throw new FormatException($"Le gabarit demande « {nom} », qui n'a pas de valeur.");

                case MissingTokenBehavior.Blank:
                    break;

                default:
                    rendu.Append(settings.Open).Append(nom).Append(settings.Close);
                    break;
            }
        }

        return new TokenTemplateResult(rendu.ToString(), consommes, manquants);
    }

    /// <summary>Relève les noms de jetons d'un gabarit, sans rien remplacer.</summary>
    /// <param name="template">Gabarit ; nul ou vide rend une liste vide.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les noms rencontrés, sans doublon, dans l'ordre.</returns>
    /// <exception cref="ArgumentException">Si les délimiteurs sont invalides.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si la longueur maximale de jeton est invalide.</exception>
    /// <remarks>
    /// Sert à savoir ce qu'un gabarit réclame avant de disposer des valeurs : proposer les cases
    /// à remplir, ou vérifier une configuration au démarrage plutôt qu'au premier appel.
    /// </remarks>
    public static IReadOnlyList<string> FindTokens(string? template, TokenTemplateOptions? options = null)
    {
        TokenTemplateOptions settings = options ?? TokenTemplateOptions.Default;
        settings.Validate();

        // Un dictionnaire vide avec « laisser tel quel » ne remplace rien : tous les jetons
        // ressortent alors comme manquants, ce qui est exactement l'inventaire cherché.
        TokenTemplateOptions inventaire = new()
        {
            Open = settings.Open,
            Close = settings.Close,
            AllowEscape = settings.AllowEscape,
            NameComparer = settings.NameComparer,
            MaxTokenLength = settings.MaxTokenLength,
            OnMissing = MissingTokenBehavior.Leave,
        };

        return Render(template, new Dictionary<string, string>(settings.NameComparer), inventaire).MissingTokens;
    }

    private static bool IsAt(string text, int index, string fragment)
        => index + fragment.Length <= text.Length
           && string.CompareOrdinal(text, index, fragment, 0, fragment.Length) == 0;
}
