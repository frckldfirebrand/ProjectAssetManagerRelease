using System.Collections;
using System.ComponentModel;
using System.Data;
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

    class PWMod
    {
        public PWMod() { }
        public String Name;
        public String Author;
        public List<String> Coauthors;
        public List<String>? ModFiles;
        public List<String>? ModDataTables;
        public List<String>? ModJSONs;
    }
    class PWDataTable
    {
        public PWDataTable() { }
        String Name;
        List<PWDataEntry> DataEntries;
    }
    class PWDataEntry
    {
        [JsonConstructor]
        public PWDataEntry(String Name, Dictionary<String, String> DataAssets, Dictionary<String, DataTableProperty> DataProperties, Dictionary<String, String>? AssetPackages)
        {
            this.Name = Name;
            this.DataAssets = DataAssets;
            this.DataProperties = DataProperties;
            this.AssetPackages = AssetPackages;
        }
        [JsonPropertyName("Name")]
        public String? Name { get; set; }
        [JsonPropertyName("DataAssets")]
        public Dictionary<String, String>? DataAssets { get; set; }
        [JsonPropertyName("AssetPackages")]
        public Dictionary<String, String>? AssetPackages { get; set; }
        [JsonPropertyName("DataProperties")]
        public Dictionary<String, DataTableProperty>? DataProperties { get; set; }
    }

    class DataTableProperty
    {
        public DataTableProperty() { }
        public DataTableProperty(String 
            name, String type,  String? value = null, Dictionary<String, DataTableProperty>? subprop = null, String? tag = null)
        {
            Name = name;
            Type = type;
            if (value != null) Value = value;
            if (subprop != null) SubProperties = subprop;
            if (tag != null) Tag = tag;
        }
        String Name { get; set; }
        String Type { get; set; }
        public String? Value { get; set; }
        public Dictionary<String, DataTableProperty>? SubProperties { get; set; }
        public String? Tag { get; set; }
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
    String PlaneClassFolder = "Plane Files\\";
    String vanillaDataFolder = "Vanilla Files\\Vanilla Data";
    String vanillaDBACLoc = "Vanilla Files\\VanillaDBAC.uasset";
    String vanillaHangarLoc = "Vanilla Files\\VanillaHangar.uasset";
    String createdModFolder = "Created Mods\\";


    //There's better ways to do this, such as reading from a file or something more dynamically; however, fuck you.
    readonly List<String> vanillaAircraft = new List<String>
    {
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
        "SU-37",
        "F-35",
        "F-15SMTD",
        "F-22",
        "ACG-01",
        "SPEAR",
        "PW-001",
        "CF-105_2",
        "MiG-21_2",
        "AV-8_2",
    };

    DataTableProperty SaveDataTableProperty(PropertyData propData, UAsset asset, Dictionary<String, String> importHolder, Dictionary<String, String> assetPackages)
    {
        String propertyName = propData.Name.ToString();
        String propertyType = propData.PropertyType.ToString();

        DataTableProperty property = new DataTableProperty(propertyName, propertyType);
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

    void CreateJSONsFromDatabase(UAsset asset, bool skipVanilla = false, String subFolder = "Vanilla")
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
                    { WriteAircraftJSON(asset, subFolder, line, aircraftName); } else Console.WriteLine("Found Vanilla Aircraft.");
                }
                else WriteAircraftJSON(asset, subFolder, line, aircraftName);
            }
        }
    }

    private void WriteAircraftJSON(UAsset vanilla, string subFolder, StructPropertyData line, string aircraftName)
    {
        //Where we'll save the individual aircraft Json
        String aircraftLoc = ProcessedFiles + "//" + subFolder + "//" + aircraftName;
        Dictionary<String, DataTableProperty> aircraftProps = new Dictionary<String, DataTableProperty>();
        Dictionary<String, String> aircraftAssets = new Dictionary<String, String>();
        Dictionary<String, String> assetPackages = new Dictionary<String, String>();
        foreach (PropertyData propData in line.Value)
        {
            aircraftProps.Add(propData.Name.ToString(), SaveDataTableProperty(propData, vanilla, aircraftAssets, assetPackages));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(aircraftLoc));
        PWDataEntry aircraft = new PWDataEntry(aircraftName, aircraftAssets, aircraftProps, assetPackages);
        String aircraftJSON = JsonSerializer.Serialize(aircraft, SerializerOptions);
        File.WriteAllText(aircraftLoc + ".JSON", aircraftJSON);
    }
    
    void refreshVanillaData(UAsset vanilla, UAsset hangar)
    {
        Console.WriteLine("Saving Serialized JSON Copy of Datatable...");
        String jsonSerializedAsset = vanilla.SerializeJson(true);
        File.WriteAllText(vanillaFolder + "vanillaDBAC.JSON", jsonSerializedAsset);
        Console.WriteLine("Saving Serialized JSON Copy of Hangar Text Table...");
        jsonSerializedAsset = hangar.SerializeJson(true);
        File.WriteAllText(vanillaFolder + "vanillaHangar.JSON", jsonSerializedAsset);
        Console.WriteLine("Exported full Asset as JSON");

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
            String modJSONLoc = modJSONFolder + thisPaksName + ".json";
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
                if (isVanilla) fileSavePath = "Vanilla\\" + Path.GetFileName(dataFolder) + "\\" + Path.GetFileName(dataTable);
                else fileSavePath = Path.GetFileName(dataFolder) + "\\" + Path.GetFileName(dataTable);
                UAsset dB = new UAsset(dataTable, EngineVersion.VER_UE4_27);
                CreateJSONsFromDatabase(dB, skipVanilla, fileSavePath);
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

        String[] mods = Directory.GetFiles(modJSONFolder);

        foreach (String mod in mods)
        {
            Console.WriteLine("Load {0}?", mod);
            String includeMod = Console.ReadLine().ToLower();
            if (includeMod[0] == 'y')
            {
                String modName = Path.GetFileNameWithoutExtension(mod);
                includedMods.Add(mod);
                String modJson = File.ReadAllText(mod);
                PWMod thisJson = JsonSerializer.Deserialize<PWMod>(modJson, SerializerOptions);
                foreach (String dt in thisJson.ModDataTables)
                {
                    if (!modifiedDatatables.Contains(dt)) modifiedDatatables.Add(dt);
                    //Console.WriteLine("  Getting paths for {0} at {1}",dt, Path.Combine(modDTFolder, modName));
                    String[] paths = Directory.GetFiles(Path.Combine(modDTFolder, modName), "*.uasset", DefaultEnumeration);
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
        foreach(String path in modDTPaths)
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
            Console.WriteLine("Also add vanilla aircraft to F59?");
            String addVan = Console.ReadLine();
            if (addVan[0] == 'y') addVanillaToF59 = true;
        }


        foreach (String combineDatatable in modifiedDatatables)
        {
            //Console.WriteLine("Handling {0}...", combineDatatable);

            UAsset f59DT = new UAsset();
            String[] f59DTPath = Directory.GetFiles(vanillaFolder, "DB_MF_Aircraft.json", DefaultEnumeration);
            DataTableExport f59DataTable = new DataTableExport();
            List<StructPropertyData> f59Entries = new List<StructPropertyData>();

            if (addModsToF59 && Path.GetFileName(combineDatatable) == "DB_Aircraft.json")
            {
                f59DT = new UAsset(f59DTPath[0], EngineVersion.VER_UE4_27);
                f59DataTable = f59DT.Exports[0] as DataTableExport;
                f59Entries = f59DataTable.Table.Data;
            }

            String baseAssetPath = Directory.GetFiles(vanillaDataFolder, combineDatatable, DefaultEnumeration)[0];
            //Console.WriteLine("Found vanilla asset at {0}", baseAssetPath);

            List<String> includedEntries = new List<String>();
            
            UAsset baseAsset = new UAsset(baseAssetPath, EngineVersion.VER_UE4_27);

            DataTableExport baseDataTable = baseAsset.Exports[0] as DataTableExport;
            List<StructPropertyData> baseEntries = baseDataTable.Table.Data;

            foreach (StructPropertyData entry in baseEntries)
            {
                includedEntries.Add(entry.Name.ToString());
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
                        Console.WriteLine("Found " + tableEntry.Name);
                        String tableEntryFileLoc = Path.Combine(ProcessedFiles, modName, combineDatatable, tableEntry.Name + ".json");
                        //Console.WriteLine("Attempting to find" + tableEntryFileLoc);
                        if (Path.Exists(tableEntryFileLoc))
                        {
                            Console.WriteLine("Found " + tableEntryFileLoc);
                            String savedEntryJson = File.ReadAllText(tableEntryFileLoc);
                            //Console.WriteLine(savedAircraftJson);

                            PWDataEntry savedEntry = JsonSerializer.Deserialize<PWDataEntry>(savedEntryJson, SerializerOptions);

                            if (modifyPrez && Path.GetFileName(combineDatatable) == "DB_Aircraft.uasset")
                            {
                                savedEntry.DataProperties[
                                    "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF"].SubProperties[
                                    "PilotCount_2_65D06B7D492533AA2F8E9DA8254C5747"].Value = forcedSeating.ToString();
                            }
                            else Console.WriteLine("Not editing Prez for {0} at {1}", tableEntry.Name, combineDatatable);
                            if (!includedEntries.Contains(tableEntry.Name.ToString()))
                            {
                                Console.WriteLine("Adding {0} to datatable...", tableEntry.Name.ToString());
                                includedEntries.Add(tableEntry.Name.ToString());
                                baseEntries.Add(tableEntry);
                            }

                            if (addModsToF59)
                            {
                                if (addVanillaToF59)
                                {

                                }
                            }

                            StructPropertyData currentAircraft = baseEntries.Find(x => x.Name == tableEntry.Name);
                            foreach (PropertyData currentProp in currentAircraft.Value)
                            {
                                String propertyName = currentProp.Name.ToString();
                                DataTableProperty currentProperty = savedEntry.DataProperties[propertyName];
                                handleProperty(currentProp, baseAsset, currentProperty, savedEntry);
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
            }
            
            String fileName = comboModDirect + "//" + Path.GetFileNameWithoutExtension(combineDatatable) + ".uasset";
            baseAsset.Write(fileName);
            UAsset dtRefresh = new UAsset(fileName, EngineVersion.VER_UE4_27);
            Console.WriteLine(dtRefresh.VerifyBinaryEquality());
            dtRefresh.Write(fileName);



        }


    }

    private static void Main(string[] args)
    {
        Program program = new Program();
        bool managerContinue = true;
        Console.WriteLine("Project Assetmanager is away.");
        UAsset vanillaDBAC = new UAsset(program.vanillaDBACLoc,EngineVersion.VER_UE4_27);
        UAsset vanillaHangar = new UAsset(program.vanillaHangarLoc, EngineVersion.VER_UE4_27);
        
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
                        program.refreshVanillaData(vanillaDBAC, vanillaHangar);
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