using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace SwarchServer
{
    class PlayerSocket
    {
        public NetworkStream streamer;
        public Socket socket;
        public StreamReader reader;
        public StreamWriter writer;
        public Thread psThread;

        public PlayerSocket(NetworkStream nws, Socket soc, StreamReader sr, StreamWriter sw)
        {
            streamer = nws;
            socket = soc;
            reader = sr;
            writer = sw;
        }
    }
}
