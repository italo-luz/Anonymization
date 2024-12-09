**Source Generators** no .NET permitem gerar código em tempo de compilação, eliminando a necessidade de usar reflexão em tempo de execução. Para criar uma biblioteca funcional para mascaramento de dados usando **Source Generators**, siga este exemplo:

---

### **Passo 1: Configurar o Projeto Source Generator**
1. **Crie um Projeto para o Gerador:**
   ```bash
   dotnet new classlib -n MaskingGenerator
   cd MaskingGenerator
   ```

2. **Edite o `.csproj` para incluir o SDK Roslyn:**
   Adicione a seguinte linha no arquivo `.csproj`:
   ```xml
   <PropertyGroup>
       <OutputItemType>Analyzer</OutputItemType>
       <IncludeBuildOutput>true</IncludeBuildOutput>
   </PropertyGroup>
   ```

---

### **Passo 2: Implementar o Gerador**

#### Crie o Arquivo `MaskingGenerator.cs`:
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[Generator]
public class MaskingGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Registre o gerador para receber notificações quando uma classe com [Mask] for encontrada.
        context.RegisterForSyntaxNotifications(() => new MaskAttributeSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Obtenha as classes que têm propriedades anotadas com [Mask]
        if (context.SyntaxReceiver is not MaskAttributeSyntaxReceiver receiver)
            return;

        // Itere pelas classes encontradas e gere código
        foreach (var candidateClass in receiver.CandidateClasses)
        {
            var model = context.Compilation.GetSemanticModel(candidateClass.SyntaxTree);

            if (model.GetDeclaredSymbol(candidateClass) is not INamedTypeSymbol classSymbol)
                continue;

            var source = GenerateMaskedClass(classSymbol);
            context.AddSource($"{classSymbol.Name}_Masked.g.cs", source);
        }
    }

    private string GenerateMaskedClass(INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        var namespaceName = classSymbol.ContainingNamespace.ToString();

        // Gere as propriedades mascaradas
        var maskedProperties = new StringBuilder();
        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var maskAttribute = property.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.Name == "MaskAttribute");

            if (maskAttribute == null) continue;

            var maskValue = maskAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "\"***\"";

            maskedProperties.AppendLine($@"
                public string {property.Name}_Masked => {property.Name} != null ? ""{maskValue}"" : null;");
        }

        // Gere o código da classe mascarada
        return $@"
using System;

namespace {namespaceName}
{{
    public partial class {className}
    {{
        {maskedProperties}
    }}
}}
";
    }
}

// Receptor de sintaxe para encontrar classes com [MaskAttribute]
internal class MaskAttributeSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Identifique classes com propriedades anotadas com [Mask]
        if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
            classDeclaration.AttributeLists.Count > 0)
        {
            CandidateClasses.Add(classDeclaration);
        }
    }
}
```

---

### **Passo 3: Criar o Atributo de Máscara**

Crie um projeto separado para usar o gerador e defina o atributo `MaskAttribute`.

#### Arquivo `MaskAttribute.cs`:
```csharp
using System;

[AttributeUsage(AttributeTargets.Property)]
public class MaskAttribute : Attribute
{
    public string MaskPattern { get; }

    public MaskAttribute(string maskPattern = "***")
    {
        MaskPattern = maskPattern;
    }
}
```

---

### **Passo 4: Usar o Gerador no Projeto**

1. Adicione o projeto do gerador como referência ao projeto principal.
2. Defina uma classe com propriedades anotadas com `[Mask]`.

#### Arquivo `User.cs`:
```csharp
public partial class User
{
    public string Name { get; set; }

    [Mask("XXXX-XXXX-XXXX")]
    public string CreditCardNumber { get; set; }

    [Mask]
    public string SSN { get; set; }
}
```

3. Após compilar, o gerador criará um arquivo adicional:

#### Arquivo Gerado `User_Masked.g.cs`:
```csharp
using System;

namespace YourNamespace
{
    public partial class User
    {
        public string CreditCardNumber_Masked => CreditCardNumber != null ? "XXXX-XXXX-XXXX" : null;

        public string SSN_Masked => SSN != null ? "***" : null;
    }
}
```

---

### **Passo 5: Consumir o Código Gerado**

Agora você pode usar as propriedades _Masked_:

```csharp
var user = new User
{
    Name = "John Doe",
    CreditCardNumber = "1234-5678-9101-1121",
    SSN = "987-65-4321"
};

Console.WriteLine($"Name: {user.Name}");
Console.WriteLine($"Credit Card: {user.CreditCardNumber_Masked}");
Console.WriteLine($"SSN: {user.SSN_Masked}");
```

**Saída:**
```
Name: John Doe
Credit Card: XXXX-XXXX-XXXX
SSN: ***
```

---

### **Vantagens**
1. Nenhum custo em tempo de execução.
2. Código gerado automaticamente, reduzindo a necessidade de reflexão.
3. Fácil de integrar com projetos existentes.

### **Desvantagens**
1. Necessita compilar para ver o código gerado.
2. Pode ser complexo em projetos grandes. 

Se precisar de mais detalhes, posso ajudar!