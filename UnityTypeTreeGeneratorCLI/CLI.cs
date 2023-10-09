using AssetsTools.NET.Extra;
using System.Text.Json;
using AssetsTools.NET;
using Cpp2IL.Core;
using Mono.Cecil;

namespace UnityTypeTreeGeneratorCLI;

public class UnityGameAnalyzer
{
    public readonly string exePath;
    public readonly string dataPath;
    public readonly string assembliesPath;
    public readonly string gameAssemblyPath;
    public readonly string metadataPath;
    public readonly string? unityBackend;

    public UnityGameAnalyzer(string gamePath)
    {
        exePath = Directory.GetFiles(gamePath, "*.exe")[0];
        dataPath = Path.Combine(gamePath, $"{Path.GetFileNameWithoutExtension(exePath)}_Data");
        
        assembliesPath = Path.Combine(dataPath, "Managed");
        
        gameAssemblyPath = Path.Combine(gamePath, "GameAssembly.dll");
        metadataPath = Path.Combine(dataPath, "il2cpp_data", "Metadata", "global-metadata.dat");

        if (Directory.Exists(assembliesPath) && Directory.GetFiles(assembliesPath, "*.dll").Any()) unityBackend = "Mono";
        else if (File.Exists(gameAssemblyPath) && File.Exists(metadataPath)) unityBackend = "IL2CPP";
        else unityBackend = null;
    }
}

public class UnityTypeTreeGenerator
{
    private readonly Dictionary<string, AssemblyDefinition> assembliesMap = new();
    private readonly MonoCecilTempGenerator monoTemplateFieldsGenerator;
    private readonly ClassDatabaseFile classDatabase = new();
    private readonly UnityVersion unityVersion = new();

    public UnityTypeTreeGenerator(string gamePath, string unityVersionString = "")
    {   
        // Initialize unity game analyzer, which identifies important paths and the backend of the game.
        var unityBackendDetector = new UnityGameAnalyzer(gamePath);
        
        // initialize AssetsTools.NET's mono template fields generator to be used later to get template fields for types.
        monoTemplateFieldsGenerator = new MonoCecilTempGenerator(unityBackendDetector.assembliesPath);
        
        // Get unity version in both digit and string forms, if unity version if not given, CPP2Il utility function is used.
        int[]? unityVersionDigits;
        if (unityVersionString.Length == 0)
        {
            unityVersionDigits = Cpp2IlApi.DetermineUnityVersion(unityBackendDetector.exePath, unityBackendDetector.dataPath);
            if (unityVersionDigits is null) return;
            
            unityVersion = new UnityVersion(string.Join(".", unityVersionDigits));
        }
        else
        {
            unityVersionDigits = unityVersionString.Split(".").Select(int.Parse).ToArray();
            unityVersion = new UnityVersion(unityVersionString);
        }
        
        // Initialize the class database to be used later to get template fields for unity base types. 
        var stream = File.OpenRead("classdata.tpk");
        var classPackage = new ClassPackageFile();
        classPackage.Read(new AssetsFileReader(stream));
        
        classDatabase = classPackage.GetClassDatabase(unityVersion);
        
        // Generate and write assemblies for the game if the backend is IL2CPP and assemblies wasn't generated before.
        if (unityBackendDetector.unityBackend == "IL2CPP")
        {   
            Console.WriteLine("Generating Assemblies...");
            Cpp2IlApi.InitializeLibCpp2Il(unityBackendDetector.gameAssemblyPath,
                                          unityBackendDetector.metadataPath,
                                          unityVersionDigits,
                                          false);
            
            if (!Directory.Exists(unityBackendDetector.assembliesPath))
                Directory.CreateDirectory(unityBackendDetector.assembliesPath);
            
            foreach (var assembly in Cpp2IlApi.MakeDummyDLLs())
                assembly.Write(Path.Combine(unityBackendDetector.assembliesPath, $"{assembly.Name.Name}.dll"));
        }

        // Load all assemblies once in  assembly name and assembly definition pairs, to be used later by other functions.
        var assemblyPaths = Directory.GetFiles(unityBackendDetector.assembliesPath, "*.dll", SearchOption.AllDirectories);
        foreach (var assemblyPath in assemblyPaths)
            assembliesMap[Path.GetFileName(assemblyPath)] = monoTemplateFieldsGenerator.GetAssemblyWithDependencies(assemblyPath); 
    }
    
    /* Returns a template field for a given type using the class database, It's essential for unity base types
     like MonoBehavior and GameObject, MonoCecilTempGenerator will never return template fields for unity base types. */
    private AssetTypeTemplateField? QueryClassDatabase(TypeDefinition type)
    {
        var typeTemplate = new AssetTypeTemplateField();
        var classDatabaseType = classDatabase.FindAssetClassByName(type.Name);

        if (classDatabaseType is null || (classDatabaseType.EditorRootNode == null && classDatabaseType.ReleaseRootNode == null)) return null;
        typeTemplate.FromClassDatabase(classDatabase, classDatabaseType);
        
        return typeTemplate;
    }
    
