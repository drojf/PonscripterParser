using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser.VirtualMachine
{
#if false
    class POnscripterVirtualMachine
    {
        //Ponscripter numeric and string variable "arrays" 
        Dictionary<string, int> userFunctionLookup = new Dictionary<string, int>();

        // Runtime Variables
        VarManager<int> VAR = new VarManager<int>(4096, "numeric");
        VarManager<int> STR = new VarManager<int>(4096, "string");

        ScriptPositionManager scriptPositionManager;

        public POnscripterVirtualMachine(List<List<Node>> script)
        {
            this.scriptPositionManager = new ScriptPositionManager(script);
        }

        public bool Intepret()
        {
            Node nodeToIntepret = scriptPositionManager.ConsumeInstruction();
            if (nodeToIntepret == null)
                return false;

            Console.WriteLine(nodeToIntepret.ToString());

            return true;
        }

        /// <summary>
        /// Jump to the line given by the specified function name
        /// Raises an exception if the function is not found
        /// </summary>
        /// <param name="functionName"></param>
        private void JumpFunction(string functionName)
        {
            if (!userFunctionLookup.TryGetValue(functionName, out int functionDefLineNumber))
            {
                throw new Exception($"Unknown function {functionName}");
            }

            scriptPositionManager.CallLine(functionDefLineNumber);
        }

        private void RegisterFunction(string functionName, int lineNumber)
        {
            userFunctionLookup.Add(functionName, lineNumber);
        }
    }
#endif
}
