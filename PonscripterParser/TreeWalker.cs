//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;

namespace PonscripterParser
{
    interface FunctionHandler
    {
        string FunctionName();
        string HandleFunctionNode(TreeWalker walker, FunctionNode function);
    }

    class IgnoreCaseDictionary<V>
    {
        Dictionary<string, V> innerDictionary;

        public IgnoreCaseDictionary()
        {
            this.innerDictionary = new Dictionary<string, V>();
        }

        public void Set(string key, V value)
        {
            this.innerDictionary[key.ToLower()] = value;
        }

        public bool TryGetValue(string key, out V value)
        {
            return this.innerDictionary.TryGetValue(key.ToLower(), out value);
        }

        public bool Contains(string key)
        {
            return this.innerDictionary.ContainsKey(key);
        }
    }

    class FunctionHandlerLookup
    {
        IgnoreCaseDictionary<FunctionHandler> systemFunctions;
        IgnoreCaseDictionary<FunctionHandler> userFunctions;

        public FunctionHandlerLookup()
        {
            this.systemFunctions = new IgnoreCaseDictionary<FunctionHandler>();
            this.userFunctions = new IgnoreCaseDictionary<FunctionHandler>();

            //Register predefined functions here?
        }

        public bool TryGetFunction(string functionName, out FunctionHandler retHandler)
        {
            //First, check user functions to see if function with name exists
            if(this.userFunctions.TryGetValue(functionName, out FunctionHandler userFunction))
            {
                retHandler = userFunction;
                return true;
            }

            //Then, check system functions to see if function exists
            if (this.systemFunctions.TryGetValue(functionName, out FunctionHandler systemFunction))
            {
                retHandler = systemFunction;
                return true;
            }

            //Finally, check if function has been overridden, if it has a '_' at the front ('_' prefixed function
            if(functionName.StartsWith("_") && 
                this.systemFunctions.TryGetValue(functionName.Substring(1), out FunctionHandler overridenFunction))
            {
                retHandler = overridenFunction;
                return true;
            }

            retHandler = null;
            return false;
        }

        public void RegisterUserFunction(FunctionHandler userFunctionHandler)
        {
            RegisterFunctionWithCheck(userFunctions, userFunctionHandler);
        }

        public void RegisterSystemFunction(FunctionHandler systemFunctionHandler)
        {
            RegisterFunctionWithCheck(systemFunctions, systemFunctionHandler);
        }

        private void RegisterFunctionWithCheck(IgnoreCaseDictionary<FunctionHandler> dict, FunctionHandler userFunctionHandler)
        {
            if (dict.Contains(userFunctionHandler.FunctionName()))
            {
                throw new Exception($"User function {userFunctionHandler.FunctionName()} was defined twice");
            }
            else
            {
                dict.Set(userFunctionHandler.FunctionName(), userFunctionHandler);
            }
        }
    }

    class TreeWalker
    {
        List<Node> nodes;
        FunctionHandlerLookup functionLookup;

        public TreeWalker(List<Node> nodes)
        {
            this.nodes = nodes;
            this.functionLookup = new FunctionHandlerLookup();
        }

        public void Walk()
        {
            foreach(Node n in nodes)
            {
                if(!HandleNode(n))
                {
                    Console.WriteLine($"Warning: Node {n} is not handled");
                }

            }
        }

        private bool HandleNode(Node n)
        {
            switch(n)
            {
                case DialogueNode dialogue:
                    Console.WriteLine($"Display Text: {dialogue.lexeme.text}");
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);
            }

            return false;
        }

        private bool HandleFunction(FunctionNode function)
        {
            if(this.functionLookup.TryGetFunction(function.lexeme.text, out FunctionHandler handler))
            {
                handler.HandleFunctionNode(this, function);
                return true;
            }

            return false;

            //switch(function.lexeme.text)
            //{
            //    case "numalias":
            //        break;

            //    case "stralias":
            //        break;

            //    case "defsub":
            //        break;

            //    case "mov":
            //        break;

            //    case "gosub":
            //        break;

            //    case "goto":
            //        break;

            //    case "bg":
            //        break;
            //}
        }
    }
}
