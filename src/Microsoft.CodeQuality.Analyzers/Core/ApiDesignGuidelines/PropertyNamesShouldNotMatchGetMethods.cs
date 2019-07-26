// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1721: Property names should not match get methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PropertyNamesShouldNotMatchGetMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1721";

        private const string Get = "Get";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1721-property-names-should-not-match-get-methods",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Analyze properties, methods 
            analysisContext.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            string identifier;
            var symbol = context.Symbol;

            // Bail out if the method/property is not exposed (public, protected, or protected internal) by default
            var configuredVisibilities = context.Options.GetSymbolVisibilityGroupOption(Rule, SymbolVisibilityGroup.Public, context.CancellationToken);
            if (!configuredVisibilities.Contains(symbol.GetResultantVisibility()))
            {
                return;
            }

            if (symbol.Kind == SymbolKind.Property)
            {
                // Want to look for methods named the same as the property but with a 'Get' prefix
                identifier = Get + symbol.Name;
            }
            else if (symbol.Kind == SymbolKind.Method && symbol.Name.StartsWith(Get, StringComparison.Ordinal))
            {
                // Want to look for properties named the same as the method sans 'Get'
                identifier = symbol.Name.Substring(3);
            }
            else
            {
                // Exit if the method name doesn't start with 'Get'
                return;
            }

            // Iterate through all declared types, including base
            foreach (INamedTypeSymbol type in symbol.ContainingType.GetBaseTypesAndThis())
            {
                Diagnostic diagnostic = null;

                var exposedMembers = type.GetMembers(identifier).Where(member => configuredVisibilities.Contains(member.GetResultantVisibility()));
                foreach (var member in exposedMembers)
                {
                    // Ignore Object.GetType, as it's commonly seen and Type is a commonly-used property name.
                    if (member.ContainingType.SpecialType == SpecialType.System_Object &&
                        member.Name == nameof(GetType))
                    {
                        continue;
                    }

                    // If the declared type is a property, was a matching method found?
                    if (symbol.Kind == SymbolKind.Property && member.Kind == SymbolKind.Method)
                    {
                        diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], symbol.Name, identifier);
                        break;
                    }

                    // If the declared type is a method, was a matching property found?
                    if (symbol.Kind == SymbolKind.Method
                        && member.Kind == SymbolKind.Property
                        && !symbol.ContainingType.Equals(type)) // prevent reporting duplicate diagnostics
                    {
                        diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], identifier, symbol.Name);
                        break;
                    }
                }

                if (diagnostic != null)
                {
                    // Once a match is found, exit the outer for loop
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            }
        }
    }
}