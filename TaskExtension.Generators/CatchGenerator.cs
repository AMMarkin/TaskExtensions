﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskExtension.Generators;

[Generator]
public class CatchGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var taskType = context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName(typeof(Task).FullName));
        var assemblyName = context.CompilationProvider.Select((x, _) => x.AssemblyName);

        var types = context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => node is InvocationExpressionSyntax invocationExpression,
            (syntax, _) => (IMethodSymbol)syntax.SemanticModel.GetSymbolInfo(syntax.Node).Symbol)
            .Where(x => x?.ReturnsVoid is false)
            .Select((x, _) => x.ReturnType as INamedTypeSymbol)
            .Where(x => x is not null)
            .Combine(taskType)
            .Where(x => x.Left.BaseType?.Equals(x.Right, SymbolEqualityComparer.Default) is true)
            .Where(x => !x.Left.TypeArguments.First().Equals(x.Right, SymbolEqualityComparer.Default))
            .Where(x => x.Left.TypeArguments.First().BaseType?.Equals(x.Right, SymbolEqualityComparer.Default) is not true)
            .Select((x, _) => x.Left)
            .Select((x, _) => x.TypeArguments.First())
            .Collect()
            .Select((x, _) => x.ToImmutableHashSet(SymbolEqualityComparer.Default));

        context.RegisterSourceOutput(types.Combine(assemblyName), Generate);
    }

    private void Generate(SourceProductionContext context, (ImmutableHashSet<ISymbol> Left, string Right) source)
    {
        var (types, assemblyName) = source;

        var text = new StringBuilder();

        text.AppendFormat("global using {0}.Generated.TaskExtensions;\n", assemblyName);
        text.AppendLine("""
using System;
using System.Threading.Tasks;
""");

        text.AppendFormat("namespace {0}.Generated.TaskExtensions;\n\n", assemblyName);
        text.Append("""
public static class TaskExtensions
{
    public static Task Catch(this Task task, Action<Exception> exceptionHandler)
        => task.ContinueWith(completedTask =>
        {
            if (completedTask.IsFaulted)
                exceptionHandler(completedTask.Exception.InnerException!);
        });

    public static Task<TResult> Catch<TResult>(this Task<TResult> task, Func<Exception, TResult> exceptionHandler)
        => task.ContinueWith(completedTask =>
        {
            if (completedTask.IsFaulted)
                return Task.FromResult(exceptionHandler(completedTask.Exception.InnerException!));
            else
                return completedTask;
        }).Unwrap();

    public static Task Catch<TException>(this Task task, Action<TException> exceptionHandler)
        where TException : Exception
        => task.ContinueWith(completedTask =>
        {
            if (completedTask is { IsFaulted: true, Exception.InnerException: TException exception })
            {
                exceptionHandler(exception);
                return Task.CompletedTask;
            }
            else
            {
                return completedTask;
            }
        }).Unwrap();

""");

        foreach (var type in types)
        {
            text.AppendFormat(@"
    public static Task<{0}> Catch<TException>(this Task<{0}> task, Func<TException,{0}> exceptionHandler)
        where TException : Exception
        => task.ContinueWith(completedTask =>
        {{
            if (completedTask is {{ IsFaulted: true, Exception.InnerException: TException exception }})
            {{
                return Task.FromResult(exceptionHandler(exception));
            }}
            else
            {{
                return completedTask;
            }}
        }}).Unwrap();
", type);
        }

        text.AppendLine("}");

        context.AddSource("Test.gen.cs", text.ToString());
    }
}
