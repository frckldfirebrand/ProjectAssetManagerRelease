using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using static DataTableProperty;
using static PWDataEntry;
using static PWMod;


internal class Program
{
    static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
    static EnumerationOptions DefaultEnumeration = new EnumerationOptions { RecurseSubdirectories = true };
    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
    
    

    bool IsList(Object o)
    {
        if (o == null) return false;
        return o is IList &&
               o.GetType().IsGenericType &&
               o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
    }

    String ProcessedFiles = "Processed Files\\";
    String modJSONFolder = "Mod JSONs\\";
    String modPakFolder = "Mod Paks\\";
    String modDTFolder = "Mod DataTables\\";
    String modExtractedFiles = "Mod Extracted Files\\";
    String vanillaFolder = "Vanilla Files\\";
    String vanillaDataFolder = "Vanilla Files\\Vanilla Data";
    String f59DataFolder = "Vanilla Files\\F59 Data";
    String createdModFolder = "Created Mods\\";


    //There's better ways to do this, such as reading from a file or something more dynamically; however, fuck you.
    readonly List<String> vanillaAircraft = new List<String>
    {
        "Mig-15",
        "T-21",
        "TF-4E",
        "MiG-21",
        "AJS-37",
        "F-4E",
        "F-16C",
        "CF-105",
        "Su-25",
        "MIG-31",
        "F-14D",
        "MiG-29",
        "A-10A",
        "AV-8",
        "F-18E",
        "F-15C",
        "SU-27",
        "F-18F",
        "Su-30",
        "F-15E",
        "J-10B",
        "Su-30MK",
        "FT-15",
        "RF-1",
        "Su-47",
        "X-16",
        "SU-37",
        "F-35",
        "F-15SMTD",
        "F-22",
        "Su-57",
        "ACG-01",
        "SPEAR",
        "X-PF",
        "PW-001",
        "E-3",
        "CF-105_2",
        "MiG-21_2",
        "AV-8_2",
    };

    bool CompareDataTableProperty(DataTableProperty prop1, DataTableProperty prop2)
    {
        if (prop1.Name.Equals(prop2.Name)) { return true; }
        if (prop1.Tag.Equals(prop2.Tag)) { return true; }
        if (prop1.Value.Equals(prop2.Value)) { return true; }
        if (prop1.Type.Equals(prop2.Type)) { return true; }
        if (prop1.SubProperties != null && prop2.SubProperties == null) { return true; }
        if (prop1.SubProperties == null && prop2.SubProperties != null) { return true; }
        if (prop1.SubProperties != null && prop2.SubProperties != null)
        {
            if (prop1.SubProperties.Count != prop2.SubProperties.Count) { return true; }
            for (int i = 0; i < prop1.SubProperties.Count; i++)
            {
                if (CompareDataTableProperty(prop1.SubProperties.ElementAt(i).Value, prop2.SubProperties.ElementAt(i).Value)) { return true; }
            }
        }

        return false;
    }
    DataTableProperty SaveDataTableProperty(PropertyData propData, UAsset asset, Dictionary<String, String> importHolder, Dictionary<String, String> assetPackages)
    {
        String propertyName = propData.Name.ToString();
        String propertyType = propData.PropertyType.ToString();

        DataTableProperty property = new DataTableProperty { Name = propertyName, Type = propertyType };
        //Generic casting is bad but I don't know a smarter way to do it yet.
        Object propValue = propData.GetObject<Object>();
        switch (propertyType)
        {
            case "ObjectProperty":
                //Console.WriteLine("  Adding ObjectProperty...");
                ObjectPropertyData objValue = (ObjectPropertyData)propData;
                if (objValue.IsImport())
                {
                    Import objImport = objValue.ToImport(asset);
                    String objName = objImport.ObjectName.ToString();

                    importHolder.Add(objValue.Name.ToString(), objName);
                    if (!assetPackages.ContainsKey(objName)) assetPackages.Add(objName, objImport.OuterIndex.ToImport(asset).ObjectName.ToString());

                }
                property.Value = objValue.ToString();
                
                break;
            case "TextProperty":
                //Console.WriteLine("  Adding TextProperty...");
                TextPropertyData textProp = (TextPropertyData)propData;
                property.Value = textProp.ToString();
                if (textProp.CultureInvariantString != null) 
                { 
                    property.Tag = textProp.CultureInvariantString.ToString(); 
                }
                break;
            case "ArrayProperty":
                //Console.WriteLine("  Adding ArrayProperty...");
                if (propData != null)
                {
                    ArrayPropertyData arrayProp = (ArrayPropertyData)propData;
                    Dictionary<String, DataTableProperty> subProperties = new Dictionary<String, DataTableProperty>();
                    for (int i = 0; i < arrayProp.Value.Length; i++)
                    {
                        PropertyData subData = arrayProp.Value[i];
                        subProperties.Add(subData.Name.ToString(), SaveDataTableProperty(subData, asset, importHolder, assetPackages));
                    }
                    property.SubProperties = subProperties;
                }
                break;
            case "StructProperty":
                //Console.WriteLine("  Adding StructProperty...");
                if (IsList(propValue))
                {
                    List<PropertyData> subDataList = propValue as List<PropertyData>;
                    Dictionary<String, DataTableProperty> subProperties = new Dictionary<String, DataTableProperty>();
                    foreach (PropertyData subData in subDataList)
                    {
                        subProperties.Add(subData.Name.ToString(), SaveDataTableProperty(subData, asset, importHolder, assetPackages));
                    }
                    property.SubProperties = subProperties;
                }
                break;
            default:
                //Console.WriteLine("  Adding " + propData.PropertyType.ToString() + "...");
                if (propValue != null)
                {
                    property.Value = propValue.ToString();
                }
                break;
        }
        return property;
    }

