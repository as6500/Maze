using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;

string host = args.Length > 0 ? args[0] : "127.0.0.1";

var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

sock.Connect(new IPEndPoint(IPAddress.Parse(host), 8856));

var stream = new NetworkStream(sock);
var reader = new StreamReader(stream, new UTF8Encoding(false));
var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

// What the client knows: its own id, and where everybody is, as last told.
int myId = -1;
var players = new Dictionary<int, (char avatar, int x, int y)>();
var view = new object(); // players + myId, shared by two threads

if (!Console.IsOutputRedirected) Console.Clear();
// The receive thread: parse each line, update the picture, redraw.
var receiver = new Thread(() =>
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var f = line.Split('|');
                lock (view)
                {
                    switch (f[0])
                    {
                        case "W":
                            myId = int.Parse(f[1]);
                            break;
                        case "U":
                            players[int.Parse(f[1])] = (f[2][0], int.Parse(f[3]), int.Parse(f[4]));
                            break;
                        case "Q":
                            players.Remove(int.Parse(f[1]));
                            break;
                    }

                    Maze.Render(players.Values.Select(p => (p.x, p.y, p.avatar)),
                        $"you are player {myId} · arrows/WASD move, Q quits");
                }
            }
        }
        catch (IOException)
        {
        }

        Console.WriteLine("Server closed the connection.");
        Environment.Exit(0);
    })
    
    { IsBackground = true };
receiver.Start();
// The keyboard thread (this one): every key is a REQUEST, not a move.
string? dir;
while ((dir = Maze.ReadDirection()) != null)
{
    int id;
    lock (view) id = myId;
    if (dir.Length == 2 && dir[0] == 'S')
    {
        // sprint: S|playerid|direction|
        writer.WriteLine($"S|{id}|{dir[1]}|");
    }
    else
    {
        // normal: direction|playerid|
        writer.WriteLine($"{dir}|{id}|");
    }
}
    