using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akinator
{
    public class Question
    {
        public int Id;
        public string Text;
        public int AnsweredCount;
        public float IG;
        public Question(int id, string text, int answeredCount)
        {
            Id = id;
            Text = text;
            AnsweredCount = answeredCount;
        }
    }
}