    void CreateJSONsFromDatabase(UAsset asset, bool skipVanilla = false, String subFolder = "Vanilla", bool isVanilla = false)
    {
        Export assetEXP = asset.Exports[0];
        if (assetEXP is DataTableExport assetDT)
        {
            List<StructPropertyData> entries = assetDT.Table.Data;
            foreach (StructPropertyData line in entries)
            {
                //Using the name of the row of stats, we can get a name to use for the aircraft
                String aircraftName = line.Name.ToString();
                Console.WriteLine("Found " + aircraftName + "...");
                if (skipVanilla)
                {
                    if (!vanillaAircraft.Contains(aircraftName))
                    { WriteAircraftJSON(asset, subFolder, line, aircraftName, isVanilla); } else Console.WriteLine("Found Vanilla Aircraft.");
                }
                else WriteAircraftJSON(asset, subFolder, line, aircraftName, isVanilla);
            }
        }
    }

    private void WriteAircraftJSON(UAsset asset, string subFolder, StructPropertyData line, string aircraftName, bool isVanilla = false)
    {
        //Where we'll save the individual aircraft Json
        Console.WriteLine(aircraftName);

        String[] vanillaPath = new string[0];
        String[] pathToVanillaFile = new string[0];

        bool isVanillaEntry = false;

        if (!isVanilla)
        {
            pathToVanillaFile = Directory.GetDirectories(Path.Combine(ProcessedFiles, "Vanilla"), Path.GetFileName(asset.FilePath), DefaultEnumeration);
            vanillaPath = Directory.GetFiles(pathToVanillaFile[0], aircraftName + ".json", DefaultEnumeration);
            if (vanillaPath.Length != 0) isVanillaEntry = true;
        }


        Console.WriteLine("{0}: Vanilla Entry?: {1}", aircraftName, isVanillaEntry);

        String aircraftLoc = ProcessedFiles + "\\" + subFolder + "\\" + aircraftName;

        bool isIdenticalToVanilla = true;
        PWDataEntry vanillaEntry = new PWDataEntry();
        if (!isVanilla && isVanillaEntry)
        {
            String vanillaString = File.ReadAllText(vanillaPath[0]);

            vanillaEntry = JsonSerializer.Deserialize<PWDataEntry>(vanillaString, SerializerOptions);
        }
        else isIdenticalToVanilla = false;
        Dictionary<String, DataTableProperty> aircraftProps = new Dictionary<String, DataTableProperty>();
        Dictionary<String, String> aircraftAssets = new Dictionary<String, String>();
        Dictionary<String, String> assetPackages = new Dictionary<String, String>();

        foreach (PropertyData propData in line.Value)
        {
            DataTableProperty currProp = SaveDataTableProperty(propData, asset, aircraftAssets, assetPackages);
            if (isVanilla || !isVanillaEntry)
            {
                aircraftProps.Add(propData.Name.ToString(), currProp);
            }
            else
            {
                DataTableProperty vannProp = vanillaEntry.DataProperties[propData.Name.ToString()];
                if (!CompareDataTableProperty(currProp, vannProp))
                {
                    aircraftProps.Add(propData.Name.ToString(), currProp);
                    isIdenticalToVanilla = false;
                }
            }
        }

        if (!isIdenticalToVanilla)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(aircraftLoc));
            PWDataEntry aircraft = new PWDataEntry(aircraftName, aircraftAssets, aircraftProps, assetPackages);
            String aircraftJSON = JsonSerializer.Serialize(aircraft, SerializerOptions);
            File.WriteAllText(aircraftLoc + ".JSON", aircraftJSON);
        }
    }
    
    
    void refreshVanillaData()
    {
        Console.WriteLine("Creating Vanilla Processed Files Directory...");
        Directory.CreateDirectory(Path.Combine(ProcessedFiles, "Vanilla"));
        
        Console.WriteLine("Scanning Vanilla Database...");



        DatabaseExtract(vanillaDataFolder, false, true);

    }

    void extractModPacks()
    {
        //This gets us every file in the folder where we put our Paks.
        String[] pakNames = Directory.GetFiles(modPakFolder, "*.pak");
        
        //Iterate over each String
        foreach (String pakName in pakNames)
        {
            //Not entirely sure how to best create a Pak, this is what the UAssetAPI tests used, so I copied it.
            PakBuilder pakBuilder = new PakBuilder();
            FileStream pakFile = new FileStream(pakName, FileMode.Open);
            PakReader pakReader = pakBuilder.Reader(pakFile);

            String thisPaksName = Path.GetFileNameWithoutExtension(pakName);
            PWMod thisMod = new PWMod
            {
                Name = thisPaksName,
                ModFiles = new List<String>(),
                ModDataTables = new List<String>(),
                ModJSONs = new List<String>()
            };
            Console.WriteLine("Extracting files from {0}", thisPaksName);
            Console.WriteLine("Who is the author of the mod {0}?", thisPaksName);
            thisMod.Author = Console.ReadLine();

            if (!Directory.Exists(modJSONFolder)) Directory.CreateDirectory(modJSONFolder);

            String modDTDirectory = modDTFolder + "\\" + Path.GetFileNameWithoutExtension(pakName) + "\\Data\\";

            //Delete the modDTFolder first.
            if (Directory.Exists(modDTDirectory))
                Directory.Delete(modDTDirectory, true);

            //Then we want to scan each file in the pak.
            foreach (var file in pakReader.Files())
            {
                //Log the file for sanity.
                Console.WriteLine($"File: {file}");
                //Set an export location in the same structure as it was found in the file, but sorted.
                String fileLocation = modExtractedFiles + thisPaksName + "\\" + file;
                //Remember: a file in a pak is just a bunch of bytes, and the Pakreader gets us that information as it would be extracted. Neat!
                byte[] pakBytes = pakReader.Get(pakFile, file);

                //Make sure we have somewhere to write to...
                Directory.CreateDirectory(Path.GetDirectoryName(fileLocation));
                //Then write it!
                File.WriteAllBytes(fileLocation, pakBytes);
                thisMod.ModFiles.Add(file);
            }
            //Now recreate the DT Directory
            Directory.CreateDirectory(modDTFolder + "\\" + Path.GetFileNameWithoutExtension(pakName) + "\\");

            String ModDataDirectory = modExtractedFiles + thisPaksName + "\\ProjectWingman\\Content\\ProjectWingman\\Blueprints\\Data";

            if (Path.Exists(ModDataDirectory))
            {
                foreach(String dtFile in Directory.GetFiles(ModDataDirectory, "*.uasset", DefaultEnumeration))
                {
                    thisMod.ModDataTables.Add(Path.GetFileName(dtFile));
                }
                CopyDirectory(ModDataDirectory, modDTDirectory, true);
            }
            String modJSONLoc = Path.Combine(modJSONFolder, thisPaksName);
            if (Directory.Exists(modJSONLoc)) { Directory.Delete(modJSONLoc, true); }
            Directory.CreateDirectory(modJSONLoc);
            modJSONLoc = Path.Combine(modJSONLoc, "modInfoJson") + ".json";
            String modJSON = JsonSerializer.Serialize(thisMod, SerializerOptions);
            File.WriteAllText(modJSONLoc, modJSON);
        }
    }
    void DatabaseExtract(String directory, bool skipVanilla, bool isVanilla = false)
    {
        if (!Directory.Exists(ProcessedFiles)) { Directory.CreateDirectory(ProcessedFiles); }
        String[] dataFolders = Directory.GetDirectories(directory);
        foreach (String dataFolder in dataFolders)
        {
            String[] dataTables = Directory.GetFiles(dataFolder, "*.uasset", DefaultEnumeration);
            foreach (String dataTable in dataTables)
            {
                String fileSavePath;
                if (isVanilla) fileSavePath = Path.Combine("Vanilla", Path.GetFileName(dataFolder), Path.GetFileName(dataTable));
                else fileSavePath = Path.GetFileName(dataFolder) + "\\" + Path.GetFileName(dataTable);
                //Directory.CreateDirectory(fileSavePath);
                UAsset dB = new UAsset(dataTable, EngineVersion.VER_UE4_27);
                CreateJSONsFromDatabase(dB, skipVanilla, fileSavePath, isVanilla);
            }
        }
    }
    
    void handleProperty(PropertyData property, UAsset asset, DataTableProperty dataProp, PWDataEntry plane)
    {
        String propertyName = property.Name.ToString();
        String propertyType = property.PropertyType.ToString();
        Object propertyValue = property.GetObject<Object>();

        //Console.WriteLine(property.ToString());

        switch (propertyType)
        {
            case "ObjectProperty":
                if (plane.DataAssets.ContainsKey(propertyName)) 
                {
                    String assetName = plane.DataAssets[propertyName];
                    String outerName = plane.AssetPackages[assetName];

                    Import import = asset.Imports.Find(x => x.ObjectName.ToString() == assetName);
                    Import outerImport = asset.Imports.Find(x => x.ObjectName.ToString() == plane.AssetPackages[assetName]);
                    
                    import.OuterIndex = new FPackageIndex(asset.SearchForImport(outerImport.ObjectName));
                    property.SetObject(new FPackageIndex(asset.SearchForImport(import.ObjectName)));
                }
                break;
            case "ArrayProperty":
                ArrayPropertyData arrayProp = (ArrayPropertyData)property;
                for (int i = 0; i < arrayProp.Value.Length; i++)
                {
                    PropertyData subData = arrayProp.Value[i];
                    handleProperty(subData, asset, dataProp.SubProperties[subData.Name.ToString()], plane);
                }
                break;
            case "StructProperty":
                if (IsList(propertyValue))
                {
                    List<PropertyData> subDataList = propertyValue as List<PropertyData>;
                    foreach (PropertyData subData in subDataList)
                    {
                        handleProperty(subData, asset, dataProp.SubProperties[subData.Name.ToString()], plane);
                    }
                }
                break;
         /* textPropertyData.FromString is currently unimplemented. I don't know of a way to set multiple values within a TextProperty yet.
          * The safest way, thus, to handle TextProperties seems to at least for now be to not handle them. Oh well?
          * case "TextProperty":
                TextPropertyData textPropertyData = new TextPropertyData();
                textPropertyData.FromString(dataProp.Value.Split(", "), asset);
                property.SetObject(textPropertyData); break; */
            case "FloatProperty":
                property.SetObject(float.Parse(dataProp.Value)); break;
            case "IntProperty":
                property.SetObject(int.Parse(dataProp.Value)); break;
            case "DoubleProperty":
                property.SetObject(float.Parse(dataProp.Value)); break;
            case "StringProperty":
                property.SetObject(dataProp.Value); break;
            case "BoolProperty":
                property.SetObject(bool.Parse(dataProp.Value)); break;
            default:
                //Console.WriteLine(propertyType);
                //property.FromString(dataProp.Value, asset);
                break;
        }
    }

    void combineDatabases()
    {
        Console.WriteLine("What should this mod be called?");
        String combinedModname = Console.ReadLine();
        String comboModDirect = Path.Combine(createdModFolder, combinedModname);
        String comboModContent = Path.Combine(comboModDirect, "Content");

        if (Directory.Exists(comboModDirect)) Directory.Delete(comboModContent, true);
        if (Directory.Exists(comboModContent)) Directory.Delete(comboModContent, true);

        Directory.CreateDirectory(comboModDirect);
        Directory.CreateDirectory(comboModContent);

        PWMod combinedMod = new PWMod
        {
            Name = combinedModname,
            ModFiles = new List<String>(),
            ModDataTables = new List<String>(),
            ModJSONs = new List<String>(),
            Author = "Current User",
            Coauthors = new List<String>()
        };

        List<String> modifiedDatatables = new List<String>();
        List<String> includedAircraft = new List<String>();
        List<String> includedMods = new List<String>();
        List<String> combinedModAuthors = new List<String>();

        List<String> modDTPaths = new List<String>();

        String[] modDirectories = Directory.GetDirectories(modJSONFolder);

        List<String> modJsons = new List<String>();

        foreach (String modFolder in modDirectories)
        {
            String[] thisModJson = Directory.GetFiles(modFolder, "modInfoJson.json");
            if (thisModJson.Length > 0) modJsons.Add(thisModJson[0]);
        }

        foreach (String mod in modJsons)
        {
            Console.WriteLine("Load {0}?", mod);
            String includeMod = Console.ReadLine().ToLower();
            if (includeMod[0] == 'y')
            {
                String modJson = File.ReadAllText(mod);
                PWMod thisJson = JsonSerializer.Deserialize<PWMod>(modJson, SerializerOptions);

                includedMods.Add(thisJson.Name);
                String modName = Path.GetFileNameWithoutExtension(thisJson.Name);

                foreach (String dt in thisJson.ModDataTables)
                {
                    if (!modifiedDatatables.Contains(dt)) modifiedDatatables.Add(dt);
                    //Console.WriteLine("  Getting paths for {0} at {1}",dt, Path.Combine(modDTFolder, modName));
                    String[] paths = Directory.GetFiles(Path.Combine(modDTFolder, modName), "*", DefaultEnumeration);
                    //Console.WriteLine("  Found {0}", paths);
                    modDTPaths.AddRange(paths);
                }
                if (thisJson.Author != null) combinedMod.Coauthors.Add(thisJson.Author);
                foreach (string asset in thisJson.ModFiles)
                {
                    combinedMod.ModFiles.Add(asset);
                    String assetLoc = new DirectoryInfo(asset).ToString();
                    //Console.WriteLine(assetLoc);
                    String assetSaveLoc = Path.Combine(comboModContent, Path.GetDirectoryName(asset));
                    Directory.CreateDirectory(assetSaveLoc);
                    //Console.WriteLine(Path.Combine(modExtractedFiles, modName, asset));
                    File.Copy(Path.Combine(modExtractedFiles, modName, asset), Path.Combine(comboModContent, assetLoc), true);
                }
            }
        }

        Console.WriteLine("Requires the following datatables:");
        foreach (String datatable in modifiedDatatables)
        {
            Console.WriteLine("  {0},", datatable);
        }

        Console.WriteLine("\n Found Data Tables at:");
        foreach (String path in modDTPaths)
        {
            Console.WriteLine("  {0},", path);
        }

        Console.WriteLine("Force Enable Prez in each plane? \n(D will disable Presz in all planes) [Y/N/D]");
        String prezAnswer = Console.ReadLine().ToLower();
        bool modifyPrez = false;
        String forcedSeating = "0";
        if (prezAnswer[0] == 'd' || prezAnswer[0] == 'y')
        {
            modifyPrez = true;
            if (prezAnswer[0] == 'd') forcedSeating = "1";
            else forcedSeating = "2";
        }

        Console.WriteLine("Add any aircraft to DB_MF_Aircraft (used by Frontline 59?) [Y/N]");
        String f59Answer = Console.ReadLine().ToLower();
        bool addModsToF59 = false;
        bool addVanillaToF59 = false;
        if (f59Answer[0] == 'y')
        {
            addModsToF59 = true;
            Console.WriteLine("Also add vanilla aircraft to F59? [Y/N]");
            String addVan = Console.ReadLine();
            if (addVan[0] == 'y') addVanillaToF59 = true;
        }


        foreach (String combineDatatable in modifiedDatatables)
        {
            //Console.WriteLine("Handling {0}...", combineDatatable);

            UAsset f59DT = new UAsset();
            String[] f59DTPath = Directory.GetFiles(vanillaFolder, "DB_MF_Aircraft.uasset", DefaultEnumeration);
            DataTableExport f59DataTable = new DataTableExport();
            List<StructPropertyData> f59Entries = new List<StructPropertyData>();
            List<String> addedToF59 = new List<String>();


            String baseAssetPath = Directory.GetFiles(vanillaDataFolder, combineDatatable, DefaultEnumeration)[0];
            //Console.WriteLine("Found vanilla asset at {0}", baseAssetPath);

            List<String> includedEntries = new List<String>();

            UAsset baseAsset = new UAsset(baseAssetPath, EngineVersion.VER_UE4_27);

            DataTableExport baseDataTable = baseAsset.Exports[0] as DataTableExport;
            List<StructPropertyData> baseEntries = baseDataTable.Table.Data;

            foreach (StructPropertyData entry in baseEntries)
            {
                includedEntries.Add(entry.Name.ToString());

                if (modifyPrez && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                {
                    String[] pathToVanillaFile = Directory.GetDirectories(Path.Combine(ProcessedFiles, "Vanilla"), Path.GetFileName(baseAsset.FilePath), DefaultEnumeration);
                    String[] vanillaPath = Directory.GetFiles(pathToVanillaFile[0], entry.Name.ToString() + ".json", DefaultEnumeration);

                    String vanillaString = File.ReadAllText(vanillaPath[0]);

                    PWDataEntry vanillaEntry = JsonSerializer.Deserialize<PWDataEntry>(vanillaString, SerializerOptions);

                    String boneKey = "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF";
                    String prezKey = "PilotCount_2_65D06B7D492533AA2F8E9DA8254C5747";

                    DataTableProperty prezProp = new DataTableProperty
                    {
                        Name = prezKey,
                        Value = forcedSeating.ToString(),
                        Type = "IntProperty",
                        SubProperties = null,
                        Tag = null
                    };

                    vanillaEntry.DataProperties[boneKey].SubProperties[prezKey] = prezProp;

                    foreach(PropertyData property in entry.Value)
                    {
                        if (property.Name.ToString() == "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF")
                            handleProperty(property, baseAsset, vanillaEntry.DataProperties[property.Name.ToString()], vanillaEntry);
                    }

                }
            }


            if (addModsToF59 && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
            {
                f59DT = new UAsset(f59DTPath[0], EngineVersion.VER_UE4_27);
                f59DataTable = f59DT.Exports[0] as DataTableExport;
                f59Entries = f59DataTable.Table.Data;
                foreach (StructPropertyData entry in f59Entries)
                {
                    addedToF59.Add(entry.Name.ToString());
                    Console.WriteLine("Added {0} to F59 Base DT", entry.Name.ToString());
                }
            }

            foreach (String mod in includedMods)
            {
                String modName = Path.GetFileNameWithoutExtension(mod);
                String modJsonFolder = Path.Combine(ProcessedFiles, modName);
                String modDataFolder = Path.Combine(modDTFolder, modName);

                String[] paths = Directory.GetFiles(Path.Combine(modDTFolder, modName), Path.GetFileName(combineDatatable), DefaultEnumeration);

                if (paths != null && paths.Length > 0)
                {
                    string dataTableEntry = paths[0];
                    //Console.WriteLine("Found {0}", dataTableEntry);
                    UAsset modDBAC = new UAsset(dataTableEntry, EngineVersion.VER_UE4_27);

                    DataTableExport moddedDataTable = modDBAC.Exports[0] as DataTableExport;
                    List<StructPropertyData> moddedEntries = moddedDataTable.Table.Data;

                    foreach (FString name in modDBAC.GetNameMapIndexList())
                    {
                        if (!baseAsset.ContainsNameReference(name)) baseAsset.AddNameReference(name, false, false);

                    }
                    foreach (Import import in modDBAC.Imports)
                    {
                        if (baseAsset.SearchForImport(import.ObjectName) == 0) baseAsset.AddImport(import);

                    }
                    foreach (StructPropertyData tableEntry in moddedEntries)
                    {
                        String entryName = tableEntry.Name.ToString();
                        Console.WriteLine("Found " + entryName);
                        String tableEntryFileLoc = Path.Combine(ProcessedFiles, modName, combineDatatable, entryName + ".json");
                        //Console.WriteLine("Attempting to find" + tableEntryFileLoc);
                        if (Path.Exists(tableEntryFileLoc))
                        {
                            Console.WriteLine("Found " + tableEntryFileLoc);
                            String savedEntryJson = File.ReadAllText(tableEntryFileLoc);
                            //Console.WriteLine(savedAircraftJson);

                            PWDataEntry savedEntry = JsonSerializer.Deserialize<PWDataEntry>(savedEntryJson, SerializerOptions);

                            if (modifyPrez && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                            {
                                String boneKey = "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF";
                                String prezKey = "PilotCount_2_65D06B7D492533AA2F8E9DA8254C5747";

                                DataTableProperty prezProp = new DataTableProperty
                                {
                                    Name = prezKey,
                                    Value = forcedSeating.ToString(),
                                    Type = "IntProperty",
                                    SubProperties = null,
                                    Tag = null
                                };

                                if (savedEntry.DataProperties.ContainsKey(boneKey))
                                {
                                    if (savedEntry.DataProperties[boneKey].SubProperties.ContainsKey(prezKey))
                                    {
                                        savedEntry.DataProperties[boneKey].SubProperties[prezKey] = prezProp;
                                    }
                                    else
                                    {
                                        savedEntry.DataProperties[boneKey].SubProperties.TryAdd(prezKey, prezProp);
                                    }
                                }
                            }
                            else Console.WriteLine("Not editing Prez for {0} at {1}", entryName, combineDatatable);
                            if (!includedEntries.Contains(entryName))
                            {
                                Console.WriteLine("Adding {0} to datatable...", entryName);
                                includedEntries.Add(entryName);
                                baseEntries.Add(tableEntry);
                            }

                            StructPropertyData currentAircraft = baseEntries.Find(x => x.Name == tableEntry.Name);

                            if (addModsToF59 && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                            {
                                if (addVanillaToF59)
                                {
                                    if (!addedToF59.Contains(entryName))
                                    {
                                        addedToF59.Add(entryName);

                                    }
                                }
                                else if (!vanillaAircraft.Contains(entryName))
                                {
                                    if (!addedToF59.Contains(entryName)) addedToF59.Add(entryName);
                                }

                            }
                            foreach (PropertyData currentProp in currentAircraft.Value)
                            {
                                String propertyName = currentProp.Name.ToString();
                                if (savedEntry.DataProperties.ContainsKey(propertyName))
                                {
                                    DataTableProperty currentProperty = savedEntry.DataProperties[propertyName];
                                    handleProperty(currentProp, baseAsset, currentProperty, savedEntry);
                                }
                            }
                        }
                    }
                }
                else
                {
                    //If we're here, then it means that this wasn't a mod for merging datatable uassets
                    //But it could still have JSONs to patch with this datatable.
                    String modInfoJson = Path.Combine(modJsonFolder, modName, "modInfoJson.json");
                    if (File.Exists(modInfoJson))
                    {
                        PWMod jsonMod = JsonSerializer.Deserialize<PWMod>(File.ReadAllText(modInfoJson));
                        if (jsonMod.ModJSONs.Count > 0 && jsonMod.ModDataTables.Count > 0)
                        {
                            foreach (StructPropertyData tableEntry in baseEntries)
                            {
                                String entryName = tableEntry.Name.ToString();
                                String tableEntryFileLoc = Path.Combine(modJsonFolder, modName, combineDatatable, entryName + ".json");
                                if (Path.Exists(tableEntryFileLoc))
                                {
                                    Console.WriteLine("Found " + entryName);
                                    String savedEntryJson = File.ReadAllText(tableEntryFileLoc);
                                    //Console.WriteLine(savedAircraftJson);
                                    PWDataEntry savedEntry = JsonSerializer.Deserialize<PWDataEntry>(savedEntryJson, SerializerOptions);
                                    if (modifyPrez && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                                    {
                                        String boneKey = "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF";
                                        String prezKey = "PilotCount_2_65D06B7D492533AA2F8E9DA8254C5747";

                                        DataTableProperty prezProp = new DataTableProperty
                                        {
                                            Name = prezKey,
                                            Value = forcedSeating.ToString(),
                                            Type = "IntProperty",
                                            SubProperties = null,
                                            Tag = null
                                        };
                                        
                                        if (savedEntry.DataProperties.ContainsKey(boneKey))
                                        {
                                            if (savedEntry.DataProperties[boneKey].SubProperties.ContainsKey(prezKey))
                                            {
                                                savedEntry.DataProperties[boneKey].SubProperties[prezKey] = prezProp;
                                            } else
                                            {
                                                savedEntry.DataProperties[boneKey].SubProperties.TryAdd(prezKey, prezProp);
                                            }
                                        }
                                    }
                                    else Console.WriteLine("Not editing Prez for {0} at {1}", entryName, combineDatatable);
                                    if (!includedEntries.Contains(entryName))
                                    {
                                        Console.WriteLine("Adding {0} to datatable...", entryName);
                                        includedEntries.Add(entryName);
                                        baseEntries.Add(tableEntry);
                                    }

                                    StructPropertyData currentAircraft = baseEntries.Find(x => x.Name == tableEntry.Name);

                                    if (addModsToF59 && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                                    {
                                        if (addVanillaToF59)
                                        {
                                            if (!addedToF59.Contains(entryName))
                                            {
                                                addedToF59.Add(entryName);

                                            }
                                        }
                                        else if (!vanillaAircraft.Contains(entryName))
                                        {
                                            if (!addedToF59.Contains(entryName)) addedToF59.Add(entryName);
                                        }

                                    }
                                    foreach (PropertyData currentProp in currentAircraft.Value)
                                    {
                                        String propertyName = currentProp.Name.ToString();
                                        if (savedEntry.DataProperties.ContainsKey(propertyName))
                                        {
                                            DataTableProperty currentProperty = savedEntry.DataProperties[propertyName];
                                            handleProperty(currentProp, baseAsset, currentProperty, savedEntry);
                                        }
                                    }
                                } 
                                
                            }

                        }
                        
                    }
                }
                foreach (Import import in baseAsset.Imports)
                {
                    int objectNameInClassName = baseAsset.SearchForImport(import.ClassName);
                    if (objectNameInClassName != 0)
                    {
                        //Console.WriteLine(objectNameInClassName + " " + import.ClassName.ToString());
                        import.OuterIndex = baseAsset.Imports[-objectNameInClassName - 1].OuterIndex;
                    }
                }


            }


            String fileName = comboModDirect + "\\" + Path.GetFileNameWithoutExtension(combineDatatable) + ".uasset";
            baseAsset.Write(fileName);
            UAsset dtRefresh = new UAsset(fileName, EngineVersion.VER_UE4_27);
            Console.WriteLine(dtRefresh.VerifyBinaryEquality());
            dtRefresh.Write(fileName);


            if (addModsToF59)
            {
                String f59fileName = comboModDirect + "\\" + "DB_MF_Aircraft.uasset";

                f59DT = dtRefresh;

                f59DataTable = f59DT.Exports[0] as DataTableExport;
                f59Entries = f59DataTable.Table.Data;
                foreach (StructPropertyData entry in baseEntries)
                {
                    if (!addedToF59.Contains(entry.Name.ToString()))
                    {
                        f59Entries.Remove(f59Entries.Find(x => x.Name == entry.Name));
                    }
                }

                f59DT.Write(f59fileName);
                UAsset f59Ref = new UAsset(f59fileName, EngineVersion.VER_UE4_27);
                //Console.WriteLine(f59Ref.VerifyBinaryEquality());
                f59Ref.Write(f59fileName);
                addModsToF59 = false;
                addVanillaToF59 = false;
            }
        }

        String[] excessDatatables = Directory.GetFiles(comboModDirect);
        foreach (String file in excessDatatables)
        {
            if (Path.GetFileNameWithoutExtension(file) == "DB_MF_Aircraft")
            {
                String f59ModSpot = comboModDirect + "Content/ProjectWingman/Plugins/MagadanFront/Content/MagadanFront/Blueprints/Data/";
                if (!Directory.Exists(f59ModSpot)) Directory.CreateDirectory(f59ModSpot);
                File.Copy(file, f59ModSpot + Path.GetFileName(file), true);
            }
            String[] array = Directory.GetFiles(comboModContent, Path.GetFileName(file), DefaultEnumeration);
            if (array != null && array.Length > 0)
            {
                String path = array[0];
                File.Copy(file, path, true);
            }

        }

        String oldDirect = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(comboModDirect + "/Content");

        using (FileStream stream = new FileStream("output_P.pak", FileMode.Create))
        {
            var builder = new PakBuilder();
            var pak_writer = builder.Writer(stream);
            String[] modFiles = Directory.GetFiles("ProjectWingman", "*", DefaultEnumeration);
            foreach (String file in modFiles)
            {
                String fileFixed = file.Replace("\\", "/");
                pak_writer.WriteFile(fileFixed, File.ReadAllBytes(file));
            }


            pak_writer.WriteIndex();
        }
        //Process.Start("UnrealPak-With-Compression.bat", comboModContent);
        Directory.SetCurrentDirectory(oldDirect);
    }

    private static void Main(string[] args)
    {
        Program program = new Program();
        bool managerContinue = true;
        Console.WriteLine("Project Assetmanager is away.");

        Console.WriteLine("Checking for necessary directories...");
        bool firstRun = false;
        if (!Directory.Exists(program.ProcessedFiles)) { firstRun = true; Directory.CreateDirectory("Processed Files\\"); }
        if (!Directory.Exists(program.modJSONFolder)) { firstRun = true; Directory.CreateDirectory("Mod JSONs\\"); }
        if (!Directory.Exists(program.modPakFolder)) { firstRun = true; Directory.CreateDirectory("Mod Paks\\"); }
        if (!Directory.Exists(program.modDTFolder)) { firstRun = true; Directory.CreateDirectory("Mod DataTables\\"); }
        if (!Directory.Exists(program.modExtractedFiles)) { firstRun = true; Directory.CreateDirectory("Mod Extracted Files\\"); }
        if (!Directory.Exists(program.vanillaFolder)) { firstRun = true; Directory.CreateDirectory("Vanilla Files\\"); }
        if (!Directory.Exists(program.vanillaDataFolder)) { firstRun = true; Directory.CreateDirectory("Vanilla Files\\Vanilla Data"); }
        if (!Directory.Exists(program.f59DataFolder)) { firstRun = true; Directory.CreateDirectory("Vanilla Files\\F59 Data"); }
        if (!Directory.Exists(program.createdModFolder)) { firstRun = true; Directory.CreateDirectory("Created Mods\\"); }

        if (Directory.GetDirectories(program.vanillaFolder).Length < 2 || firstRun)
        {
            Console.WriteLine("Missing necessary vanilla Project Wingman files! Extract them now? [Y/N]");
            String grabVanilla = Console.ReadLine().ToLower();
            switch (grabVanilla[0])
            {
                case 'y':
                    Console.WriteLine("Please paste the Project Wingman\\ProjectWingman\\Content\\Paks folder here...");
                    String pwAssetAddress = Console.ReadLine();
                    String[] pwMainPaks = Directory.GetFiles(pwAssetAddress);

                    Console.WriteLine("Found {0} paks!", pwMainPaks.Length);

                    String ProjectWingmanDataPath = "Content/ProjectWingman/Blueprints/Data";
                    String F59DataPath = "Plugins/MagadanFront/Content/MagadanFront/Blueprints/Data";

                    Console.WriteLine(F59DataPath);
                    Console.WriteLine(ProjectWingmanDataPath);

                    foreach (String pak in pwMainPaks)
                    {
                        PakBuilder pakBuilder = new PakBuilder();
                        FileStream pakFile = new FileStream(pak, FileMode.Open);
                        PakReader pakReader = pakBuilder.Reader(pakFile);
                        foreach (var file in pakReader.Files())
                        {
                            if (file.Contains(F59DataPath,StringComparison.InvariantCultureIgnoreCase))
                            {
                                String destString = Path.Combine(program.f59DataFolder, file.Substring(F59DataPath.Length).TrimStart(Path.DirectorySeparatorChar));

                                byte[] pakBytes = pakReader.Get(pakFile, file);

                                //Make sure we have somewhere to write to...
                                Directory.CreateDirectory(Path.GetDirectoryName(destString));
                                //Then write it!
                                File.WriteAllBytes(destString, pakBytes);

                            } else if (file.Contains(ProjectWingmanDataPath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                String destString = Path.Combine(program.vanillaDataFolder, file.Substring(ProjectWingmanDataPath.Length).TrimStart(Path.DirectorySeparatorChar));

                                byte[] pakBytes = pakReader.Get(pakFile, file);

                                //Make sure we have somewhere to write to...
                                Directory.CreateDirectory(Path.GetDirectoryName(destString));
                                //Then write it!
                                File.WriteAllBytes(destString, pakBytes);
                            } 
                        }
                    }
                    break;
                case 'n':
                default:
                    Console.WriteLine("This program does not work without them. Now qutting PAM.");
                    managerContinue = false;
                    break;
            }
        }

        while (managerContinue)
        {
            Console.WriteLine
                (
                "Enter [R] to Refresh Vanilla Files (needed first time)\n" +
                "Enter [E] to Extract Mod Paks. \n" +
                "Enter [B] to Build Individual Databases from Modded DB_Aircraft's\n" +
                "Enter [C] to Create a Combined Mod\n" +
                "Enter [Q] to Quit.\n"
                );
            string managerChoice = Console.ReadLine();
            if (managerChoice is null)
            {
                Console.WriteLine("Null! Null! Null!");
                managerContinue = false;
            }
            else
            {
                managerChoice = managerChoice.ToLower();
                switch (managerChoice[0])
                {
                    case 'r':
                        program.refreshVanillaData();
                        break;
                    case 'e':
                        program.extractModPacks();
                        break;
                    case 'b':
                        Console.WriteLine("Skip Vanilla Playable Aircraft?\n" +
                            "Skipping Vanilla Playable Aircraft will make processing the Datatables in the future faster.\n" +
                            "This comes at the cost, of course, of not including their information\n" +
                            "(in case the extracted mods altered them at all)\n\n" +
                            "On most modern PCs, however, the time saved is likely negligible.\n\n" + 
                            "[Y/N]");
                        string skipChoice = Console.ReadLine();
                        switch (skipChoice[0])
                        {
                            case 'y':
                                program.DatabaseExtract(program.modDTFolder, true); 
                                break;
                            case 'n':
                                program.DatabaseExtract(program.modDTFolder, false);
                                break;
                        }
                        break;
                    case 'c':
                        program.combineDatabases();
                        break;
                    case 'q':
                        managerContinue = false;
                        break;
                    default:
                        Console.WriteLine("Invalid entry!");
                        break;
                }
            }
        }
    }
}