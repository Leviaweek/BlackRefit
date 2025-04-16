using System.Collections.Immutable;
using System.Text;
using BlackRefit.Attributes;
using BlackRefit.Extension;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BlackRefit;

[Generator]
public class MyRefitGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceSyntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: GetNamedTypeSymbolWithAttribute)
            .Where(symbol => symbol is not null);
        context.RegisterSourceOutput(interfaceSyntaxProvider, (spc, interfaceSyntax) =>
        {
            var interfaceName = interfaceSyntax?.Name ??
                                throw new InvalidOperationException("Interface name cannot be null");
            
            var className = $"{interfaceName}GeneratedClient";
            
            var generatedCode = GenerateClientCode(interfaceSyntax);
            generatedCode = CSharpSyntaxTree.ParseText(generatedCode)
                .GetRoot()
                .NormalizeWhitespace()
                .ToFullString();
            spc.AddSource($"{className}.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
            
            var sourceGenerator = new SourceGeneratorBuilder();
            
            var registrationModule = CreateRegistrationModule(sourceGenerator, interfaceSyntax, className, interfaceName);
            
            spc.AddSource($"{className}RegistrationModule.g.cs", SourceText.From(registrationModule, Encoding.UTF8));
        });
    }

    private static string CreateRegistrationModule(SourceGeneratorBuilder sourceGenerator, INamedTypeSymbol interfaceSyntax, string className, string interfaceName)
    {
        return sourceGenerator
            .Append($"namespace {interfaceSyntax.ContainingNamespace.ToDisplayString()};")
            .Append($"using {interfaceSyntax.ContainingNamespace.ToDisplayString()};")
            .Append("using System;")
            .Append("using System.Net.Http;")
            .Append("using System.Net.Http.Json;")
            .Append("using System.Text;")
            .Append("using BlackRefit;")
            .Append("using System.Threading.Tasks;")
            .Append("using System.Collections.Generic;")
            .Append("using System.Runtime.CompilerServices;")
            .AppendClass($"{className}RegistrationModule", nameof(Object), builder =>
            {
                builder.Append("[ModuleInitializer]");
                builder.Append("public static void Init()")
                    .AppendOpenBracket()
                    .Append(
                        $"RestService.RegisterClient<{interfaceName}>((httpClient) => new {className}(httpClient));")
                    .AppendCloseBracket();
            })
            .Build();
    }

    private static INamedTypeSymbol? GetNamedTypeSymbolWithAttribute(GeneratorSyntaxContext context,
        CancellationToken token)
    {
        var interfaceNode = (InterfaceDeclarationSyntax)context.Node;
        var symbol = ModelExtensions.GetDeclaredSymbol(context.SemanticModel, interfaceNode,
            cancellationToken: token) as INamedTypeSymbol;
                    
        var hasRestClientAttribute = symbol?.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == typeof(RestClientAttribute).FullName) ?? false;

        return hasRestClientAttribute ? symbol : null;
    }

    private static string GenerateClientCode(INamedTypeSymbol symbol)
    {
        var className = $"{symbol.Name}GeneratedClient";
        
        var sourceGenerator = new SourceGeneratorBuilder();
        
        var methods = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(attr =>
                attr.AttributeClass?.BaseType?.ToDisplayString() == typeof(HttpMethodAttribute).FullName))
            .ToList();
        
        sourceGenerator.Append($"namespace {symbol.ContainingNamespace.ToDisplayString()};");
        sourceGenerator.Append($"using {symbol.ContainingNamespace.ToDisplayString()};");
        sourceGenerator.Append("using System;");
        sourceGenerator.Append("using System.Net.Http;");
        sourceGenerator.Append("using System.Net.Http.Json;");
        sourceGenerator.Append("using System.Text;");
        sourceGenerator.Append("using BlackRefit;");
        sourceGenerator.Append("using System.Threading.Tasks;");
        sourceGenerator.Append("using System.Collections.Generic;");
        sourceGenerator.Append("using System.Runtime.CompilerServices;");
        
        sourceGenerator.AppendClass($"{className}", symbol.Name, builder =>
        {
            builder.Append("private readonly HttpClient _httpClient;");
            builder.Append($"public {className}(HttpClient httpClient) {{this._httpClient = httpClient;}}");
            
            foreach (var method in methods)
            {
                var attr = method.GetAttributes().First(attr =>
                    attr.AttributeClass?.BaseType?.ToDisplayString() == typeof(HttpMethodAttribute).FullName);
                
                var httpMethod = attr.AttributeClass!.Name.Replace("Attribute", "");
                var path = attr.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "";

                var returnType = method.ReturnType.ToDisplayString();
                var parameters = method.Parameters;
                var paramList = string.Join(", ", parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var isAsync = returnType.StartsWith("Task") || returnType.StartsWith("ValueTask");
                if (isAsync)
                    AddAsyncMethod(sourceGenerator, method, returnType, parameters, path, httpMethod, paramList);
                else
                    AddMethod(sourceGenerator, method, returnType, parameters, path, httpMethod, paramList);
            }
        });
        return sourceGenerator.Build();
    }

    private static void AddMethod(SourceGeneratorBuilder builder, IMethodSymbol method, string returnType,
        ImmutableArray<IParameterSymbol> parameters, string path, string httpMethod, string paramList)
    {
        builder.AppendMethod(method.Name, returnType, b =>
        {
            var queries = parameters
                .Where(p => p.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == typeof(QueryAttribute).FullName))
                .ToList();
            var joinedQueries = string.Join("&", queries.Select(q => $"{q.Name}={{{q.Name}}}"));

            b.Append($"var url = $\"{{_httpClient.BaseAddress}}{path}\";");
            if (queries.Count != 0)
            {
                b.Append($"url += \"?{joinedQueries}\";");
            }
            b.Append($"var request = new HttpRequestMessage(HttpMethod.{httpMethod}, url);");

            var bodyParam = parameters.FirstOrDefault(p =>
                !p.Type.Name.Contains("CancellationToken") && !path.Contains($"{p.Name}"));

            if (bodyParam is not null)
                b.Append($"request.Content = new StringContent(JsonSerializer.Serialize({bodyParam.Name}), Encoding.UTF8, \"application/json\");");

            b.Append("var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();");
            b.Append("response.EnsureSuccessStatusCode();");
            b.Append($"return response.Content.ReadFromJsonAsync<{returnType}>().GetAwaiter().GetResult();");
        }, paramList);
    }
    
    private static void AddAsyncMethod(SourceGeneratorBuilder builder, IMethodSymbol method, string returnType,
        ImmutableArray<IParameterSymbol> parameters, string path, string httpMethod, string paramList)
    {
        builder.AppendMethod(method.Name, returnType, b =>
        {
            var queries = parameters
                .Where(p => p.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == typeof(QueryAttribute).FullName))
                .ToList();
            var joinedQueries = string.Join("&", queries.Select(q => $"{q.Name}={{{q.Name}}}"));
                    
            b.Append($"var url = $\"{{_httpClient.BaseAddress}}{path}\";");
            if (queries.Count != 0)
            {
                b.Append($"url += \"?{joinedQueries}\";");
            }
            b.Append($"var request = new HttpRequestMessage(HttpMethod.{httpMethod}, url);");

            var bodyParam = parameters.FirstOrDefault(p =>
                !p.Type.Name.Contains("CancellationToken") && !path.Contains($"{p.Name}"));

            if (bodyParam is not null)
                b.Append($"request.Content = new StringContent(JsonSerializer.Serialize({bodyParam.Name}), Encoding.UTF8, \"application/json\");");

            b.Append("var response = await _httpClient.SendAsync(request);");
            b.Append("response.EnsureSuccessStatusCode();");
            b.Append($"return await response.Content.ReadFromJsonAsync<{returnType}>();");
        }, paramList);
    }
}