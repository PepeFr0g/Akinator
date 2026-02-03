using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akinator
{
    public class Character
    {
        public int Id;
        public string Name;
        public int PlayCount;

        public Dictionary<int, Answer> Answers;
        
        public Character(int id, string name, int playCount)
        {
            Id = id;
            Name = name;
            PlayCount = playCount;
        }
    }
}
