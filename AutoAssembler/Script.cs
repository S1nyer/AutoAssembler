using System.Collections.Generic;

namespace AutoAssembler
{
    public class Script
    {
        public enum Status
        {
            Enabled = 0,
            Disabled = 1,
            Inexistence = 2
        }
        public string Name;
        public string ScriptCode;
        public List<AutoAssembler.AllocedMemory> alloceds;
        public Status GetStatus
        {
            get 
            {
                if (Enable)
                    return Status.Disabled;
                else
                    return Status.Enabled;
            }
        }
        public bool Enable = true;
    }
}
