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
        public static bool gameWon = false;

        public static Random random = new Random();

        public static PlayerObject[] players = new PlayerObject[3];
        public static Pellet[] pellets = new Pellet[5];

        public static PlayerSocket[] getPlayerSockets()
        {
            return playerSockets;
        }

        static TcpListener listener;
        static PlayerSocket[] playerSockets = new PlayerSocket[3];

        public static void Main()
        {
            // Start network connection
            listener = new TcpListener(4645);
            listener.Start();

            // Connect both players
            for (int i = 0; i < playerSockets.Length; i++)
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
            bool allSignedIn = false;
            while (!allSignedIn)
            {
                // Do nothing
                if (players[0].username != "" && players[1].username != "" && players[2].username != "") {
                    allSignedIn = true;
                }
            }

            for (int i = 0; i < playerSockets.Length; i++)
            {
                playerSockets[i].writer.WriteLine("connect&start");
            }

            Console.WriteLine("All players connected. Begin playing.");
            playing = true;
            watch = new Stopwatch();
            watch.Start();

            Console.WriteLine("Sending initial player data.");
            for (int i = 0; i < players.Length; i++)
            {
                players[i].SendData();
            }

            System.Timers.Timer timer = new System.Timers.Timer(100.0);

            timer.Elapsed += new ElapsedEventHandler(GameLoop);
            timer.Start();

            //Thread.CurrentThread.Join(); // Wait until other threads cease before closing program
        }

        // Main gameplay loop
        public static void GameLoop(object source, ElapsedEventArgs e)
        {
            if (!gameWon)
            {
                // Send player positions every 100 milliseconds
                if (watch.ElapsedMilliseconds >= 100)
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        players[i].Update();
                        players[i].SendData();
                    }
                    watch.Restart();
                }

                CheckPlayerCollisions();
                CheckPelletCollisions();
            }
        }

        // Check if the two players collided with each other
        public static void CheckPlayerCollisions()
        {
            for (int i = 0; i < 2; i++)
            {
                if (i == 0)
                {
                    for (int j = 1; j < 3; j++)
                    {
                        if (Math.Abs(players[i].x - players[j].x) < (players[i].size / 2 + players[j].size / 2)
                    && Math.Abs(players[i].z - players[j].z) < (players[i].size / 2 + players[j].size / 2))  // If the players are touching
                        {
                            if (players[i].size > players[j].size)
                            {
                                players[i].Eat(1);
                                players[j].ResetPosition();
                            }
                            else if (players[i].size < players[j].size)
                            {
                                players[i].ResetPosition();
                                players[j].Eat(1);
                            }
                            else if (players[i].size == players[j].size)
                            {
                                players[i].ResetPosition();
                                players[j].ResetPosition();
                            }
                        }
                    }
                }
                else if (i == 1)
                {
                    if (Math.Abs(players[i].x - players[2].x) < (players[i].size / 2 + players[2].size / 2)
                    && Math.Abs(players[i].z - players[2].z) < (players[i].size / 2 + players[2].size / 2))  // If the players are touching
                    {
                        if (players[i].size > players[2].size)
                        {
                            players[i].Eat(1);
                            players[2].ResetPosition();
                        }
                        else if (players[i].size < players[2].size)
                        {
                            players[i].ResetPosition();
                            players[2].Eat(1);
                        }
                        else if (players[i].size == players[2].size)
                        {
                            players[i].ResetPosition();
                            players[2].ResetPosition();
                        }
                    }
                }
            }
        }

        // Check if either of the players collided with any of the pellets
        public static void CheckPelletCollisions()
        {
            for (int i = 0; i < 3; i++)
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
                while (!Program.gameWon)
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
                        Console.WriteLine("Received direction change from client " + client);
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
            //insert code to check length of string - are username/password long enough?

            Console.WriteLine("In HandleLogin for client " + client);

            if (data.Split('&').Length < 2)
            {
                Console.WriteLine("Too few ampersands in signal. Send fail to client.");
                Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + "fail");
                return;
            }

            username = data.Split('&')[1];
            password = data.Split('&')[2];

            username.Replace("'", "''");

            Dictionary<String, String> insertData = new Dictionary<String, String>();
            insertData.Add("USERNAME", username);
            insertData.Add("PASSWORD", password);
            try
            {
                Console.WriteLine("Checking if other client is using this username.");
                if (client == 2)
                {
                    if (username == Program.players[0].username)
                    {
                        Console.WriteLine("Other client already signed in with this username. Send fail to client.");
                        username = "";
                        password = "";
                        Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + "fail");
                        return;
                    }
                }
                else if (client == 3)
                {
                    if (username == Program.players[0].username || username == Program.players[1].username)
                    {
                        Console.WriteLine("Other client already signed in with this username. Send fail to client.");
                        username = "";
                        password = "";
                        Program.getPlayerSockets()[client - 1].writer.WriteLine("login&" + "fail");
                        return;
                    }
                }
                Console.WriteLine("This username is available.");

                bool entered = false; // Whether or not the username exists in the database
                Console.WriteLine("Inserting user data.");

                DataTable user = Program.db.GetUser(username, password);

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
                            Program.players[client - 1].username = username;
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
                    for (int i = 0; i < Program.getPlayerSockets().Length; i++)
                    {
                        Program.getPlayerSockets()[i].writer.WriteLine("login&" + client + "&" + username);
                    }
                    Program.players[client - 1].username = username;
                }
            }
            catch
            {
                Console.WriteLine("Failed to insert user");
            }
        }

        public void HandleDirectionChange()
        {
            if (data.Split('&').Length < 1)
            {
                Console.WriteLine("Too few ampersands in signal. Not a valid direction change.");
                return;
            }
            tempData = data.Split('&')[1];
            Program.players[client - 1].direction = Convert.ToInt32(tempData);
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
        public string username;
        private int score;
        public float x;
        public float z;
        private float prevx;
        private float prevz;
        public float speed;
        public float xvelocity;
        public float zvelocity;
        public float size;
        public enum Directions { up, down, left, right }
        public int direction;

        public PlayerObject(int clientNumber)
        {
            client = clientNumber;
            username = "";
            score = 0;
            x = Program.random.Next(-14, 15);
            z = Program.random.Next(-14, 15);
            prevx = x;
            prevz = z;
            speed = 5;
            xvelocity = 0;
            zvelocity = -speed;
            size = 1;
            direction = (int)Directions.down;
        }

        public void Update()
        {
            prevx = x;
            prevz = z;

            x += xvelocity / 10; // Velocity divided by fps
            z += zvelocity / 10;

            if (x < (-15.5 + size/2) || x > (15.5 - size/2) || z < (-15.5 + size/2) || z > (15.5 - size/2))
            {
                ResetPosition();
            }
        }

        public void ResetPosition()
        {
            if (client == 2)
            {
                Console.WriteLine("Reset position");
            }

            x = Program.random.Next(-14, 15);
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
                score += 10;
                for (int i = 0; i < Program.getPlayerSockets().Length; i++)
                {
                    Program.getPlayerSockets()[i].writer.WriteLine("score&" + client + "&" + 10);
                }
            }
            else if (type == 2) // Ate a pellet
            {
                score += 1;
                for (int i = 0; i < Program.getPlayerSockets().Length; i++)
                {
                    Program.getPlayerSockets()[i].writer.WriteLine("score&" + client + "&" + 1);
                }
            }

            SendData();

            if (score >= 100)
            {
                for (int i = 0; i < Program.getPlayerSockets().Length; i++)
                {
                    Program.getPlayerSockets()[i].writer.WriteLine("winner&" + client);
                }
                Program.gameWon = true;
            }
        }

        public void SendData()
        {
            for (int i = 0; i < Program.getPlayerSockets().Length; i++)
            {
                if (client == 2)
                {
                    Console.WriteLine(x + "," + z + " speed: " + speed);
                }
                Program.getPlayerSockets()[i].writer.WriteLine("player&" + client + "&" + x + "&" + z + "&" + size + "&" + speed + "&" + direction + "&" + username);
            }
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
            x = Program.random.Next(-14, 15);
            z = Program.random.Next(-14, 15);
            SendData();
        }

        public void SendData()
        {
            for (int i = 0; i < Program.getPlayerSockets().Length; i++)
            {
                Program.getPlayerSockets()[i].writer.WriteLine("pellet&" + pellet + "&" + x + "&" + z);
            }
        }
    }
}