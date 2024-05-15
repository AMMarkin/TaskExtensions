using Microsoft.CodeAnalysis;
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
            .Where(x => x is { IsAnonymousType: false, IsGenericType: true })
            .Combine(taskType)
            .Where(x => x.Left.BaseType?.Equals(x.Right, SymbolEqualityComparer.Default) is true)
            .Select((x, _) => (Type: x.Left.TypeArguments.First(), TaskType: x.Right))
            .Where(x => x.Type is { TypeKind: not TypeKind.TypeParameter })
            .Where(x => x.Type.IsDerivedFrom(x.TaskType) is false)
            .Select((x, _) => x.Type)
            .Collect()
            .Select((x, _) => x.ToImmutableHashSet(SymbolEqualityComparer.Default));

        context.RegisterSourceOutput(types.Combine(assemblyName), Generate);
    }

    private void Generate(SourceProductionContext context, (ImmutableHashSet<ISymbol> Left, string Right) source)
    {
        var (types, assemblyName) = source;

        var taskExtensions = GenerateTaskExtensions(types, assemblyName);

        context.AddSource("TaskExtensions.gen.cs", taskExtensions);

        var valueTaskExtensions = GenerateValueTaskExtensions(types, assemblyName);

        context.AddSource("ValueTaskExtensions.gen.cs", valueTaskExtensions);
    }

    private static string GenerateTaskExtensions(ImmutableHashSet<ISymbol> types, string assemblyName)
    {
        var source = new StringBuilder();

        source.AppendFormat("global using {0}.Generated.TaskExtensions;\n", assemblyName);
        source.AppendLine("""
using System;
using System.Threading.Tasks;
""");

        source.AppendFormat("namespace {0}.Generated.TaskExtensions;\n\n", assemblyName);
        source.Append("""
internal static class TaskExtensions
{
    internal static Task Catch(this Task task, Action<Exception> exceptionHandler)
    {{
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return task.ContinueWith(completedTask =>
        {
            if (completedTask.IsFaulted)
                exceptionHandler(completedTask.Exception.InnerException!);
        });
    }}

    internal static Task<TResult> Catch<TResult>(this Task<TResult> task, Func<Exception, TResult> exceptionHandler)
    {{
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return task.ContinueWith(completedTask =>
        {

            if (completedTask.IsFaulted)
                return Task.FromResult(exceptionHandler(completedTask.Exception.InnerException!));
            else
                return completedTask;
        }).Unwrap();
    }}

    internal static Task Catch<TException>(this Task task, Action<TException> exceptionHandler)
        where TException : Exception
    {{
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return task.ContinueWith(completedTask =>
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
    }}

""");

        foreach (var type in types)
        {
            source.AppendFormat(@"
    internal static Task<{0}> Catch<TException>(this Task<{0}> task, Func<TException,{0}> exceptionHandler)
        where TException : Exception
    {{
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        return task.ContinueWith(completedTask =>
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
    }}
", type);
        }
        
        source.AppendLine("}");
        return source.ToString();
    }

    private static string GenerateValueTaskExtensions(ImmutableHashSet<ISymbol> types, string assemblyName)
    {
        var source = new StringBuilder();

        source.AppendFormat("global using {0}.Generated.TaskExtensions;\n", assemblyName);
        source.AppendLine("""
using System;
using System.Threading.Tasks;
""");

        source.AppendFormat("namespace {0}.Generated.TaskExtensions;\n\n", assemblyName);
        source.Append("""
internal static class ValueTaskExtensions
{
    internal static Task Catch(this ValueTask task, Action<Exception> exceptionHandler)
        => task.AsTask().Catch(exceptionHandler);

    internal static Task<TResult> Catch<TResult>(this ValueTask<TResult> task, Func<Exception, TResult> exceptionHandler)
        => task.AsTask().Catch(exceptionHandler);

    internal static Task Catch<TException>(this ValueTask task, Action<TException> exceptionHandler)
        where TException : Exception
        => task.AsTask().Catch<TException>(exceptionHandler);

""");

        foreach (var type in types)
        {
            source.AppendFormat(@"
    internal static Task<{0}> Catch<TException>(this ValueTask<{0}> task, Func<TException,{0}> exceptionHandler)
        where TException : Exception
        => task.AsTask().Catch<TException>(exceptionHandler);
", type);
        }

        source.AppendLine("}");
        return source.ToString();
    }
}
