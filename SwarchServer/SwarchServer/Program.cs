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
        public static Stopwatch watch;

        public static SQLiteDatabase db;

        public static bool playing = false;

        public static Random random = new Random();

        public static PlayerObject[] players = new PlayerObject[2];
        public static Pellet[] pellets = new Pellet[5];

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

            // Connect both players
            for (int i = 0; i < 2; i++)
            {
                // Create a PlayerObject for this client
                players[i] = new PlayerObject(i+1);

                Console.WriteLine("Waiting for connection " + (i + 1));

                Socket soc = listener.AcceptSocket(); // accept an incoming request to make a connection, then return the socket(endpoint) associated with the connection

                Console.WriteLine("Connected: {0}", soc.RemoteEndPoint); // print connection details

                Program.db = new SQLiteDatabase();

                try
                {
                    NetworkStream nws = new NetworkStream(soc);
                    StreamReader sr = new StreamReader(nws); // return the stream to read from
                    StreamWriter sw = new StreamWriter(nws); // establish a stream to write to
                    sw.AutoFlush = true; // enable automatic flushing, flush the write stream after every write command, no need to send buffered data
                    playerSockets[i] = new PlayerSocket(nws, soc, sr, sw);
                    playerSockets[i].writer.WriteLine("connect&player " + (i + 1));

                    // Start a thread for this client
                    ReadThread threadClass = new ReadThread(i + 1);
                    playerSockets[i].psThread = new Thread(new ThreadStart(threadClass.Service));
                    playerSockets[i].psThread.Start();
                }
                catch
                {
                    Console.WriteLine("Error during connection.");
                }
            }

            // Create the pellets
            for (int j = 0; j < 5; j++)
            {
                pellets[j] = new Pellet(j);
            }

            // Send the start signal to the clients
            playerSockets[0].writer.WriteLine("connect&start");
            playerSockets[1].writer.WriteLine("connect&start");
            Console.WriteLine("Players 1 and 2 connected. Begin playing.");
            playing = true;
            watch = new Stopwatch();
            watch.Start();

            Console.WriteLine("Sending initial player data.");
            players[0].SendData();
            players[1].SendData();

            while (playing)
            {
                GameLoop();
            }
        }

        // Main gameplay loop
        public static void GameLoop()
        {
            // Send player positions every 100 milliseconds
            if (watch.ElapsedMilliseconds >= 100)
            {
                players[0].Update();
                players[1].Update();

                players[0].SendData();
                players[1].SendData();
                watch.Restart();
            }

            CheckPlayerCollisions();
            CheckPelletCollisions();
        }

        // Check if the two players collided with each other
        public static void CheckPlayerCollisions()
        {
            if (Math.Abs(players[0].x - players[1].x) < (players[0].size / 2 + players[1].size / 2)
                && Math.Abs(players[0].z - players[1].z) < (players[0].size / 2 + players[1].size / 2))  // If the players are touching
            {
                Console.WriteLine("Player collision");
                if (players[0].size > players[1].size)
                {
                    players[0].Eat(1);
                    players[1].ResetPosition();
                }
                else if (players[0].size < players[1].size)
                {
                    players[0].ResetPosition();
                    players[1].Eat(1);
                }
                else if (players[0].size == players[1].size)
                {
                    players[0].ResetPosition();
                    players[1].ResetPosition();
                }
            }
        }

        // Check if either of the players collided with any of the pellets
        public static void CheckPelletCollisions()
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    if (Math.Abs(players[i].x - pellets[j].x) < (players[i].size/2 + .25)
                        && Math.Abs(players[i].z - pellets[j].z) < (players[i].size/2 + .25))
                    {
                        pellets[j].NewPosition();
                        players[i].Eat(2);
                    }
                }
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
                        HandleLogin();
                    }
                    else if (tempData == "direction")
                    {
                        HandleDirectionChange();
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

        public void HandleLogin()
        {
            //insert code to check length of string
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
                            Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + username);
                            entered = true;
                        }
                        else
                        {
                            Console.WriteLine("Password didn't match. Send fail to client.");
                            Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + "fail");
                            entered = true;
                        }
                    }
                }
                if (!entered)
                {
                    Console.WriteLine("No username match. Create a new user. Send data to client.");
                    Program.db.Insert("USERS", insertData);
                    Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + username + "&" + password);
                }
            }
            catch
            {
                Console.WriteLine("Failed to insert user");
            }
        }

        public void HandleDirectionChange()
        {
            tempData = data.Split('&')[1];
            if (tempData == "0")
            {
                Program.players[client - 1].xvelocity = 0;
                Program.players[client - 1].zvelocity = Program.players[client - 1].speed;
            }
            else if (tempData == "1")
            {
                Program.players[client - 1].xvelocity = 0;
                Program.players[client - 1].zvelocity = -Program.players[client - 1].speed;
            }
            else if (tempData == "2")
            {
                Program.players[client - 1].xvelocity = -Program.players[client - 1].speed;
                Program.players[client - 1].zvelocity = 0;
            }
            else if (tempData == "3")
            {
                Program.players[client - 1].xvelocity = Program.players[client - 1].speed;
                Program.players[client - 1].zvelocity = 0;
            }
        }
    }

    class PlayerObject
    {
        public int client;
        private int score;
        public float x;
        public float z;
        private float prevx;
        private float prevz;
        public float speed;
        public float xvelocity;
        public float zvelocity;
        public float size;

        public PlayerObject(int clientNumber)
        {
            client = clientNumber;
            score = 0;
            x = Program.random.Next(-29, 30);
            z = Program.random.Next(-14, 15);
            prevx = x;
            prevz = z;
            speed = 5;
            xvelocity = 0;
            zvelocity = -speed;
            size = 1;
        }

        public void Update()
        {
            prevx = x;
            prevz = z;

            x += xvelocity / 10; // Velocity divided by fps
            z += zvelocity / 10;

            if (x < (-30.5 + size/2) || x > (30.5 - size/2) || z < (-15.5 + size/2) || z > (15.5 - size/2))
            {
                ResetPosition();
            }
        }

        public void ResetPosition()
        {
            x = Program.random.Next(-29, 30);
            z = Program.random.Next(-14, 15);
            prevx = x;
            prevz = z;
            speed = 5;

            if (xvelocity < 0)
            {
                xvelocity = -speed;
            }
            else if (xvelocity > 0)
            {
                xvelocity = speed;
            }
            else if (zvelocity < 0)
            {
                zvelocity = -speed;
            }
            else if (zvelocity > 0)
            {
                zvelocity = speed;
            }

            size = 1;

            SendData();
        }

        public void Eat(int type)
        {
            size += 2;
            speed = 4 * speed / 5;
            if (type == 1) // Ate a player
            {
                score += 2;
            }
            else if (type == 2) // Ate a pellet
            {
                score += 1;
            }

            SendData();
        }

        public void SendData()
        {
            Program.getPlayerSockets()[0].writer.WriteLine("player&" + client + "&" + x + "&" + z + "&" + size + "&" + speed);
            Program.getPlayerSockets()[1].writer.WriteLine("player&" + client + "&" + x + "&" + z + "&" + size + "&" + speed);
        }
    }

    class Pellet
    {
        public int pellet;
        public float x;
        public float z;

        public Pellet(int pelletNumber)
        {
            pellet = pelletNumber;
            NewPosition();
        }

        public void NewPosition()
        {
            x = Program.random.Next(-29, 30);
            z = Program.random.Next(-14, 15);
            SendData();
        }

        public void SendData()
        {
            Program.getPlayerSockets()[0].writer.WriteLine("pellet&" + pellet + "&" + x + "&" + z);
            Program.getPlayerSockets()[1].writer.WriteLine("pellet&" + pellet + "&" + x + "&" + z);
        }
    }
}