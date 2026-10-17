using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;
const int Port = 8856;
//test

var world = new World(); // players and their positions: the truth
int nextId = 1;

var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

listener.Bind(new IPEndPoint(IPAddress.Any, Port));
listener.Listen(5);

Console.WriteLine($"MazeClient server on port {Port}, {Maze.Width}x{Maze.Height}");

while (true)
{
    Socket sock = listener.Accept();
    int id;
    lock (world) id = nextId++;
    new Thread(() => HandleClient(sock, id)).Start();
}

void HandleClient(Socket sock, int id) {
    // A stream over the socket, and a reader that hands us whole lines.
    var stream = new NetworkStream(sock);
    var reader = new StreamReader(stream, new UTF8Encoding(false));
    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    
    // The welcome: spawn, tell them who they are, show them the world, tell the others.
        Player me;
        
    lock (world)
    {
        me = world.Spawn(id, writer);
        writer.WriteLine($"W|{me.Id}|{me.Avatar}|"); // who you are
        foreach (var p in world.Players) // the world as it is
            writer.WriteLine(Update(p));
        world.Broadcast(Update(me), except: me); // and you, to everyone else
    }
    
// The loop: read a request, check it, apply it, tell everyone.
    try
    {
        string? line;
        while ((line = reader.ReadLine()) != null) // one message at a time
        {
            var f = line.Split('|'); // "r|7|" -> ["r", "7", ""]

            //sprint part
            bool sprint = f[0] == "S";
            int who;
            string direction;

            if (sprint)
            {
                //S|playerId|direction|
                if (f.Length < 3 ||
                    !int.TryParse(f[1], out who) ||
                    who != id)
                    continue;

                direction = f[2];
            }
            else
            {
                if (f.Length < 2 ||
                    !int.TryParse(f[1], out who) ||
                    who != id)
                    continue;

                direction = f[0];
            }

            var (dx, dy) = Maze.Step(direction);

            if (dx == 0 && dy == 0) continue;

            lock (world)
            {
                int steps = sprint ? 2 : 1;
                int nx = me.X;
                int ny = me.Y;
                bool valid = true;

                for (int i = 0; i < steps; i++)
                {
                    int tx = nx + dx;
                    int ty = ny + dy;
                    
                    //wrap
                    (tx, ty) = Maze.Wrap(tx, ty);

                    if (!Maze.IsFree(tx, ty) || world.Occupied(tx, ty))
                        break;

                    nx = tx;
                    ny = ty;
                }
                
                if (nx == me.X && ny == me.Y)
                    continue;

                me.X = nx;
                me.Y = ny; // the only maze that counts
                world.Broadcast(Update(me)); // everyone, including the mover
            }
        }
    }
    catch (IOException)
    {
    } // client vanished mid-way

// However the loop ended: remove the player and tell everyone.
    lock (world)
    {
        world.Remove(me);
        world.Broadcast($"Q|{id}|");
    }
    sock.Close();
    Console.WriteLine($"Player {id} ({me.Avatar}) left");
}

static string Update(Player p) => 
    $"U|{p.Id}|{p.Avatar}|{p.X}|{p.Y}|";

class Player {
    public int Id;
    public char Avatar;
    public int X, Y;
    public StreamWriter Writer = null!; //added this to fix player not being recognised on rider
}

// Everything in here is touched by every client thread. Callers hold lock(world).
    class World
    {
        public readonly List<Player> Players = new();
        public Player Spawn(int id, StreamWriter writer)
        {
            char avatar = (char)('A' + (id - 1) % 26);
            for (int y = 0; y < Maze.Height; y++)
            for (int x = 0; x < Maze.Width; x++)
                if (Maze.IsFree(x, y) && !Occupied(x, y))
                {
                    var p = new Player
                    {
                        Id = id, Avatar = avatar, X = x, Y = y,
                        Writer = writer
                    };
                    Players.Add(p);
                    return p;
                }

            throw new InvalidOperationException("maze is full");
        }

        public bool Occupied(int x, int y) => 
            Players.Any(p => p.X == x && p.Y == y);
        public void Remove(Player p) => 
            Players.Remove(p);

        public void Broadcast(string msg, Player? except = null)
        {
            foreach (var p in Players)
            {
                if (p == except) continue;
                try
                {
                    p.Writer.WriteLine(msg);
                }
                catch (IOException)
                {
                    
                }
                catch (ObjectDisposedException)
                {

                }
            }
        }
    }
 