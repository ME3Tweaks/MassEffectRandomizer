using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    /// <summary>
    /// Class for passing data between theads
    /// </summary>
    public class ThreadCommand
    {
        /// <summary>
        /// Creates a new thread command object with the specified command and data object. This constructori s used for passing data to another thread. The receiver will need to read the command then cast the data.
        /// </summary>
        /// <param name="command">command for this thread communication.</param>
        /// <param name="data">data to pass to another thread</param>
        public ThreadCommand(string command, object data)
        {
            Command = command;
            Data = data;
        }

        /// <summary>
        /// Creates a new thread command object with the specified command. This constructor is used for notifying other threads something has happened.
        /// </summary>
        /// <param name="command">command for this thread communication.</param>
        /// <param name="data">data to pass to another thread</param>
        public ThreadCommand(string command)
        {
            Command = command;
        }

        public string Command;
        public object Data;
    }
}
