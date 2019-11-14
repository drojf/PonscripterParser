namespace PonscripterParser.VirtualMachine
{
    class VarManager<T>
    {
        string debug_name;
        int vars_count;
        T[] vars;

        public VarManager(int numVars, string debug_name)
        {
            this.vars = new T[this.vars_count];
            this.vars_count = numVars;
            this.debug_name = debug_name;
        }

        T Get(int index)
        {
            if(index >= vars_count)
            {
                Log.Warning($"Out of range {debug_name} read: {index} (limit: {vars_count-1})");
                return default;
            }

            return vars[index];
        }

        void Set(int index, T value)
        {
            if (index >= vars_count)
            {
                Log.Warning($"Out of range {debug_name} write: {index} (limit: {vars_count - 1})");
                return;
            }

            vars[index] = value;
        }
    }
}
