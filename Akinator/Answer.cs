using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akinator
{
    public class Answer
    {
        public int CharacterId;
        public int QuestionId;
        public sbyte Value;
        public int Count;
        public Answer(int characterId, int questionId, sbyte value)
        {
            CharacterId = characterId;
            QuestionId = questionId;
            Value = value;
        }
    }
}
