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
    string vanillaDBAC = "VanillaDBAC.uasset";
    private static void Main(string[] args)
    {
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