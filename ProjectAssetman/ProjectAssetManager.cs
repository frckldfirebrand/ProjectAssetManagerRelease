using ProjectAssetman;
using System.Collections;
using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace ProjectAssetman;

internal class ProjectAssetManager
{
    UserSettings user = new UserSettings();
    String UserSettingsFilePath = "UserSettings.json";
    
    static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
    static EnumerationOptions DefaultEnumeration = new EnumerationOptions { RecurseSubdirectories = true };

    Dictionary<String, String> coreFiles = new Dictionary<String, String>
    {
     {"ProcessedFiles" , "Processed Files\\" },
     {"modJSONFolder" , "Mod JSONs\\"},
     {"modPakFolder" , "Mod Paks\\"},
     {"modDTFolder" , "Mod DataTables\\"},
     {"modExtractedFiles" , "Mod Extracted Files\\"},
     {"vanillaFolder" , "Vanilla Files\\"},
     {"vanillaDataFolder" , "Vanilla Files\\Vanilla Data"},
     {"f59DataFolder" , "Vanilla Files\\F59 Data"},
     {"createdModFolder" , "Created Mods\\"},
    };

    public String ProcessedFiles = "Processed Files\\";
    public String modJSONFolder = "Mod JSONs\\";
    public String modPakFolder = "Mod Paks\\";
    public String modDTFolder = "Mod DataTables\\";
    public String modExtractedFiles = "Mod Extracted Files\\";
    public String vanillaFolder = "Vanilla Files\\";
    public String vanillaDataFolder = "Vanilla Files\\Vanilla Data";
    public String f59DataFolder = "Vanilla Files\\F59 Data";
    public String createdModFolder = "Created Mods\\";


    //There's better ways to do this, such as reading from a file or something more dynamically; however, fuck you.
    public static readonly List<String> vanillaAircraft = new List<String>
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
        if (!prop1.Name.Equals(prop2.Name)) { return false; }
        if (!prop1.Tag.Equals(prop2.Tag)) { return false; }
        if (!prop1.Value.Equals(prop2.Value)) { return false; }
        if (!prop1.Type.Equals(prop2.Type)) { return false; }
        if (prop1.SubProperties != null && prop2.SubProperties == null) { return false; }
        if (prop1.SubProperties == null && prop2.SubProperties != null) { return false; }
        if (prop1.SubProperties != null && prop2.SubProperties != null)
        {
            if (prop1.SubProperties.Count != prop2.SubProperties.Count) { return false; }
            for (int i = 0; i < prop1.SubProperties.Count; i++)
            {
                String subProp = prop1.SubProperties.ElementAt(i).Key;
                if (!CompareDataTableProperty(prop1.SubProperties[subProp], prop2.SubProperties[subProp])) { return false; }
            }
        }