    // Returns a dictionary of type name and type tree pairs for a given list of types.
    private Dictionary<string, List<Dictionary<string, object>>> GenerateAssemblyTypeTree(string assemblyName)
    {
        var typeTree = new Dictionary<string, List<Dictionary<string, object>>>();
        var types = assembliesMap[assemblyName].MainModule.Types.ToList();
        
        foreach (var type in types)
        {   
            if (type.Name is "<Module>" or "<PrivateImplementationDetails>") continue;
            
            var typeTemplates = GenerateTypeTemplates(type);
            if (typeTemplates.Any()) typeTree[type.FullName] = GenerateTypeTreeNodes(typeTemplates);
        }

        return typeTree;
    }
    
    /* Returns a list of type templates for a given type utilizing the class database for unity base types, and
     MonoCecilTempGenerator for the type it self, it returns an empty list if there was no type templates returned
     from any of the unity base types. */
    private List<AssetTypeTemplateField> GenerateTypeTemplates(TypeDefinition type)
    {
        var typeTemplates = new List<AssetTypeTemplateField>();
        var baseTypes = GetBaseTypes(type);
        
        foreach (var typeTemplate in baseTypes.Select(QueryClassDatabase))
        {   
            // template fields of type component are not needed for deserialization.
            if (typeTemplate is null || typeTemplate.Type == "Component") continue;
            typeTemplates.Add(typeTemplate);
            
            /* If the current template field type was one of those, then break as the generator recursively gets the
             rest of the base types internally, thus we prevent possible duplicates */
            if (typeTemplate.Type is "MonoBehaviour" or "ScriptableObject")
                break;
        }
        
        /* If there's not a single type template for any of the base types, it means this type is not supported, thus,
        do not generate type templates for the type it self and return an empty list. */
        if (typeTemplates.Any())
            typeTemplates.AddRange(monoTemplateFieldsGenerator.Read(assembliesMap[type.Scope.Name],
                                   type.Namespace,
                                   type.Name,
                                   unityVersion));
        
        return typeTemplates;
    }
    
    // Returns a list of type tree nodes given a list of type templates.
    private static List<Dictionary<string, object>> GenerateTypeTreeNodes(List<AssetTypeTemplateField> typeTemplates)
    {   
        var typeTreeNodes = new List<Dictionary<string, object>>();
        if (!typeTemplates.Any()) return typeTreeNodes;
            
        typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(typeTemplates[0], level: 0));
        
        foreach (var typeTemplate in typeTemplates.GetRange(1, typeTemplates.Count - 1))
            typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(typeTemplate, level: 1));

        return typeTreeNodes;
    }
    
    // Recursive function that returns a list of type tree nodes for a type template including it's child type templates too.
    private static List<Dictionary<string, object>> TypeTemplateToTypeTreeNodes(AssetTypeTemplateField typeTemplate, int level = 0)
    {
        var typeTreeNodes = new List<Dictionary<string, object>> { GenerateTypeTreeNode(typeTemplate, level) };
        if (!typeTemplate.Children.Any()) return typeTreeNodes;

        foreach (var child in typeTemplate.Children)
            typeTreeNodes.AddRange(TypeTemplateToTypeTreeNodes(child, level + 1));

        return typeTreeNodes;
    }
    
    // Returns a type tree node out of a type template.
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
    
    // Returns a list of the base types for a given type, ordered from top type to bottom type.
    private static List<TypeDefinition> GetBaseTypes(TypeDefinition type)
    {
        var baseTypes = new List<TypeDefinition>();
        if (type.BaseType is null) return baseTypes;
        
        var baseType = type.BaseType.Resolve();
        
        baseTypes.Insert(0, baseType);
        baseTypes.InsertRange(0, GetBaseTypes(baseType));
        
        return baseTypes;
    }
    
    // Generates type trees and writes them to a JSON file for all assemblies.
    public void DumpJson(string outputPath)
    {   
        var assemblyTypeTrees = new Dictionary<string, Dictionary<string, List<Dictionary<string, object>>>>();
        
        foreach (var assemblyName in assembliesMap.Keys)
        {
            Console.WriteLine("Processing: " + assemblyName);
            var typeTree = GenerateAssemblyTypeTree(assemblyName);
            
            assemblyTypeTrees[assemblyName] = typeTree;
        }
        
        if (!assemblyTypeTrees.Any()) return;
        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
        
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        foreach (var assemblyName in assemblyTypeTrees.Keys)
        {   
            var jsonString = JsonSerializer.Serialize(assemblyTypeTrees[assemblyName], jsonOptions);
            File.WriteAllText(Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(assemblyName)}.json"), jsonString);
        }
    }
    
}

public static class CLI
{
    public static void Main(string[] args)
    {   
        string? gamePath = null;
        string? outputPath = null;
        var unityVersion = "";
        
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p" when i + 1 < args.Length:
                    gamePath = args[i + 1];
                    i++;
                    break;
                
                case "-o" when i + 1 < args.Length:
                    outputPath = args[i + 1];
                    i++;
                    break;
                
                case "-v" when i + 1 < args.Length:
                    unityVersion = args[i + 1];
                    i++;
                    break;
            }
        }
        
        if (gamePath is null || outputPath is null) return;
        
        var typeTreeGenerator = new UnityTypeTreeGenerator(gamePath, unityVersion);
        typeTreeGenerator.DumpJson(outputPath);
    }
}