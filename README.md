# Unity Type Tree Generator
Wrapper for AssetTools.NET library to generates type trees for MonoBehavior objects in Unity as JSON files, it support both Mono and IL2CPP backends.

# Usage
UnityTypeTreeGenerator.exe -p GAME_PATH -o OUTPUT_PATH

> You can optionally add (-v UNITY_VERSION) in case you want to inforce a spacific Unity version, normally Unity version will get automaticlly detected using Cpp2IlApi.DetermineUnityVersion helper function.

> If you encountered errors later while using the type trees, manually generate the dummy dlls using IL2CPPDumper and leave them on Managed folder and they will be auto detected, as Cpp2IL still have some bugs and misses some C# attributes.

# What's a Type Tree?

First make sure you understand those definitions:-
```
Assembly -> DLL file
Type -> Class
Field -> Attribute -> Class Variable
Method -> Class Function
Object -> Class Instance

Serialization -> Writing an object bytes on disk in a way that's reversable.
Deserialization -> Reading back a serialized object from disk.
```
Now with that in mind, assemblies are simply a collections of types, every type have fields, every field have a spacific data type, to deserialize objects that inherites from MonoBehavior type in unity game's asset files, we need info about the fields of those objects to be able parse them, normally but not always, when you try to read MonoBehavior objects from an asset file, what you get only is the location and bounds of a portion of bytes in that asset file where this MonoBehavior object exists, this portion is a mix of all of field values, but you cannot make sense of those bytes unless you know what kind of fields exists in this object? what's the order to read them in? how much bytes you should read for every field? here is where type trees comes into play.

For example, here's a type, called "B", that we want to serialize an object based on it:-
```
class A
{
    public int Number;
    public decimal Number2;
}

class B : A
{
    public bool Flag;
}
```

Type "B" itself have a single boolian field (1 byte), also it inherits from type "A" which have two fields, an integer field (8 bytes), and a decimal field (16 bytes), assuming I will take them in this order, we can serialize an object based on type "B" into a single byte stream where the first byte is the boolian, the 8 bytes after it is the integer and the next 16 bytes are the decimal.

Now imagine I have given you this stream of bytes, without any info on what type "B" fields are and what it inherites from, can you parse the bytes? abvoiusly not, you will need a type tree that tells you what type "B" fields are and what their datatype is to be able to parse those bytes back to the object.

# How Type Tree Looks Like?
A type tree is a three layer neasted-map-array data structure (map of arrays of maps to be spacific), there's a type tree per assembly, a single type tree starts with a root map, that map have pairs of type names as keys, and node arrays as values, every node is a map of four pairs, here's an example of a single type tree root map pair:-
```
"CustomSignalExtendReceiver": [
    {
      "m_Type": "MonoBehaviour",
      "m_Name": "Base",
      "m_MetaFlag": 0,
      "m_Level": 0
    },
    {
      "m_Type": "PPtr\u003CGameObject\u003E",
      "m_Name": "m_GameObject",
      "m_MetaFlag": 0,
      "m_Level": 1
    },
    {
      "m_Type": "int",
      "m_Name": "m_FileID",
      "m_MetaFlag": 0,
      "m_Level": 2
    },
    {
      "m_Type": "SInt64",
      "m_Name": "m_PathID",
      "m_MetaFlag": 0,
      "m_Level": 2
    },
    {
      "m_Type": "UInt8",
      "m_Name": "m_Enabled",
      "m_MetaFlag": 16384,
      "m_Level": 1
    },
    {
      "m_Type": "PPtr\u003CMonoScript\u003E",
      "m_Name": "m_Script",
      "m_MetaFlag": 0,
      "m_Level": 1
    },
    {
      "m_Type": "int",
      "m_Name": "m_FileID",
      "m_MetaFlag": 0,
      "m_Level": 2
    },
    {
      "m_Type": "SInt64",
      "m_Name": "m_PathID",
      "m_MetaFlag": 0,
      "m_Level": 2
    },
    {
      "m_Type": "string",
      "m_Name": "m_Name",
      "m_MetaFlag": 0,
      "m_Level": 1
    },
    {
      "m_Type": "Array",
      "m_Name": "Array",
      "m_MetaFlag": 16384,
      "m_Level": 2
    },
    {
      "m_Type": "int",
      "m_Name": "size",
      "m_MetaFlag": 0,
      "m_Level": 3
    },
    {
      "m_Type": "char",
      "m_Name": "data",
      "m_MetaFlag": 0,
      "m_Level": 3
    },
    {
      "m_Type": "UnityEvent\u00603",
      "m_Name": "unity_event",
      "m_MetaFlag": 0,
      "m_Level": 1
    }
  ],
```

# What's a Node?
A node is a map of four pairs, it starts with "m_Type" for the name of the type the node represents, "m_Name" for the name of variable assigned with that type, "m_Metaflag" which is a flag used by Unity editor to assign spacific properties, there's only one important flag that changes how the bytes being parsed, lastly "m_Level" which spacifies the neasting level of the node relative to the nodes behind it.

When you align all of the nodes mentioned in the previous section based on their levels, you will see that the tree shape of that type is:-

![Screenshot_2](https://github.com/AhmedAhmedEG/UnityTypeTreeGenerator/assets/16827679/526e8784-bfaf-4755-98e4-b0f43361a06d)

