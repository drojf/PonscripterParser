using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    /// <summary>
    /// WIP class to hold user subroutine information (such as number of arguments)
    /// </summary>
    public class SubroutineInformation
    {
        public class SubroutineArgument
        {
            string name;
            bool isStringArgument; //arguments are either numeric or string

            public SubroutineArgument(bool isStringArgument, string name)
            {
                this.name = name;
                this.isStringArgument = isStringArgument;
            }
        }

        //string getParamString; //if function takes no arguments, this is null (for now)
        public List<SubroutineArgument> arguments;
        public bool hasArguments;

        public SubroutineInformation(List<SubroutineArgument> arguments)
        {
            this.arguments = arguments;
            hasArguments = arguments.Count > 0;
        }

        public SubroutineInformation(bool hasArguments)
        {
            this.arguments = null; //TODO: figure out a better way to do this!
            this.hasArguments = hasArguments;
        }

        /// <summary>
        /// Input is a list of arguments, NOT including the 'getparam' function call.
        /// eg. "%clock_moto_h,%clock_moto_m,%clock_h"
        /// </summary>
        /// <param name="argumentList"></param>
        public SubroutineInformation(string argumentsAsString)
        {
            //since getparam only takes 'output' variables as argument (no complicated expressions), 
            //we can cheat here and just use string split
            try
            {
                this.arguments = argumentsAsString.Split(new char[] { ',' })
                    .Select(x => x.Trim())
                    .Select(x => new SubroutineArgument(x[0] == '$', x.Substring(1)))
                    .ToList();
            }
            catch
            {
                Console.WriteLine($"Incorrectly formatted argument list: {argumentsAsString}");
            }

            this.hasArguments = this.arguments.Count > 0;
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

        public Dictionary<string, SubroutineInformation> GetRawDict()
        {
            return subroutineInfoDict;
        }

        public bool TryAdd(string sAnyCase, SubroutineInformation value)
        {
            string s = NormalizeKey(sAnyCase);

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
            return subroutineInfoDict.TryGetValue(NormalizeKey(sAnyCase), out output);
        }

        public SubroutineInformation this[string key]
        {
            get
            {
                return subroutineInfoDict[NormalizeKey(key)];
            }
            set
            {
                subroutineInfoDict[NormalizeKey(key)] = value;
            }
        }

        public bool ContainsKey(string key)
        {
            return subroutineInfoDict.ContainsKey(NormalizeKey(key));
        }

        private string NormalizeKey(string sAnyCase)
        {
            return preserveCase ? sAnyCase : sAnyCase.ToLower();
        }
    }




    class UserFunctionScanner
    {
        //Note: there are 2 special labels, "*define" and "*start" which indicate the start of the definition block and program block
        static Regex defsubRegex = new Regex(@"^\s*defsub\s+([^;\s]+)", RegexOptions.IgnoreCase);
        static Regex labelRegex = new Regex(@"^\s*\*([^;\s]+)", RegexOptions.IgnoreCase);
        static Regex returnRegex = new Regex(@"^\s*return", RegexOptions.IgnoreCase);
        static Regex getParamRegex = new Regex(@"^\s*getparam\s+([^;]+)", RegexOptions.IgnoreCase); //this assumes there are no other commands on the getparam line besides a comment.

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
        /// Scan the entire script for subroutine declarations and definitions
        /// </summary>
        /// <param name="allLines"></param>
        public static SubroutineDatabase buildInitialUserList(string[] allLines, SubroutineDatabase database)
        {
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

            //iterate starting at the label definition until (worst case) the end of file
            //when a getparam or return is found, the number of parameters is recorded
            SubroutineInformation GetSubroutineInformation(string[] linesToSearch, LabelInfo label)
            {
                for (int i = label.labelLineIndex; i < linesToSearch.Length; i++)
                {
                    string s = linesToSearch[i];

                    Match getParamMatch = getParamRegex.Match(s);
                    Match returnMatch = returnRegex.Match(s);

                    if (getParamMatch.Success)
                    {
                        Console.WriteLine($"{label.labelName} arguments are {getParamMatch.Groups[0].Value} [{s}]");
                        return new SubroutineInformation(getParamMatch.Groups[0].Value);
                    }
                    else if (returnMatch.Success)
                    {
                        Console.WriteLine($"{label.labelName} takes no arguments");
                        return new SubroutineInformation(hasArguments: false);
                    }
                }

                //somehow reached end of the document
                return null;
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

                SubroutineInformation subInfo = GetSubroutineInformation(allLines, label);

                if(subInfo == null)
                {
                    Console.WriteLine($"ERROR: no return or getparam for subroutine {label.labelName}");
                }
                else
                {
                    database[label.labelName] = subInfo;
                }
            }

            return database;

        }

    }
}
