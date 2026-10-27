using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ProjectAssetman
{
    class HelperFunctions
    {
        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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

        public static char GetUserInput(char[] Options, String Prompt)
        {
            for(int i = 0; i < Options.Length; i++)
            {
                Options[i] = Char.ToUpper(Options[i]);
            }
            Console.WriteLine();
            String userInput = "";
            bool invalidInput = true;
            while (invalidInput)
            {
                Console.WriteLine(Prompt);
                Console.Write("OPTIONS: ");
                foreach(char option in Options)
                {
                    Console.Write(" [{0}] ", option);
                }
                Console.WriteLine("\n");
                userInput = Console.ReadLine().ToUpper();

                if (userInput == null || userInput.Length == 0)
                {
                    Console.WriteLine("Please actually input a value.");
                } else if (userInput.Length > 1)
                {
                    Console.WriteLine("You can break something if you input something more than just a single character, you know.");
                } else if (!Options.Contains(userInput[0])) { Console.WriteLine("That isn't one of your options, y'know."); }
                else invalidInput = false;
            }

            return char.ToLower(userInput[0]);
        }


        public static bool IsList(Object o)
        {
            if (o == null) return false;
            return o is IList &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }
    }
}
