using Newtonsoft.Json.Linq;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
internal class Program
{
    string modPakFolder = "Mod Paks\\";
    string modDBFolder = "Mod Databases\\";
    string modExtractedFiles = "Mod Extracted Files\\";
    string vanillaDBAC = "Vanilla Files\\VanillaDBAC.uasset";

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
                //package.Write(packedFile);
            }
        }
    }

    private static void Main(string[] args)
    {
        Program program = new Program();
        bool managerContinue = true;
        Console.WriteLine("Project Assetmanager is away.");
        while (managerContinue)
        {
            Console.WriteLine
                (
                "Enter [E] to Extract Mod Paks. \n" +
                "Enter [B] to Build Databases from Modded DB_Aircraft's\n" +
                "Enter [C] to choose Aircraft from Available Databases\n" +
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
                    case 'e':
                        program.extractModPacks();
                        break;
                    case 'b':
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