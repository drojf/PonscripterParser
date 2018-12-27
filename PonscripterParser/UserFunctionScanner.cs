using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class UserFunctionScanner
    {
        //Note: there are 2 special labels, "*define" and "*start" which indicate the start of the definition block and program block
        static Regex defsubRegex = new Regex(@"^\s*defsub\s+([^;\s]+)", RegexOptions.IgnoreCase);
        static Regex labelRegex = new Regex(@"^\s*\*([^;\s]+)", RegexOptions.IgnoreCase);
        static Regex returnRegex = new Regex(@"^\s*return", RegexOptions.IgnoreCase);
        static Regex getParamRegex = new Regex(@"^\s*getparam\s+([^;\s]+)", RegexOptions.IgnoreCase);

        /// <summary>
        /// WIP class to hold user subroutine information (such as number of arguments)
        /// </summary>
        public class SubroutineInformation
        {
            string getParamString; //if function takes no arguments, this is null (for now)

            public SubroutineInformation(string s)
            {
                getParamString = s;
            }
        }

        /// <summary>
        /// Structure to hold a (label_name, label_line_index) pair
        /// </summary>
        class LabelInfo
        {
            public string labelName;
            public int labelLineIndex;

            public LabelInfo(string labelName, int labelLineIndex)
            {
                this.labelName = labelName;
                this.labelLineIndex = labelLineIndex;
            }
        }


        /// <summary>
        /// A database which holds information about each subroutine.
        /// keys are saved all lower-case so as to be case insensitive
        /// </summary>
        public class SubroutineDatabase
        {
            Dictionary<string, SubroutineInformation> subroutineInfoDict = new Dictionary<string, SubroutineInformation>();
            List<string> duplicateDefsubs = new List<string>();
            bool preserveCase = false;

            public bool TryAdd(string sAnyCase, SubroutineInformation value)
            {
                string s = preserveCase ? sAnyCase : sAnyCase.ToLower();

                if (subroutineInfoDict.ContainsKey(s))
                {
                    //Console.WriteLine($"WARNING: duplicate defsub found for {s}");
                    duplicateDefsubs.Add(s);
                    return false;
                }
                else
                {
                    subroutineInfoDict.Add(s, value);
                    return true;
                }
            }

            public bool TryGetValue(string sAnyCase, out SubroutineInformation output)
            {
                string s = preserveCase ? sAnyCase : sAnyCase.ToLower();
                return subroutineInfoDict.TryGetValue(s, out output);
            }

            public SubroutineInformation this[string key]
            {
                get
                {
                    return subroutineInfoDict[key];
                }
                set
                {
                    subroutineInfoDict[key] = value;
                }
            }
        }

        /// <summary>
        /// Scan the entire script for subroutine declarations and definitions
        /// </summary>
        /// <param name="allLines"></param>
        public static void scan(string[] allLines)
        {
            SubroutineDatabase database = new SubroutineDatabase();
            List<LabelInfo> labels = new List<LabelInfo>();

            //scan for subroutines definitions and labels
            for (int i = 0; i < allLines.Length; i++)
            {
                string s = allLines[i];

                Match defsubMatch = defsubRegex.Match(s);
                Match labelMatch = labelRegex.Match(s);

                if (defsubMatch.Success)
                {
                    //search the script for "defsub ut_mld2" type calls. store each in a hashmap
                    string subName = defsubMatch.Groups[1].Value;
                    Console.WriteLine($"Got sub definition {subName}");

                    if(!database.TryAdd(subName, null))
                    {
                        Console.WriteLine($"WARNING: duplicate defsub found for {subName}");
                    }
                }
                else if (labelMatch.Success)
                {
                    string labelName = labelMatch.Groups[1].Value;
                    Console.WriteLine($"Got label definition {labelName}");
                    labels.Add(new LabelInfo(labelName, i));
                }
            }

            //For reach subroutine, determine what arguments (if any) it uses
            //May not work for all cases, but usually getparam is the first call 
            //after the label def so use this method for now.
            foreach (LabelInfo label in labels)
            {
                //Only process subroutine definitions, not ordinary labels
                if(!database.TryGetValue(label.labelName, out _))
                {
                    continue;
                }

                Console.WriteLine($"{label.labelName} is a subroutine");

                //iterate starting at the label definition until (worst case) the end of file
                //when a getparam or return is found, the number of parameters is recorded, then move on to next label
                for (int i = label.labelLineIndex; i < allLines.Length; i++)
                {
                    string s = allLines[i];

                    Match getParamMatch = getParamRegex.Match(s);
                    Match returnMatch = returnRegex.Match(s);
                    
                    if (getParamMatch.Success)
                    {
                        Console.WriteLine($"{label.labelName} arguments are {s}");
                        database[label.labelName] = new SubroutineInformation(getParamMatch.Groups[0].Value);
                        break;
                    }
                    else if(returnMatch.Success)
                    {
                        Console.WriteLine($"{label.labelName} takes no arguments");
                        database[label.labelName] = new SubroutineInformation(null);
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: no return or getparam for subroutine {label.labelName}");
                    }
                }

            }

        }

    }
}
