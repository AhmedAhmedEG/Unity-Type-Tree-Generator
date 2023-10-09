# UnityTypeTreeGenerator
Wrapper for AssetTools.NET library to generates type trees for MonoBehavior objects in Unity assemblies as JSON files.

# Usage
UnityTypeTreeGenerator.exe -p GAME_PATH -o OUTPUT_PATH

> You can optionally add (-v UNITY_VERSION) in case you want to inforce a spacific Unity version, normally Unity version will get automaticlly detected using Cpp2IlApi.DetermineUnityVersion helper function.

# What's a Type Tree?

First make sure you understand those definitions:-
```
Assembly -> DLL file
Type -> Class
Field -> Attribute -> Class Variable
Method -> Class Function
Object -> Class Instance
```
Now with that in mind, assemblies are simply a collections of types, every type have fields, every field have a spacific data type, to deserialize objects that inherite from MonoBehavior type in unity game's asset files, we need info about the fields of those objects to be able parser them, normally but not always, when you try to read MonoBehavior objects from asset file, what you get only is the location and bounds of a portion of bytes in that asset file where this MonoBehavior object exists, this portion is a mix of all of field values, but you cannot make sense of those bytes unless you know what kind of fields exists in this object? what's their order? how much bytes you should read for every field? this is where type trees comes into play!

For example, here a class want to serialize:-
```

```

# How Type Tree Looks Like?
Now let's start, a type tree is a three layer neasted-map-array data structure (map of arrays of maps to be spacific), there's a type tree per assembly, a single type tree starts with a root map, that map have pairs of type names as keys, and node arrays as values, every node is a map of four pairs, here's an example of a single type tree root map pair:-
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
