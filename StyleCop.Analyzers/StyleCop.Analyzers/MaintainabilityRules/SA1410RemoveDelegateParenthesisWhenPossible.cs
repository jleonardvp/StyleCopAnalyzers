﻿// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace StyleCop.Analyzers.MaintainabilityRules
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    /// <summary>
    /// A call to a C# anonymous method does not contain any method parameters, yet the statement still includes
    /// parenthesis.
    /// </summary>
    /// <remarks>
    /// <para>When an anonymous method does not contain any method parameters, the parenthesis around the parameters are
    /// optional.</para>
    ///
    /// <para>A violation of this rule occurs when the parenthesis are present on an anonymous method call which takes
    /// no method parameters. For example:</para>
    ///
    /// <code language="csharp">
    /// this.Method(delegate() { return 2; });
    /// </code>
    ///
    /// <para>The parenthesis are unnecessary and should be removed:</para>
    ///
    /// <code language="csharp">
    /// this.Method(delegate { return 2; });
    /// </code>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class SA1410RemoveDelegateParenthesisWhenPossible : DiagnosticAnalyzer
    {
        /// <summary>
        /// The ID for diagnostics produced by the <see cref="SA1410RemoveDelegateParenthesisWhenPossible"/> analyzer.
        /// </summary>
        public const string DiagnosticId = "SA1410";
        private const string Title = "Remove delegate parenthesis when possible";
        private const string MessageFormat = "Remove delegate parenthesis when possible";
        private const string Description = "A call to a C# anonymous method does not contain any method parameters, yet the statement still includes parenthesis.";
        private const string HelpLink = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1410.md";

        private static readonly DiagnosticDescriptor Descriptor =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, AnalyzerCategory.MaintainabilityRules, DiagnosticSeverity.Warning, AnalyzerConstants.EnabledByDefault, Description, HelpLink, WellKnownDiagnosticTags.Unnecessary);

        private static readonly Action<SyntaxNodeAnalysisContext> AnonymousMethodExpressionAction = HandleAnonymousMethodExpression;

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptor);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnonymousMethodExpressionAction, SyntaxKind.AnonymousMethodExpression);
        }

        private static void HandleAnonymousMethodExpression(SyntaxNodeAnalysisContext context)
        {
            AnonymousMethodExpressionSyntax syntax = context.Node as AnonymousMethodExpressionSyntax;
            if (syntax == null)
            {
                return;
            }

            // ignore if no parameter list exists
            if (syntax.ParameterList == null)
            {
                return;
            }

            // ignore if parameter list is not empty
            if (syntax.ParameterList.Parameters.Count > 0)
            {
                return;
            }

            // if the delegate is passed as a parameter, verify that there is no ambiguity.
            if (syntax.Parent.IsKind(SyntaxKind.Argument))
            {
                var argumentSyntax = (ArgumentSyntax)syntax.Parent;
                var argumentListSyntax = (ArgumentListSyntax)argumentSyntax.Parent;

                switch (argumentListSyntax.Parent.Kind())
                {
                case SyntaxKind.ObjectCreationExpression:
                case SyntaxKind.InvocationExpression:
                    if (HasAmbiguousOverload(context, argumentListSyntax.Arguments.IndexOf(argumentSyntax), argumentListSyntax.Parent))
                    {
                        return;
                    }

                    break;
                }
            }

            // Remove delegate parenthesis when possible
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, syntax.ParameterList.GetLocation()));
        }

        private static bool HasAmbiguousOverload(SyntaxNodeAnalysisContext context, int parameterIndex, SyntaxNode methodCallSyntax)
        {
            var methodSymbol = (IMethodSymbol)context.SemanticModel.GetSymbolInfo(methodCallSyntax, context.CancellationToken).Symbol;

            var nameOverloads = methodSymbol.ContainingType.GetMembers(methodSymbol.Name);
            var parameterCountMatchingOverloads = nameOverloads.OfType<IMethodSymbol>().Where(symbol => (symbol != methodSymbol) && (symbol.Parameters.Length == methodSymbol.Parameters.Length));

            foreach (var overload in parameterCountMatchingOverloads)
            {
                var isAmbiguousOverload = true;

                for (var i = 0; isAmbiguousOverload && (i < methodSymbol.Parameters.Length); i++)
                {
                    if (i == parameterIndex)
                    {
                        isAmbiguousOverload = overload.Parameters[i].Type.TypeKind == TypeKind.Delegate;
                    }
                    else
                    {
                        isAmbiguousOverload = methodSymbol.Parameters[i].Type == overload.Parameters[i].Type;
                    }
                }

                if (isAmbiguousOverload)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
