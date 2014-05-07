using System;
using System.Data;
using System.Timers;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Diagnostics;


namespace SwarchServer
{
    class Program
    {
        public static SQLiteDatabase db;

        public static bool playing = false;

        public static PlayerSocket[] getPlayerSockets()
        {
            return playerSockets;
        }

        static TcpListener listener;
        static PlayerSocket[] playerSockets = new PlayerSocket[2];

        public static void Main()
        {
            // Start network connection

            listener = new TcpListener(4645);
            listener.Start();

            Console.WriteLine("Waiting for connection");

            Socket soc = listener.AcceptSocket(); // accept an incoming request to make a connection, then return the socket(endpoint) associated with the connection

            Console.WriteLine("Connected: {0}", soc.RemoteEndPoint); // print connection details

            try
            {
                NetworkStream nws = new NetworkStream(soc);
                StreamReader sr = new StreamReader(nws); // return the stream to read from
                StreamWriter sw = new StreamWriter(nws); // establish a stream to write to
                sw.AutoFlush = true; // enable automatic flushing, flush the write stream after every write command, no need to send buffered data
                playerSockets[0] = new PlayerSocket(nws, soc, sr, sw);
                playerSockets[0].writer.WriteLine("connect&player " + 1);

                // Start a thread for this client
                ReadThread threadClass = new ReadThread(1);
                playerSockets[0].psThread = new Thread(new ThreadStart(threadClass.Service));
                playerSockets[0].psThread.Start();
            }
            catch
            {
                Console.WriteLine("Error during connection.");
            }

            Program.db = new SQLiteDatabase();
            playerSockets[0].writer.WriteLine("connect&start");
            Console.WriteLine("Player connected. Begin playing.");
            playing = true;

            while (playing)
            {

            }
        }
    }

    class ReadThread
    {
        int client;
        string data;
        string tempData;
        string username;
        string password;

        public ReadThread(int clientNumber)
        {
            client = clientNumber;
            Console.WriteLine("ReadThread started for client " + client);
        }

        public void Service()
        {
            try
            {
                while (true)
                {
                    PlayerSocket[] sockets = Program.getPlayerSockets();
                    data = sockets[client - 1].reader.ReadLine();
                    tempData = data.Split('&')[0];

                    if (tempData == "logins")
                    {
                        username = data.Split('&')[1];
                        password = data.Split('&')[2];

                        
                        Dictionary<String, String> insertData = new Dictionary<String, String>();
                        insertData.Add("USERNAME", username);
                        insertData.Add("PASSWORD", password);
                        try
                        {
                            bool entered = false; // Whether or not the username exists in the database
                            Console.WriteLine("Inserting user data.");

                            DataTable user;
                            String query = "select USERNAME \"Username\", PASSWORD \"Password\"";
                            query += "from USERS";
                            user = Program.db.GetDataTable(query);
                            foreach (DataRow r in user.Rows)
                            {
                                if (r["Username"].ToString() == username)
                                {
                                    Console.WriteLine("Found a username match");
                                    if (r["Password"].ToString() == password)
                                    {
                                        Console.WriteLine("Found a password match. Send data to client.");
                                        Program.getPlayerSockets()[0].writer.WriteLine("login&" + username);
                                        entered = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Password didn't match. Send fail to client.");
                                        Program.getPlayerSockets()[0].writer.WriteLine("login&" + "fail");
                                        entered = true;
                                    }
                                }
                            }
                            if (!entered)
                            {
                                Console.WriteLine("No username match. Create a new user. Send data to client.");
                                Program.db.Insert("USERS", insertData);
                                Program.getPlayerSockets()[0].writer.WriteLine("login&" + username + "&" + password);
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Failed to insert user");
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Error in ReadThread Service for client " + client);
                Program.getPlayerSockets()[client].psThread.Abort();
                Program.playing = false;
            }
        }
    }
}