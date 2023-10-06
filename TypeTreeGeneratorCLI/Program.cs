using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Mono.Cecil;

namespace TypeTreeGeneratorCLI;

public static class TypeTreeGeneratorCLI
{
    private const string AssembliesPath = @"C:\Python Projects\Big Projects\PyTranslate\Samples\Dojo NTR\Dojo NTR_Data\Managed";
    private const string OutputPath = @"C:\Users\PC\Desktop\Type Trees\AssetsTools.NET";
    private static readonly UnityVersion UnityVersion = new("2021.3.21f1");
    private static readonly MonoCecilTempGenerator Generator = new(AssembliesPath);
    private static readonly ClassDatabaseFile ClassDatabase = GetClassDatabase();
    private static Dictionary<string, AssemblyDefinition> AassembliesMap = new Dictionary<string, AssemblyDefinition>();

    private static void Main(string[] args)
    {
        var assemblyPaths = Directory.GetFiles(AssembliesPath, "*.dll", SearchOption.AllDirectories);
        var assemblyTypeTrees = new Dictionary<string, Dictionary<string, List<Dictionary<string, object>>>>();
        
        foreach (var assemblyPath in assemblyPaths)
            AassembliesMap[Path.GetFileName(assemblyPath)] = Generator.GetAssemblyWithDependencies(assemblyPath); 
        
        foreach (var assemblyName in AassembliesMap.Keys)
        {
            Console.WriteLine("Processing: " + assemblyName);
            
            var types = AassembliesMap[assemblyName].MainModule.Types.ToList();
            var typeTree = GenerateTypeTree(types);
            
            assemblyTypeTrees[assemblyName] = typeTree;
        }
        
        DumpJson(assemblyTypeTrees);
        Console.WriteLine("Done.");
    }

    private static Dictionary<string, List<Dictionary<string, object>>> GenerateTypeTree(List<TypeDefinition> types)
    {
        var typeTree = new Dictionary<string, List<Dictionary<string, object>>>();
        
        foreach (var type in types)
        {
            var typeTemplates = GenerateTypeTemplates(type);
            typeTree[type.FullName] = GenerateTypeTreeNodes(typeTemplates);
        }

        return typeTree;
    }

    private static List<AssetTypeTemplateField> GenerateTypeTemplates(TypeDefinition type)
    {
        var typeTemplates = new List<AssetTypeTemplateField>();
        var baseTypes = GetBaseTypes(type);

        foreach (var baseType in baseTypes)
        {
            var typeTemplate = new AssetTypeTemplateField();
            var classDatabaseType = ClassDatabase.FindAssetClassByName(baseType.Name);

            if (classDatabaseType is null || (classDatabaseType.EditorRootNode == null && classDatabaseType.ReleaseRootNode == null)) continue;
            
            typeTemplate.FromClassDatabase(ClassDatabase, classDatabaseType);
            
            if (typeTemplate.Type == "Component") continue;
            
            typeTemplates.Add(typeTemplate);
        }
        
        if (!typeTemplates.Any()) return typeTemplates;
        
        typeTemplates.AddRange(Generator.Read(AassembliesMap[type.Scope.Name], type.Namespace, type.Name, UnityVersion));

        return typeTemplates;
    }
    
    private static List<Dictionary<string, object>> GenerateTypeTreeNodes(List<AssetTypeTemplateField> typeTemplates)
    {   
        var typeTreeNodes = new List<Dictionary<string, object>>();

        if (!typeTemplates.Any()) return typeTreeNodes;
            
        typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(typeTemplates[0], level: 0));
        
        foreach (var typeTemplate in typeTemplates.GetRange(1, typeTemplates.Count - 1))
            typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(typeTemplate, level: 1));

        return typeTreeNodes;
    }
    
    private static List<Dictionary<string, object>> TypeTemplateToTypeTreeNodes(AssetTypeTemplateField typeTemplate, int level = 0)
    {
        var typeTreeNodes = new List<Dictionary<string, object>> { GenerateTypeTreeNode(typeTemplate, level) };

        if (!typeTemplate.Children.Any())
            return typeTreeNodes;

        foreach (var child in typeTemplate.Children)
            typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(child, level + 1));

        return typeTreeNodes;
    }
    
    private static Dictionary<string, object> GenerateTypeTreeNode(AssetTypeTemplateField typeTemplate, int level)
    {
        var typeTreeNode = new Dictionary<string, object>
        {
            ["m_Type"] = typeTemplate.Type,
            ["m_Name"] = typeTemplate.Name,
            ["m_MetaFlag"] = typeTemplate.IsAligned ? 0x4000 : 0,
            ["m_Level"] = level
        };

        return typeTreeNode;
    }

    private static void DumpJson(Dictionary<string, Dictionary<string, List<Dictionary<string, object>>>> assemblyTypeTrees)
    {   
        
        if (!assemblyTypeTrees.Any()) return;
        
        if (!Directory.Exists(OutputPath))
            Directory.CreateDirectory(OutputPath);
        
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        foreach (var assemblyName in assemblyTypeTrees.Keys)
        {   
            string jsonString = JsonSerializer.Serialize(assemblyTypeTrees[assemblyName], jsonOptions);
            File.WriteAllText(Path.Combine(OutputPath, $"{Path.GetFileNameWithoutExtension(assemblyName)}.json"), jsonString);
        }
    }
    
    private static List<TypeDefinition> GetBaseTypes(TypeDefinition type)
    {
        var baseTypes = new List<TypeDefinition>();

        if (type.BaseType is null)
            return baseTypes;
        
        var baseType = type.BaseType.Resolve();
        
        baseTypes.Insert(0, baseType);
        baseTypes.InsertRange(0, GetBaseTypes(baseType));
        
        return baseTypes;
    }
    
    public static ClassDatabaseFile GetClassDatabase()
    {
        var stream = File.OpenRead("classdata.tpk");
        
        var classPackage = new ClassPackageFile();
        classPackage.Read(new AssetsFileReader(stream));
        
        return classPackage.GetClassDatabase(UnityVersion);
    }
    
}