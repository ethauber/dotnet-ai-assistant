using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Infrastructure.Services;

/// <summary>
/// Extracts a compact structural summary from a C# source file using Roslyn syntax analysis.
/// Produces type declarations, base types, property signatures, and method signatures
/// without method bodies — high signal, low token cost.
/// </summary>
internal static class RoslynAstExtractor
{
    /// <summary>
    /// Parses <paramref name="source"/> and returns a structured summary suitable for
    /// injecting into an LLM context window.
    /// Returns an empty string if the file contains no type declarations.
    /// </summary>
    public static string ExtractSummary(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        var sb = new System.Text.StringBuilder();

        // Walk top-level and namespace-scoped type declarations.
        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            // Skip nested types (they'll appear under their parent's output naturally).
            if (typeDecl.Parent is BaseTypeDeclarationSyntax)
                continue;

            var ns = GetNamespace(typeDecl);
            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine($"namespace {ns}");

            sb.AppendLine(TypeHeader(typeDecl));

            IEnumerable<MemberDeclarationSyntax> members = typeDecl switch
            {
                TypeDeclarationSyntax t => t.Members,
                EnumDeclarationSyntax e => e.Members,
                _ => [],
            };

            foreach (var member in members)
            {
                switch (member)
                {
                    case PropertyDeclarationSyntax prop:
                        sb.AppendLine(
                            $"  {Accessibility(prop.Modifiers)} {prop.Type} {prop.Identifier.Text}{AccessorSummary(prop.AccessorList)}"
                        );
                        break;

                    case MethodDeclarationSyntax method:
                        sb.AppendLine(
                            $"  {Accessibility(method.Modifiers)} {method.ReturnType} {method.Identifier.Text}{method.TypeParameterList}({ParameterList(method.ParameterList)})"
                        );
                        break;

                    case ConstructorDeclarationSyntax ctor:
                        sb.AppendLine(
                            $"  ctor {ctor.Identifier.Text}({ParameterList(ctor.ParameterList)})"
                        );
                        break;

                    case FieldDeclarationSyntax field
                        when field.Modifiers.Any(m =>
                            m.IsKind(SyntaxKind.PublicKeyword)
                            || m.IsKind(SyntaxKind.InternalKeyword)
                        ):
                        foreach (var variable in field.Declaration.Variables)
                            sb.AppendLine(
                                $"  {Accessibility(field.Modifiers)} {field.Declaration.Type} {variable.Identifier.Text}"
                            );
                        break;

                    case EnumMemberDeclarationSyntax enumMember:
                        sb.AppendLine($"  {enumMember.Identifier.Text}");
                        break;
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetNamespace(SyntaxNode node)
    {
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return ns?.Name.ToString() ?? string.Empty;
    }

    private static string TypeHeader(BaseTypeDeclarationSyntax typeDecl)
    {
        var keyword = typeDecl switch
        {
            ClassDeclarationSyntax => "class",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                ? "record struct"
                : "record",
            EnumDeclarationSyntax => "enum",
            _ => "type",
        };

        var modifiers = Accessibility(typeDecl.Modifiers);
        var name = typeDecl is RecordDeclarationSyntax rec
            ? $"{typeDecl.Identifier.Text}{rec.ParameterList}"
            : typeDecl.Identifier.Text;

        if (
            typeDecl is TypeDeclarationSyntax typeSyntax
            && typeSyntax.TypeParameterList is not null
        )
            name += typeSyntax.TypeParameterList.ToString();

        var bases = typeDecl.BaseList?.Types.Select(t => t.ToString()).ToList() ?? [];
        var baseClause = bases.Count > 0 ? $" : {string.Join(", ", bases)}" : string.Empty;

        return $"{modifiers} {keyword} {name}{baseClause}".TrimStart();
    }

    private static string Accessibility(SyntaxTokenList modifiers)
    {
        var parts = modifiers
            .Where(m =>
                m.IsKind(SyntaxKind.PublicKeyword)
                || m.IsKind(SyntaxKind.PrivateKeyword)
                || m.IsKind(SyntaxKind.ProtectedKeyword)
                || m.IsKind(SyntaxKind.InternalKeyword)
                || m.IsKind(SyntaxKind.StaticKeyword)
                || m.IsKind(SyntaxKind.AbstractKeyword)
                || m.IsKind(SyntaxKind.SealedKeyword)
                || m.IsKind(SyntaxKind.OverrideKeyword)
                || m.IsKind(SyntaxKind.VirtualKeyword)
                || m.IsKind(SyntaxKind.AsyncKeyword)
            )
            .Select(m => m.Text);
        return string.Join(" ", parts);
    }

    private static string AccessorSummary(AccessorListSyntax? accessorList)
    {
        if (accessorList is null)
            return string.Empty;
        var accessors = accessorList.Accessors.Select(a => a.Keyword.Text);
        return $" {{ {string.Join("; ", accessors)}; }}";
    }

    private static string ParameterList(ParameterListSyntax? parameterList)
    {
        if (parameterList is null)
            return string.Empty;
        return string.Join(
            ", ",
            parameterList.Parameters.Select(p =>
                p.Modifiers.Any()
                    ? $"{string.Join(" ", p.Modifiers.Select(m => m.Text))} {p.Type} {p.Identifier.Text}"
                    : $"{p.Type} {p.Identifier.Text}"
            )
        );
    }
}
