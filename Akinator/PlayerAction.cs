using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akinator
{
    public class PlayerAction
    {
        public int InputId;
        public PlayerActionType Type;
        public int TargetId;


        public PlayerAction(int inputId, PlayerActionType type, int targetId = 0)
        {
            InputId = inputId;
            Type = type;
            TargetId = targetId;

        }
    }
}
