using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser.PonsToVM
{
    struct ScriptPosition
    {
        public readonly int lineIndex;
        public readonly int instructionIndex;

        public ScriptPosition(int lineIndex, int instructionIndex)
        {
            this.lineIndex = lineIndex;
            this.instructionIndex = instructionIndex;
        }
    }

    class ScriptPositionManager
    {
        Stack<ScriptPosition> callStack = new Stack<ScriptPosition>();

        int nextLineIndex;
        int nextInstructionIndex;
        List<List<Node>> script;

        public ScriptPositionManager(List<List<Node>> script)
        {
            this.nextLineIndex = 0;
            this.nextInstructionIndex = 0;
            this.script = script;
        }

        /// <summary>
        /// Advance to the next instruction on the current line
        /// If there are no more instructions, you should call JumpNextValidLine
        /// </summary>
        /// <returns></returns>
        public Node ConsumeInstructionOnLine()
        {
            if (LineFinished())
            {
                return null;
            }

            return script[nextLineIndex][nextInstructionIndex++];
        }

        public bool LineFinished()
        {
            return !IsValidIndex(nextLineIndex, nextInstructionIndex);
        }

        /// <summary>
        /// Jump to the next valid, non-empty line
        /// When this function returns false, you've reached the end of the script
        /// </summary>
        /// <returns></returns>
        public bool JumpNextValidLine()
        {
            int jumpLocation = nextLineIndex;

            while(true)
            {
                jumpLocation += 1;

                //Check for end of file
                if (!IsValidLine(jumpLocation))
                {
                    return false;
                }

                //Accept the next line if it has at least one valid instruction
                if(script[jumpLocation].Count > 0)
                {
                    break;
                }
            }

            Jump(jumpLocation, 0);
            return true;
        }

        /// <summary>
        ///Jump to a given line, without saving the call position 
        /// </summary>
        /// <param name="position"></param>
        public void Jump(ScriptPosition position) => Jump(position.lineIndex, position.instructionIndex);
        public void Jump(int jumpLineNumber, int jumpInstructionIndex = 0)
        {
            if (!IsValidIndex(jumpLineNumber, jumpInstructionIndex))
            {
                throw new Exception($"Jump to line [{jumpLineNumber}] failed as exceeds script length");
            }

            nextLineIndex = jumpLineNumber;
            nextInstructionIndex = jumpInstructionIndex;
        }


        // Save the calling index (for use with Return()), then Jump to the specified line
        public void CallLine(int callLineNumber)
        {
            callStack.Push(new ScriptPosition(nextLineIndex, nextInstructionIndex));
            Jump(callLineNumber);
        }

        /// <summary>
        /// Jump to the last location on the stack
        /// If no indexes saved on the stack, just print a warning
        /// </summary>
        public void Return()
        {
            if (callStack.Count > 0)
            {
                Jump(callStack.Pop());
            }
            else
            {
                Log.Warning($"Return with no matching function call at line {nextLineIndex} (ignored)");
            }
        }

        private bool IsValidIndex(int lineIndex, int instructionIndex)
        {
            return IsValidLine(lineIndex) && instructionIndex < script[lineIndex].Count;
        }

        /// <summary>
        /// Checks whether the given line number is a valid line
        /// Note that valid lines may have no valid indexes if there are no instructions on that line!
        /// </summary>
        /// <param name="lineIndex"></param>
        /// <returns></returns>
        private bool IsValidLine(int lineIndex)
        {
            return lineIndex < script.Count;
        }

    }
}
