using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Type technique exigé par le compilateur pour les accesseurs <c>init</c> et les
/// <c>record</c>. Il est fourni par .NET 5 et suivants, mais absent de netstandard2.0
/// — cible imposée aux analyseurs Roslyn, qui doivent pouvoir être chargés par un
/// compilateur tournant sur .NET Framework.
///
/// Ce fichier est le complément standard de cette situation ; il ne contient aucune
/// logique.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
