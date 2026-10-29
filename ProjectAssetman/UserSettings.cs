using ProjectAssetman;
using System;
using System.Collections.Generic;
using System.Text;
using UAssetAPI;

namespace ProjectAssetman
{
    class UserSettings
    {
        public UserSettings() { }
        public String UserID = "";
        public String ProjectWingmanFileLocation = "";
        public String ProjectWingmanPAMLocation = "";
        public bool AllowEditToPWFolders = false;
        public bool AutomateDatabases = false;
        public bool AutomaticallyDeploy = false;

        private void ResetValues()
        {
            UserID = string.Empty;
            ProjectWingmanFileLocation = string.Empty;
            ProjectWingmanPAMLocation = string.Empty;
            AllowEditToPWFolders = false;
            AutomateDatabases= false;
            AutomaticallyDeploy = false;
        }
        public void userStartup(Dictionary<String, String>folders)
        {
            if (!Directory.Exists(folders["ProcessedFiles"])) { Directory.CreateDirectory("Processed Files\\"); }
            if (!Directory.Exists(folders["modJSONFolder"])) { Directory.CreateDirectory("Mod JSONs\\"); }
            if (!Directory.Exists(folders["modPakFolder"])) { Directory.CreateDirectory("Mod Paks\\"); }
            if (!Directory.Exists(folders["modDTFolder"])) { Directory.CreateDirectory("Mod DataTables\\"); }
            if (!Directory.Exists(folders["modExtractedFiles"])) { Directory.CreateDirectory("Mod Extracted Files\\"); }
            if (!Directory.Exists(folders["vanillaFolder"])) {Directory.CreateDirectory("Vanilla Files\\"); }
            if (!Directory.Exists(folders["vanillaDataFolder"])) {Directory.CreateDirectory("Vanilla Files\\Vanilla Data"); }
            if (!Directory.Exists(folders["f59DataFolder"])) {Directory.CreateDirectory("Vanilla Files\\F59 Data"); }
            if (!Directory.Exists(folders["createdModFolder"])) {Directory.CreateDirectory("Created Mods\\"); }

            if (AllowEditToPWFolders)
            {
                if (!Directory.Exists(Path.Combine(ProjectWingmanFileLocation, "~PAM Mods")))
                {
                    ProjectWingmanPAMLocation = Path.Combine(ProjectWingmanFileLocation, "~PAM Mods");
                    Directory.CreateDirectory(ProjectWingmanPAMLocation);
                }
            }
        }
        public void userSetup(Dictionary<String, String> folders) 
        {
            ResetValues();
            Console.WriteLine("Enter in a User ID for us to attribute combined mods to.");
            UserID = Console.ReadLine();
            bool lacksPWPak = true;
            while (lacksPWPak)
            {
                Console.WriteLine("Please paste the Project Wingman\\ProjectWingman\\Content\\Paks folder here...");
                ProjectWingmanFileLocation = Console.ReadLine();
                String[] pwMainPaks = Directory.GetFiles(ProjectWingmanFileLocation);
                if (pwMainPaks.Length != 1)
                {
                    Console.WriteLine("This doesn't seem to be the right folder... it has a different amount of paks than expected.");
                    if (pwMainPaks.Length > 1)
                    {
                        Console.WriteLine("The program can continue anyways, but results may be unexpected!");
                        if (HelperFunctions.GetUserInput("YN".ToCharArray(), "Continue?") == 'y') lacksPWPak = false;
                    }
                } else lacksPWPak = false;
                if (!lacksPWPak)
                {
                    lacksPWPak = false;
                    WriteBaseFiles(folders, pwMainPaks);
                }
            }

            if (HelperFunctions.GetUserInput("YN".ToCharArray(),
                "Do you wish to allow Project Asset Manager to modify folders in your Project Wingman Directory?\n" +
                "(Under normal circumstances, this is limited to writing mods to a new directory PAM will create called ~PAM)") == 'y')
            {
                AllowEditToPWFolders = true;

                if (!Directory.Exists(Path.Combine(ProjectWingmanFileLocation, "~PAM Mods")))
                {
                    ProjectWingmanPAMLocation = Path.Combine(ProjectWingmanFileLocation, "~PAM Mods");
                    Directory.CreateDirectory(ProjectWingmanPAMLocation);
                }

                if (HelperFunctions.GetUserInput("YN".ToCharArray(), "Do you wish to automatically deploy mods without asking in the future?") == 'y')
                {
                    AutomaticallyDeploy = true;
                }
                else AutomaticallyDeploy = false;
            }
            else AllowEditToPWFolders = false;
            if (HelperFunctions.GetUserInput("YN".ToCharArray(),
                "Do you wish to automatically create Mod Databases after extracting modified Pak files?") == 'y')
            {  AutomateDatabases = true; } else AutomateDatabases = false;
            
        }
        void WriteBaseFiles(Dictionary<String, String> folders, String[] pwMainPaks)
        {
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
                    if (file.Contains(F59DataPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        String destString = Path.Combine(folders["f59DataFolder"], file.Substring(F59DataPath.Length).TrimStart(Path.DirectorySeparatorChar));

                        byte[] pakBytes = pakReader.Get(pakFile, file);

                        //Make sure we have somewhere to write to...
                        Directory.CreateDirectory(Path.GetDirectoryName(destString));
                        //Then write it!
                        File.WriteAllBytes(destString, pakBytes);

                    }
                    else if (file.Contains(ProjectWingmanDataPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        String destString = Path.Combine(folders["vanillaDataFolder"], file.Substring(ProjectWingmanDataPath.Length).TrimStart(Path.DirectorySeparatorChar));

                        byte[] pakBytes = pakReader.Get(pakFile, file);

                        //Make sure we have somewhere to write to...
                        Directory.CreateDirectory(Path.GetDirectoryName(destString));
                        //Then write it!
                        File.WriteAllBytes(destString, pakBytes);
                    }
                }
            }
        }
    }
}
