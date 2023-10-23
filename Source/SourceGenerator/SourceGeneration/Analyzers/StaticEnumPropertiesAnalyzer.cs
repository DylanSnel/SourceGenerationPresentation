using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SourceGeneration.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StaticEnumPropertiesAnalyzer : DiagnosticAnalyzer
{
    internal const string ErrorId = "ME0001";
    readonly DiagnosticDescriptor _enumPropertiesShouldBeStaticDescriptor = new(
               id: ErrorId,
               title: "MyEnum",
               messageFormat: "EpicEnums: Property '{0}' of type '{1}' should be marked as static",
               category: "MyEnum",
               DiagnosticSeverity.Error,
               isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_enumPropertiesShouldBeStaticDescriptor);

    public override void Initialize(AnalysisContext context)
    {
        //Register our analyzer for the syntax node types we care about
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

        // Check if the enclosing type is a record that inherits from MyEnum<T>
        if (propertyDeclaration.Parent is RecordDeclarationSyntax recordDeclaration &&
            recordDeclaration.BaseList is not null)
        {
            foreach (var baseType in recordDeclaration.BaseList.Types)
            {
                var typeSymbol = context.SemanticModel.GetTypeInfo(baseType.Type).Type as INamedTypeSymbol;

                // Check if the base type is MyEnum<T> where T is the same type as the property
                if (typeSymbol?.ConstructedFrom.Name == "MyEnum" &&
                    typeSymbol.TypeArguments.Length == 1 &&
                    typeSymbol.TypeArguments[0].Name == propertyDeclaration.Type.ToString())
                {
                    // Check if the property is not static
                    if (!propertyDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        // Report the diagnostic
                        var diagnostic = Diagnostic.Create(_enumPropertiesShouldBeStaticDescriptor, propertyDeclaration.GetLocation(), propertyDeclaration.Identifier.Text, propertyDeclaration.Type.ToString());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
