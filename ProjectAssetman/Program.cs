using Newtonsoft.Json.Linq;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
internal class Program
{
    class ProWingPlane
    {
        public ProWingPlane(String name, Dictionary<String, String> assets, Dictionary<String, String> properties, Dictionary<string, Dictionary<string, string>> planeArrayProperties, String seat)
        {
            Name = name;
            PlaneAssets = assets;
            PlaneProperties = properties;
            PlaneArrayProperties = planeArrayProperties;
            PilotSeatOffset = seat;
        }
        public String Name;
        public Dictionary<String, String> PlaneAssets;
        public Dictionary<String, String> PlaneProperties;
        public Dictionary<String, Dictionary<String, String>> PlaneArrayProperties;
        public String PilotSeatOffset;
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
    readonly string[] vanillaAircraft = new string[]
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
    }

    void refreshVanillaData(UAsset vanilla)
    {
        Console.WriteLine("Saving Serialized JSON Copy...");
        String jsonSerializedAsset = vanilla.SerializeJson(true);
        File.WriteAllText(vanillaFolder + "vannilaDBAC.JSON", jsonSerializedAsset);
        Console.WriteLine("Exported full Asset as JSON");

        Console.WriteLine("Scanning Vanilla Database...");

        CreateJSONsFromDatabase(vanilla);

    }

    void extractModPacks()
    {
        String[] pakNames = Directory.GetFiles(modPakFolder);
        foreach (String pakName in pakNames)
        {
            PakBuilder pakBuilder = new PakBuilder();
            FileStream pakFile = new FileStream(pakName, FileMode.Open);
            PakReader pakReader = pakBuilder.Reader(pakFile);
            foreach (var file in pakReader.Files())
            {
                Console.WriteLine($"File: {file}");
                String fileLocation = modExtractedFiles + pakName + "\\" + file;
                byte[] pakBytes = pakReader.Get(pakFile, file);
                Directory.CreateDirectory(Path.GetDirectoryName(fileLocation));
                File.WriteAllBytes(fileLocation, pakBytes);
                if (Path.GetFileNameWithoutExtension(fileLocation) == "DB_Aircraft")
                {
                    String databaseLocation = modDBFolder + Path.GetFileNameWithoutExtension(pakName) + "_dbac" + Path.GetExtension(file);
                    Directory.CreateDirectory(modDBFolder);
                    Console.WriteLine(databaseLocation);
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
                CreateJSONsFromDatabase(modDBAC, true, modDB);
            }
        }
    }
    void partialModdedDatabaseExtract()
    {

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