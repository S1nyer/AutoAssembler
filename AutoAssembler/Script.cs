using System.Collections.Generic;

namespace AutoAssembler
{
    public class Script
    {
        public string Name;
        public string ScriptCode;
        public List<AutoAssembler.AllocedMemory> alloceds;
        public bool IsEnable
        {
            get { return !Enable;}
        }
        public bool Enable = true;
    }
}