        return true;
    }
    DataTableProperty SaveDataTableProperty(PropertyData propData, UAsset asset, Dictionary<String, String> importHolder, Dictionary<String, String> assetPackages)
    {
        String propertyName = propData.Name.ToString();
        String propertyType = propData.PropertyType.ToString();

        DataTableProperty property = new DataTableProperty { Name = propertyName, Type = propertyType };
        //Generic casting is bad but I don't know a smarter way to do it yet.
        Object propValue = propData.GetObject<Object>();
        if (propValue != null) property.UassetString = propData.ToString();

        switch (propertyType)
        {
            case "ObjectProperty":
                //Console.WriteLine("  Adding ObjectProperty...");
                ObjectPropertyData objValue = (ObjectPropertyData)propData;
                if (objValue.IsImport())
                {
                    Import objImport = objValue.ToImport(asset);
                    String objName = objImport.ObjectName.ToString();
                    property.Tag = objImport.ObjectName.ToString();

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
                        subProperties[subData.Name.ToString()].Tag = propertyName;
                    }
                    property.SubProperties = subProperties;
                }
                break;
            case "StructProperty":
                //Console.WriteLine("  Adding StructProperty...");
                if (HelperFunctions.IsList(propValue))
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
                Console.WriteLine("  Found " + aircraftName + "...");
                if (skipVanilla)
                {
                    if (!vanillaAircraft.Contains(aircraftName))
                    { WriteAircraftJSON(asset, subFolder, line, aircraftName, isVanilla); }
                    else Console.WriteLine("  Found Vanilla Aircraft.");
                }
                else WriteAircraftJSON(asset, subFolder, line, aircraftName, isVanilla);
            }
        }
    }

    private void WriteAircraftJSON(UAsset asset, string subFolder, StructPropertyData line, string aircraftName, bool isVanilla = false)
    {

        String vanillaPath = "";
        String[] pathToVanillaFile = new string[0];

        bool isVanillaEntry = false;

        if (!isVanilla)
        {
            pathToVanillaFile = Directory.GetDirectories(Path.Combine(ProcessedFiles, "Vanilla"), Path.GetFileName(asset.FilePath), DefaultEnumeration);
            //Console.WriteLine(pathToVanillaFile[0]);
            String[] files = Directory.GetFiles(pathToVanillaFile[0]);
            List<String> vanillaFolderFiles = new List<String>();
            vanillaFolderFiles.AddRange(files);
            vanillaPath = vanillaFolderFiles.Find(x => Path.GetFileName(x).ToLower() == (aircraftName + ".json").ToLower());
            if (vanillaPath != null)
            {
                isVanillaEntry = true;
                Console.WriteLine("  Comparing this to {0}...", vanillaPath[0]);
            }
        }

        Console.WriteLine("  {0}: Vanilla Entry?: {1}", aircraftName, isVanillaEntry);

        //Where we'll save the individual aircraft Json
        String aircraftLoc = ProcessedFiles + "\\" + subFolder + "\\" + aircraftName;

        bool isIdenticalToVanilla = true;
        PWDataEntry vanillaEntry = new PWDataEntry();
        if (!isVanilla && isVanillaEntry)
        {
            String vanillaString = File.ReadAllText(vanillaPath);
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
        else Console.WriteLine("  Entry {0} was found to be identical to vanilla! Skipping...", aircraftName);
    }


    void refreshVanillaData()
    {
        Console.WriteLine("Creating Vanilla Processed Files Directory...");
        Directory.CreateDirectory(Path.Combine(ProcessedFiles, "Vanilla"));

        Console.WriteLine("Scanning Vanilla Database...");

        DatabaseExtract(vanillaDataFolder, false, true);
        DatabaseExtract(f59DataFolder, false, true);
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

            //Then we want to scan each file in the pak.
            foreach (var file in pakReader.Files())
            {
                //Log the file for sanity.
                Console.WriteLine($"File: {file}");
                //Set an export location in the same structure as it was found in the file, but sorted.
                String fileLocation = Path.Combine(modExtractedFiles, thisPaksName, file);
                //Remember: a file in a pak is just a bunch of bytes, and the Pakreader gets us that information as it would be extracted. Neat!
                byte[] pakBytes = pakReader.Get(pakFile, file);

                //Make sure we have somewhere to write to...
                Directory.CreateDirectory(Path.GetDirectoryName(fileLocation));
                //Then write it!
                File.WriteAllBytes(fileLocation, pakBytes);
                thisMod.ModFiles.Add(file);
            }
            //Now recreate the DT Directory
            Directory.CreateDirectory(Path.Combine(modDTFolder, thisPaksName));

            String modFilesDirectory = Path.Combine(modExtractedFiles, thisPaksName);

            CopyDataFromMod(modFilesDirectory, thisMod);


            String modJSONLoc = Path.Combine(modJSONFolder, thisPaksName);
            if (Directory.Exists(modJSONLoc)) { Directory.Delete(modJSONLoc, true); }
            Directory.CreateDirectory(modJSONLoc);
            modJSONLoc = Path.Combine(modJSONLoc, "modInfoJson") + ".json";
            String modJSON = JsonSerializer.Serialize(thisMod, SerializerOptions);
            File.WriteAllText(modJSONLoc, modJSON);

            if (user.AutomateDatabases)
            {
               DatabaseExtract(modDTFolder, false);
            }
        }
    }


    void CopyDataFromMod(String directory, PWMod mod)
    {
        String modDTDirectory = Path.Combine(modDTFolder, mod.Name);
        //Delete the modDTFolder first.
        if (Directory.Exists(modDTDirectory))
            Directory.Delete(modDTDirectory, true);

        Directory.CreateDirectory(modDTDirectory);

        String[] DataFolders = Directory.GetDirectories(directory, "Data", DefaultEnumeration);
        foreach (String DataFolder in DataFolders)
        {
            String[] DataTables = Directory.GetFiles(DataFolder, "*.*", DefaultEnumeration);
            foreach (String DataTable in DataTables)
            {
                String thisDataTablesName = Path.GetFileNameWithoutExtension(DataTable);
                if ((Path.GetExtension(thisDataTablesName) == string.Empty))
                {
                    if (!mod.ModDataTables.Contains(thisDataTablesName + ".uasset")) mod.ModDataTables.Add(thisDataTablesName + ".uasset");
                }
                File.Copy(DataTable, Path.Combine(modDTDirectory, Path.GetFileName(DataTable)), true);
            }
        }
    }

    void DatabaseExtract(String directory, bool skipVanilla, bool isVanilla = false)
    {
        if (!Directory.Exists(ProcessedFiles)) { Directory.CreateDirectory(ProcessedFiles); }
        String[] dataFolders = Directory.GetDirectories(directory);
        foreach (String dataFolder in dataFolders)
        {
            Console.WriteLine("Working on Datatables in the Folder {0}", dataFolder);
            String[] dataTables = Directory.GetFiles(dataFolder, "*.uasset", DefaultEnumeration);
            foreach (String dataTable in dataTables)
            {
                Console.WriteLine(" Now scanning Data Table: {0}", dataTable);
                String fileSavePath;
                if (isVanilla) fileSavePath = Path.Combine("Vanilla", Path.GetFileName(dataFolder), Path.GetFileName(dataTable));
                else fileSavePath = Path.GetFileName(dataFolder) + "\\" + Path.GetFileName(dataTable);
                //Directory.CreateDirectory(fileSavePath);
                UAsset dB = new UAsset(dataTable, EngineVersion.VER_UE4_27);
                CreateJSONsFromDatabase(dB, skipVanilla, fileSavePath, isVanilla);
            }
        }
    }

    PropertyData CreateSubPropertyFromJSON(DataTableProperty property, UAsset asset, string name)
    {
        switch(property.Type)
        {
            default:
                String[] fromString = { property.UassetString };
                ObjectPropertyData returner = new ObjectPropertyData();
                returner.Name = FName.DefineDummy(asset, name);
                returner.FromString(fromString, asset);
                return returner;
        }
    }
    void handleProperty(PropertyData property, UAsset asset, DataTableProperty dataProp, PWDataEntry plane, Dictionary<String, FPackageIndex> imports)
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
                    /* if (imports.ContainsKey(plane.DataAssets[propertyName]))
                    {
                        String assetName = plane.DataAssets[propertyName];
                        Import imported = imports[plane.DataAssets[propertyName]].ToImport(asset);
                        if (plane.AssetPackages.ContainsKey(assetName)) imported.OuterIndex = imports[assetName];
                        property.SetObject(imports[plane.DataAssets[propertyName]]);
                        //Console.WriteLine(imported.ObjectName);
                    } */
                    
                    Console.WriteLine("   Looking for internal Object Value...");
                    String assetName = plane.DataAssets[propertyName];
                    Console.WriteLine("    Using {0}", assetName);
                    String outerName = plane.AssetPackages[assetName];
                    Console.WriteLine("    Which should be part of {0}", outerName);

                    Import import = asset.Imports.Find(x => x.ObjectName.ToString() == assetName);
                    FPackageIndex importLoc = FPackageIndex.FromRawIndex(asset.SearchForImport(import.ObjectName));

                    import.ObjectName.Value = new FString(assetName);

                    Console.WriteLine($"   Import is {import.ToString()}, has Class Name {import.ClassName}, Class Package {import.ClassPackage}, and Object Name {import.ObjectName}\n" +
                        $"   It's value is {importLoc}.");

                    Import outerImport = asset.Imports.Find(x => x.ObjectName.ToString() == plane.AssetPackages[assetName]);
                    import.OuterIndex = new FPackageIndex(asset.SearchForImport(outerImport.ObjectName));
                    property.SetObject(importLoc);
                }
                break;
            case "ArrayProperty":
                ArrayPropertyData arrayProp = (ArrayPropertyData)property;
                if (arrayProp.Value != null)
                {
                    if (arrayProp.Value.Length != dataProp.SubProperties.Count)
                    {
                        PropertyData[] newArray = arrayProp.Value.Clone() as PropertyData[];
                        Array.Resize(ref newArray, dataProp.SubProperties.Count);
                        for (int i = 0; i < newArray.Length; i++)
                        {
                            newArray[i] = CreateSubPropertyFromJSON(dataProp.SubProperties[i.ToString()], asset, i.ToString());
                        }
                        arrayProp.Value = newArray;
                    }
                    Console.WriteLine("   Found non-null ArrayProp...");
                    List<PropertyData> subDataList = arrayProp.Value.ToList();
                    foreach (PropertyData subData in subDataList)
                    {
                        //StrPropertyData strData = subData as StrPropertyData;
                        //if (strData != null) Console.WriteLine("    Iterating over ArrayProp Subdata {0} with value {1}...", subData.Name.ToString(), strData.Value);
                        //Console.WriteLine("    Using {0} from JSON which has value {1}", dataProp.SubProperties[subData.Name.ToString()].Name, dataProp.SubProperties[subData.Name.ToString()].Value);
                        handleProperty(subData, asset, dataProp.SubProperties[subData.Name.ToString()], plane, imports);
                    }
                }
                break;
            case "StructProperty":
                if (HelperFunctions.IsList(propertyValue))
                {
                    List<PropertyData> subDataList = propertyValue as List<PropertyData>;
                    foreach (PropertyData subData in subDataList)
                    {
                        handleProperty(subData, asset, dataProp.SubProperties[subData.Name.ToString()], plane, imports);
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
            case "StrProperty":
                property.SetObject(new FString(dataProp.Value)); break;
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
            Author = user.UserID,
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
            Console.WriteLine();
            char userInput = HelperFunctions.GetUserInput(new char[] { 'y', 'n' }, $"Load {mod}?");
            if (userInput == 'y')
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

        char prezAnswer = HelperFunctions.GetUserInput("YND".ToCharArray(), "Force Enable Prez in each plane? \n (D will disable Presz in all planes)");
        bool modifyPrez = false;
        String forcedSeating = "0";
        if (prezAnswer == 'd' || prezAnswer == 'y')
        {
            modifyPrez = true;
            if (prezAnswer == 'd') forcedSeating = "1";
            else forcedSeating = "2";
        }
        char f59Answer = HelperFunctions.GetUserInput("YN".ToCharArray(), "Add any aircraft to DB_MF_Aircraft (used by Frontline 59?)");

        bool addModsToF59 = false;
        bool addVanillaToF59 = false;
        if (f59Answer == 'y')
        {
            addModsToF59 = true;
            char addVan = HelperFunctions.GetUserInput("YN".ToCharArray(), "Also add vanilla aircraft to F59?");
            if (addVan == 'y') addVanillaToF59 = true;
        }
        
        
        Dictionary<char, String> modNames = new Dictionary<char, String>();
        Dictionary<char, String> modNamesDisplay = new Dictionary<char, String>();
        

        List<String> loadorder = new List<String>();

        if (HelperFunctions.GetUserInput("YN".ToCharArray(), "ADVANCED: Use a custom loadorder?") == 'y')
        {
            String loadPrompt = "Which mod should be loaded now?\n";
            for (char letter = 'a'; letter < 'a' + includedMods.Count; letter++)
            {
                modNames.Add(letter, includedMods[letter - 'a']);
                modNamesDisplay.Add(letter, $"[{letter}] - {includedMods[letter - 'a']}");
            }
            while (modNames.Count > 0)
            {
                char modAnswer = HelperFunctions.GetUserInput(modNames.Keys.ToArray(), loadPrompt + String.Join("\n", modNamesDisplay.Values));
                loadorder.Add(modNames[modAnswer]);
                modNames.Remove(modAnswer);
                modNamesDisplay.Remove(modAnswer);
            }
        }
        else loadorder = includedMods;
        
        
        
        bool overrideVan = false;
        String overrideMod = "";

        if (HelperFunctions.GetUserInput("YN".ToCharArray(), "ADVANCED: Use a mod's datatable instead of vanillas? Can help with compatability.") == 'y')
        {
            overrideVan = true;
            for (char letter = 'a'; letter < 'a' + includedMods.Count; letter++)
            {
                modNames.Add(letter, includedMods[letter - 'a']);
                modNamesDisplay.Add(letter, $"[{letter}] - {includedMods[letter - 'a']}");
            }

            overrideMod = modNames[HelperFunctions.GetUserInput(modNames.Keys.ToArray(), "Which mod to use for base?" + String.Join("\n", modNamesDisplay.Values))];
        }
        

        foreach (String combineDatatable in modifiedDatatables)
        {
            Console.WriteLine("Handling {0}...", combineDatatable);

            UAsset f59DT = new UAsset();
            String[] f59DTPath = Directory.GetFiles(vanillaFolder, "DB_MF_Aircraft.uasset", DefaultEnumeration);
            DataTableExport f59DataTable = new DataTableExport();
            List<StructPropertyData> f59Entries = new List<StructPropertyData>();
            List<String> addedToF59 = new List<String>();


            String baseAssetPath = Directory.GetFiles(vanillaFolder, combineDatatable, DefaultEnumeration)[0];
            //Console.WriteLine("Found vanilla asset at {0}", baseAssetPath);

            List<String> includedEntries = new List<String>();
            Dictionary<String, List<PWDataEntry>> modifiedBy = new Dictionary<String, List<PWDataEntry>>();

            if (overrideVan)
            {
                String[] paths = Directory.GetFiles(Path.Combine(modDTFolder, overrideMod), Path.GetFileName(combineDatatable), DefaultEnumeration);
                if (paths != null && paths.Length > 0)
                {
                    baseAssetPath = paths[0];
                }
            }

            UAsset baseAsset = new UAsset(baseAssetPath, EngineVersion.VER_UE4_27);

            DataTableExport baseDataTable = baseAsset.Exports[0] as DataTableExport;
            List<StructPropertyData> baseEntries = baseDataTable.Table.Data;

            Dictionary<String, FPackageIndex> importMap = new Dictionary<String, FPackageIndex>();
            Dictionary<String, int> nameMap = new Dictionary<String, int>();

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

                    foreach (PropertyData property in entry.Value)
                    {
                        if (property.Name.ToString() == "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF")
                            handleProperty(property, baseAsset, vanillaEntry.DataProperties[property.Name.ToString()], vanillaEntry, importMap);
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

            foreach (String mod in loadorder)
            {
                
                Console.WriteLine(" Handling {0} values...", mod);
                
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
                        if (!baseAsset.ContainsNameReference(name))
                        {
                            FString newName = new FString(name.ToString());
                            int index = baseAsset.AddNameReference(newName);
                            nameMap.Add(newName.ToString(), index);
                            Console.WriteLine("  Added {0} to name map at {1}!", newName.ToString(), index);
                        }

                    }

                    foreach (Import import in modDBAC.Imports)
                    {
                        if (baseAsset.SearchForImport(import.ObjectName) == 0)
                        {
                            //import.ObjectName.Number = nameMap[import.ObjectName.ToString()];
                            var dst = new Import(
                                import.ClassPackage.Transfer(baseAsset),
                                import.ClassName.Transfer(baseAsset),
                                new FPackageIndex(0), // fix up in a second pass somehow
                                import.ObjectName.Transfer(baseAsset),
                                import.bImportOptional);
                            if (import.PackageName != null) dst.PackageName = import.PackageName.Transfer(baseAsset);
                            baseAsset.AddImport(dst);
                        }

                    }

                    
                    
                    foreach (StructPropertyData tableEntry in moddedEntries)
                    {
                        String entryName = tableEntry.Name.ToString();
                        if (!baseAsset.ContainsNameReference(tableEntry.Name.Value)) baseAsset.AddNameReference(tableEntry.Name.Value);
                        Console.WriteLine("  Found " + entryName);
                        String tableEntryFileLoc = Path.Combine(ProcessedFiles, modName, combineDatatable, entryName + ".json");
                        //Console.WriteLine("Attempting to find" + tableEntryFileLoc);
                        if (Path.Exists(tableEntryFileLoc))
                        {
                            //Console.WriteLine("Found " + tableEntryFileLoc);
                            String savedEntryJson = File.ReadAllText(tableEntryFileLoc);
                            //Console.WriteLine(savedAircraftJson);

                            PWDataEntry savedEntry = JsonSerializer.Deserialize<PWDataEntry>(savedEntryJson, SerializerOptions);
                            if (!modifiedBy.ContainsKey(entryName)) modifiedBy.Add(entryName, new List<PWDataEntry>() { savedEntry });
                            else modifiedBy[entryName].Add(savedEntry);

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
                            else Console.WriteLine("  Not editing Prez for {0} at {1}", entryName, combineDatatable);
                            if (!includedEntries.Contains(entryName))
                            {
                                Console.WriteLine("  Adding {0} to datatable...", entryName);
                                includedEntries.Add(entryName);
                                baseEntries.Add(tableEntry.Clone() as StructPropertyData);
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
                                    StructPropertyData moddedAircraft = moddedEntries.Find(x => x.Name == tableEntry.Name);
                                    PropertyData moddedprop = moddedAircraft.Value.Find(x => x.Name == currentProp.Name).Clone() as PropertyData;
                                    currentProp.SetObject(moddedprop.GetObject<object>());
                                    handleProperty(currentProp, baseAsset, currentProperty, savedEntry, importMap);
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
                                    includedEntries.Add(entryName);
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
                                            handleProperty(currentProp, baseAsset, currentProperty, savedEntry, importMap);
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

            foreach (String key in nameMap.Keys)
            {
                FString keyFString = new FString(key);
                int index = baseAsset.SearchNameReference(keyFString);
                Console.WriteLine("Found Name Reference {0} at {1}. It is supposed to be at {2}", key, index, nameMap[key]);
                //baseAsset.SetNameReference(nameMap[key], keyFString);
                baseAsset.AddNameReference(keyFString);
            }

            foreach (Import import in baseAsset.Imports)
            {
                if (importMap.ContainsKey(import.ObjectName.ToString()))
               {
                    Console.WriteLine("Still have import: {0}\n" +
                    "Expected Location: {1}\n" +
                    "Actual Location:{2}", import.ObjectName,
                    importMap[import.ObjectName.ToString()],
                    baseAsset.SearchForImport(import.ObjectName));
                }
                    
            }
            foreach (String entry in includedEntries)
            {
                List<PWDataEntry> modifiers = new List<PWDataEntry>();
                if (modifiedBy.TryGetValue(entry, out modifiers))
                {
                    StructPropertyData currentEntry = baseEntries.Find(x => x.Name.ToString() == entry);
                    Console.WriteLine($"Reanalyzing {currentEntry.Name}...");
                    foreach (PWDataEntry moddedValues in modifiers)
                    {
                        foreach (PropertyData currentProp in currentEntry.Value)
                        {
                            String propertyName = currentProp.Name.ToString();
                            if (currentProp is ObjectPropertyData objProp)
                            {
                                if (objProp.Value.ToString() != "0")
                                Console.WriteLine("  " + objProp.Value.ToImport(baseAsset).ObjectName);
                            }
                            if (currentProp is ArrayPropertyData arrayProp)
                            {
                                foreach (PropertyData data in arrayProp.Value)
                                {
                                    if (data is ObjectPropertyData subObjProp)
                                    {
                                        if (subObjProp.Value.ToString() != "0")
                                            Console.WriteLine("  " + subObjProp.Value.ToImport(baseAsset).ObjectName);
                                    }
                                }
                            }
                            /* if (moddedValues.DataProperties.ContainsKey(propertyName))
                            {
                                DataTableProperty currentProperty = moddedValues.DataProperties[propertyName];
                                handleProperty(currentProp, baseAsset, currentProperty, moddedValues, importMap);
                            } */
                        }
                    }
                }
            }
            //Console.WriteLine(String.Join(", ", importMap.Keys));


            String fileName = Path.Combine(comboModDirect, Path.GetFileNameWithoutExtension(combineDatatable) + ".uasset");
            baseAsset.Write(fileName);
            UAsset dtRefresh = new UAsset(fileName, EngineVersion.VER_UE4_27);
            Console.WriteLine(dtRefresh.VerifyBinaryEquality());
            dtRefresh.Write(fileName);


            if (addModsToF59)
            {
                String f59fileName = Path.Combine(comboModDirect, "DB_MF_Aircraft.uasset");

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
                //f59Ref.Write(f59fileName);
                addModsToF59 = false;
                addVanillaToF59 = false;
            }
        }

        String[] excessDatatables = Directory.GetFiles(comboModDirect);
        foreach (String file in excessDatatables)
        {
            if (Path.GetFileNameWithoutExtension(file) == "DB_MF_Aircraft")
            {
                String f59ModSpot = Path.Combine(comboModDirect,"Content/ProjectWingman/Plugins/MagadanFront/Content/MagadanFront/Blueprints/Data/");
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
        String modPakName = combinedModname + "output_P.pak";
        using (FileStream stream = new FileStream(modPakName, FileMode.Create))
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
        

        if (user.AllowEditToPWFolders)
        {
            if (user.AutomaticallyDeploy)
            {
                File.Copy(modPakName, Path.Combine(user.ProjectWingmanPAMLocation, modPakName), true);
            } else
            {
                char CanCopy = HelperFunctions.GetUserInput("YN".ToCharArray(), "Deploy PAK to ~PAM folder in Paks?");
                if (CanCopy == 'y') File.Copy(modPakName, Path.Combine(user.ProjectWingmanPAMLocation, modPakName), true);
            }
        }
        

        Directory.SetCurrentDirectory(oldDirect);
    }

    private static void Main(string[] args)
    {
        ProjectAssetManager program = new ProjectAssetManager();
        bool managerContinue = true;
        Console.WriteLine("Project Assetmanager is away.");

        Console.WriteLine("Checking for necessary directories and user preferences...");
        
        program.user.userStartup(program.coreFiles);
        if (!File.Exists(program.UserSettingsFilePath))
        {
            Console.WriteLine("User settings not found! Initializing first time user setup...");
            program.user.userSetup(program.coreFiles);
            File.WriteAllText(program.UserSettingsFilePath, JsonSerializer.Serialize(program.user, SerializerOptions));
        }
        else program.user = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(program.UserSettingsFilePath), SerializerOptions);
        

        while (managerContinue)
        {
            String managerPrompt = "Enter [P] to Edit your User Preferences\n" +
                "Enter [R] to Refresh Vanilla Files (needed first time)\n" +
                "Enter [E] to Extract Mod Paks (needed before building databases) \n" +
                "Enter [B] to Build Individual Databases from Mods (needed before combining mods)\n" +
                "Enter [C] to Create a Combined Mod\n" +
                "Enter [Q] to Quit.\n";
            Char managerChoice = HelperFunctions.GetUserInput("PREBCQ".ToCharArray(), managerPrompt);
                switch (managerChoice)
                {
                case 'p':
                    program.user.userSetup(program.coreFiles);
                    File.WriteAllText(program.UserSettingsFilePath, JsonSerializer.Serialize(program.user, SerializerOptions));
                    break;
                case 'r':
                        program.refreshVanillaData();
                        break;
                    case 'e':
                        program.extractModPacks();
                        break;
                    case 'b':
                        String skipPrompt = ("Skip Vanilla Playable Aircraft?\n" +
                            "Skipping Vanilla Playable Aircraft will make processing the Datatables in the future faster.\n" +
                            "This comes at the cost, of course, of not including their information\n" +
                            "(in case the extracted mods altered them at all)\n\n" +
                            "On most modern PCs, however, the time saved is likely negligible.\n\n");

                        char skipChoice = HelperFunctions.GetUserInput("YN".ToCharArray(), skipPrompt);
                        switch (skipChoice)
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