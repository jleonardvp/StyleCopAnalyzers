﻿// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace StyleCop.Analyzers.NamingRules
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using StyleCop.Analyzers.Helpers;
    using StyleCop.Analyzers.Lightup;
    using StyleCop.Analyzers.Settings.ObjectModel;

    /// <summary>
    /// Field names within a tuple declaration should have the correct casing.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class SA1316TupleFieldNamesMustUseCorrectCasing : DiagnosticAnalyzer
    {
        /// <summary>
        /// The ID for diagnostics produced by the <see cref="SA1316TupleFieldNamesMustUseCorrectCasing"/> analyzer.
        /// </summary>
        public const string DiagnosticId = "SA1316";

        /// <summary>
        /// The key used to signal the fixed tuple field name to the code fix.
        /// </summary>
        internal const string ExpectedTupleFieldNameKey = "ExpectedTupleFieldName";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(NamingResources.SA1316Title), NamingResources.ResourceManager, typeof(NamingResources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(NamingResources.SA1316MessageFormat), NamingResources.ResourceManager, typeof(NamingResources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(NamingResources.SA1316Description), NamingResources.ResourceManager, typeof(NamingResources));
        private static readonly string HelpLink = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1316.md";

        private static readonly DiagnosticDescriptor Descriptor =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, AnalyzerCategory.NamingRules, DiagnosticSeverity.Warning, AnalyzerConstants.EnabledByDefault, Description, HelpLink);

        private static readonly Action<SyntaxNodeAnalysisContext> TupleTypeAction = HandleTupleTypeAction;
        private static readonly Action<SyntaxNodeAnalysisContext> TupleExpressionAction = HandleTupleExpressionAction;

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptor);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(TupleTypeAction, SyntaxKindEx.TupleType);
            context.RegisterSyntaxNodeAction(TupleExpressionAction, SyntaxKindEx.TupleExpression);
        }

        private static void HandleTupleTypeAction(SyntaxNodeAnalysisContext context)
        {
            if (!context.SupportsTuples())
            {
                return;
            }

            var settings = context.Options.GetStyleCopSettings(context.CancellationToken);
            var tupleType = (TupleTypeSyntaxWrapper)context.Node;

            foreach (var tupleElement in tupleType.Elements)
            {
                CheckTupleElement(context, settings, tupleElement);
            }
        }

        private static void HandleTupleExpressionAction(SyntaxNodeAnalysisContext context)
        {
            if (!context.SupportsInferredTupleFieldNames())
            {
                return;
            }

            var settings = context.Options.GetStyleCopSettings(context.CancellationToken);
            if (!settings.NamingRules.IncludeInferredTupleFieldNames)
            {
                return;
            }

            var tupleExpression = (TupleExpressionSyntaxWrapper)context.Node;
            foreach (var argument in tupleExpression.Arguments)
            {
                var inferredMemberName = SyntaxFactsEx.TryGetInferredMemberName(argument.Expression);
                if (inferredMemberName != null)
                {
                    CheckName(context, settings, inferredMemberName, argument.Expression.GetLocation(), false);
                }
            }
        }

        private static void CheckTupleElement(SyntaxNodeAnalysisContext context, StyleCopSettings settings, TupleElementSyntaxWrapper tupleElement)
        {
            if (tupleElement.Identifier == default)
            {
                return;
            }

            CheckName(context, settings, tupleElement.Identifier.ValueText, tupleElement.Identifier.GetLocation(), true);
        }

        private static void CheckName(SyntaxNodeAnalysisContext context, StyleCopSettings settings, string tupleFieldName, Location location, bool prepareCodeFix)
        {
            var firstCharacterIsLower = char.IsLower(tupleFieldName[0]);

            bool reportDiagnostic = false;
            string fixedName;

            switch (settings.NamingRules.TupleFieldNameCasing)
            {
            case TupleFieldNameCase.PascalCase:
                reportDiagnostic = firstCharacterIsLower;
                fixedName = char.ToUpper(tupleFieldName[0]) + tupleFieldName.Substring(1);
                break;

            default:
                reportDiagnostic = !firstCharacterIsLower;
                fixedName = char.ToLower(tupleFieldName[0]) + tupleFieldName.Substring(1);
                break;
            }

            if (reportDiagnostic)
            {
                var diagnosticProperties = ImmutableDictionary.CreateBuilder<string, string>();

                if (prepareCodeFix)
                {
                    diagnosticProperties.Add(ExpectedTupleFieldNameKey, fixedName);
                }

                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, diagnosticProperties.ToImmutableDictionary()));
            }
        }
    }
}
