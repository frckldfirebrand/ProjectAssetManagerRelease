using System.Collections;
using System.ComponentModel;
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
    class ProWingPlane
    {
        [JsonConstructor]
        public ProWingPlane(String Name, Dictionary<String, String> PlaneAssets, Dictionary<String, DataTableProperty> PlaneProperties)
        {
            this.Name = Name;
            this.PlaneAssets = PlaneAssets;
            this.PlaneProperties = PlaneProperties;
        }
        [JsonPropertyName("Name")]
        public String? Name { get; set; }
        [JsonPropertyName("PlaneAssets")]
        public Dictionary<String, String>? PlaneAssets { get; set; }
        [JsonPropertyName("PlaneProperties")]
        public Dictionary<String, DataTableProperty>? PlaneProperties { get; set; }
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

    string modPakFolder = "Mod Paks\\";
    string modDBFolder = "Mod Databases\\";
    string modExtractedFiles = "Mod Extracted Files\\";
    string vanillaFolder = "Vanilla Files\\";
    string PlaneClassFolder = "Plane Files\\";
    string vanillaDBACLoc = "Vanilla Files\\VanillaDBAC.uasset";

    
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

    DataTableProperty SaveDataTableProperty(PropertyData propData, UAsset asset, Dictionary<String, String> importHolder)
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
                    importHolder.Add(objValue.Name.ToString(), objImport.ObjectName.ToString());
                    property.Tag = objImport.ObjectName.ToString();
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
                        subProperties.Add(subData.Name.ToString(), SaveDataTableProperty(subData, asset, importHolder));
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
                        subProperties.Add(subData.Name.ToString(), SaveDataTableProperty(subData, asset, importHolder));
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

    void CreateJSONsFromDatabase(UAsset vanilla, bool skipVanilla = false, String subFolder = "Vanilla")
    {
        Export vanillaEXP = vanilla["DB_Aircraft"];
        if (vanillaEXP is DataTableExport vanillaDT)
        {
            List<StructPropertyData> entries = vanillaDT.Table.Data;
            foreach (StructPropertyData line in entries)
            {
                //Using the name of the row of stats, we can get a name to use for the aircraft
                String aircraftName = line.Name.ToString();
                Console.WriteLine("Found " + aircraftName + "...");
                if (skipVanilla)
                {
                    if (!vanillaAircraft.Contains(aircraftName))
                    { WriteAircraftJSON(vanilla, subFolder, line, aircraftName); } else Console.WriteLine("Found Vanilla Aircraft.");
                }
                else WriteAircraftJSON(vanilla, subFolder, line, aircraftName);
            }
        }
    }

    private void WriteAircraftJSON(UAsset vanilla, string subFolder, StructPropertyData line, string aircraftName)
    {
        //Where we'll save the individual aircraft Json
        String aircraftLoc = PlaneClassFolder + "//" + subFolder + "//" + aircraftName;
        Dictionary<String, DataTableProperty> aircraftProps = new Dictionary<String, DataTableProperty>();
        Dictionary<String, String> aircraftAssets = new Dictionary<String, String>();
        foreach (PropertyData propData in line.Value)
        {
            aircraftProps.Add(propData.Name.ToString(), SaveDataTableProperty(propData, vanilla, aircraftAssets));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(aircraftLoc));
        ProWingPlane aircraft = new ProWingPlane(aircraftName, aircraftAssets, aircraftProps);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        String aircraftJSON = JsonSerializer.Serialize(aircraft, options);
        File.WriteAllText(aircraftLoc + ".JSON", aircraftJSON);
    }



    /*
    void OldCreateJSONsFromDatabase(UAsset vanilla, bool skipVanilla = false, String subFolder = "Vanilla")
    {
        Export vanillaEXP = vanilla["DB_Aircraft"];
        if (vanillaEXP is DataTableExport vanillaDT)
        {
            List<StructPropertyData> entries = vanillaDT.Table.Data;
            foreach (StructPropertyData line in entries)
            {
                //Using the name of the row of stats, we can get a name to use for the aircraft
                String aircraftName = line.Name.ToString();
                Console.WriteLine("Found " + aircraftName + "...");
                //Where we'll save the individual aircraft Json
                String aircraftLoc = PlaneClassFolder + "//" + subFolder + "//" + aircraftName;
                Dictionary<String, String> aircraftProps = new Dictionary<String, String>();
                Dictionary<String, Dictionary<String, String>> aircraftArrayProperties = new Dictionary<String, Dictionary<String, String>>();
                Dictionary<String, String> aircraftAssets = new Dictionary<String, String>();
                String seat = "";
                foreach (PropertyData propData in line.Value)
                {
                    //Generic casting is bad but I don't know a smarter way to do it yet.
                    Object propValue = propData.GetObject<Object>();
                    switch (propData.PropertyType.ToString())
                    {
                        case "ObjectProperty":
                            Console.WriteLine("  Adding ObjectProperty...");
                            ObjectPropertyData objValue = (ObjectPropertyData)propData;
                            if (objValue.IsImport())
                            {
                                Import objImport = objValue.ToImport(vanilla);
                                aircraftAssets.Add(objValue.Name.ToString(), objImport.ObjectName.ToString());
                            }
                            aircraftProps.Add(propData.Name.ToString(), propValue.ToString());
                            break;
                        case "TextProperty":
                            Console.WriteLine("  Adding TextProperty...");
                            TextPropertyData textProp = (TextPropertyData)propData;
                            if (textProp.CultureInvariantString != null) { aircraftProps.Add(propData.Name.ToString(), textProp.CultureInvariantString.ToString()); }
                            break;
                        case "ArrayProperty":
                            Console.WriteLine("  Adding ArrayProperty...");
                            ArrayPropertyData arrayProp = (ArrayPropertyData)propData;
                            Dictionary<String, String> subProps = new Dictionary<String, String>();
                            for (int i = 0; i < arrayProp.Value.Length; i++)
                            {
                                PropertyData subData = arrayProp.Value[i];
                                Object subValue = subData.GetObject<Object>();
                                if (subValue != null) { subProps.Add(subData.Name.ToString(), subValue.ToString()); }
                            }
                            aircraftArrayProperties.Add(propData.Name.ToString(), subProps);
                            break;
                        case "StructProperty":
                            Console.WriteLine("  Adding StructProperty...");
                            if (IsList(propValue))
                            {
                                List<PropertyData> subDataList = propValue as List<PropertyData>;
                                Dictionary<String, String> subPropList = new Dictionary<String, String>();
                                foreach (PropertyData subData in subDataList)
                                {
                                    //Blargh. More Data.
                                    Object subValue = subData.GetObject<Object>();

                                    if (subData.PropertyType.ToString() == "StructProperty")
                                    {
                                        if (subData.Name.ToString() == "PilotSeatOffset_67_0AB6CD564D0433D8C96FB7B6B5AFD133")
                                        {
                                            List<PropertyData> subSubDataList = subValue as List<PropertyData>;
                                            Object subSubValue = subSubDataList.FirstOrDefault().GetObject<Object>();
                                            seat = subSubValue.ToString();
                                        }
                                    } else
                                    {
                                        if (subValue != null) { subPropList.Add(subData.Name.ToString(), subValue.ToString()); }
                                    }

                                }
                                aircraftArrayProperties.Add(propData.Name.ToString(), subPropList);
                            }
                            break;
                        default:
                            Console.WriteLine("  Adding " + propData.PropertyType.ToString() + "...");
                            if (propValue != null)
                            {
                                aircraftProps.Add(propData.Name.ToString(), propValue.ToString());
                            }
                            break;
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(aircraftLoc));
                ProWingPlane aircraft = new ProWingPlane(aircraftName, aircraftAssets, aircraftProps, aircraftArrayProperties, seat);
                var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
                String aircraftJSON = JsonSerializer.Serialize(aircraft, options);
                File.WriteAllText(aircraftLoc + ".JSON", aircraftJSON);
            }
        }
    } */

    void refreshVanillaData(UAsset vanilla)
    {
        Console.WriteLine("Saving Serialized JSON Copy...");
        String jsonSerializedAsset = vanilla.SerializeJson(true);
        File.WriteAllText(vanillaFolder + "vanillaDBAC.JSON", jsonSerializedAsset);
        Console.WriteLine("Exported full Asset as JSON");

        Console.WriteLine("Scanning Vanilla Database...");

        CreateJSONsFromDatabase(vanilla);

    }

    void extractModPacks()
    {
        //This gets us every file in the folder where we put our Paks.
        String[] pakNames = Directory.GetFiles(modPakFolder);
        
        //Iterate over each String
        foreach (String pakName in pakNames)
        {
            //Not entirely sure how to best create a Pak, this is what the UAssetAPI tests used, so I copied it.
            PakBuilder pakBuilder = new PakBuilder();
            FileStream pakFile = new FileStream(pakName, FileMode.Open);
            PakReader pakReader = pakBuilder.Reader(pakFile);
            
            //Then we want to scan each file in the pak.
            foreach (var file in pakReader.Files())
            {
                //Log the file for sanity.
                Console.WriteLine($"File: {file}");
                //Set an export location in the same structure as it was found in the file, but sorted.
                String fileLocation = modExtractedFiles + pakName + "\\" + file;
                //Remember: a file in a pak is just a bunch of bytes, and the Pakreader gets us that information as it would be extracted. Neat!
                byte[] pakBytes = pakReader.Get(pakFile, file);

                //Make sure we have somewhere to write to...
                Directory.CreateDirectory(Path.GetDirectoryName(fileLocation));
                //Then write it!
                File.WriteAllBytes(fileLocation, pakBytes);
                
                //Additionally, if it's a DB_Aircraft file, we want to take a copy of it out so that we can reference it easier elsewhere.
                if (Path.GetFileNameWithoutExtension(fileLocation) == "DB_Aircraft")
                {
                    //We want the file extension so we copy both the Uasset and Uexp.
                    String databaseLocation = modDBFolder + Path.GetFileNameWithoutExtension(pakName) + "_dbac" + Path.GetExtension(file);
                    //Give it a subfolder in the Databases
                    Directory.CreateDirectory(modDBFolder);
                    
                    //Console.WriteLine(databaseLocation);
                    File.Copy(fileLocation, databaseLocation, true);
                }
            }
        }
    }
    void fullModdedDatabaseExtract()
    {
        String[] moddedDBs = Directory.GetFiles(modDBFolder);
        foreach (String modDB in moddedDBs)
        {
            if (Path.GetExtension(modDB) == ".uasset")
            {
                UAsset modDBAC = new UAsset(modDB, EngineVersion.VER_UE4_27);
                CreateJSONsFromDatabase(modDBAC, false, Path.GetFileNameWithoutExtension(modDB));
            }
        }
    }
    void partialModdedDatabaseExtract()
    {
        String[] moddedDBs = Directory.GetFiles(modDBFolder);
        foreach (String modDB in moddedDBs)
        {
            if (Path.GetExtension(modDB) == ".uasset")
            {
                UAsset modDBAC = new UAsset(modDB, EngineVersion.VER_UE4_27);
                CreateJSONsFromDatabase(modDBAC, true, modDB);
            }
        }
    }
    
    void handleProperty(PropertyData property, UAsset asset, DataTableProperty dataProp, ProWingPlane plane)
    {
        String propertyName = property.Name.ToString();
        String propertyType = property.PropertyType.ToString();
        Object propertyValue = property.GetObject<Object>();

        //Console.WriteLine(property.ToString());

        switch (propertyType)
        {
            case "ObjectProperty":
                if (plane.PlaneAssets.ContainsKey(propertyName)) 
                {
                    Import import = asset.Imports.Find(x => x.ObjectName.ToString() == plane.PlaneAssets[propertyName]);
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
        //Load in the vanilla DB_Aircraft as a baseline.
        FileStream vanillaJson = new FileStream(vanillaFolder + "vanillaDBAC.JSON", FileMode.Open);
        UAsset baseAsset = UAsset.DeserializeJson(vanillaJson);
        DataTableExport baseDataTable = baseAsset.Exports[0] as DataTableExport;

        List<StructPropertyData> baseEntries = baseDataTable.Table.Data;

        String[] moddedDBs = Directory.GetFiles(modDBFolder);

        Console.WriteLine("Force Enable Prez in each plane? \n (D will disable Presz in all planes) [Y/N/D]");
        String prezAnswer = Console.ReadLine().ToLower();
        bool modifyPrez = false;
        String forcedSeating = "0";
        List<String> includedAircraft = new List<String>();
        foreach (StructPropertyData baseAircraft in baseEntries)
        {
            includedAircraft.Add(baseAircraft.Name.ToString());
        }
        if (prezAnswer[0] == 'd' || prezAnswer[0] == 'y')
        {
            modifyPrez = true;
            if (prezAnswer[0] == 'd') forcedSeating = "1";
            else forcedSeating = "2";
        }

        foreach (String modDB in moddedDBs)
        {
            if (Path.GetExtension(modDB) == ".uasset")
            {
                String modAircraftFolder = PlaneClassFolder + Path.GetFileNameWithoutExtension(modDB);
                if (Path.Exists(modAircraftFolder))
                {
                    Console.WriteLine("Include " + modDB + "? [Y/N]");
                    String includeMod = Console.ReadLine().ToLower();
                    if (includeMod[0] == 'y')
                    {
                        UAsset modDBAC = new UAsset(modDB, EngineVersion.VER_UE4_27);
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
                        
                        foreach (StructPropertyData aircraft in moddedEntries)
                        {
                            Console.WriteLine("Found " + aircraft.Name);
                            String aircraftFileLoc = modAircraftFolder + "\\" + aircraft.Name + ".json";
                            Console.WriteLine("Attempting to find" + aircraftFileLoc);
                            if (Path.Exists(aircraftFileLoc))
                            {
                                Console.WriteLine("Found " + aircraftFileLoc);
                                String savedAircraftJson = File.ReadAllText(aircraftFileLoc);
                                //Console.WriteLine(savedAircraftJson);

                                var options = new JsonSerializerOptions {IncludeFields = true};

                                ProWingPlane savedAircraft = JsonSerializer.Deserialize<ProWingPlane>(savedAircraftJson, options);
                                if (modifyPrez)
                                {
                                    savedAircraft.PlaneProperties[
                                        "BoneDetails_46_F00ED91D4FDF91F973A7DAAF8A291DBF"].SubProperties[
                                        "PilotCount_2_65D06B7D492533AA2F8E9DA8254C5747"].Value = forcedSeating.ToString();
                                }
                                if (!includedAircraft.Contains(aircraft.Name.ToString()))
                                {
                                    includedAircraft.Add(aircraft.Name.ToString());
                                    baseEntries.Add(aircraft);
                                }
                                StructPropertyData currentAircraft = baseEntries.Find(x => x.Name == aircraft.Name);
                                foreach (PropertyData currentProp in currentAircraft.Value)
                                {
                                    String propertyName = currentProp.Name.ToString();
                                    DataTableProperty currentProperty = savedAircraft.PlaneProperties[propertyName];
                                    handleProperty(currentProp, baseAsset, currentProperty, savedAircraft);
                                }
                            }

                        }

                    }
                }

            }
        }
        Console.WriteLine("What should we name this new Database?");
        String fileName = Console.ReadLine().ToLower();
        fileName = modDBFolder + Path.GetFileNameWithoutExtension(fileName) + ".uasset";
        baseAsset.Write(fileName);
        UAsset dtRefresh = new UAsset(fileName, EngineVersion.VER_UE4_27);
        Console.WriteLine(dtRefresh.VerifyBinaryEquality());
        dtRefresh.Write(fileName);

    }

    private static void Main(string[] args)
    {
        Program program = new Program();
        bool managerContinue = true;
        Console.WriteLine("Project Assetmanager is away.");
        UAsset vanillaDBAC = new UAsset(program.vanillaDBACLoc,EngineVersion.VER_UE4_27);
        
        
        while (managerContinue)
        {
            Console.WriteLine
                (
                "Enter [R] to Refresh Vanilla Files (needed first time)\n" +
                "Enter [E] to Extract Mod Paks. \n" +
                "Enter [B] to Build Individual Databases from Modded DB_Aircraft's\n" +
                "Enter [C] to Combine Databases\n" +
                "Enter [D] to Create a new DB_Aircraft in the Deployment Directory\n" +
                "Enter [P] to Pak the Deployment Directory\n" +
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
                        program.refreshVanillaData(vanillaDBAC);
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
                                program.partialModdedDatabaseExtract(); 
                                break;
                            case 'n':
                                program.fullModdedDatabaseExtract();
                                break;
                        }
                        break;
                    case 'c':
                        program.combineDatabases();
                        break;
                    case 'd':
                        break;
                    case 'p':
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