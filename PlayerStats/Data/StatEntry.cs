using System;

namespace PlayerStats.Data
{
    [Serializable]
    public class StatEntry
    {
        public string name;
        public int count;

        public StatEntry(string name, int count = 1)
        {
            this.name = name;
            this.count = count;
        }
    }
}
