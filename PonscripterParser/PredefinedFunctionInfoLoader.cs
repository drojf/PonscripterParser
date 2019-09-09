using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class PredefinedFunctionInfoLoader
    {
        public static void load(string filePath, SubroutineDatabase database)
        {
            string[] lines = File.ReadAllLines(filePath);
            foreach(string line in lines)
            {
                string[] segments = line.Split(new char[] { '\0' });
                string functionName = segments[0];

                if (segments.Length > 2)
                {
                    Console.WriteLine($"Function '{functionName}' has more than one set of arguments - will just use first function definition");
                }

                string firstArgsList = segments[1];

                //If the function is already defined by user, assume it has been overwritten, and prefix an underscore
                string databaseKeyName = database.ContainsKey(functionName) ? $"_{functionName}" : functionName;

                database[databaseKeyName] = decodeArgumentsFromList(functionName, firstArgsList);
            }
        }


        public static SubroutineInformation decodeArgumentsFromList(string functionName, string argList)
        {
            //if the argument list is just the function name, the function has no arguments
            bool hasNoArguments = functionName.Trim() == argList.Trim();

            return new SubroutineInformation(!hasNoArguments);
        }
    }
}
